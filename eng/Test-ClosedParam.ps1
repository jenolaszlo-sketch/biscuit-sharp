#Requires -Version 5.1
<#
.SYNOPSIS
  Verifies from an external consumer that BiscuitParam cannot be subclassed.
.DESCRIPTION
  Builds a temporary project referencing BiscuitSharp and attempts to derive
  from the public parameter type. The build must fail specifically with CS0509
  (cannot derive from sealed type), so unrelated restore or compilation errors
  cannot make this negative gate pass.
#>
param(
    [ValidateSet("net8.0", "net10.0")][string]$TargetFramework = "net8.0"
)

$ErrorActionPreference = "Stop"
$repository = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$libraryProject = Join-Path $repository "src/BiscuitSharp/BiscuitSharp.csproj"
if (-not (Test-Path -LiteralPath $libraryProject)) {
    throw "CLOSED-PARAM: library project not found: $libraryProject"
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("biscuit-closed-param-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $project = Join-Path $work "ExternalConsumer.csproj"
    $projectText = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>__TARGET_FRAMEWORK__</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="__LIBRARY_PROJECT__" />
  </ItemGroup>
</Project>
'@
    $projectText = $projectText.Replace("__TARGET_FRAMEWORK__", $TargetFramework)
    $projectText = $projectText.Replace("__LIBRARY_PROJECT__", [System.Security.SecurityElement]::Escape($libraryProject))
    Set-Content -LiteralPath $project -Value $projectText
    Set-Content -LiteralPath (Join-Path $work "ConsumerParam.cs") -Value @'
using BiscuitSharp;

public sealed record ConsumerParam : BiscuitParam;
'@

    $buildOutput = & dotnet build $project --nologo --verbosity quiet 2>&1
    $buildExitCode = $LASTEXITCODE
    $buildText = $buildOutput -join [Environment]::NewLine
    if ($buildExitCode -eq 0) {
        throw "CLOSED-PARAM: an external record successfully derived from BiscuitParam."
    }

    if ($buildText -notmatch '\bCS0509\b') {
        Write-Output $buildText
        throw "CLOSED-PARAM: expected CS0509 for deriving from sealed BiscuitParam; build failed for another reason (exit $buildExitCode)."
    }

    Write-Output "CLOSED-PARAM: external derivation rejected with CS0509."
}
finally {
    if (Test-Path -LiteralPath $work) {
        $resolvedWork = [IO.Path]::GetFullPath($work)
        $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedWork.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -or
            -not ([IO.Path]::GetFileName($resolvedWork)).StartsWith("biscuit-closed-param-", [StringComparison]::Ordinal)) {
            throw "CLOSED-PARAM: refusing cleanup outside the consumer temporary directory"
        }
        Remove-Item -LiteralPath $resolvedWork -Recurse -Force
    }
}
