# Verification

Executed evidence recorded from prior runs. The earlier full CI run passed at
SHA `7294611c908adb17375f150890c61ebd9f8cf4a6` ([run 36878763035](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36878763035)); the audit found that its Valgrind command selected zero tests. HEAD CI at SHA `fb82be79737396da5cb4823072a94738eabd118b` ([run 36880499152](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36880499152)) also completed successfully, but the same leak-coverage limitation applies. No new CI was executed for this correction. These historical runs do not verify changes made after their SHAs or prove that the corrected leak workload passes.

The earlier M2 green claim is not release qualification: full dependency license
material was also incomplete. Corrected gates and target-specific legal bundles
are implemented; the full three-RID CI rerun remains pending.

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Pass (0 warnings, 0 errors) |
| M1 functional tests (3 OS × 2 TFM) | `dotnet run --project tests/BiscuitSharp.Tests` | Pass (154/154 on a clean tree): version identity + differential hashes, loader gates + manifest probes, key round-trips (DER/PEM export; raw/hex/prefixed public import), value equality, token issue (incl. empty authority) /verify/attenuate/seal, builder reuse, rules, authorizer limits, tamper/truncation/garbage rejection, unbound-template rejection, revocation growth, inspection, typed params, unicode, time-fact expiry, disposal, privacy, allow/deny/failed-checks/explicit-deny, determinism, adversarial sweeps, concurrency |
| Native tests (`--locked`, 3 OS) | `cargo test --locked --lib` in `native/` | Pass (45/45): boundary envelope, panic containment, key round-trips (differential vs direct upstream), public import, token create/parse canonical round-trips, typed params incl. unbound rejection, empty authority, rules, tamper/truncation/garbage codes, attenuate/seal semantics, revocation growth, inspection, malformed envelopes/limits, authorizer default-limits guard, allow/deny/failed-checks, first-match-wins, determinism, concurrency, 4,096-case mutation matrix |
| Locked dependency fetch | `cargo fetch --locked` (Rust 1.89.0) | Pass (94 packages) |
| Bidirectional compatibility (3 OS) | `eng/Test-Compat.ps1` | Pass: all four directions verified + authorized across the boundary; committed genesis fixture verified |
| Mutation/fuzz | `cargo test --locked --lib adversarial` + managed adversarial section | Pass (dev profile): 4,096 deterministic cases — zero panics, zero unjustified successes; managed 96-position token sweep + 7 invalid policies, all fail-closed. Pass (release profile, 43/43 lib): same invariants hold; per-run distribution varies from fresh corpus randomness in both profiles. Locked in CI (`native-release` job, Ubuntu). |
| Leak workload selection | `cargo test --locked --offline --lib --manifest-path native/Cargo.toml -- adversarial::leak_probe_cycles --exact --nocapture` | Pass locally: exactly one named test, start/completion markers for 50 cycles; corrected test output verified. Linux Valgrind instrumentation is pending |
| Leak instrumentation | Linux `valgrind` on `adversarial::leak_probe_cycles` | Earlier result invalidated because the filter ran zero tests. Corrected CI requires the named passing test, both workload markers, exact one-test count, and zero definite leaks; rerun pending |
| Distribution staging + NativeAOT | `eng/Test-Dist.ps1 -Rid <rid>` | Corrected target-runtime license inventory implemented. Local win-x64 native staging and legal verification pass; three-RID NativeAOT and staging rerun pending |
| Package verification | `dotnet pack` + `eng/Verify-NuGetPackage.ps1` | Local win-x64 smoke package passes, including archive legal coverage/checksums and net8.0/net10.0 PDBs. Full three-RID package gate pending |
| Clean consumers (3 RIDs × 2 TFMs) | `eng/Test-PackagedConsumer.ps1` | Pass in CI: six isolated-cache consumers (generate/import, issue, serialize, parse/verify, attenuate, allow + deny, seal, revocation IDs, version) |
| Publication | `publish.yml` (manual) | Workflow verifies the exact successful `ci.yml` run, main push SHA, all full-matrix jobs, immutable run artifact, protected `nuget-production` environment, and NuGet OIDC. Environment protection, trusted-publisher policy, credentials, and publication are not configured/performed here |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.

## Audit fix verification — 2026-10-01 (Windows x64)

Executed against the uncommitted fix tree based on fb82be79737396da5cb4823072a94738eabd118b. These results do not qualify a new three-RID release.

