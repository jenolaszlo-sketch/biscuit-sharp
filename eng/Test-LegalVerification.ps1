#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [string]$Rid = "win-x64",
    [string[]]$Rids = @("win-x64", "linux-x64", "osx-arm64")
)
$ErrorActionPreference = "Stop"

function Assert-Rejected([scriptblock]$Action, [string]$ExpectedText) {
    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -match $ExpectedText) {
            Write-Output "OK tamper rejected: $ExpectedText"
            return
        }
        throw
    }
    throw "Verifier accepted tampered input; expected '$ExpectedText'."
}

if (-not (Test-Path -LiteralPath $Package)) { throw "Package not found: $Package" }
$repoRoot = Split-Path -Parent $PSScriptRoot
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-legal-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    # Prove the staging verifier catches an altered legal file.
    $staging = Join-Path $tempRoot "staging"
    $ridStage = Join-Path $staging $Rid
    New-Item -ItemType Directory -Path $ridStage -Force | Out-Null
    foreach ($area in @("native", "legal")) {
        Copy-Item -LiteralPath (Join-Path $repoRoot "native/staging/$Rid/$area") -Destination $ridStage -Recurse
    }
    $stageInventory = Get-Content -LiteralPath (Join-Path $ridStage "legal/licenses.json") -Raw | ConvertFrom-Json
    $stageMaterial = $stageInventory[0].license_material[0]
    $stageLicense = Join-Path (Join-Path $ridStage "legal") ([string]$stageMaterial.path -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $stageLicense)) { throw "Staged legal material missing: $($stageMaterial.path)" }
    [System.IO.File]::AppendAllText($stageLicense, "`ntamper probe`n")
    Assert-Rejected { & (Join-Path $PSScriptRoot "Verify-NativeStaging.ps1") -Rid $Rid -StagingRoot $staging } "legal material hash mismatch"

    # Prove package verification catches changed text after archive extraction.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $unpacked = Join-Path $tempRoot "unpacked"
    $packageCopy = Join-Path $tempRoot "package.zip"
    Copy-Item -LiteralPath $Package -Destination $packageCopy
    Expand-Archive -LiteralPath $packageCopy -DestinationPath $unpacked
    $packageLegalRoot = Join-Path $unpacked "third-party/$Rid/legal"
    $packageInventory = Get-Content -LiteralPath (Join-Path $packageLegalRoot "licenses.json") -Raw | ConvertFrom-Json
    $packageMaterial = $packageInventory[0].license_material[0]
    $packageLicense = Join-Path $packageLegalRoot ([string]$packageMaterial.path -replace '/', [System.IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $packageLicense)) { throw "Packaged legal material missing: $($packageMaterial.path)" }
    [System.IO.File]::AppendAllText($packageLicense, "`ntamper probe`n")
    $badPackage = Join-Path $tempRoot ([System.IO.Path]::GetFileName($Package))
    [System.IO.Compression.ZipFile]::CreateFromDirectory($unpacked, $badPackage)
    Assert-Rejected { & (Join-Path $PSScriptRoot "Verify-NuGetPackage.ps1") -Package $badPackage -Rids $Rids } "checksum mismatch"
}
finally {
    $cleanupPath = [System.IO.Path]::GetFullPath($tempRoot)
    $tempBoundary = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([char[]]@('/', '\')) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $cleanupPath.StartsWith($tempBoundary, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Cleanup path escapes the temporary directory." }
    Remove-Item -LiteralPath $cleanupPath -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Legal staging and package tamper checks passed."
