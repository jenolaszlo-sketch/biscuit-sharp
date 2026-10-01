#Requires -Version 5.1
<#
.SYNOPSIS
  Runs one RID's distribution gate: stage, verify staging, AOT smoke.
.DESCRIPTION
  1. eng/Build-Native.ps1 -Rid <rid> (release triple build + staging).
  2. eng/Verify-NativeStaging.ps1 -Rid <rid>.
  3. NativeAOT publish + execute of samples/BiscuitSharp.AotSmoke against the
     staged release asset.
  Packaging (all RIDs) and clean consumers run in separate CI jobs; see
  docs/implementation-plan.md.
.EXAMPLE
  pwsh -NoProfile -File eng/Test-Dist.ps1 -Rid linux-x64
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64", "osx-arm64")]
    [string]$Rid
)

$ErrorActionPreference = "Stop"

function Fail([string]$message) { throw "DIST: $message" }

$repoRoot = Split-Path -Parent $PSScriptRoot
$eng = Join-Path $repoRoot "eng"
$files = @{
    "win-x64"   = "biscuitsharp_native.dll"
    "linux-x64" = "libbiscuitsharp_native.so"
    "osx-arm64" = "libbiscuitsharp_native.dylib"
}
$exeName = "BiscuitSharp.AotSmoke"
if ($Rid -eq "win-x64") { $exeName += ".exe" }

& (Join-Path $eng "Build-Native.ps1") -Rid $Rid
& (Join-Path $eng "Verify-NativeStaging.ps1") -Rid $Rid

Push-Location -LiteralPath $repoRoot
try {
    dotnet publish samples/BiscuitSharp.AotSmoke -r $Rid -c Release
    if ($LASTEXITCODE -ne 0) { Fail "AOT publish failed ($Rid)" }
    $aot = Join-Path $repoRoot "samples/BiscuitSharp.AotSmoke/bin/Release/net10.0/$Rid/publish/$exeName"
    if (-not (Test-Path -LiteralPath $aot)) { Fail "AOT exe missing: $aot" }
    $env:BISCUITSHARP_NATIVE_PATH = Join-Path $repoRoot "native/staging/$Rid/native/$($files[$Rid])"
    try {
        $aotOut = & $aot 2>&1
        if ($LASTEXITCODE -ne 0) { Fail "AOT smoke failed ($Rid): $aotOut" }
        Write-Output "OK AOT smoke ($Rid): $aotOut"
    }
    finally {
        Remove-Item Env:\BISCUITSHARP_NATIVE_PATH -ErrorAction SilentlyContinue
    }
}
finally { Pop-Location }

Write-Output "Distribution gate passed ($Rid)."
