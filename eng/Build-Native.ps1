#Requires -Version 5.1
<#
.SYNOPSIS
  Builds the BiscuitSharp native bridge for one RID and stages it with its
  identity manifest and licenses (M2 distribution input).
.DESCRIPTION
  Runs `cargo build --locked --release --target <triple>`, copies the cdylib to
  native/staging/<rid>/native/, writes biscuitsharp-native.json (ABI, versions,
  hashes, commit, features), and stages upstream + transitive license notices.
  Pinned values are asserted against Cargo.lock; hashes are computed, never
  hardcoded. See docs/native-boundary.md and docs/implementation-plan.md.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Build-Native.ps1 -Rid win-x64
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64", "osx-arm64")]
    [string]$Rid
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$nativeDir = Join-Path $repoRoot "native"
# Cross-platform cargo discovery: rustup home on PATH-independent location,
# falling back to PATH. ($HOME exists on Windows PowerShell 5.1 and PS 7.)
$cargo = "cargo"
foreach ($candidate in @((Join-Path $HOME ".cargo/bin/cargo"), (Join-Path $HOME ".cargo/bin/cargo.exe"))) {
    if (Test-Path -LiteralPath $candidate) { $cargo = $candidate; break }
}
$rustc = "rustc"
foreach ($candidate in @((Join-Path $HOME ".cargo/bin/rustc"), (Join-Path $HOME ".cargo/bin/rustc.exe"))) {
    if (Test-Path -LiteralPath $candidate) { $rustc = $candidate; break }
}

$triples = @{
    "win-x64"   = "x86_64-pc-windows-msvc"
    "linux-x64" = "x86_64-unknown-linux-gnu"
    "osx-arm64" = "aarch64-apple-darwin"
}
$files = @{
    "win-x64"   = "biscuitsharp_native.dll"
    "linux-x64" = "libbiscuitsharp_native.so"
    "osx-arm64" = "libbiscuitsharp_native.dylib"
}
$triple = $triples[$Rid]
$dllName = $files[$Rid]

# Pinned baseline (M0). Re-verify on every upstream upgrade.
$expectedBiscuitAuth = "6.0.0"
$expectedUpstreamCommit = "0f0b4e0e6fe07220c1ba6b51bff21d450d94a975"
$expectedBridge = "0.1.0"
$expectedAbi = 1
$expectedFeatures = @("regex-full", "datalog-macro", "pem") # biscuit-auth 6.0.0 defaults, manifest-verified
$expectedMinSchema = 3
$expectedMaxSchema = 6
$expectedDatalog = "3.3"

# Assert the lockfile still pins the baseline.
$lockText = Get-Content -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Raw
if ($lockText -notmatch '(?ms)name = "biscuit-auth"\r?\nversion = "6\.0\.0"') {
    throw "Cargo.lock no longer pins biscuit-auth $expectedBiscuitAuth; re-verify the baseline before staging."
}

# Bridge version from native/Cargo.toml ([package] version).
$bridgeVersion = Select-String -LiteralPath (Join-Path $nativeDir "Cargo.toml") -Pattern '^version = "([^"]+)"' |
    Select-Object -First 1 -ExpandProperty Matches |
    ForEach-Object { $_.Groups[1].Value }
if ($bridgeVersion -ne $expectedBridge) {
    throw "native/Cargo.toml bridge version is $bridgeVersion, expected $expectedBridge."
}

# Rust toolchain channel from rust-toolchain.toml.
$toolchain = Select-String -LiteralPath (Join-Path $repoRoot "rust-toolchain.toml") -Pattern 'channel = "([^"]+)"' |
    Select-Object -First 1 -ExpandProperty Matches |
    ForEach-Object { $_.Groups[1].Value }
