#Requires -Version 5.1
<#
.SYNOPSIS
  Builds the BiscuitSharp native bridge for one RID (M2).
.DESCRIPTION
  Scaffolding stub. M2 implements: cargo --locked build --release --target
  <triple> for win-x64 / linux-x64 / osx-arm64, then stages the binary,
  biscuitsharp-native.json manifest, and licenses under native/staging/<rid>/.
#>
param(
  [Parameter(Mandatory = $true)][string]$Rid
)
throw "Not implemented yet (M2). See docs/implementation-plan.md."
