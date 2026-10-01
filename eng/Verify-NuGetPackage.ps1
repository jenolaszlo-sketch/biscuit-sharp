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

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-nupkg-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Copy-Item -LiteralPath $Package -Destination (Join-Path $work "package.zip") -Force
    Expand-Archive -LiteralPath (Join-Path $work "package.zip") -DestinationPath (Join-Path $work "unpacked")

    $root = Join-Path $work "unpacked"
    $need = @(
        "lib\net8.0\BiscuitSharp.dll", "lib\net8.0\BiscuitSharp.xml",
        "lib\net10.0\BiscuitSharp.dll", "lib\net10.0\BiscuitSharp.xml",
        "README.md", "LICENSE", "NOTICE"
    )
    foreach ($rel in $need) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $rel))) { Fail "missing $rel" }
    }

    foreach ($r in $Rids) {
        if (-not $files.ContainsKey($r)) { Fail "unknown RID '$r'" }
        $dll = Join-Path $root "runtimes\$r\native\$($files[$r])"
        $manifest = Join-Path $root "runtimes\$r\native\biscuitsharp-native.json"
        if (-not (Test-Path -LiteralPath $dll)) { Fail "missing runtimes/$r/native/$($files[$r])" }
        if (-not (Test-Path -LiteralPath $manifest)) { Fail "missing runtimes/$r/native/biscuitsharp-native.json" }
        $info = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
        if ("$($info.binary_sha256)" -ne $hash) { Fail "$r : packaged binary does not match manifest hash" }
        if ("$($info.rid)" -ne $r) { Fail "$r : manifest rid is '$($info.rid)'" }
        if ("$($info.abi)" -ne "1") { Fail "$r : manifest abi is '$($info.abi)'" }
        # Legal notices are split by the project packing rules: the upstream
        # license travels with the runtime, the transitive inventory under
        # third-party/.
        $runtimeLegal = Get-ChildItem -File -LiteralPath (Join-Path $root "runtimes\$r\legal") -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty Name
        $thirdPartyLegal = Join-Path $root "third-party\$r\legal\THIRD_PARTY_NOTICES.md"
        if (-not ($runtimeLegal -contains "biscuit-auth-LICENSE")) {
            Fail "$r : runtimes legal notices omit biscuit-auth-LICENSE"
        }
        if (-not (Test-Path -LiteralPath $thirdPartyLegal)) {
            Fail "$r : third-party notices missing"
        }
        $notices = Get-Content -LiteralPath $thirdPartyLegal -Raw
        if ($notices -notmatch "biscuit-auth 6\.0\.0") {
            Fail "$r : third-party notices do not pin biscuit-auth 6.0.0"
        }
        Write-Output "OK $r : packaged asset matches manifest ($hash)"
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
    Write-Output "OK symbols package present"
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

Write-Output "Package verification passed for: $([System.IO.Path]::GetFileName($Package))"
