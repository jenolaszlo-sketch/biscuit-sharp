#Requires -Version 5.1
<#
.SYNOPSIS
  Verifies an assembled BiscuitSharp NuGet package: inventory, hashes, identity.
.DESCRIPTION
  Expands the nupkg to a temp dir and checks: lib/net8.0 + lib/net10.0 assemblies
  with XML docs; per expected RID, runtimes/<rid>/native/{binary, manifest} with
  the manifest hash matching the packaged binary; README/LICENSE/NOTICE present;
  legal notices present; nuspec id/version; no unexpected top-level entries.
  Fails closed on anything missing or mismatched. See docs/native-boundary.md.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Verify-NuGetPackage.ps1 -Package artifacts/packages/BiscuitSharp.0.1.0-preview.1.nupkg -Rids win-x64
#>
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [string[]]$Rids = @("win-x64", "linux-x64", "osx-arm64")
)

$ErrorActionPreference = "Stop"

function Fail([string]$message) { throw "PACKAGE-VERIFY: $message" }

if (-not (Test-Path -LiteralPath $Package)) { Fail "package not found: $Package" }
$files = @{
    "win-x64"   = "biscuitsharp_native.dll"
    "linux-x64" = "libbiscuitsharp_native.so"
    "osx-arm64" = "libbiscuitsharp_native.dylib"
}
$triples = @{
    "win-x64"   = "x86_64-pc-windows-msvc"
    "linux-x64" = "x86_64-unknown-linux-gnu"
    "osx-arm64" = "aarch64-apple-darwin"
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$nativeDir = Join-Path $repoRoot "native"
$cargo = Join-Path $HOME ".cargo/bin/cargo"
if (-not (Test-Path -LiteralPath $cargo)) { $cargo = Join-Path $HOME ".cargo/bin/cargo.exe" }
if (-not (Test-Path -LiteralPath $cargo)) { $cargo = "cargo" }
$lockText = Get-Content -LiteralPath (Join-Path $nativeDir "Cargo.lock") -Raw

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-nupkg-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $verificationStage = Join-Path $work "staging"
    New-Item -ItemType Directory -Path $verificationStage -Force | Out-Null
    Copy-Item -LiteralPath $Package -Destination (Join-Path $work "package.zip") -Force
    Expand-Archive -LiteralPath (Join-Path $work "package.zip") -DestinationPath (Join-Path $work "unpacked")

    $root = Join-Path $work "unpacked"
    $need = @(
        "lib/net8.0/BiscuitSharp.dll", "lib/net8.0/BiscuitSharp.xml",
        "lib/net10.0/BiscuitSharp.dll", "lib/net10.0/BiscuitSharp.xml",
        "README.md", "LICENSE", "NOTICE"
    )
    foreach ($rel in $need) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $rel))) { Fail "missing $rel" }
    }

    foreach ($r in $Rids) {
        if (-not $files.ContainsKey($r)) { Fail "unknown RID '$r'" }
        $expectedPackages = @(& (Join-Path $PSScriptRoot "Get-CargoRedistributedPackages.ps1") -Cargo $cargo -ManifestPath (Join-Path $nativeDir "Cargo.toml") -Target $triples[$r])
        if ($LASTEXITCODE -ne 0) { Fail "$r : cargo runtime dependency resolution failed" }
        $packagesById = @{}
        foreach ($pkg in $expectedPackages) { $packagesById["$($pkg.name)@$($pkg.version)"] = $pkg }
        $dll = Join-Path $root "runtimes/$r/native/$($files[$r])"
        $manifest = Join-Path $root "runtimes/$r/native/biscuitsharp-native.json"
        if (-not (Test-Path -LiteralPath $dll)) { Fail "missing runtimes/$r/native/$($files[$r])" }
        if (-not (Test-Path -LiteralPath $manifest)) { Fail "missing runtimes/$r/native/biscuitsharp-native.json" }
        $runtimeEntries = @(Get-ChildItem -LiteralPath (Join-Path $root "runtimes/$r") | Select-Object -ExpandProperty Name)
        if ($runtimeEntries.Count -ne 1 -or $runtimeEntries[0] -ne "native") { Fail "$r : unexpected runtime package content" }
        $info = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
        if ("$($info.binary_sha256)" -ne $hash) { Fail "$r : packaged binary does not match manifest hash" }
        if ("$($info.rid)" -ne $r) { Fail "$r : manifest rid is '$($info.rid)'" }
        if ("$($info.abi)" -ne "1") { Fail "$r : manifest abi is '$($info.abi)'" }
        $thirdPartyRoot = Join-Path $root "third-party/$r/legal"
        $thirdPartyLegal = Join-Path $thirdPartyRoot "THIRD_PARTY_NOTICES.md"
        $inventoryPath = Join-Path $thirdPartyRoot "licenses.json"
        if (-not (Test-Path -LiteralPath $thirdPartyLegal) -or -not (Test-Path -LiteralPath $inventoryPath)) {
            Fail "$r : full third-party license inventory is missing"
        }
        $notices = Get-Content -LiteralPath $thirdPartyLegal -Raw
        $inventory = @(Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json)
        $ids = @($inventory | ForEach-Object { $_.package_id } | Sort-Object -Unique)
        if ($ids.Count -ne $inventory.Count -or $ids.Count -eq 0) { Fail "$r : duplicate or empty dependency inventory" }
        $expectedIds = @($expectedPackages | ForEach-Object { "$($_.name)@$($_.version)" } | Sort-Object -Unique)
        if (($ids -join "|") -ne ($expectedIds -join "|")) { Fail "$r : packaged dependency coverage differs from target runtime graph" }
        if (-not ($ids -contains "biscuit-auth@6.0.0")) { Fail "$r : inventory omits biscuit-auth 6.0.0" }
        $expectedLegal = @("THIRD_PARTY_NOTICES.md", "licenses.json")
        foreach ($entry in $inventory) {
            if ([string]::IsNullOrWhiteSpace([string]$entry.declared_license) -or [string]$entry.source -notmatch '^registry\+' -or [string]$entry.registry_archive_sha256 -notmatch '^[0-9a-f]{64}$' -or @($entry.license_material).Count -eq 0) {
                Fail "$r : incomplete legal inventory entry $($entry.package_id)"
            }
            $resolved = $packagesById[[string]$entry.package_id]
            if ($null -eq $resolved -or $entry.source -ne $resolved.source -or $entry.declared_license -ne $resolved.license) { Fail "$r : source/license metadata differs from Cargo for $($entry.package_id)" }
            $lockPattern = '(?ms)^\[\[package\]\]\r?\n(?:(?!\[\[package\]\]).)*?^name = "' + [regex]::Escape($entry.name) + '"\r?\nversion = "' + [regex]::Escape($entry.version) + '"(?:(?!\[\[package\]\]).)*?^checksum = "([0-9a-f]{64})"'
            $lockMatch = [regex]::Match($lockText, $lockPattern)
            if (-not $lockMatch.Success -or $lockMatch.Groups[1].Value -ne $entry.registry_archive_sha256) { Fail "$r : source checksum mismatch for $($entry.package_id)" }
            $archiveName = "$($entry.name)-$($entry.version).crate"
            $archive = Get-ChildItem -LiteralPath (Join-Path $HOME ".cargo/registry/cache") -Filter $archiveName -File -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -eq $archive -or (Get-FileHash -LiteralPath $archive.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.registry_archive_sha256) { Fail "$r : cached registry archive checksum mismatch for $($entry.package_id)" }
            foreach ($material in $entry.license_material) {
                $relative = [string]$material.path -replace '/', [System.IO.Path]::DirectorySeparatorChar
                $path = Join-Path $thirdPartyRoot $relative
                if (-not (Test-Path -LiteralPath $path)) { Fail "$r : package omits $($material.path)" }
                $materialHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($materialHash -ne $material.sha256) { Fail "$r : checksum mismatch for $($material.path)" }
                $expectedLegal += $relative
            }
        }
        $actualLegal = @(Get-ChildItem -LiteralPath $thirdPartyRoot -File -Recurse | ForEach-Object { $_.FullName.Substring($thirdPartyRoot.Length + 1) })
        if ((($actualLegal | Sort-Object -Unique) -join "|") -ne (($expectedLegal | Sort-Object -Unique) -join "|")) { Fail "$r : package legal files differ from checksummed inventory" }
        if ($notices -notmatch "biscuit-auth 6\.0\.0") { Fail "$r : third-party notices do not pin biscuit-auth 6.0.0" }
        $ridStage = Join-Path $verificationStage $r
        New-Item -ItemType Directory -Path (Join-Path $ridStage "native") -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $ridStage "legal") -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $root "runtimes/$r/native") -Force | Copy-Item -Destination (Join-Path $ridStage "native") -Force
        Get-ChildItem -LiteralPath $thirdPartyRoot -Force | Copy-Item -Destination (Join-Path $ridStage "legal") -Recurse -Force
        Write-Output "OK $r : packaged asset matches manifest ($hash)"
    }

    $runtimeRids = @(Get-ChildItem -LiteralPath (Join-Path $root "runtimes") -Directory | Select-Object -ExpandProperty Name | Sort-Object)
    $expectedRids = @($Rids | Sort-Object -Unique)
    if (($runtimeRids -join "|") -ne ($expectedRids -join "|")) { Fail "runtime RID inventory differs: found $($runtimeRids -join ', ')" }
    foreach ($r in $Rids) {
        & (Join-Path $PSScriptRoot "Verify-NativeStaging.ps1") -Rid $r -StagingRoot $verificationStage
    }

    # No unexpected top-level entries.
    $top = @(Get-ChildItem -LiteralPath $root | Select-Object -ExpandProperty Name | Sort-Object)
    $allowedFiles = @("[Content_Types].xml", "LICENSE", "NOTICE", "README.md") | Sort-Object
    $allowedDirs = @("_rels", "lib", "package", "runtimes", "third-party") | Sort-Object
    $nuspec = @(Get-ChildItem -LiteralPath $root -Filter "*.nuspec" | Select-Object -ExpandProperty Name)
    if ($nuspec.Count -ne 1) { Fail "expected exactly one nuspec" }
    foreach ($entry in $top) {
        $full = Join-Path $root $entry
        if ((Get-Item -LiteralPath $full) -is [System.IO.DirectoryInfo]) {
            if ($allowedDirs -notcontains $entry) { Fail "unexpected directory: $entry" }
        } elseif ($entry -ne $nuspec[0] -and $allowedFiles -notcontains $entry) {
            Fail "unexpected file: $entry"
        }
    }

    [xml]$spec = Get-Content -LiteralPath (Join-Path $root $nuspec[0])
    if ($spec.package.metadata.id -ne "BiscuitSharp") { Fail "nuspec id is '$($spec.package.metadata.id)'" }
    $fileVersion = [System.IO.Path]::GetFileNameWithoutExtension($Package) -replace '^BiscuitSharp\.', ''
    if ($spec.package.metadata.version -ne $fileVersion) {
        Fail "nuspec version '$($spec.package.metadata.version)' disagrees with file '$fileVersion'"
    }
    Write-Output "OK nuspec: id=BiscuitSharp version=$fileVersion"

    $snupkg = [System.IO.Path]::ChangeExtension($Package, ".snupkg")
    if (-not (Test-Path -LiteralPath $snupkg)) { Fail "missing symbols package: $snupkg" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $symbolArchive = [System.IO.Compression.ZipFile]::OpenRead($snupkg)
    try {
        $symbolFiles = @($symbolArchive.Entries | ForEach-Object { $_.FullName })
        foreach ($requiredSymbol in @("BiscuitSharp.nuspec", "lib/net8.0/BiscuitSharp.pdb", "lib/net10.0/BiscuitSharp.pdb", "[Content_Types].xml")) {
            if ($symbolFiles -notcontains $requiredSymbol) { Fail "symbols package missing $requiredSymbol" }
        }
        foreach ($symbol in @("lib/net8.0/BiscuitSharp.pdb", "lib/net10.0/BiscuitSharp.pdb")) {
            $entry = $symbolArchive.GetEntry($symbol)
            if ($null -eq $entry -or $entry.Length -eq 0) { Fail "symbols package has an empty PDB: $symbol" }
        }
    }
    finally { $symbolArchive.Dispose() }
    Write-Output "OK symbols package contains net8.0 and net10.0 PDBs"
}
finally {
    $cleanupPath = [System.IO.Path]::GetFullPath($work)
    $tempBoundary = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([char[]]@('/', '\')) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $cleanupPath.StartsWith($tempBoundary, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Cleanup path escapes the temporary directory." }
    Remove-Item -LiteralPath $cleanupPath -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Package verification passed for: $([System.IO.Path]::GetFileName($Package))"
