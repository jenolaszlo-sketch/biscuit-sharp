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
    [string]$Rid = "",
    [string]$StagingRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$nativeDir = Join-Path $repoRoot "native"
if ($StagingRoot -eq "") { $StagingRoot = Join-Path $nativeDir "staging" }
$stagingRoot = $StagingRoot

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
$cargo = Join-Path $HOME ".cargo/bin/cargo"
if (-not (Test-Path -LiteralPath $cargo)) { $cargo = Join-Path $HOME ".cargo/bin/cargo.exe" }
if (-not (Test-Path -LiteralPath $cargo)) { $cargo = "cargo" }
$lockText = Get-Content -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Raw

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
    foreach ($legal in @("THIRD_PARTY_NOTICES.md", "licenses.json")) {
        if (-not (Test-Path -LiteralPath (Join-Path $legalStage $legal))) { Fail "$r : missing legal/$legal" }
    }
    $notices = Get-Content -LiteralPath (Join-Path $legalStage "THIRD_PARTY_NOTICES.md") -Raw
    $triple = $triples[$r]
    $expectedPackages = @(& (Join-Path $PSScriptRoot "Get-CargoRedistributedPackages.ps1") -Cargo $cargo -ManifestPath (Join-Path $nativeDir "Cargo.toml") -Target $triple)
    if ($LASTEXITCODE -ne 0) { Fail "$r : cargo runtime dependency resolution failed" }
    $packagesById = @{}
    foreach ($pkg in $expectedPackages) { $packagesById["$($pkg.name)@$($pkg.version)"] = $pkg }
    $inventory = @(Get-Content -LiteralPath (Join-Path $legalStage "licenses.json") -Raw | ConvertFrom-Json)
    $expectedIds = @($expectedPackages | ForEach-Object { "$($_.name)@$($_.version)" } | Sort-Object -Unique)
    $actualIds = @($inventory | ForEach-Object { $_.package_id } | Sort-Object -Unique)
    if (($expectedIds -join "|") -ne ($actualIds -join "|")) { Fail "$r : dependency inventory coverage differs from Cargo target runtime graph" }
    $listedMaterials = @("THIRD_PARTY_NOTICES.md", "licenses.json")
    foreach ($entry in $inventory) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.declared_license) -or [string]$entry.source -notmatch '^registry\+' -or [string]$entry.registry_archive_sha256 -notmatch '^[0-9a-f]{64}$') { Fail "$r : incomplete license/source metadata for $($entry.package_id)" }
        $resolved = $packagesById[[string]$entry.package_id]
        if ($null -eq $resolved -or $entry.source -ne $resolved.source -or $entry.declared_license -ne $resolved.license) { Fail "$r : source/license metadata differs from Cargo for $($entry.package_id)" }
        $pattern = '(?ms)^\[\[package\]\]\r?\n(?:(?!\[\[package\]\]).)*?^name = "' + [regex]::Escape($entry.name) + '"\r?\nversion = "' + [regex]::Escape($entry.version) + '"(?:(?!\[\[package\]\]).)*?^checksum = "([0-9a-f]{64})"'
        $match = [regex]::Match($lockText, $pattern)
        if (-not $match.Success -or $match.Groups[1].Value -ne $entry.registry_archive_sha256) { Fail "$r : source checksum mismatch for $($entry.package_id)" }
        $archiveName = "$($entry.name)-$($entry.version).crate"
        $archive = Get-ChildItem -LiteralPath (Join-Path $HOME ".cargo/registry/cache") -Filter $archiveName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $archive -or (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.registry_archive_sha256) { Fail "$r : cached registry archive checksum mismatch for $($entry.package_id)" }
        if (@($entry.license_material).Count -eq 0) { Fail "$r : no redistributed legal text for $($entry.package_id)" }
        foreach ($material in $entry.license_material) {
            $relative = [string]$material.path -replace '/', [System.IO.Path]::DirectorySeparatorChar
            $path = Join-Path $legalStage $relative
            if (-not (Test-Path -LiteralPath $path)) { Fail "$r : missing legal material $($material.path)" }
            $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($hash -ne $material.sha256) { Fail "$r : legal material hash mismatch $($material.path)" }
            $listedMaterials += $relative
        }
    }
    $actualLegal = @(Get-ChildItem -LiteralPath $legalStage -File -Recurse | ForEach-Object { $_.FullName.Substring($legalStage.Length + 1) })
    $actualLegalList = (($actualLegal | Sort-Object -Unique) -join "|")
    $listedLegalList = (($listedMaterials | Sort-Object -Unique) -join "|")
    if ($actualLegalList -ne $listedLegalList) { Fail "$r : legal staging contains unlisted or missing files" }
    if ($notices -notmatch "biscuit-auth 6\.0\.0") { Fail "$r : notices do not include biscuit-auth 6.0.0" }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $binaryHash = (Get-FileHash -LiteralPath $dllPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checks = @(
        @{ Field = "abi"; Expected = 1; Actual = $manifest.abi },
        @{ Field = "bridge"; Expected = "0.1.0"; Actual = $manifest.bridge },
        @{ Field = "biscuit_auth"; Expected = "6.0.0"; Actual = $manifest.biscuit_auth },
        @{ Field = "upstream_commit"; Expected = "0f0b4e0e6fe07220c1ba6b51bff21d450d94a975"; Actual = $manifest.upstream_commit },
        @{ Field = "cargo_lock_sha256"; Expected = $lockHash; Actual = $manifest.cargo_lock_sha256 },
        @{ Field = "toolchain"; Expected = "1.89.0"; Actual = $manifest.toolchain },
        @{ Field = "target"; Expected = $triples[$r]; Actual = $manifest.target },
        @{ Field = "rid"; Expected = $r; Actual = $manifest.rid },
        @{ Field = "binary"; Expected = $files[$r]; Actual = $manifest.binary },
        @{ Field = "binary_sha256"; Expected = $binaryHash; Actual = $manifest.binary_sha256 },
        @{ Field = "min_schema_version"; Expected = 3; Actual = $manifest.min_schema_version },
        @{ Field = "max_schema_version"; Expected = 6; Actual = $manifest.max_schema_version },
        @{ Field = "datalog"; Expected = "3.3"; Actual = $manifest.datalog }
    )
    foreach ($c in $checks) {
        if ("$($c.Actual)" -ne "$($c.Expected)") {
            Fail "$r : manifest field '$($c.Field)' is '$($c.Actual)', expected '$($c.Expected)'"
        }
    }
    if ("$($manifest.rust)" -notmatch '^1\.89\.0(?:\s|$)') { Fail "$r : rust identity is '$($manifest.rust)', expected pinned 1.89.0" }
    $expectedFeatures = @("regex-full", "datalog-macro", "pem") | Sort-Object
    $actualFeatures = @($manifest.enabled_features | Sort-Object)
    if (($expectedFeatures -join "|") -ne ($actualFeatures -join "|")) { Fail "$r : manifest features differ from pinned feature set" }

    Write-Output "OK $r : manifest + hashes + licenses verified ($binaryHash)"
}

Write-Output "Staging verification passed for: $($rids -join ', ')"