| Gate | Result |
| --- | --- |
| dotnet build BiscuitSharp.slnx --no-restore | Passed, 0 warnings / 0 errors. |
| dotnet run --no-build --project tests/BiscuitSharp.Tests --framework net8.0 and net10.0 | Both passed, including concurrent first-load success/failure/live-identity retry probes, byte identity/dictionary mutation regressions, result constructor/with snapshots, malformed response taxonomy, policy-index consistency, normalization/authorization/revocation parity, and submillisecond limits. |
| eng/Test-Compat.ps1 | Passed complete native/direct-Rust/managed exchange and consume phases on Windows, with committed fixture and both managed TFMs. |
| cargo test --locked --offline --lib --manifest-path native/Cargo.toml | 45 passed. |
| cargo test --locked --offline --release --lib --manifest-path native/Cargo.toml | 45 passed. |
| cargo test --locked --offline --lib --manifest-path native/Cargo.toml -- adversarial::leak_probe_cycles --exact --nocapture | Exactly 1 passed; start/completion markers reported 50 cycles. This is not Linux Valgrind evidence. |
| cargo fmt --manifest-path native/Cargo.toml -- --check | Passed. |
| cargo clippy --locked --offline --all-targets --manifest-path native/Cargo.toml -- -D warnings | Passed. |
| dotnet publish samples/BiscuitSharp.AotSmoke -r win-x64 -c Release --no-restore, then executable with staged native override | Passed native compilation and execution; bridge=0.1.0, ABI=1. |

The corrected Linux leak gate, Linux/macOS distribution and consumers, and manual trusted-publishing job need a new final-commit CI run. Publication and baseline restoration were not performed.

### Final Windows package checks

- Build-Native.ps1 and Verify-NativeStaging.ps1 -Rid win-x64 passed after the target-aware legal inventory changes.
- A local smoke pack (BiscuitSharpEnablePack=true; BiscuitSharpLocalSmokePack=true) produced artifacts/audit-fixes/BiscuitSharp.0.1.0-preview.1.nupkg and its snupkg. Only win-x64 is staged in this local package; it is not the three-RID release.
- Verify-NuGetPackage.ps1 -Rids win-x64 passed full archive/native/manifest/dependency/source checksum/legal material/symbol checks.
- Test-PackagedConsumer.ps1 passed on net8.0 and net10.0 with isolated NuGet caches and a local-only feed.
- Test-LegalVerification.ps1 passed: altered staged and packaged license text was rejected. The tests are wired into CI's pack job for each RID.
- nupkg SHA-256: 5a28c5bfe658fbe6d156e57996383b9c6b34190d31c7155ae4a085687eb6c351.
- snupkg SHA-256: 26831d650023ed3fac399107b7d027b0fdb8366bc23a4abf22f762e442ebc92c.
- win-x64 native SHA-256: c560b5a00163f0f355c95540b2ca05d06ae236301e60f9ce01c615c764278250.

Manual publishing setup is documented in [publishing.md](publishing.md). No remote release configuration or publication was performed.

## Valgrind failure follow-up — 2026-10-02

[Diagnostic CI run 36933754724](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36933754724), SHA 44cea75ca20a69ee0157d52541a51de4ae322f92, executed all 50 cycles under Valgrind 3.22.0. It reported zero definite/indirect losses, 1,028 bytes possibly lost and 456 bytes reachable. Full stacks identify 48 bytes in Rust libtest's thread/event-channel context, 980 bytes in the bridge key-store HashMap allocation, and the reachable allocation in Rust's stack-overflow runtime.

The follow-up releases map capacity when the last handle is destroyed. CI now instruments a standalone example using the same shared 50-cycle public-ABI workload as the unit test, avoiding libtest's event-channel allocation. Memory errors and possible leaks still fail; no suppressions or leak-policy relaxation were added. Start, completion and success markers prevent an empty workload from passing.

Windows checks on the follow-up tree: all 45 native unit tests pass, the standalone example prints all three 50-cycle markers and exits successfully, and cargo fmt/all-target clippy with -D warnings pass. Linux Valgrind verification passed in [CI run 36934503054](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36934503054) at implementation SHA 332dd0dca69021589419d8b292c91bb90ae1f4e8: all three 50-cycle markers, zero definite/indirect/possible losses, zero suppressed errors, and ERROR SUMMARY: 0 errors from 0 contexts. The remaining 456 reachable bytes are in Rust runtime stack-overflow bookkeeping. Windows optimized native tests also pass all 45 cases. This evidence covers that implementation SHA; the full package/consumer matrix was still running when this entry was written.