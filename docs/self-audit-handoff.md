# Independent self-audit and release handoff

Audit date: 2026-10-01 (Asia/Manila).
Repository: C:\Users\Laszlos\source\repos\biscuit-sharp
Reviewed HEAD: fb82be79737396da5cb4823072a94738eabd118b.
Original audit scope: managed public API and contract, native boundary, authorization semantics, provenance, packaging/CI, structure, and consumer usability. The follow-up below records implementation changes made after the review; publication remains pending.


## Fix follow-up — 2026-10-01

The findings below describe the reviewed original commit. The initial fixes were committed and pushed as d8e24b49c84ef5da70fc1f103c6f6dea86e0e465; release qualification remains separate.

| Finding | Current implementation status | Verification / remaining qualification |
| --- | --- | --- |
| F01 loader synchronization | Fixed: every entry acquires the reentrant load lock; exports resolve against the current handle, avoiding stale cached addresses after a rejected load; hash/handle/path cleanup stays inside the failure boundary. | Deterministic child-process barriers pass for success, bad hash, and live-identity mismatch. Both callers wait or fail; failed loads can retry safely. |
| F02 byte identity | Fixed: public array properties return copies; equality/hash and internal marshalling use owned storage. | Input/output mutation, dictionary revocation lookup, and token-root stability regressions pass. |
| F03 empty leak gate | Shared 50-cycle ABI workload runs both as a unit test and a standalone Valgrind example. The standalone gate checks start/completion/success markers and preserves fatal memory errors and possible leaks. Last-handle destruction releases empty key-store capacity. | Diagnostic Linux run 36933754724 identified 48 bytes from libtest and 980 bytes from retained key-store capacity. Windows: 45 tests, standalone probe and all-target clippy pass. Final Linux instrumentation rerun pending; historical zero-test green evidence is invalid. |
| F04 legal material | Fixed in tooling: target-aware runtime dependency graph, checksummed source identity, full LICENSE/COPYING/COPYRIGHT/NOTICE material, coverage/inventory validation; packaged legal files consolidated under third-party. | Windows staging/package checks and rejection checks recorded below. Linux/macOS final packages still require CI. |
| F05 response taxonomy | Fixed: JSON object roots and error fields validated; malformed/unknown envelopes remain bridge failures; policy indices checked for consistency. | Scalar/array/null roots, malformed JSON/error fields, unknown codes and contradictory result regressions pass. Existing native output free remains in finally. |
| F06 collection ownership | Fixed: constructor and record with-expression collection setters make owned read-only snapshots and reject null collections/elements. | Source-list mutation and returned-list mutation checks pass. Collection equality remains explicitly reference-based. |
| F07 contract drift | Fixed: API/security/architecture/plan/roadmap/review ledger reconciled; ADR 0002 records defaults and normalization. | Defaults retained at 100k facts / 100k iterations / 5 s; integer millisecond truncation and tolerated framing pinned by tests. Team acceptance and Hufu workload budgeting remain human/consumer decisions. |
| F08 release binding | Fixed in tooling: manual publisher binds reviewed SHA to exact completed full CI run and its package/symbol artifact; final archive verification checks expected identity, RID inventory and legal coverage. | Publication is not performed. Protected GitHub environment, NuGet trusted-publisher policy/account setting, and a new full CI run are release prerequisites. |

Additional pre-freeze corrections: unknown algorithm values fail fast in raw public-key construction; duplicate typed parameter names are rejected; supported BiscuitParam construction goes through factories; private export/issuance retain the key object through native calls, and allocated key handles are cleaned up if response validation fails. NativeSha256 is captured during load rather than rehashing a possibly replaced pathname on every version query. Public ParseHex/ParsePrefixed/ToPrefixedString stay because they serve configuration and upstream interop.

