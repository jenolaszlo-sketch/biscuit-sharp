# Verification

## Current Hufu consumer continuation — 2026-10-03

The [handoff](hufu-integration-handoff.md) supersedes historical Hufu counts below:
93 Biscuit, 99 existing Hufu and 19 IO cases pass per framework on Windows x64.
Real Local/Luban/Hufu.IO read authorization components and explicit local
fixed-policy/host measurements are covered. The source inventory is refreshed.
Atomic start-to-read ordering, actual governed mutations, production host
authentication/custody/capacity, published IO adoption and adapter release remain
open. No BiscuitSharp runtime/native/API change or new wrapper release occurred.

## M3.3 Hufu adapter consumption — 2026-10-02

The sibling local Hufu adapter consumes the exact CI 36979786333 preview.2 nupkg;
the restored cache archive hash matches c5f0c94aa14cf82b68239ed49314a3cf468780337432cdff89975beed6ead3b1.
No wrapper project reference or native-path override is used. On Windows x64,
63 adapter cases pass on net8.0 and net10.0, plus 95 existing Hufu cases per TFM.
These exercise real native Biscuit, real all-layer Cedar and disk SQLite,
including dual required evidence, concurrent writer ordering for revocation/key
retirement, exact start binding and non-dispatching receipt replay. The runtime
participant is a SQL fixture; actual resource/provider integration is pending.
Solution restore and its transitive vulnerability inventory pass after aligning
Hufu's SQLite bundle with Zhinu's 2.1.12 pin.

