#Requires -Version 5.1
<#
.SYNOPSIS
  Verifies staged native assets: manifest schema, hashes, versions, licenses.
.DESCRIPTION
  For each staged RID under native/staging (or one -Rid), checks the exact
  file inventory, parses biscuitsharp-native.json, and cross-checks every
  pinned field: binary SHA-256 recomputed from the staged file, lockfile hash
  recomputed from native/Cargo.lock, biscuit-auth pin, bridge/ABI identity,
  target triple, RID, and the legal notices. Fails closed on anything missing
  or mismatched. See docs/native-boundary.md.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Verify-NativeStaging.ps1 -Rid win-x64
#>
param(
    [string]$Rid = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$nativeDir = Join-Path $repoRoot "native"
$stagingRoot = Join-Path $nativeDir "staging"

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

function Fail([string]$message) { throw "STAGING-VERIFY: $message" }

$rids = @()
if ($Rid -ne "") {
    if (-not $triples.ContainsKey($Rid)) { Fail "unknown RID '$Rid'" }
    $rids = @($Rid)
} else {
    $rids = @(Get-ChildItem -Directory -LiteralPath $stagingRoot -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    if ($rids.Count -eq 0) { Fail "no staged RIDs under native/staging" }
}

$lockHash = (Get-FileHash -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Algorithm SHA256).Hash.ToLowerInvariant()

foreach ($r in $rids) {
    if (-not $triples.ContainsKey($r)) { Fail "unexpected staged directory '$r'" }
    $nativeStage = Join-Path $stagingRoot "$r/native"
    $legalStage = Join-Path $stagingRoot "$r/legal"
    $dllPath = Join-Path $nativeStage $files[$r]
    $manifestPath = Join-Path $nativeStage "biscuitsharp-native.json"
    if (-not (Test-Path -LiteralPath $dllPath)) { Fail "$r : missing $($files[$r])" }
    if (-not (Test-Path -LiteralPath $manifestPath)) { Fail "$r : missing biscuitsharp-native.json" }

    # Exact inventory: nothing more, nothing less.
    $nativeFiles = @(Get-ChildItem -File -LiteralPath $nativeStage | Select-Object -ExpandProperty Name | Sort-Object)
    $expectedNative = @("biscuitsharp-native.json", $files[$r]) | Sort-Object
    if (($nativeFiles -join "|") -ne ($expectedNative -join "|")) {
        Fail "$r : unexpected native inventory: $($nativeFiles -join ', ')"
    }
    foreach ($legal in @("biscuit-auth-LICENSE", "THIRD_PARTY_NOTICES.md")) {
        if (-not (Test-Path -LiteralPath (Join-Path $legalStage $legal))) { Fail "$r : missing legal/$legal" }
    }
    $notices = Get-Content -LiteralPath (Join-Path $legalStage "THIRD_PARTY_NOTICES.md") -Raw
    if ($notices -notmatch "biscuit-auth 6\.0\.0") { Fail "$r : notices do not pin biscuit-auth 6.0.0" }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $binaryHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checks = @(
        @{ Field = "abi"; Expected = 1; Actual = $manifest.abi },
        @{ Field = "bridge"; Expected = "0.1.0"; Actual = $manifest.bridge },
        @{ Field = "biscuit_auth"; Expected = "6.0.0"; Actual = $manifest.biscuit_auth },
        @{ Field = "upstream_commit"; Expected = "0f0b4e0e6fe07220c1ba6b51bff21d450d94a975"; Actual = $manifest.upstream_commit },
        @{ Field = "target"; Expected = $triples[$r]; Actual = $manifest.target },
        @{ Field = "rid"; Expected = $r; Actual = $manifest.rid },
        @{ Field = "binary"; Expected = $files[$r]; Actual = $manifest.binary },
        @{ Field = "binary_sha256"; Expected = $binaryHash; Actual = $manifest.binary_sha256 },
        @{ Field = "cargo_lock_sha256"; Expected = $lockHash; Actual = $manifest.cargo_lock_sha256 }
    )
    foreach ($c in $checks) {
        if ("$($c.Actual)" -ne "$($c.Expected)") {
            Fail "$r : manifest field '$($c.Field)' is '$($c.Actual)', expected '$($c.Expected)'"
        }
    }
    if (-not ($manifest.enabled_features -contains "pem")) { Fail "$r : manifest features omit pem" }

    Write-Output "OK $r : manifest + hashes + licenses verified ($binaryHash)"
}

Write-Output "Staging verification passed for: $($rids -join ', ')"
