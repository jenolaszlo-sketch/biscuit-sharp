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
    [ValidateSet("net8.0", "net10.0")][string]$TargetFramework = "net10.0"
)

$ErrorActionPreference = "Stop"

function Fail([string]$message) { throw "PACKAGED-CONSUMER: $message" }

if (-not (Test-Path -LiteralPath $Package)) { Fail "package not found: $Package" }
$Package = (Resolve-Path -LiteralPath $Package).Path
$version = [System.IO.Path]::GetFileNameWithoutExtension($Package) -replace '^BiscuitSharp\.', ''
if ($version -eq [System.IO.Path]::GetFileNameWithoutExtension($Package)) { Fail "cannot parse version from $Package" }

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

        Set-Content -LiteralPath (Join-Path $work "nuget.config") -Value @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="./local-packages" />
  </packageSources>
</configuration>
"@

        $env:NUGET_PACKAGES = Join-Path $work "nuget-cache"
        dotnet add package BiscuitSharp --version $version 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "package restore failed (isolated cache, local feed only)" }

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
    Remove-Item Env:\NUGET_PACKAGES -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

Write-Output "Packaged-consumer test passed ($TargetFramework)."