This is local consumer evidence, not wrapper code changes, package publication
or a qualified production Hufu boundary. See the
[integration handoff](hufu-integration-handoff.md#m33-local-implementation-and-next-handoff)
and sibling Hufu qualification record for scope and remaining gates.

## M3.2 typed-failure qualification — 2026-10-02

Implementation f89285702ebade4b7fe92e4bfb2f72080c8d72ab passed all 19 jobs in
[CI 36979786333](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36979786333).
This qualifies the 210-entry development preview.2 candidate on win-x64,
linux-x64 and osx-arm64, including both managed TFMs, native release/lints,
distribution, legal/source/tamper gates, exact-SHA SourceLink, six typed packaged
consumers, package-reference net10 NativeAOT on every RID and strict Linux
Valgrind. Preview.2 is not published.

Downloaded exact CI artifacts (before any NuGet repository signing):

| Artifact | SHA-256 |
| --- | --- |
| BiscuitSharp.0.1.0-preview.2.nupkg | c5f0c94aa14cf82b68239ed49314a3cf468780337432cdff89975beed6ead3b1 |
| BiscuitSharp.0.1.0-preview.2.snupkg | 01fed20b3984e139f745006e5135e4fcb2601844ccd5e4b77251cfdd5986b256 |

The exact three-RID nupkg additionally passed 13 Hufu fixed-policy checks on each
of net8.0/net10.0 on Windows, including origin-scoped attenuation pollution and
typed zero-time denial. Restore used only the selected artifact feed and a new
isolated cache, with no project reference or native override; the restored
archive SHA-256 matched the table. Probe output is
PACKAGED_SPEC_POLICY_PROBE_PASSED cases=13; scratch source is ignored under
artifacts/hufu-package-policy-probe. This verifies a policy package consumer,
not a complete Hufu adapter or host enforcement.

The local implementation checks below were executed against the working tree
based on f057b1c. Older 201-entry records are historical; published preview.1
and its evidence are unchanged.

| Windows x64 check | Result |
| --- | --- |
| dotnet build BiscuitSharp.slnx | Pass, zero warnings/errors |
| cargo build --locked --manifest-path native/Cargo.toml | Pass, real debug bridge rebuilt |
| cargo test --locked --lib --manifest-path native/Cargo.toml | Pass, 49/49 native tests; 16 authorizer tests |
| cargo fmt / all-target clippy with -D warnings | Pass |
| BiscuitSharp.Tests on net8.0 and net10.0 | Pass, all typed mappings, live fact/iteration/time starvation, expression error, ordinary denials and malformed/unknown/absent reasons |
| BiscuitSharp.ApiSurface on net8.0 and net10.0 | Pass, reviewed 210-entry inventory |
| eng/Build-Native.ps1 -Rid win-x64 | Pass, locked release staging and legal material |
| Single-RID smoke pack and Verify-NuGetPackage.ps1 -Rids win-x64 | Pass, archive/native/manifest/legal/symbol checks |
| Test-PackagedConsumer.ps1 net8.0 / net10.0 and net10 NativeAOT win-x64 | Pass, isolated caches and selected package archive comparison; typed failure and closed-decision markers |

Local package output: artifacts/hufu-budget-reasons. It contains only win-x64;
it is not the three-RID release artifact. The enum/property are generic wrapper
diagnostics; this is not Hufu adapter implementation or Hufu workload qualification.
Full cross-platform CI is qualified in the exact run above. No new package was published.

## Published preview and restored baseline — 2026-10-02

BiscuitSharp 0.1.0-preview.1 is [available on NuGet.org](https://www.nuget.org/packages/BiscuitSharp/0.1.0-preview.1).
This record supersedes the historical candidate selections and pending release
steps below. The published candidate is
78924586b70f4188db6cfc47983f8171c78725f8, qualified by all 19 jobs in
[CI 36946889887](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36946889887).
The corrected publisher at 6b011e83a53aa4cd2aa90d32c0ecedbb3898de9e also passed
all 19 jobs in [CI 36967314470](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36967314470).

[Publication 36967314658](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36967314658)
succeeded using NuGet trusted login. NuGet accepted the package at 05:07:39 UTC
and symbols at 05:09:44 UTC. All six isolated NuGet.org consumers passed in
[verification 36968162532](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36968162532):
.NET 8/10 on Windows x64, Linux x64 and macOS arm64. Each compared every restored
archive entry with the selected CI artifact, excluding the NuGet repository
signature. Public-feed restore and ordinary execution are verified; NativeAOT
execution is covered by the original qualified CI artifact.

| Uploaded artifact | SHA-256 before NuGet repository signing |
| --- | --- |
| BiscuitSharp.0.1.0-preview.1.nupkg | 7c240882c8b63b91f9fe122ff39529ceb08f1090200e60c15b443d7e91e7323e |
| BiscuitSharp.0.1.0-preview.1.snupkg | 07d845c40ad83919a3f854dcc4e803a69b520b1cb7f39624da28f21f10b87c6c |

PackageValidationBaselineVersion is restored to 0.1.0-preview.1. The development
version is 0.1.0-preview.2; it has not been published. All 19 jobs passed at
a67b29a8d6db24b1c7144ce8cbb83b5035a5ddb4 in
[CI 36968484267](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36968484267).
This validates the three-RID development package against the published binary
baseline, including six packaged consumers and net10 NativeAOT on each platform.
A local Windows single-RID pack independently passed with zero warnings/errors;
the SDK diagnostic confirms the preview.1 archive was the baseline input.
Both framework inventories still match all 201 entries. The contract, API
inventory, upstream pin, lockfile and ABI remain frozen. Preview graduation is
complete. This final release-record update changes documentation only.

| Qualified development artifact (not published) | SHA-256 |
| --- | --- |
| BiscuitSharp.0.1.0-preview.2.nupkg | c86a425b72496abe4d7dfb63b24514c172c9b92d5d3419fc06520eda887ab6d1 |
| BiscuitSharp.0.1.0-preview.2.snupkg | 86b619891c26af862bfa6b0889d124b4bdfcef5f3e9deac12bbe8126f33d1ebe |

Stable 1.0 still requires complete public XML documentation; Hufu integration
and its workload budgets remain separate consumer work.

## Historical second-audit qualification — 2026-10-02

F09–F12 and the process-RID, budget and package-reference NativeAOT improvements
are qualified by all 19 successful jobs at
112bdffaaa933da594588ba256ea0db6efbb211f
([run 36943970067](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36943970067)).
The complete three-RID legal/source/tamper gates, both-TFM inventory/compile
rejection/budget tests, all six package consumers, net10 package-reference
NativeAOT on each RID, compatibility and strict Valgrind passed.

| Selected artifact | SHA-256 |
| --- | --- |
| BiscuitSharp.0.1.0-preview.1.nupkg | 84d0b440502d48486a2b7099a0f5286f10e5fad6eb9c4d3d238769d06ec732a2 |
| BiscuitSharp.0.1.0-preview.1.snupkg | 61790753fcf27985954f3015df4371903924449f1b01a980b6b27e4fb06f39c8 |

These artifacts were downloaded and fingerprinted locally; both portable PDB
SourceLink mappings were reverified against the full selected SHA. The old
5dd9e19 artifact below is superseded. Historical candidate instructions used
the optional publish ci_run_id 36943970067;
its SHA is derived. verify-published.yml still takes that run ID and the full SHA
above. Normal future publishes can leave both publisher inputs blank after the
dispatch commit passes its full CI. Public-feed qualification and baseline restoration remain pending.


Local Windows evidence: both managed TFMs pass, including all four parameter
factory round-trips/byte ownership and malformed inspection decoding. Windows
staging and a single-RID smoke package pass source-bound legal verification;
both staging and package reject changed text, self-consistently rehashed text,
and a removed source entry. Operator/protected-member inventory regressions,
external-consumer subclass rejection and process-RID mapping run on both TFMs.
The 201-entry API inventory matches both TFMs. Four concurrent hostile default
workloads fail closed on both TFMs with sampled memory observations. Clean
Windows package-reference NativeAOT publish/run passes without a native override.
Cross-platform qualification is complete in the selected run above.

## Superseded preview candidate qualification — 2026-10-02

The complete 19-job matrix passed at release SHA
5dd9e199b32ea18d1499a5e055cbe658c9e0b0a1
([run 36940126702](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36940126702)).
All jobs succeeded, covering:

- Managed .NET 8/10 on Windows, Linux and macOS; public API inventory checks on
  both frameworks and ordinary/concurrent/hostile-growth budget probes.
- Bidirectional Rust compatibility on all three RIDs; native lints and release
  hardening tests.
- Three-RID assets, complete legal/source inventories and NativeAOT execution.
- Full package verification, legal tamper rejection and portable PDB SourceLink
  mappings bound to the exact release SHA.
- Six isolated clean packaged consumers (.NET 8/10 on every supported RID).
- Real 50-cycle Linux Valgrind with zero definite/indirect/possible losses,
  zero suppressed errors and zero memory errors.

Exact candidate artifacts downloaded from that run:

| Artifact | SHA-256 |
| --- | --- |
| BiscuitSharp.0.1.0-preview.1.nupkg | 2b0d1ecfdd6a7835d387b5ddcc88e6aa6b2fbc71e09f5c29d86c82638ad342c1 |
| BiscuitSharp.0.1.0-preview.1.snupkg | 2b921d6282a20986651952ee1df648e3a774eb81f46556323834678cddce4d56 |

The checked preview surface has 189 inventory entries. Semantics are recorded in
ADR 0002 and budget observations in authorizer-budget.md. NUGET_USER exists;
the maintainer reports the trusted-publisher policy configured. Protected
environment approval configuration is pending an explicit reviewer/self-review
choice. Automatic approval review rejected creating it with self-approval
enabled without explicit authorization; no environment was created by that
attempt.

Historical publication instructions used run ID 36940126702 and the full SHA
above. Do not publish that superseded artifact. After a new qualified publication, dispatch
verify-published.yml with the same inputs for six clean NuGet.org consumers and
archive content comparisons, then restore the published API compatibility
baseline and validate the next candidate. No preview or stable package was
published during this preparation; baseline restoration remains dependent on
public availability.

## Historical evidence

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