$rustVersion = & $rustc --version 2>$null
if (-not $rustVersion) { $rustVersion = "unknown (rustc not on PATH)" }
# Strip the "rustc " prefix to match the bridge's BISCUITSHARP_RUST_VERSION env.
$rustVersion = "$rustVersion" -replace '^rustc ', ''
if ($rustVersion -notmatch [regex]::Escape($toolchain)) {
    throw "Active rustc ($rustVersion) does not match pinned toolchain $toolchain."
}

Write-Output "Building $Rid ($triple)..."
& $cargo build --locked --release --target $triple --manifest-path (Join-Path $nativeDir "Cargo.toml")
if ($LASTEXITCODE -ne 0) { throw "cargo build failed for $Rid" }

$builtDll = Join-Path $nativeDir "target/$triple/release/$dllName"
if (-not (Test-Path -LiteralPath $builtDll)) { throw "Expected asset missing: $builtDll" }

$stageDir = Join-Path $nativeDir "staging/$Rid/native"
$legalDir = Join-Path $nativeDir "staging/$Rid/legal"
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
New-Item -ItemType Directory -Path $legalDir -Force | Out-Null
Copy-Item -LiteralPath $builtDll -Destination (Join-Path $stageDir $dllName) -Force

$binaryHash = (Get-FileHash -LiteralPath (Join-Path $stageDir $dllName) -Algorithm SHA256).Hash.ToLowerInvariant()
$lockHash = (Get-FileHash -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    abi = $expectedAbi
    bridge = $expectedBridge
    biscuit_auth = $expectedBiscuitAuth
    upstream_commit = $expectedUpstreamCommit
    cargo_lock_sha256 = $lockHash
    rust = $rustVersion
    toolchain = $toolchain
    target = $triple
    rid = $Rid
    binary = $dllName
    binary_sha256 = $binaryHash
    enabled_features = $expectedFeatures
    min_schema_version = $expectedMinSchema
    max_schema_version = $expectedMaxSchema
    datalog = $expectedDatalog
}
$manifest | ConvertTo-Json -Depth 4 | ForEach-Object {
    [System.IO.File]::WriteAllText(
        (Join-Path $stageDir "biscuitsharp-native.json"),
        $_ + "`r`n",
        [System.Text.UTF8Encoding]::new($false))
}

# Upstream license text (published biscuit-auth crate ships its LICENSE).
$registry = Get-ChildItem -LiteralPath (Join-Path $HOME ".cargo/registry/src") -Directory -ErrorAction SilentlyContinue |
    Select-Object -First 1
$biscuitLicense = $null
if ($null -ne $registry) {
    $biscuitLicense = Join-Path $registry.FullName "biscuit-auth-6.0.0/LICENSE"
}
if ($biscuitLicense -and (Test-Path -LiteralPath $biscuitLicense)) {
    Copy-Item -LiteralPath $biscuitLicense -Destination (Join-Path $legalDir "biscuit-auth-LICENSE") -Force
}

# Transitive dependency inventory (name, version, declared license).
$metadata = & $cargo metadata --locked --format-version 1 --manifest-path (Join-Path $nativeDir "Cargo.toml") | ConvertFrom-Json
$notices = @(
    "# Third-party notices for the BiscuitSharp native bridge ($Rid)",
    "",
    "Generated from Cargo.lock by eng/Build-Native.ps1. Re-generate on every upstream upgrade.",
    ""
)
foreach ($pkg in ($metadata.packages | Sort-Object name, version)) {
    if ($pkg.name -eq "biscuitsharp_native") { continue }
    $notices += "- $($pkg.name) $($pkg.version) -- $($pkg.license)"
}
$noticesText = $notices -join "`r`n"
[System.IO.File]::WriteAllText(
    (Join-Path $legalDir "THIRD_PARTY_NOTICES.md"),
    $noticesText + "`r`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Staged ${Rid}:"
Write-Output "  binary:  $dllName ($binaryHash)"
Write-Output "  manifest: native/staging/$Rid/native/biscuitsharp-native.json"
Write-Output "  legal:    native/staging/$Rid/legal/"
