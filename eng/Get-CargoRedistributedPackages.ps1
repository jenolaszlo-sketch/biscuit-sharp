# Shared target-aware resolver used by staging and verification.
param(
    [Parameter(Mandatory = $true)][string]$Cargo,
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$Target
)
$ErrorActionPreference = 'Stop'
$json = & $Cargo metadata --locked --format-version 1 --filter-platform $Target --manifest-path $ManifestPath
if ($LASTEXITCODE -ne 0) { throw "cargo metadata failed for $Target" }
$metadata = ($json -join "`n") | ConvertFrom-Json
if ($null -eq $metadata.resolve) { throw 'cargo metadata did not return a resolved graph' }
$packages = @{}
foreach ($package in $metadata.packages) { $packages[$package.id] = $package }
$rootId = @($metadata.resolve.root)
if ($rootId.Count -eq 0 -or -not $rootId[0]) { throw 'Cargo workspace root package is missing' }
$visited = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$queue = [System.Collections.Generic.Queue[string]]::new()
$queue.Enqueue([string]$rootId[0])
while ($queue.Count -gt 0) {
    $id = $queue.Dequeue()
    if (-not $visited.Add($id)) { continue }
    $node = $metadata.resolve.nodes | Where-Object { $_.id -eq $id } | Select-Object -First 1
    if ($null -eq $node) { continue }
    foreach ($dep in $node.deps) {
        $usable = @($dep.dep_kinds | Where-Object {
            ($null -eq $_.kind -or $_.kind -eq '')
        }).Count -gt 0
        if (-not $usable) { continue }
        $child = $packages[[string]$dep.pkg]
        if ($null -eq $child) { throw "Resolved package missing from cargo metadata: $($dep.pkg)" }
        # Procedural macro crates and their private support graph execute on the
        # build host and are not part of the shipped runtime dependency graph.
        $isProcMacro = @($child.targets | Where-Object { $_.crate_types -contains 'proc-macro' }).Count -gt 0
        if (-not $isProcMacro) { $queue.Enqueue([string]$dep.pkg) }
    }
}
$result = foreach ($id in $visited) {
    $pkg = $packages[$id]
    if ($pkg.name -ne 'biscuitsharp_native') { $pkg }
}
@($result | Sort-Object name, version)
