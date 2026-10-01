#Requires -Version 5.1
<#
.SYNOPSIS
  Runs a clean external package consumer against a built nupkg (M2 gate).
.DESCRIPTION
  Creates an isolated console project outside the repo with an isolated NuGet
  cache and a package source containing ONLY the given nupkg, then exercises:
  generate/import key, issue, serialize, parse/verify, attenuate, authorize
  (allow + deny), seal, revocation IDs, and version identity. Asserts exit code
  0 and the expected stdout markers. Fails closed otherwise.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File eng/Test-PackagedConsumer.ps1 -Package artifacts/packages/BiscuitSharp.0.1.0-preview.1.nupkg -TargetFramework net10.0
#>
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [ValidateSet("net8.0", "net10.0")][string]$TargetFramework = "net10.0",
    [switch]$UsePublicFeed
)

$ErrorActionPreference = "Stop"

function Fail([string]$message) { throw "PACKAGED-CONSUMER: $message" }

if (-not (Test-Path -LiteralPath $Package)) { Fail "package not found: $Package" }
$Package = (Resolve-Path -LiteralPath $Package).Path
$version = [System.IO.Path]::GetFileNameWithoutExtension($Package) -replace '^BiscuitSharp\.', ''
if ($version -eq [System.IO.Path]::GetFileNameWithoutExtension($Package)) { Fail "cannot parse version from $Package" }

$previousPackages = $env:NUGET_PACKAGES
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-consumer-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
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

        $sourceName = "local"
        $sourceUrl = "./local-packages"
        if ($UsePublicFeed) {
            $sourceName = "nuget.org"
            $sourceUrl = "https://api.nuget.org/v3/index.json"
        }
        Set-Content -LiteralPath (Join-Path $work "nuget.config") -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="$sourceName" value="$sourceUrl" />
  </packageSources>
</configuration>
"@

        $env:NUGET_PACKAGES = Join-Path $work "nuget-cache"
        dotnet add package BiscuitSharp --version $version 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "package restore failed (isolated cache, source=$sourceName)" }

        if ($UsePublicFeed) {
            # Repository signing adds .signature.p7s; other content must match CI.
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $cachedPackage = Join-Path $env:NUGET_PACKAGES "biscuitsharp/$version/biscuitsharp.$version.nupkg"
            function ContentHashes([string]$archivePath) {
                $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
                try {
                    $map = @{}
                    foreach ($entry in $archive.Entries) {
                        if ($entry.FullName.EndsWith("/") -or $entry.FullName -eq ".signature.p7s") { continue }
                        if ($map.ContainsKey($entry.FullName)) { Fail "duplicate archive entry" }
                        $stream = $entry.Open()
                        $sha = [Security.Cryptography.SHA256]::Create()
                        try { $map[$entry.FullName] = [BitConverter]::ToString($sha.ComputeHash($stream)) }
                        finally { $sha.Dispose(); $stream.Dispose() }
                    }
                    return $map
                } finally { $archive.Dispose() }
            }
            $expected = ContentHashes $Package
            $actual = ContentHashes $cachedPackage
            if ($actual.Count -ne $expected.Count) { Fail "published archive entry count differs from qualified artifact" }
            foreach ($name in $expected.Keys) {
                if ($actual[$name] -ne $expected[$name]) { Fail "published content differs: $name" }
            }
            Write-Output "Published archive content matches the qualified CI artifact."
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
    }
    finally { Pop-Location }
}
finally {
    if ($null -eq $previousPackages) { Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue }
    else { $env:NUGET_PACKAGES = $previousPackages }
    $resolvedWork = [IO.Path]::GetFullPath($work)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedWork.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedWork)).StartsWith("biscuit-consumer-", [StringComparison]::Ordinal)) {
        Fail "refusing cleanup outside the consumer temporary directory"
    }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "Packaged-consumer test passed ($TargetFramework)."
