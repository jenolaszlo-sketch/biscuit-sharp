# Hufu Biscuit integration handoff

Updated 2026-10-03. The public keyring, asynchronous adapter and corrected
Windows read authorization components are implemented and locally qualified.
Complete protected-operation start, production host and adapter release gates
remain open. Read the [specification](hufu-integration-spec.md) and
[ADR 0003](decisions/0003-hufu-integration-profile.md).

## Current evidence

BiscuitSharp preview.1 is published. The typed-budget-reason preview.2 candidate
passed all 19 wrapper jobs at f89285702ebade4b7fe92e4bfb2f72080c8d72ab /
CI 36979786333 and remains unpublished. The exact nupkg SHA-256 is
c5f0c94aa14cf82b68239ed49314a3cf468780337432cdff89975beed6ead3b1.
No wrapper runtime/API/native asset changed in this consumer continuation.

The previous missing Hufu.IO/old-constructor discrepancy is reconciled.
Hufu.Luban retains exact semantic admission; Hufu.IO supplies separate resource
interception; explicit Local provider composition stays in the host/tests.
Corrected IO.Abstractions, IO.Protocols and IO.Local are exact local
0.1.0-preview.1 candidate packages. Published adoption remains RA-5C after
RA-5B under the [corrective ledger](../../Penghou/docs/resource-abstractions-corrective-plan.md)
and selected [resource ADR](../../Penghou/docs/decisions/0003-replaceable-resource-providers.md).
VFS remains deferred.

The isolated Windows x64 candidate-package run passed with no IO/Luban source checkout and a fresh cache, with zero failures/skips on each net8.0 and net10.0:

| Suite | Cases per framework |
| --- | ---: |
| Biscuit adapter, Local/Luban and Hufu.IO read composition | 93 |
| Existing Hufu/Cedar/SQLite/Zhinu/Luban integration | 101 |
| IO resource integration | 19 |

426 cases executed across both frameworks. Source remains local/uncommitted:
Hufu has no committed HEAD. The [source inventory](hufu-adapter-source-manifest.json)
hashes 337 Hufu/Zhinu code/configuration inputs, seven package archives and three reports (the existing two measurements plus the isolated candidate-package result). These are input identities, not a release
attestation. Earlier source tables/counts later in this document are historical.

## Resource publication checkpoint — 2026-10-03