Local execution against the modified tree:
- Solution build: 0 warnings, 0 errors.
- Native dev and release-profile tests: 45 passed in each profile.
- Full eng/Test-Compat.ps1: passed (direct Rust issue/consume, committed fixture, C# issue/attenuate/consume, .NET 8 and .NET 10).
- Focused managed regressions passed on net8.0 and net10.0.
- Windows NativeAOT publish and executable smoke: passed.
- Final Windows smoke package: full archive/legal/symbol checks, altered-license rejection checks, and isolated-cache consumers on net8.0/net10.0 passed. Package and native hashes are recorded in [verification.md](verification.md).
- cargo fmt --check and clippy --locked --offline --all-targets -- -D warnings: passed.
- Exact leak workload: 1 passed, 50 cycles started and completed; this Windows execution is not Valgrind instrumentation.

The initial fixes and Valgrind diagnostic commit were pushed to main. NuGet publication and baseline restoration remain pending; no newly qualified three-RID release is claimed here. Restore the API package baseline only after the preview exists publicly; re-freeze only after final release evidence and semantic acceptance.


## Original audit recommendation

Do not publish or freeze this surface yet. The existing happy-path and adversarial tests pass locally, but the loader has a first-load synchronization defect, value objects expose mutable identity, and the leak gate can pass without executing its workload. The earlier statement in review-findings.md that there are no known correctness defects should be retired after triage.

P1 = resolve before preview; P2 = resolve or explicitly accept before API freeze; P3 = follow-on improvement. Static findings below are code-derived unless a reproduction is explicitly recorded. No new credential-forgery bypass was demonstrated.

## Findings

### F01 — P1: first-load callers can bypass verification and race an unload

Evidence: src/BiscuitSharp/NativeLoader.cs:57-59, 100-101, 121-128.
EnsureLoaded checks _handle outside Sync and returns whenever it is nonzero. The initializing thread sets _handle before ABI/manifest verification. A second thread can therefore return from EnsureLoaded and invoke the library while initialization is incomplete. If verification then fails, the initializing thread frees that same library while another call may be executing. The comment claiming the lock prevents observation of half-verified state is incorrect because the fast path takes no lock. _loadedPath is published separately as well.

Fix: distinguish an initializing handle from a successfully verified state. A simple initial correction is to put all EnsureLoaded entry checks under Sync; Monitor is reentrant for the internal version probe. Prefer a later refactor that probes exports/version directly using the provisional handle and publishes one immutable verified state only after success. Do not merely make _handle volatile: that preserves the early-publication defect.

Acceptance: a child-process test with a deterministic barrier during initialization must show another caller cannot enter the native operation until verification succeeds. A failing manifest must make both callers fail without invoking an unloaded library.

### F02 — P1: mutable byte identities break dictionaries and token immutability

Evidence: BiscuitKeys.cs:185 (Encoded); BiscuitToken.cs:38 (Value); token Root is the supplied public-key object; builder Build retains rootKey.PublicKey.
Both constructors copy input arrays, but both properties return stored arrays. Equality and hash codes read those arrays. Example: insert a revocation id into a dictionary, mutate id.Value[0], then a lookup can fail because the key hash changed. Mutating token.Root.Encoded also changes the root used by subsequent authorization/attenuation/inspection although the token is advertised as immutable and thread-safe. This is a correctness problem for the documented revocation-key use case, not just a per-access performance tradeoff.

Fix before freeze: keep private arrays, return defensive copies through existing properties, and provide internal read-only span access for marshalling/equality to avoid internal copies. A carefully designed new read-only API is another option; preserve actual ownership rather than relying on a read-only interface alone.

Acceptance: mutations of constructor inputs and returned arrays cannot change equality/hash, dictionary lookup, a built token's root, or subsequent token operations.

### F03 — P1: the Valgrind gate selects zero tests

Evidence: .github/workflows/ci.yml:197 passes "leak_probe --exact"; the actual test is adversarial::leak_probe_cycles (native/src/adversarial.rs:528).
Reproduced using cargo test --locked --offline --lib --manifest-path native/Cargo.toml -- leak_probe --exact --nocapture:
"running 0 tests"; "0 passed ... 45 filtered out". Rust exits successfully. The zero-definite-leaks assertion can therefore describe an empty test run rather than 50 lifecycle cycles. Existing green CI does not establish the claimed lifecycle leak evidence.

Fix: use adversarial::leak_probe_cycles --exact, and assert one test passed or an explicit workload completion marker. Keep the actual process exit and definite-leak checks. Correct verification.md and ROADMAP claims until Linux instrumentation reruns the real workload.

Acceptance: CI log proves exactly one named workload ran, 50 cycles completed, and the leak summary belongs to that execution.

### F04 — P1: legal staging is a declaration inventory, not complete redistribution material

Evidence: eng/Build-Native.ps1 copies only biscuit-auth LICENSE and emits a list of dependency names/versions/license expressions. Verify-NativeStaging and Verify-NuGetPackage check file presence and a biscuit-auth version substring, not dependency coverage or license text.
The local win-x64 inventory includes MIT/Apache and other declarations, but no complete collection of the corresponding dependency license/copyright notices. NOTICE says packages include licenses and notices of resolved Cargo dependencies; the implementation does not substantiate that statement.

Fix: generate a reproducible inventory with source/checksum, chosen license where alternatives exist, required text/copyright notices, and relevant upstream NOTICE files. Explicitly distinguish build-only/platform-inapplicable dependencies from redistributed native code. Fail staging when required legal material cannot be found; the upstream license copy currently silently skips a missing source file. Compare generated package inventory against the resolved target dependency graph.

Acceptance: every redistributed dependency maps to its required material in the actual nupkg, with automated coverage checks. This finding concerns missing release evidence/material; it is not a legal determination.

### F05 — P2: malformed bridge response shapes can escape the exception taxonomy

Evidence: NativeBridge.ReadErrorBody uses TryGetProperty/GetString and catches only JsonException. Valid JSON such as [] or {"code":42} can throw InvalidOperationException. BridgeJson.Parse accepts non-object roots, after which Required* readers similarly call TryGetProperty. NativeLoader manifest readers also assume an object.
Consumers are promised BiscuitBridgeException for response decoding/transport corruption; these cases leak framework exceptions.

Fix: enforce object root kinds and string error-field kinds; consistently wrap invalid response shapes as BiscuitBridgeException. Handle unknown operation error codes as explicitly specified rather than silently treating corruption as an ordinary domain error.
Acceptance: inject array/null/scalar roots, numeric error fields, absent fields, and malformed JSON through an internal response-reader seam. Every case produces the promised exception and frees native output.

### F06 — P2: authorization result collections are mutable despite IReadOnlyList

Evidence: ParseResult returns a mutable List through IReadOnlyList; public record constructors accept arbitrary lists; IsAuthorized derives from Errors.Count. A caller can cast a returned error list to List and edit it. For caller-constructed Allow results, changing the original list can change IsAuthorized after construction. Inspection collections also retain mutable lists. Records suggest value semantics but their collection equality is reference equality.

Fix: choose explicit result semantics before freeze. Prefer defensive snapshots with truly read-only storage; decide whether these should remain records or become immutable classes with explicit equality. Do not add complex structural equality unless consumers need it. Validate public constructor inputs or provide controlled factories.
Acceptance: modifying caller-supplied lists or attempting to mutate returned collections cannot alter a result.

### F07 — P2: API/security docs disagree with implemented behavior

Evidence:
- api-contract.md:38 still says upstream default execution limits apply, contradicting the robust-default paragraph and code.
- Serialization says round-trip without explaining normalization/trailing-byte removal.
- architecture.md:74 says instances are thread-safe despite mutable single-threaded builders/authorizers.
- implementation-plan.md says authorization requires byte-identical content; that is an adversarial test's acceptance invariant, not a production canonical-input rejection rule.
- review-findings.md still calls implemented CI gates missing and understates existing manifest probes.
- security.md's blanket tampering-invalidates-verification statement needs to distinguish signed-content tampering from tolerated outer framing changes.

Fix: update contract/security/architecture/review ledger together and link exact CI evidence. Explicitly say concurrent Authorize is supported only after configuration is complete, with no concurrent mutation.
Acceptance: each behavior has one unambiguous statement and a corresponding test or explicitly stated evidence limitation.

### F08 — P2: insufficient release evidence binding and package verification

Evidence: verification.md asks for an exact CI URL/commit but supplies neither. Package verifier checks binary hash, RID and ABI but does not fully validate upstream/toolchain/lock/features/spec identity, complete legal inventory, or exact RID inventory. A self-consistent incorrect manifest is weaker evidence than expected identity. Load-time comparison binds manifest to binary declarations, which still must be bound to reviewed release inputs.

Fix: record release SHA, run URL, artifact identities/hashes, and toolchain; validate the final archive against expected identity and expected inventory. Bind manual publishing to the fully successful CI artifact from that exact reviewed SHA rather than independently rebuilding or choosing the latest artifact by name.

## Public API review and usability decisions

| Surface | Assessment / handoff |
| --- | --- |
| Private key Generate/Import/Export/ExportPem/Dispose | Opaque ownership is appropriate; keep explicit export secret. Define concurrent Dispose vs use behavior. Use GC.KeepAlive or a handle lease around native operations if finalization can race extraction of an integer handle; native store serialization prevents this becoming a raw key pointer use-after-free, but domain failures remain possible. Clean up a newly allocated native handle if response validation fails after reading it. |
| Public keys | Resolve F02. The public constructor permits arbitrary bytes/invalid algorithms while Parse validates; make validation guarantees clear, or narrow construction before freeze. Noncanonical upstream key decode must not be advertised as stronger validation than it provides. |
| ParseHex / ParsePrefixed / ToPrefixedString | Keep: hex is common configuration transport and prefixed form is algorithm-self-describing upstream interop. They earn their small surface. Preserve diagnostic ToString and explicit export names; document exact prefix/case behavior for both algorithms and test the round-trip invariant for accepted inputs. |
| Token Parse/ParseBase64Url/ToBytes/ToBase64Url | Keep verified immutable model, after F02. Contract must say serialization returns upstream-normalized bytes. Equality is canonical serialized-byte identity within an issuance, not equivalent authority across issuances. |
| Attenuate / Seal / BiscuitBlock | Appropriate narrow responsibilities. Datalog is validated at the native operation, not Create/Add*. Document validation timing and repeated Seal behavior. Do not introduce Hufu workflow logic here. |
| Revocation ids / Inspection | Fix F02/F06. Inspection is opt-in and prints block source; it is not privacy-safe telemetry merely because it omits private keys. |
| Authorizer / structured results | Good separation of ordinary denial and exceptions. Extend response consistency checks to policy indices if contract requires them. Parameterized authorizer facts/rules/checks/policies and attenuation blocks would improve secure consumer code substantially. |
| AuthorizerLimits | Code and native fallback agree; decision below remains pending. WithLimits truncates fractional milliseconds via TotalMilliseconds -> ulong. Document precision or carry integer ticks/nanoseconds; test submillisecond and zero budgets. |
| BiscuitParam | Typed substitution is the right primitive. Externally derivable abstract record supports subclasses that WriteTo rejects; close construction or define supported extension semantics before freeze. Reject duplicate parameter names rather than silently taking the last value if ambiguity matters. |
| Exceptions | Hierarchy matches intended responsibilities; resolve F05 and specify Datalog parse/build versus evaluation-failure result behavior. |
| Engine/version info | Useful support/provenance API. HashLoadedFile hashes the current pathname, not necessarily the mapped binary on systems permitting replacement. Capture load-time identity and label future rehashes honestly. Cache identity only after correct verified publication. |
| XML docs / nullability | CS1591 suppression conceals gaps in shipped IntelliSense. Document all public entry points, units, parse timing, exceptions, sensitive outputs and concurrency before 1.0. Span inputs have no null state; collection element/record constructor validation needs explicit rules. |

## The two semantic decisions

### Robust default limits — recommendation, not team sign-off

Managed Default and native default_limits are identical: 100,000 facts, 100,000 iterations, 5 seconds. UpstreamDefault exposes 1,000 facts, 100 iterations, 1 ms. Local guards and exhausted-limit tests pass. The increase fixes unreliable tiny wall-clock budgets, but increases all three axes, not just time. A five-second synchronous evaluation can occupy a request thread; this is an evaluation budget, not a hard end-to-end request deadline, memory cap, or cancellation mechanism. Parsing/building/serialization and individual operations need separate consideration.

Recommend retaining an explicit documented wrapper default, but require workload evidence before accepting 100x facts / 1000x iterations as the universal default. Benchmark representative Hufu policies and hostile growth rules, including concurrency. Consider keeping conservative facts/iteration limits while increasing time, or expose clearly named interactive/batch profiles if measured demand warrants them. Do not silently change this during integration. Maintainer/team acceptance is pending; tests cannot establish agreement.

### Trailing bytes and normalization — recommendation, not team sign-off

Keep upstream Parse normalization for compatibility rather than changing Biscuit semantics. Existing adversarial code explicitly accepts verified framing variants and checks canonical outputs; ToBytes returns upstream reserialization, not a promise to reproduce arbitrary input. Rebuilding tokens also uses fresh ephemeral keys, so bytes/revocation IDs vary across issuances.

Add a deterministic focused test: append a known tolerated suffix to a valid token, parse it, compare normalized output with the canonical original, and verify identical authorization/revocation behavior. Specify that Hufu envelopes, external signatures, caches, fingerprints and audit correlation must deliberately choose raw transport bytes or normalized token bytes. If consumers need strict canonical transport, offer an explicit separate check after proving upstream reserialization stability; do not retrofit rejection into Parse. Team acceptance is pending.

## Provenance re-verification

Independent checks performed in this audit:
- git ls-remote upstream refs/tags/biscuit-auth-6.0.0 returned 0f0b4e0e6fe07220c1ba6b51bff21d450d94a975.
- Cached published crate .cargo_vcs_info.json reports that same commit and path biscuit-auth.
- Cached biscuit-auth-6.0.0.crate SHA-256 is d5884fc86b3e21f5649ef4326e17ef729b3096e6502deaf13db7b7fb05bb992b, identical to Cargo.lock and docs/native-boundary.md.
- native/Cargo.toml uses exact =6.0.0; committed lock uses registry checksum. This is a registry version/checksum pin with a verified source mapping, not a direct git-source dependency.
- Current Cargo.lock SHA-256: ad3a231ee5ec0dd3c9763ee01a3af5a97fc6758131e5ae2b83ced2a05b13ed9c.
- Cached upstream manifest declares Apache-2.0 and defaults regex-full,datalog-macro,pem, matching documented bridge identity.
- NOTICE has upstream attribution and source commit. Transitive redistribution material is incomplete as described in F04.

Not established: full historical lockfile-change justification, complete transitive legal review, dependency advisory audit, independent source-to-binary reproducibility, complete secret/history scan, or authenticity from hashes alone. Fixed fixture private material is intentional test material; do not classify it as a production secret solely because it is a private key.

## Executed evidence and limits

Local Windows x64:
- cargo test --locked --offline --lib --manifest-path native/Cargo.toml: 45 passed.
- dotnet run --no-restore --project tests/BiscuitSharp.Tests --framework net8.0: completed successfully, M1 checks passed.
- Same managed command for net10.0: completed successfully, M1 checks passed.
- Both managed runs explicitly skipped generated Rust exchange / consume phases because fixtures/phase setup were absent. Do not claim full bidirectional compatibility from these runs.
- Exact leak_probe filter reproduction: zero tests, 45 filtered out.

Remote CI inspected through gh:
- [Successful prior full CI](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36878763035), SHA 7294611c908adb17375f150890c61ebd9f8cf4a6. Reported success is real; leak-workload coverage is invalidated by F03.
- [HEAD CI](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36880499152), SHA fb82be79737396da5cb4823072a94738eabd118b, was still running at the initial snapshot; final read-only verification confirmed status completed and conclusion success. The green leak job remains subject to F03.

Linux/macOS tests, NativeAOT, final three-RID package consumers and real Valgrind instrumentation were not rerun locally. Static race finding has no deterministic race reproduction yet. Existing successful tests do not cover the new findings.

## Structure and usefulness improvements

Keep sealed public types and the upstream delegation boundary; inheritance/framework abstractions would add little here. Extract focused internal response readers and an owned immutable byte-value representation to remove repeated shape/equality logic. Split BiscuitToken.cs's revocation/block/error-mapping responsibilities into small files if they change independently. Avoid a generic repository/service layer.

Highest-value usability work after blockers: typed parameters consistently across Datalog entry points (reduces pressure to interpolate untrusted values), documented root-key selection/rotation with optional builder root_key_id, public-key-only verification sample, and a verify -> revocation lookup -> authorize -> enforce sample. Document explicit time facts and units. Benchmark repeated token decode/verify and JSON/base64 copies before adding native token handles or caches; those would expand lifetime and memory complexity.

Third-party blocks, snapshots, PEM/DER public keys, cancellation, fingerprint helpers, and more RIDs remain demand-driven. Any fingerprint needs a deliberate definition and privacy treatment.

## Release handoff sequence

1. Fix F01-F04 and add the meaningful regressions above. Resolve or explicitly accept F05-F08 and the API decisions before freezing.
2. Record maintainer acceptance of limits and normalization; reconcile all docs and retire stale findings/status.
3. Implement .github/workflows/publish.yml: manual dispatch, least privilege, protected release environment, trusted NuGet publishing identity (or configured secret), exact reviewed SHA, successful full CI gate, immutable artifact binding, complete package/symbol verification. Ensure CI uploads the snupkg too; current nupkg artifact path uploads only *.nupkg even though symbols are built. Prevent overlapping releases and avoid treating duplicate publication as a blanket success.
4. Rerun the full matrix on the final release SHA, including the corrected Valgrind workload; record run URL, hashes, actual counts/skips, RIDs, TFMs and toolchain.
5. Publish 0.1.0-preview.1 only after those gates and release authorization are satisfied. The requested review does not constitute team agreement on newly identified tradeoffs.
6. Verify public NuGet restore plus clean consumers of the published artifact. Then restore PackageValidationBaselineVersion to 0.1.0-preview.1, and validate the next candidate version against it.
7. Re-freeze with an explicit public API inventory and reviewed contract/ADR, update CHANGELOG/README/ROADMAP/verification ledger, and carry Hufu-specific integration into its own work.

Handoff status: audit implementation fixes are in the working tree. Final release matrix, semantic acceptance, publication, baseline restoration and re-freeze remain open. Read the fix follow-up above before interpreting the historical findings.
