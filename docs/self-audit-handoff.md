# Independent self-audit and release handoff

Audit date: 2026-10-01 (Asia/Manila).
Repository: C:\Users\Laszlos\source\repos\biscuit-sharp
Original reviewed HEAD: fb82be79737396da5cb4823072a94738eabd118b. Published release SHA: 78924586b70f4188db6cfc47983f8171c78725f8. Publisher correction: 6b011e83a53aa4cd2aa90d32c0ecedbb3898de9e.
Original audit scope: managed public API and contract, native boundary, authorization semantics, provenance, packaging/CI, structure, and consumer usability. The release record below supersedes historical pending steps and candidate selections.


## Hufu integration specification follow-up — 2026-10-02

The maintainer's supplied proposal is archived unchanged. The
[finalized specification](hufu-integration-spec.md), [handoff](hufu-integration-handoff.md)
and [ADR 0003](decisions/0003-hufu-integration-profile.md) now define the optional
Hufu adapter profile. The source review found that current Hufu contracts use
tenant/subject/run/string-revision/fence bindings, layered current authority,
workspace-relative paths and ordered operation start; its workflow/activity/
audience/envelope/Biscuit-registry contracts remain future integration work.

Explicit design amendments close the original ambiguities: host-authenticated
bearer context, one grant version per token, realm-scoped envelope key hints
bound to signed facts, signing-key leases, closed registered canonical transport,
fixed origin-scoped policy, same-context attenuation, positive explicit budgets
and block-new-start revocation semantics with required evidence before dispatch.

Published preview.1 cannot distinguish budget exhaustion from other runtime
errors under evaluation_failure. The adapter must fail closed as
AuthorizationFailure until typed reasons are implemented; it cannot infer
AuthorizationBudgetExceeded by parsing prose. Non-exportable signing, arbitrary
offline attenuation and cross-subject/workflow rebinding are separate profiles.

The documented policy passed 11 real native checks on both managed frameworks,
including attenuation authority/request/scope pollution and read-only narrowing.
This is documentation/policy evidence, not a qualified Hufu adapter. No Hufu
source or published wrapper runtime behavior changed. Implementation and complete
integration conformance remain open in the handoff.

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

## Publisher discovery correction — 2026-10-02

The blank-input publisher failed after CI 36946889887 had already succeeded at
78924586b70f4188db6cfc47983f8171c78725f8. github-script v9 renamed
listWorkflowRunsForWorkflow to listWorkflowRuns. The obsolete method was
undefined; passing it to paginate made a GET request to the API root and
returned no eligible runs. The original fixture supplied that obsolete method
and therefore missed the SDK integration defect.

The resolver now uses explicit REST route strings for both discovery and run
lookup. Its mocks reject missing/wrong routes, and CI adds a real action-SDK
check that discovers its own workflow run. The actual v9 distribution reproduced
the root request locally; the corrected resolver with that SDK and the live
public API selected run 36946889887 and the exact original SHA. Full-matrix,
main-push, SHA, artifact and environment gates are preserved.


## Publisher usability follow-up — 2026-10-02

Normal publish dispatches now leave both inputs blank: selection pins the
dispatched commit and discovers successful CI for that exact SHA. Missing CI
fails closed; it does not select a previous commit. Historical candidates need
only the optional CI run ID; the SHA is derived and any supplied SHA is checked.
The full 19-job/workflow/main-push/protected-environment/artifact checks remain
in place. The validated identity is printed in the workflow summary and used
for artifact download.

Selection regressions cover exact-commit discovery, newer ineligible runs,
missing/failed CI, historical overrides, mismatches and malformed inputs.
The existing Linux managed CI job runs these tests. No package was published
to test this change. Public-feed verification remains a separate manual step.


## Second-audit fix follow-up — 2026-10-02

The four new P2 findings are fixed in implementation. This section supersedes
the open-finding status and selected-candidate instructions in the second
review below. All 19 jobs passed at 112bdffaaa933da594588ba256ea0db6efbb211f
([run 36943970067](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36943970067)). This selected candidate replaces the old 5dd9e19 artifact.

