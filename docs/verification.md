# Verification

Executed evidence for the M0 baseline, the M1 functional surface, and the M2
distribution gate, qualified by the CI matrix on Windows x64, Linux x64, and
macOS ARM64 (.NET 8 and .NET 10). The CI matrix run on `main` passed
(maintainer-confirmed); record the run URL/commit here for auditability.

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Pass (0 warnings, 0 errors) |
| M1 functional tests (3 OS × 2 TFM) | `dotnet run --project tests/BiscuitSharp.Tests` | Pass (154/154 on a clean tree): version identity + differential hashes, loader gates + manifest probes, key round-trips (DER/PEM export; raw/hex/prefixed public import), value equality, token issue (incl. empty authority) /verify/attenuate/seal, builder reuse, rules, authorizer limits, tamper/truncation/garbage rejection, unbound-template rejection, revocation growth, inspection, typed params, unicode, time-fact expiry, disposal, privacy, allow/deny/failed-checks/explicit-deny, determinism, adversarial sweeps, concurrency |
| Native tests (`--locked`, 3 OS) | `cargo test --locked --lib` in `native/` | Pass (45/45): boundary envelope, panic containment, key round-trips (differential vs direct upstream), public import, token create/parse canonical round-trips, typed params incl. unbound rejection, empty authority, rules, tamper/truncation/garbage codes, attenuate/seal semantics, revocation growth, inspection, malformed envelopes/limits, authorizer default-limits guard, allow/deny/failed-checks, first-match-wins, determinism, concurrency, 4,096-case mutation matrix |
| Locked dependency fetch | `cargo fetch --locked` (Rust 1.89.0) | Pass (94 packages) |
| Bidirectional compatibility (3 OS) | `eng/Test-Compat.ps1` | Pass: all four directions verified + authorized across the boundary; committed genesis fixture verified |
| Mutation/fuzz | `cargo test --locked --lib adversarial` + managed adversarial section | Pass (dev profile): 4,096 deterministic cases — zero panics, zero unjustified successes; managed 96-position token sweep + 7 invalid policies, all fail-closed. Pass (release profile, 43/43 lib): same invariants hold; per-run distribution varies from fresh corpus randomness in both profiles. Locked in CI (`native-release` job, Ubuntu). |
| Leak instrumentation | Linux `valgrind` on `leak_probe_cycles` (50 full-lifecycle cycles) | Pass: zero definite leaks (possible-loss recorded by the job) |
| Distribution staging + NativeAOT (3 RIDs) | `eng/Test-Dist.ps1 -Rid <rid>` | Pass: release assets staged with manifests + licenses; NativeAOT published and executed on each RID |
| Package verification (3 RIDs) | `dotnet pack` + `eng/Verify-NuGetPackage.ps1` | Pass: lib/net8.0+net10.0, runtimes for all three RIDs (binary + manifest, hashes matched), legal notices, README/LICENSE/NOTICE, symbols package |
| Clean consumers (3 RIDs × 2 TFMs) | `eng/Test-PackagedConsumer.ps1` | Pass in CI: six isolated-cache consumers (generate/import, issue, serialize, parse/verify, attenuate, allow + deny, seal, revocation IDs, version) |
| Publication | `publish.yml` (manual) | Not done — first preview not yet published |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.