The exact IO release tag v0.1.0-preview.1 points to
468dde33f0cda8f8f26a734abd0e512cea70d138. [Release run 37084905564](https://github.com/jenolaszlo-sketch/penghou/actions/runs/37084905564)
passes Windows tests, Linux builds, package checks and isolated consumption.
Publication stops before login/push at missing NUGET_USER. The three corrected
IO versions and Luban preview.1 are not yet available on NuGet.org.

Luban's finalized API passes 338 tests per framework and [CI 37085937530](https://github.com/jenolaszlo-sketch/penghou-luban/actions/runs/37085937530)
at 26f4ad943a0f4370627574c43d2a78bd2c14b54d. Its patch types now belong to
Penghou.Luban.Changes, with a checked-in enforced API baseline. The OIDC release
workflow and public-dependency smoke mode are committed/pushed.

Hufu now selects exact Penghou.Luban [0.1.0-preview.1] by default and excludes
Luban source from its normal solution. Both source switches remain explicit
development options. Genuine v2 read/diff documents are rejected before
authority/evidence access; Hufu remains a v1 language-policy consumer.

The complete 93/101/19 matrix above uses exact IO release artifacts and the
finalized Luban package. [Recorded sources and archive hashes](../../Penghou.Hufu/docs/qualification/candidate-resource-packages.json)
prove fresh-cache candidate-only consumption. The repeatable
[qualification tool](../../Penghou.Hufu/eng/Test-PublishedResourcePackages.ps1)
defaults to public IO/Luban dependencies and produces separate public evidence
only after successful producer publication and all six test suites.

Resume the [resource release handoff](../../Penghou/docs/resource-package-release-handoff.md)
and [Luban release handoff](../../Penghou.Luban/docs/release-handoff.md):
configure each repository's publishing identity, retry the validated IO job,
qualify/publish Luban from public IO, then run Hufu's default isolated public
qualification. RA-5B/RA-5C remain open until that actual public-feed evidence exists.
The earlier capacity measurements remain historical measurements of the same
fixed policy; IO/package adoption alone does not require rerunning them.
## Implemented adapter and current qualification

BiscuitKeyRing is public: hosts may supply explicitly owned Ed25519 keys or
implement IBiscuitKeyProvider. Registration transfers ownership only on success;
signing leases retain native key access across rotation/retirement/disposal.
Protected key/credential custody and authenticated administrative policy remain
host responsibilities.

The optional adapter implements bounded owned envelopes, authenticated workload/
issuance/derivation/resource gates, immutable grant selection and registration,
canonical received bytes, typed same-context attenuation and fixed origin-scoped
Datalog composition with current real all-layer Cedar. Both core and Biscuit
evidence stores must acknowledge before Permit. The SQLite start composition
orders revocation/key retirement with exact start checks, compares complete
evidence and never redispatches AlreadyStarted.

The current read suites exercise real metadata/traversal/content/release checks,
root-allowed/child-excluded find/search, late mandatory denial, evidence failure,
revocation before read/release and between resource pages, forged invocation,
Unicode aliases, junction rejection/omission and directory-to-junction
substitution after Permit. The direct resource tests record the excluded-child
denial and prove initial failure opens zero provider sessions. Short-name coverage
is conditional on the volume supplying an 8.3 alias.

The read hosts perform fresh checks but do not submit actual file I/O through
BiscuitSqliteStartParticipant. Atomic start-to-read revocation ordering is not
established; the operation-start runtime participant remains a SQL fixture.
Logical provider/path hashes are not locked native-file identity. This is narrow
read-component qualification, not the complete protected-operation release.

This continuation also fixes three findings:

- Required recording now checks snapshot expiry, clock rewind and every grant's
  active state through the shared HasUnchangedValidity rule. Unrelated grant
  transitions deliberately require a fresh decision, matching existing SQLite
  evidence semantics.
- Mapping identity v2 includes the profile, typed-grant mapping revision, fixed
  policy and complete capability table. WriteFile/fs.write is distinct from
  PatchFile/fs.patch.
- Ancestor lookup failures preserve their typed unavailable/unknown/denied/
  revoked result instead of all being reported as InvalidCredential.

An early revocation rejection lacks attributable decision/evidence; strict
Hufu.IO projection returns AuthorizationUnavailable with no result. The detailed
Biscuit reason remains AuthorityRevoked. No evidence requirement is weakened.

## Measured budgets and next exact work

The [measurements](../../Penghou.Hufu/docs/biscuit-budget-measurements.md)
reuse the exact production fact/policy builder at 1/8/32 blocks and 1/4 workers.
Each framework ran 960 fixed-policy evaluations and 120 complete preflights.
Final maxima: native-policy wall time 4.526 ms (net8) / 4.653 ms (net10);
complete preflight 1356.390 ms / 1210.957 ms. One net8 four-worker/32-block
preflight was unavailable under the fixture's one-second SQLite busy timeout.
This exposed the ancestor-failure classification bug; the final run reports it
correctly. No failure retries or larger authorization budgets were used.

Retain explicit local read-host limits of 2000 facts, 50 iterations and 100 ms,
with complete checks sequential per shared database. Four-worker native policy
execution is measured; four-worker complete-host availability remains open.
Native limits do not cover authentication/Cedar/evidence latency or interrupt
synchronous calls. The sampled whole-process peaks are observations, not memory
caps or production capacity evidence.

Next, in order:

1. Coordinate corrected IO publication and actual published-package adoption
   (RA-5B/RA-5C), then requalify the consumer against the published artifacts.
2. Supply and qualify the concrete host's presenter authentication, issuer/
   approval ceilings, key/credential custody and database-wide concurrency/
   deadline/capacity profile.
3. Complete supported consumer/API/source review and exact operation-start
   integration before adapter freeze/publication. Establish a committed Hufu
   release identity; the current untracked prototype is not a release.
4. Qualify governed mutation admission, actual physical-object start binding,
   required terminal outcomes and response-loss/recovery independently.

Cross-platform/AOT Hufu consumers, backup rollback protection, drain guarantees,
general confinement and production VFS remain open or deferred. Capture-only
preview and simulated execution are distinct; simulated receipts cannot authorize
real apply. These consumer gates do not undo the wrapper's independent preview
qualification.

See Hufu's [profile](../../Penghou.Hufu/docs/biscuit-integration-profile.md),
[qualification](../../Penghou.Hufu/docs/biscuit-integration-qualification.md) and
[recalibrated resume point](../../Penghou.Hufu/docs/biscuit-integration-recalibration.md).
## Compatibility with published BiscuitSharp

| Requirement | Existing API / implementation | Required adapter work or gap |
| --- | --- | --- |
| Issue Ed25519 token | BiscuitPrivateKey.Generate/Import; BiscuitTokenBuilder.Build | Key lease/provider, issuer rights, immutable grant version and registration |
| Safe authority facts | AddFact(template, IEnumerable<KeyValuePair<string, BiscuitParam>>) | Fixed typed templates; avoid reflection for NativeAOT |
| Root selection | BiscuitToken.Parse(bytes, BiscuitPublicKey) | Resolve envelope hint in authenticated realm before Parse; signed key/realm equality after verification |
| Protobuf root ID | Inspect().RootKeyId is nullable uint; builder exposes no setter | Do not depend on it for v1 string IDs or pre-verification lookup |
| Exact token ownership | Parse, ToBytes, ToBase64Url/ParseBase64Url | Defensive envelope snapshots; raw/canonical separation and registered-byte equality |
| Revocation | GetRevocationIds(), each .Value is a defensive byte copy | Realm-scoped durable store; all ancestor/child IDs, fail-closed availability and ordered start |
| Attenuation | Attenuate(BiscuitBlock.Create(source)); Seal | Fixed typed restrictions, parent/current subset proof and exact child registration |
| Trusted request evaluation | For(token), AddFact/Check/Rule/Policy, AddTimeFact, Authorize | Authorizer/source APIs are string-only: qualify a literal writer or add a separately reviewed typed substitution feature |
| Explicit limits | WithLimits(new BiscuitAuthorizerLimits(ulong, ulong, TimeSpan)) | Positive configured values, checked conversion and integral milliseconds |
| Precise budget diagnostics | Development preview.2 adds Errors[].EvaluationFailureReason | Only FactLimitExceeded, IterationLimitExceeded, TimeLimitExceeded map to AuthorizationBudgetExceeded; all other/null reasons map to AuthorizationFailure. Preview.1 lacks this feature |
| Cancellation/metrics | Native calls synchronous; no per-budget counters | Check cancellation before/after calls; do not promise interruption or invent usage figures |
| Structured token profile | Inspect prints source, not a verified AST/query interface | v1 exact-byte trusted registration; no regex recognition of arbitrary logic |

The generic wrapper reason enhancement is implemented in development preview.2.
Native/managed tests distinguish all three budgets from expression errors,
unexpected query cardinality and other failures. Unknown reasons remain Other;
missing reasons remain null. No message parsing is permitted. The reviewed
surface is 210 entries. The maintainer has deprioritized additional compatibility
work during early adoption. See verification.md for exact candidate qualification;
the earlier a67b29a run does not cover this enhancement.

## Implementation sequence

1. **Bind the Hufu host contracts.** Define authenticated realm/audience and
   workflow/activity bindings, exact grant-version/layer selection, typed
   request identity, provider object identity and required evidence. Keep
   optional Biscuit dependencies in Penghou.Hufu.Biscuit/composition projects.
2. **Implement keys and registration.** Supply Ed25519 signing leases and
   realm-scoped verification lookup; immutable grant/issuance/derivation records,
   bounded envelope snapshots, credential custody and exact received fingerprints.
   No fallback keys or agent-selected providers.
3. **Qualify the closed Datalog mapping.** Implement the specification's
   predicates and single fixed Allow policy with authority origin scoping.
   Validate request/attenuation literal construction using quotes, slashes,
   newlines, Unicode and malformed inputs against the pinned upstream parser.
   Registration prevents unsupported token logic from entering this profile.
4. **Consume precise wrapper runtime reasons.** The development preview.2
   enhancement supplies the reason enum. Select its qualified package/artifact
   and map only the three limit reasons to AuthorizationBudgetExceeded.
   ExpressionError, UnexpectedQueryResult, Other and null map to
   AuthorizationFailure. Preview.1 cannot satisfy budget-specific conformance.
   Complete host-policy measurements and adapter budget tests independently.
5. **Compose authorization.** Verify, validate registration/profile, check every
   revocation ID, resolve current snapshot/resource, evaluate Biscuit and preserve
   the current Hufu/Cedar all-layer decision. Record required evidence. Failed or
   skipped layers cannot become Permit.
6. **Bind operation start.** Add all applicable per-block revocation and root-key
   retirement checks to Hufu's authoritative serialized start protocol. Pin
   context/fence, snapshot/evaluator/policy versions and exact effect inputs.
   The existing SQLite/Zhinu start gate does not add these checks automatically.
   Repeated AlreadyStarted results do not dispatch again.
7. **Qualify a real consumer.** Begin with Hufu's qualified Windows read profile,
   including metadata/traversal/release. Broader wrapper OS support does not
   qualify Hufu resource providers on those systems. Mutations require a separately
   qualified governed host/provider/outcome protocol.
8. **Freeze and publish separately.** Complete the acceptance suite below,
   document supported transport/enforcement profiles and only then qualify the
   adapter package. Hufu remains experimental/unpackaged at this review.

## Acceptance suite

Retain the original proposal's basic grant, revision, attenuation, revocation,
key rotation, Cedar, envelope, resource, budget and exact-enforcement cases.

Add explicit negative tests for:

- Forged workload identity and tenant/realm/run/fence/audience substitutions.
- Grant/layer/version mixing, duplicate singleton authority, arbitrary wildcard
  claims and loss of any current mandatory authority layer.
- hufu_*, request_* and scope_contains facts/rules injected into attenuation;
  none can broaden the authorizer's authority.
- Unauthorized subject/activity/child-workflow rebinding through attenuation.
- Unknown/missing registration, unregistered logic, invalid profile/version,
  trailing-frame aliases, mutable received-byte buffers and oversized chains.
- Unsafe request/source values, sub-millisecond budgets, cancelled post-native
  results, key disposal/rotation races and key-retirement races.
- Non-budget runtime failures distinguished from all three budget limit kinds.
- Unavailable revocation/key/evaluator/required-evidence dependencies.
- Revocation after preflight but before start, key retirement, stale snapshot/
  fence/policy/provider identity and exact start/argument mismatch.
- AlreadyStarted receipt replay that never submits another resource effect.
- Actual physical-object/link/junction races on qualified providers, required
  start-evidence failure and ambiguous post-effect outcome persistence.

Security conformance helpers must verify no authority amplification, no
cross-layer override, authenticated context, ordered revocation, exact operation
binding and fail-closed unavailable state. Qualification must include real
native evaluation and actual provider/start integration, not only mocks.

## Review boundary

The original design and wrapper prerequisite activities are complete. The current
continuation changes local Hufu adapter/composition code and its documentation,
and updates this wrapper handoff. It does not publish a package, create a
production host or qualify actual protected I/O. The Hufu source hashes below
identify the original reviewed inputs; the new adapter is covered by the sibling
qualification record and tests.

## Executed documentation checks

The exact fixed policy was extracted from hufu-integration-spec.md and exercised
through the real staged win-x64 bridge on both net8.0 and net10.0. All 11 cases
passed per framework: exact-context read, explicit parent capability set,
missing capability, subject/revision mismatch, missing trusted scope, attenuation
authority/request/scope pollution, read-only child Allow and child patch Deny.
The scratch probe lives in ignored artifacts/hufu-spec-policy-probe.

The wrapper follow-up exercised the same 11 cases plus zero-time Deny and typed
TimeLimitExceeded against the exact three-RID CI 36979786333 package on Windows:
13 passed on net8.0 and 13 on net10.0. The isolated restored nupkg hash equals the
selected artifact; no native override or project reference was used. The scratch
package consumer lives in ignored artifacts/hufu-package-policy-probe.
This adds package-consumption evidence, not adapter/store/start qualification.

This verifies policy parsing and representative upstream origin/attenuation
behavior. It does not qualify a general literal writer, key/registration/store
implementation, Hufu/Cedar composition, physical resource provider, ordered
revocation/start protocol, or complete adapter conformance.

The supplied proposal text is preserved unchanged in the archive.
Specification/source references and documentation links were checked.

## Source snapshot

The following hashes identify the uncommitted Hufu inputs reviewed locally.
Paths are relative to the sibling Penghou.Hufu checkout. They are evidence
identifiers, not source authenticity or release qualification.

| Hufu source path | SHA-256 |
| --- | --- |
| src/Penghou.Hufu/AuthorityModel.cs | b7aa5f7c551bb76f0b4b57b04111fb104ee56122361ec8cc5bc28aeeac511765 |
| src/Penghou.Hufu/AuthorityOperationStart.cs | 30ab31ff9c27cfbc0f85465d917684ba1d0c62ea8316f9f40433bb0a69bcc372 |
| src/Penghou.Hufu.Cedar/CedarAuthorityEvaluator.cs | 6615f740726812b4d047a0425a620babed3f87ff6adcd752533fc81b7b6a69a8 |
| docs/current-authority-profile.md | 9df84b3d7e48503a7d8f0f3050aa769f802a16104bcd423e9a1f1a9724aa8427 |
| docs/local-first-authority-runtime.md | 9df9a65c9924249b30e2f963ccf0f3925876c6f463f5d4d71e0cfcf7fd604915 |
| docs/operation-start-profile.md | 3fb84a821196b4b241e3bc1b173ae801216660d42ac2ff3a39de6ad99f4c964b |
| docs/decisions/0009-colocated-operation-start.md | b66192aef9b895732aa472f8ebe968c297be0542b8ed993d70b97296c1ba4f7f |