| Finding | Change | Regression evidence |
| --- | --- | --- |
| F09 | Shared CargoLegal resolver reads legal bytes directly from checksum-verified crate archives. Full crate-relative paths replace ambiguous basenames. Staging and package verification independently require source coverage and compare shipped bytes with archive content. | Genuine Windows staging/smoke package pass. Altered text, text plus changed inventory hash, and removed file plus inventory entry are rejected in both paths. Three-RID staging, package/source binding and all tamper cases pass in the qualified run. |
| F10 | BiscuitParam is a sealed immutable class with private construction and four factories; byte values remain snapshots. This deliberately removes the unpublished record inheritance/equality surface. | External consumer rejects derivation with CS0509 on both TFMs across all three platforms. All four factory values survive native issuance/parsing/authorization; mutation of the input byte array does not change the value. |
| F11 | Inventory includes public operators and externally accessible protected/protected-internal members, accessor visibility and selected contract attributes. One renderer drives inventory and regression checks. | Removing an operator or protected constructor changes the rendered surface on both TFMs. Published-package binary compatibility remains a separate post-publication gate. |
| F12 | Internal inspection parser checks object shape, managed count/size ranges and revocation/source/version array consistency. | Valid response passes; oversized counts/sizes, malformed base64, non-object roots and inconsistent arrays throw BiscuitBridgeException on both TFMs. |

The negative compile gate initially leaked its expected compiler exit status to
the CI wrapper. Commit 112bdff normalizes success only after CS0509 is verified;
the corrected complete run is green.

Luna handled the scoped API gate, external compile rejection and process-RID
mapping work. Asset selection now uses ProcessArchitecture; mapping tests cover
supported/unsupported combinations without expanding emulation qualification.

Additional P3 improvements are implemented: the budget probe runs four hostile
default-budget calls concurrently and samples working set every 20 ms; each
call fails closed. Local win-x64 release observations were 252,477,440 bytes
(net8, 12 periodic samples, 422 ms) and 274,534,400 bytes (net10, 11 samples,
374 ms). These are sampled process peaks, not guaranteed native allocation
peaks or caps; they do not establish universal Hufu settings.

Clean package-reference NativeAOT publish/run passes on Windows with no staged
native override and trim/AOT warnings treated as errors. Restored archive
content matches the selected package; the native override is restored for the
caller afterward. CI adds this execution to the existing three net10 consumers,
retaining six consume jobs and 19 total jobs. Both-TFM ordinary managed consumers
remain covered. The readable API inventory now has 201 entries.

Safe ambient template overloads remain consumer-driven API work, export caching
awaits profiling, and complete XML documentation remains a 1.0 requirement.
These P3 opportunities were not declared preview blockers.


Publication remains pending the protected-environment reviewer/self-review
configuration, publication of the selected qualified artifact, public-feed verification and baseline
restoration. No publication is part of this correction.


Selected candidate artifact SHA-256:

- BiscuitSharp.0.1.0-preview.1.nupkg:
  84d0b440502d48486a2b7099a0f5286f10e5fad6eb9c4d3d238769d06ec732a2
- BiscuitSharp.0.1.0-preview.1.snupkg:
  61790753fcf27985954f3015df4371903924449f1b01a980b6b27e4fb06f39c8

Both portable PDB SourceLink mappings were independently rechecked locally
against the selected full SHA. The final handoff update is documentation only;
retain the exact qualified run/artifact above for publishing.


## Second solution review — 2026-10-02

Reviewed HEAD: **cf4c56c1d96a3aa0a7fcb097e72e054ba7080a76**.
Scope: managed API against api-contract.md, ownership/native loading, native
authorization/token/key paths, provenance/release scripts, API/symbol gates,
budget evidence and consumer usability. Production code and release
configuration were not changed in this review.

### Recommendation and current status

All **19 jobs** passed at reviewed HEAD:
[CI run 36940735976](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36940735976).
The selected candidate remains the immutable artifact from
5dd9e199b32ea18d1499a5e055cbe658c9e0b0a1 /
[run 36940126702](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36940126702);
cf4c56c changes release records only. Successful CI does not establish complete
coverage of every public contract.

