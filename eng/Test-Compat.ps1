#Requires -Version 5.1
<#
.SYNOPSIS
  Runs the BiscuitSharp bidirectional compatibility gate (M1).
.DESCRIPTION
  Exchanges token fixtures between direct upstream Rust calls and the
  BiscuitSharp bridge in both directions, proving BiscuitSharp creates and
  consumes genuine Biscuit tokens:

  1. cargo unit tests (native boundary).
  2. compat_gen: direct Rust issues a root + parent token (artifacts/compat/rust).
  3. committed fixtures: deterministic vectors verified (fixtures/compat).
  4. dotnet tests: the bridge verifies/authorizes the Rust parent, attenuates it
     (managed/child.json), and issues a bridge token (managed/token.json).
  5. compat_consume: direct Rust verifies/authorizes both managed artifacts and
     attenuates the managed token (rust/child2.json).
  6. dotnet tests with BISCUITSHARP_COMPAT_CONSUME=1: the bridge verifies and
     authorizes the Rust-attenuated child.

  Generated fixtures live under artifacts/compat (git-ignored); only
  fixtures/compat is committed.
#>
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cargo = Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe"
if (-not (Test-Path -LiteralPath $cargo)) { $cargo = "cargo" }

& $cargo test --locked --lib --manifest-path (Join-Path $repoRoot "native\Cargo.toml")
if ($LASTEXITCODE -ne 0) { throw "cargo unit tests failed" }

& $cargo test --locked --test compat_gen --manifest-path (Join-Path $repoRoot "native\Cargo.toml")
if ($LASTEXITCODE -ne 0) { throw "compat_gen failed" }

& $cargo test --locked --test committed_fixtures --manifest-path (Join-Path $repoRoot "native\Cargo.toml")
if ($LASTEXITCODE -ne 0) { throw "committed fixtures failed" }

Push-Location -LiteralPath $repoRoot
try {
  dotnet run --project tests/BiscuitSharp.Tests --framework net8.0
  if ($LASTEXITCODE -ne 0) { throw "managed tests (net8.0) failed" }
}
finally { Pop-Location }

& $cargo test --locked --test compat_consume --manifest-path (Join-Path $repoRoot "native\Cargo.toml")
if ($LASTEXITCODE -ne 0) { throw "compat_consume failed" }

Push-Location -LiteralPath $repoRoot
try {
  $env:BISCUITSHARP_COMPAT_CONSUME = "1"
  dotnet run --project tests/BiscuitSharp.Tests --framework net8.0
  if ($LASTEXITCODE -ne 0) { throw "managed consume phase (net8.0) failed" }
  dotnet run --project tests/BiscuitSharp.Tests --framework net10.0
  if ($LASTEXITCODE -ne 0) { throw "managed tests (net10.0) failed" }
}
finally {
  Remove-Item Env:\BISCUITSHARP_COMPAT_CONSUME -ErrorAction SilentlyContinue
  Pop-Location }

Write-Output "Compatibility gate passed."
