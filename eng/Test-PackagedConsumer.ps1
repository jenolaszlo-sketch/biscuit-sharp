#Requires -Version 5.1
<#
.SYNOPSIS
  Runs a clean external package consumer against a built nupkg (M2 gate).
.DESCRIPTION
  Creates an isolated console project outside the repo with an isolated NuGet
  cache, restores and verifies the selected BiscuitSharp package, then exercises:
  generate/import key, issue, serialize, parse/verify, attenuate, authorize
  (allow + deny), seal, revocation IDs, and version identity. Asserts exit code
  0 and the expected stdout markers. With -Aot, it also publishes and runs the
  consumer for the selected RID. NuGet source mapping keeps BiscuitSharp local
  while allowing the NativeAOT compiler packages to restore from nuget.org.
  Fails closed otherwise.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test-PackagedConsumer.ps1 -Package artifacts/packages/BiscuitSharp.0.1.0-preview.1.nupkg -TargetFramework net10.0
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test-PackagedConsumer.ps1 -Package artifacts/packages/BiscuitSharp.0.1.0-preview.1.nupkg -TargetFramework net10.0 -Aot -Rid win-x64
#>
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [ValidateSet("net8.0", "net10.0")][string]$TargetFramework = "net10.0",
    [switch]$UsePublicFeed,
    [switch]$Aot,
    [ValidateSet("win-x64", "linux-x64", "osx-arm64")][string]$Rid
)

$ErrorActionPreference = "Stop"

function Fail([string]$message) { throw "PACKAGED-CONSUMER: $message" }

function Get-ContentHashes([string]$archivePath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $map = @{}
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith("/") -or $entry.FullName -eq ".signature.p7s") { continue }
            if ($map.ContainsKey($entry.FullName)) { Fail "duplicate archive entry '$($entry.FullName)' in $archivePath" }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $map[$entry.FullName] = [BitConverter]::ToString($sha.ComputeHash($stream)) }
            finally { $sha.Dispose(); $stream.Dispose() }
        }
        return $map
    } finally { $archive.Dispose() }
}

if ($Aot -and -not $Rid) { Fail "-Rid is required with -Aot" }
if (-not $Aot -and $Rid) { Fail "-Rid can only be used with -Aot" }
if ($Aot -and $UsePublicFeed) { Fail "-Aot consumes the selected local candidate package; omit -UsePublicFeed" }

if (-not (Test-Path -LiteralPath $Package)) { Fail "package not found: $Package" }
$Package = (Resolve-Path -LiteralPath $Package).Path
$version = [System.IO.Path]::GetFileNameWithoutExtension($Package) -replace '^BiscuitSharp\.', ''
if ($version -eq [System.IO.Path]::GetFileNameWithoutExtension($Package)) { Fail "cannot parse version from $Package" }

$previousPackages = $env:NUGET_PACKAGES
$previousNativePath = $env:BISCUITSHARP_NATIVE_PATH
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-consumer-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    # Exercise the package's RID asset in every path, even if the caller had a
    # local-development override in its environment.
    Remove-Item Env:\BISCUITSHARP_NATIVE_PATH -ErrorAction SilentlyContinue
    Push-Location -LiteralPath $work
    try {
        dotnet new console -n Consumer -o . --force --no-restore 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "dotnet new failed" }

        # Pin the requested target framework (templates default to the newest).
        $csproj = Join-Path $work "Consumer.csproj"
        $csprojText = Get-Content -LiteralPath $csproj -Raw
        if ($csprojText -notmatch "<TargetFramework>$TargetFramework</TargetFramework>") {
            $csprojText = $csprojText -replace "<TargetFramework>.*?</TargetFramework>", "<TargetFramework>$TargetFramework</TargetFramework>"
            Set-Content -LiteralPath $csproj -Value $csprojText -NoNewline
        }

        $feed = Join-Path $work "local-packages"
        New-Item -ItemType Directory -Path $feed | Out-Null
        Copy-Item -LiteralPath $Package -Destination $feed -Force
        $snupkg = [System.IO.Path]::ChangeExtension($Package, ".snupkg")
        if (Test-Path -LiteralPath $snupkg) { Copy-Item -LiteralPath $snupkg -Destination $feed -Force }

        if ($Aot) {
            $packageSources = @'
    <add key="local" value="./local-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
'@
            $sourceMapping = @'
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="BiscuitSharp" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
'@
        }
        elseif ($UsePublicFeed) {
            $packageSources = '    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
            $sourceMapping = ""
        }
        else {
            $packageSources = '    <add key="local" value="./local-packages" />'
            $sourceMapping = ""
        }
        Set-Content -LiteralPath (Join-Path $work "nuget.config") -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
$packageSources
  </packageSources>