Three gaps were reproduced: legal verification is not bound to source archive
contents, BiscuitParam remains externally derivable, and the API inventory
excludes public operators/protected members. Resolve F09–F11 before calling
the API/provenance gates frozen. F12 is a smaller source-confirmed decoding gap.
No new authorization bypass or P0/P1 runtime defect was established in this pass.

The protected nuget-production environment still does not exist. NUGET_USER
was previously confirmed; the maintainer reported the trusted-publisher policy
ready, but this review did not inspect that private NuGet policy. The explicit
reviewer/self-review choice remains outstanding. Publication, public-feed
verification and the published compatibility baseline remain pending.

### New findings

#### F09 — P2: legal material is not bound to the verified source archive

Evidence: eng/Build-Native.ps1:155–168,
eng/Verify-NativeStaging.ps1:90–100,
eng/Verify-NuGetPackage.ps1:98–107.

The scripts verify the cached .crate checksum against Cargo.lock and separately
verify each shipped legal file against licenses.json. They never establish that
those legal bytes came from the verified archive. Build-Native copies from the
extracted Cargo source tree; verifying a separate archive does not establish
the integrity of extracted files. source_name records only a basename, making
nested source locations ambiguous.

**Reproduced twice:** on a disposable staging copy and a copy of the actual
qualified three-RID nupkg, replace one legal file with
"REVIEW PROBE: this is not upstream license text." and update its licenses.json
sha256. Leave registry_archive_sha256 and Cargo.lock untouched. Both staging
and complete package verification exit successfully. Existing tamper tests
change text without updating its inventory hash, so they miss this case.

This does **not** establish that the candidate contains incorrect legal text:
an independent comparison verified all 436 material entries across all three
RID bundles against the checksum-verified source archives.

**Fix:** derive legal material from the verified archive, or compare extracted
files against archive entries before staging. Record full crate-relative source
paths. Independently reconstruct required material coverage and compare shipped
bytes to source in the shared staging/package validation logic.

**Acceptance:** changed text plus an updated inventory hash still fails; missing
recognized upstream legal entries fail; genuine three-RID material passes.
This is an integrity/evidence finding, not a legal opinion.

#### F10 — P2: a private constructor does not close BiscuitParam record inheritance

Evidence: src/BiscuitSharp/BiscuitTokenBuilder.cs:12–14 and :61.

The compiler synthesizes a protected copy constructor for the abstract record.
A separate consumer assembly can compile and instantiate:

~~~csharp
public sealed record ConsumerParam : BiscuitParam
{
    public ConsumerParam(BiscuitParam original) : base(original) { }
}
~~~

