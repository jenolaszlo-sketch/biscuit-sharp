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
. (Join-Path $PSScriptRoot "CargoLegal.ps1")

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
$legalFullPath = [System.IO.Path]::GetFullPath($legalDir)
$stagingBoundary = [System.IO.Path]::GetFullPath((Join-Path $nativeDir "staging")) + [System.IO.Path]::DirectorySeparatorChar
if (-not $legalFullPath.StartsWith($stagingBoundary, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Legal staging path escapes native/staging." }
if (Test-Path -LiteralPath $legalFullPath) { Remove-Item -LiteralPath $legalFullPath -Recurse -Force }
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

# Full redistribution inventory for the target's normal runtime graph. Each
# entry records the registry archive checksum (Cargo.lock) and SHA-256 for every
# shipped upstream license/copyright/notice file. Missing legal material fails.
$targetPackages = @(& (Join-Path $PSScriptRoot "Get-CargoRedistributedPackages.ps1") -Cargo $cargo -ManifestPath (Join-Path $nativeDir "Cargo.toml") -Target $triple)
if ($LASTEXITCODE -ne 0 -or $targetPackages.Count -eq 0) { throw "Could not resolve runtime dependency graph for $Rid" }
$lockText = Get-Content -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Raw
$inventory = [System.Collections.Generic.List[object]]::new()
$notices = [System.Collections.Generic.List[string]]::new()
$notices.Add("# Third-party license materials for BiscuitSharp ($Rid)")
$notices.Add("")
$notices.Add("Generated from the Cargo.lock runtime dependency graph for target $triple. Build-only procedural macros and target-inapplicable dependencies are excluded. The package inventory and material checksums are recorded in licenses.json.")
$notices.Add("")
foreach ($pkg in $targetPackages) {
    if ([string]::IsNullOrWhiteSpace([string]$pkg.license)) { throw "Missing declared license expression: $($pkg.name) $($pkg.version)" }
    $source = Get-CrateLegalMaterials $pkg $lockText
    $archiveHash = $source.archive_sha256
    $materials = [System.Collections.Generic.List[object]]::new()
    foreach ($material in $source.materials) {
        $destination = Join-Path $legalDir $material.path
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        [IO.File]::WriteAllBytes($destination, $material.bytes)
        $materials.Add([ordered]@{ path = $material.path; sha256 = $material.sha256; source_path = $material.source_path })
    }
    $inventory.Add([ordered]@{
        name = $pkg.name; version = $pkg.version; package_id = "$($pkg.name)@$($pkg.version)"
        source = $pkg.source; registry_archive_sha256 = $archiveHash
        declared_license = $pkg.license; license_material = @($materials)
    })
    $notices.Add("## $($pkg.name) $($pkg.version)")
    $notices.Add("")
    $notices.Add("Declared license: $($pkg.license). Registry archive SHA-256: $archiveHash.")
    foreach ($material in $materials) { $notices.Add("- $($material.path) (SHA-256 $($material.sha256))") }
    $notices.Add("")
}
$inventoryPath = Join-Path $legalDir "licenses.json"
[System.IO.File]::WriteAllText($inventoryPath, (ConvertTo-Json -InputObject @($inventory) -Depth 8) + "`r`n", [System.Text.UTF8Encoding]::new($false))
$noticesText = $notices -join "`r`n"
[System.IO.File]::WriteAllText(
    (Join-Path $legalDir "THIRD_PARTY_NOTICES.md"),
    $noticesText + "`r`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Output "Staged ${Rid}:"
Write-Output "  binary:  $dllName ($binaryHash)"
Write-Output "  manifest: native/staging/$Rid/native/biscuitsharp-native.json"
Write-Output "  legal:    native/staging/$Rid/legal/"
