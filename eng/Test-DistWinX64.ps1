#Requires -Version 5.1
<#
.SYNOPSIS
  Runs the Windows x64 distribution gate (M2 subset): stage, verify, AOT,
  pack, verify package, clean consumers.
.DESCRIPTION
  1. eng/Build-Native.ps1 -Rid win-x64 (release triple build + staging).
  2. eng/Verify-NativeStaging.ps1 -Rid win-x64.
  3. NativeAOT publish + execute of samples/BiscuitSharp.AotSmoke against the
     staged release asset.
  4. Local-smoke pack + eng/Verify-NuGetPackage.ps1 (win-x64).
  5. eng/Test-PackagedConsumer.ps1 on net8.0 and net10.0.
  The full M2 gate additionally needs linux-x64 and osx-arm64; see
  docs/implementation-plan.md.
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$eng = Join-Path $repoRoot "eng"

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $eng "Build-Native.ps1") -Rid win-x64
if ($LASTEXITCODE -ne 0) { throw "Build-Native failed" }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $eng "Verify-NativeStaging.ps1") -Rid win-x64
if ($LASTEXITCODE -ne 0) { throw "Verify-NativeStaging failed" }

Push-Location -LiteralPath $repoRoot
try {
    dotnet publish samples/BiscuitSharp.AotSmoke -r win-x64 -c Release 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "AOT publish failed" }
    $aot = Join-Path $repoRoot "samples\BiscuitSharp.AotSmoke\bin\Release\net10.0\win-x64\publish\BiscuitSharp.AotSmoke.exe"
    if (-not (Test-Path -LiteralPath $aot)) { throw "AOT exe missing: $aot" }
    $env:BISCUITSHARP_NATIVE_PATH = Join-Path $repoRoot "native\staging\win-x64\native\biscuitsharp_native.dll"
    $aotOut = & $aot 2>&1
    if ($LASTEXITCODE -ne 0) { throw "AOT smoke failed: $aotOut" }
    Write-Output "OK AOT smoke (win-x64): $aotOut"

    dotnet pack src/BiscuitSharp/BiscuitSharp.csproj -p:BiscuitSharpEnablePack=true -p:BiscuitSharpLocalSmokePack=true -o artifacts/packages 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed" }
}
finally {
    Remove-Item Env:\BISCUITSHARP_NATIVE_PATH -ErrorAction SilentlyContinue
    Pop-Location
}

$pkg = Get-ChildItem -LiteralPath (Join-Path $repoRoot "artifacts\packages") -Filter "BiscuitSharp.*.nupkg" |
    Where-Object { $_.Name -notlike "*.snupkg" } | Select-Object -First 1 -ExpandProperty FullName
if (-not $pkg) { throw "no nupkg found in artifacts/packages" }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $eng "Verify-NuGetPackage.ps1") -Package $pkg -Rids win-x64
if ($LASTEXITCODE -ne 0) { throw "Verify-NuGetPackage failed" }

foreach ($tfm in @("net8.0", "net10.0")) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $eng "Test-PackagedConsumer.ps1") -Package $pkg -TargetFramework $tfm
    if ($LASTEXITCODE -ne 0) { throw "Test-PackagedConsumer ($tfm) failed" }
}

Write-Output "Windows x64 distribution gate passed."