**Reproduced:** new ConsumerParam(BiscuitParam.Str("value")) succeeds. AddFact
accepts it; Build later throws ArgumentException ("Unsupported Biscuit parameter:
ConsumerParam", parameter "name") from WriteTo. The earlier factory-only
construction claim is incomplete.

**Fix before freeze:** choose a genuinely closed representation, such as a sealed
immutable class with private construction, or deliberately support/document
extensibility. Do not retain accidental inheritance when serialization accepts
only private concrete variants.

**Acceptance:** an external-consumer negative compile test proves unsupported
derivation is impossible if the design stays closed; all four factory round-trips
and byte ownership remain correct. Closing an already-published record hierarchy
would be a breaking change, which makes this timely to resolve.

#### F11 — P2: the 189-entry inventory omits existing source/binary API

Evidence: tests/BiscuitSharp.ApiSurface/Program.cs:46 and :52.

BindingFlags.Public excludes protected members. The !IsSpecialName method
filter excludes operator methods alongside property/event accessors.

**Reproduced by reflection:** 18 current public equality/inequality operators
are skipped, as is BiscuitParam's protected copy constructor. Removing these can
leave the inventory unchanged while breaking consumers. This also hid F10.
The published package baseline is not enabled yet.

**Fix:** include public and externally accessible protected members; distinguish
op_* operators from property/event accessors. Define coverage for constraints and
attributes, or use maintained API compatibility tooling with a known coverage
contract. Keep the readable inventory as an aid, not a claim of exhaustive binary
compatibility.

**Acceptance:** changing/removing an operator or protected constructor fails
the gate on both TFMs. Published-package compatibility remains a separate gate.

#### F12 — P2: inspection numeric conversions escape the bridge exception contract

Evidence: src/BiscuitSharp/BiscuitToken.cs:196, :217–247 and :282.

Inspect uses checked UInt32-to-Int32 and UInt64-to-Int64 conversions for
block_count and token_size. A valid JSON number outside the managed range
throws OverflowException rather than BiscuitBridgeException. Revocation-id count
is also not checked against block_count, unlike source/version counts.

This is **source-confirmed**, not reproduced through a corrupt native shim.
The current native implementation caps inspection at 4,096 blocks and ordinary
token sizes cannot reach these extremes; this is defensive response validation,
not a demonstrated valid-token failure.

**Fix:** extract a testable inspection-response parser; enforce numeric ranges
and related-array consistency; reject malformed responses with the documented
bridge exception.

**Acceptance:** oversized unsigned counts/sizes and inconsistent revocation-id
arrays fail with BiscuitBridgeException; valid inspection output is unchanged.
The earlier F05 finding is mostly addressed, not exhaustively closed.

### Structure, usability and qualification opportunities

These are follow-on improvements, not demonstrated credential bypasses.

- **P3 — process architecture:** NativeLoader.cs:35–56 uses OSArchitecture.
  Native libraries must match ProcessArchitecture; these differ under emulation.
  Select/report the process architecture. ARM-host emulation was not tested and
  remains unqualified; this suggestion does not expand supported-platform claims.
- **P3 — safe ambient parameters:** typed parameters exist for token-builder facts,
  but authorizer facts/rules/checks and attenuation expose only raw Datalog.
  Consider matching AOT-safe template overloads for ambient request values,
  sharing a small internal template representation. This reduces caller
  interpolation/escaping work. The intentional raw-source API is not itself a
  demonstrated vulnerability.
- **P3 — budget evidence:** default hostile growth is measured once; concurrent
  hostile probes use a smaller explicit budget. Working-set observations are
  endpoints, not peaks (BudgetProbe/Program.cs:21–27, :44–58). Measure hostile
  default-budget concurrency and peak memory before recommending universal
  Hufu settings. Retain the distinction between evaluation budgets, memory
  caps and end-to-end deadlines.
- **P3 — packaged NativeAOT:** Test-Dist.ps1:40–44 publishes a project-reference
  sample and forces a staged native path. It verifies staged-asset AOT execution;
  the six package consumers use ordinary dotnet run. Add a clean NuGet-reference
  AOT publish/run without the override to also qualify package-driven asset
  selection. Keep these evidence claims distinct.
- **P3 — internal structure:** a single immutable verified load state could cache
  exports after successful probing. Current locking is correct for the reviewed
  lifecycle, but performs repeated lookups. Share strict JSON readers and legal
  validation rather than adding broad abstraction layers; profile first.
- Complete XML documentation before 1.0. Preserve documentation of collection
  reference equality, synchronous evaluation and coordinated key disposal.

### Re-verification and evidence

- Reviewed-HEAD CI: 19/19 successful, including strict Valgrind, three
  distribution/NativeAOT jobs, six consumers, inventory, budgets, SourceLink
  and existing legal tamper checks.
- git ls-remote against the official upstream repository still returns
  0f0b4e0e6fe07220c1ba6b51bff21d450d94a975 for
  refs/tags/biscuit-auth-6.0.0, matching the pin.
- Cargo.lock SHA-256:
  ad3a231ee5ec0dd3c9763ee01a3af5a97fc6758131e5ae2b83ced2a05b13ed9c.
- Independent **original candidate** archive inspection: all recognized
  license/notice/copyright entries match checksum-verified crate archives:
  74 runtime crates on linux-x64, 74 on osx-arm64, 73 on win-x64; 436 redistributed
  entries across 74 unique crates. This validates current bytes and recognized
  filename coverage, not every legal obligation or the existing gate's strength.
- Separate net8.0 consumer reproduced external record derivation and delayed
  Build rejection; reflection identified 18 omitted operators.
- Staging and full three-RID package verifiers accepted self-consistently altered
  legal material in disposable copies. Original artifacts were untouched.
- Probe sources/copies are under ignored artifacts/re-review-20261002/. The
  reproductions are described here so handoff does not depend on those local
  files. No full test rerun was needed for this documentation-only review;
  current-HEAD CI supplied regression evidence.

### Revised handoff sequence

1. Resolve F09–F11 before declaring provenance/API freeze complete.
2. Close F12 with focused malformed-response tests. Prioritize P3 opportunities
   by consumer need instead of treating all of them as preview blockers.
3. Update affected inventory/contracts/release ledgers and rerun the full matrix
   at the new implementation SHA; existing runs do not qualify future fixes.
4. Resolve protected-environment reviewer/self-review configuration, publish the
   exact selected qualified artifact and run all six public-feed consumers.
5. Restore the published preview baseline and re-freeze; carry Hufu budgeting
   and integration separately.

The original audit and dated follow-ups below are historical evidence. This
second-review section takes precedence where their status claims differ.

## Graduation follow-up — 2026-10-02

The complete 19-job release matrix is green at 5dd9e19 (run 36940126702),
including all three RID/legal/NativeAOT gates, six packaged consumers and the
corrected strict Valgrind workload. Preview semantics remain as documented in
ADR 0002 under the maintainer-authorized graduation work. The checked API
inventory, budget probes and exact-SHA SourceLink checks are implemented and green; see api-contract.md and
authorizer-budget.md. Publication, public-feed consumers and package-baseline
restoration remain sequential release steps.

## Fix follow-up — 2026-10-01

The findings below describe the reviewed original commit. The initial fixes were committed and pushed as d8e24b49c84ef5da70fc1f103c6f6dea86e0e465; release qualification remains separate.

| Finding | Current implementation status | Verification / remaining qualification |
| --- | --- | --- |
| F01 loader synchronization | Fixed: every entry acquires the reentrant load lock; exports resolve against the current handle, avoiding stale cached addresses after a rejected load; hash/handle/path cleanup stays inside the failure boundary. | Deterministic child-process barriers pass for success, bad hash, and live-identity mismatch. Both callers wait or fail; failed loads can retry safely. |
| F02 byte identity | Fixed: public array properties return copies; equality/hash and internal marshalling use owned storage. | Input/output mutation, dictionary revocation lookup, and token-root stability regressions pass. |
| F03 empty leak gate | Shared 50-cycle ABI workload runs both as a unit test and a standalone Valgrind example. The standalone gate checks start/completion/success markers and preserves fatal memory errors and possible leaks. Last-handle destruction releases empty key-store capacity. | Diagnostic Linux run 36933754724 identified 48 bytes from libtest and 980 bytes from retained key-store capacity. Windows: 45 tests in dev/release, standalone probe and all-target clippy pass. Linux run 36934503054 at 332dd0d passes all 50 cycles with zero definite/indirect/possible losses and zero memory errors; historical zero-test green evidence is invalid. |
| F04 legal material | Fixed in tooling: target-aware runtime dependency graph, checksummed source identity, full LICENSE/COPYING/COPYRIGHT/NOTICE material, coverage/inventory validation; packaged legal files consolidated under third-party. | Windows staging/package checks and rejection checks recorded below. Linux/macOS final packages still require CI. |
| F05 response taxonomy | Fixed: JSON object roots and error fields validated; malformed/unknown envelopes remain bridge failures; policy indices checked for consistency. | Scalar/array/null roots, malformed JSON/error fields, unknown codes and contradictory result regressions pass. Existing native output free remains in finally. |
| F06 collection ownership | Fixed: constructor and record with-expression collection setters make owned read-only snapshots and reject null collections/elements. | Source-list mutation and returned-list mutation checks pass. Collection equality remains explicitly reference-based. |
| F07 contract drift | Fixed: API/security/architecture/plan/roadmap/review ledger reconciled; ADR 0002 records defaults and normalization. | Defaults retained at 100k facts / 100k iterations / 5 s; integer millisecond truncation and tolerated framing pinned by tests. Preview contract accepted under authorized graduation work; Hufu-specific workload budgeting remains a consumer decision. |
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

Historical handoff status: the original audit fixes, final release matrix and preview contract adoption are complete. Publication, public-feed qualification and baseline restoration remain open. Read the fix follow-up above before interpreting the historical findings.