$sourceMapping
</configuration>
"@

        $env:NUGET_PACKAGES = Join-Path $work "nuget-cache"
        dotnet add package BiscuitSharp --version $version 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "package restore failed (isolated cache)" }

        # Compare the restored archive against the selected candidate in every
        # mode. The public-feed mode tolerates repository signing metadata only.
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $cachedPackage = Join-Path $env:NUGET_PACKAGES "biscuitsharp/$version/biscuitsharp.$version.nupkg"
        if (-not (Test-Path -LiteralPath $cachedPackage)) { Fail "restored BiscuitSharp archive is missing: $cachedPackage" }
        $expected = Get-ContentHashes $Package
        $actual = Get-ContentHashes $cachedPackage
        if ($actual.Count -ne $expected.Count) { Fail "restored archive entry count differs from selected artifact" }
        foreach ($name in $expected.Keys) {
            if ($actual[$name] -ne $expected[$name]) { Fail "restored package content differs from selected artifact: $name" }
        }
        Write-Output "Restored BiscuitSharp archive matches the selected candidate package."

        if ($UsePublicFeed) {
            Write-Output "Published package content matches the selected CI artifact."
        }
        $program = @'
// Clean packaged-consumer exercise: key, issue, serialize, parse/verify,
// attenuate, authorize (allow + deny), seal, revocation IDs, version.
using BiscuitSharp;

using var root = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.Ed25519);
BiscuitToken token = BiscuitTokenBuilder
    .Create()
    .AddFact("""right("workspace.main", "read")""")
    .Build(root);

BiscuitToken parsed = BiscuitToken.Parse(token.ToBytes(), root.PublicKey);
BiscuitToken child = parsed.Attenuate(BiscuitBlock.Create("""check if operation("read");"""));
BiscuitAuthorizationResult allow = BiscuitAuthorizer
    .For(child)
    .AddFact("""operation("read")""")
    .AddPolicy("""allow if right("workspace.main", "read");""")
    .Authorize();
BiscuitAuthorizationResult deny = BiscuitAuthorizer
    .For(child)
    .AddFact("""operation("write")""")
    .AddPolicy("""allow if right("workspace.main", "read");""")
    .Authorize();
BiscuitToken sealedToken = child.Seal();
Console.WriteLine(
    $"allow={allow.IsAuthorized} deny={deny.IsAuthorized} " +
    $"sealed={sealedToken.Inspect().IsSealed} " +
    $"revocation={sealedToken.GetRevocationIds().Count} " +
    $"version={BiscuitEngine.GetVersion().BiscuitAuthVersion}");
return (allow.IsAuthorized && !deny.IsAuthorized && sealedToken.Inspect().IsSealed) ? 0 : 1;
'@
        Set-Content -LiteralPath (Join-Path $work "Program.cs") -Value $program -Encoding Ascii

        $out = dotnet run --framework $TargetFramework 2>&1
        if ($LASTEXITCODE -ne 0) { Fail "consumer exited $LASTEXITCODE :: $out" }
        foreach ($marker in @("allow=True", "deny=False", "sealed=True", "revocation=2", "version=6.0.0")) {
            if (($out -join "`n") -notmatch [regex]::Escape($marker)) {
                Fail "missing stdout marker '$marker' :: $out"
            }
        }
        Write-Output "OK consumer ($TargetFramework): $($out -join ' ')"

        if ($Aot) {
            $publishArgs = @(
                "publish", $csproj,
                "--configuration", "Release",
                "--framework", $TargetFramework,
                "--runtime", $Rid,
                "--self-contained", "true",
                "--nologo",
                "-p:PublishAot=true",
                "-p:TreatWarningsAsErrors=true",
                "-p:ILLinkTreatWarningsAsErrors=true",
                "-p:IlcTreatWarningsAsErrors=true"
            )
            $publishOutput = & dotnet @publishArgs 2>&1
            if ($LASTEXITCODE -ne 0) { Fail "NativeAOT publish failed for $Rid :: $($publishOutput -join ' ')" }

            $publishedConsumer = Join-Path $work "bin/Release/$TargetFramework/$Rid/publish/Consumer"
            if ([Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
                $publishedConsumer += ".exe"
            }
            if (-not (Test-Path -LiteralPath $publishedConsumer)) { Fail "NativeAOT executable not found: $publishedConsumer" }

            $aotOutput = & $publishedConsumer 2>&1
            if ($LASTEXITCODE -ne 0) { Fail "NativeAOT consumer exited $LASTEXITCODE :: $($aotOutput -join ' ')" }
            foreach ($marker in @("allow=True", "deny=False", "sealed=True", "revocation=2", "version=6.0.0")) {
                if (($aotOutput -join "`n") -notmatch [regex]::Escape($marker)) {
                    Fail "NativeAOT consumer missing stdout marker '$marker' :: $aotOutput"
                }
            }
            Write-Output "OK NativeAOT consumer ($TargetFramework, $Rid): $($aotOutput -join ' ')"
        }
    }
    finally { Pop-Location }
}
finally {
    if ($null -eq $previousPackages) { Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue }
    else { $env:NUGET_PACKAGES = $previousPackages }
    if ($null -eq $previousNativePath) { Remove-Item Env:\BISCUITSHARP_NATIVE_PATH -ErrorAction SilentlyContinue }
    else { $env:BISCUITSHARP_NATIVE_PATH = $previousNativePath }
    $resolvedWork = [IO.Path]::GetFullPath($work)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedWork.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedWork)).StartsWith("biscuit-consumer-", [StringComparison]::Ordinal)) {
        Fail "refusing cleanup outside the consumer temporary directory"
    }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Packaged-consumer test passed ($TargetFramework)."
