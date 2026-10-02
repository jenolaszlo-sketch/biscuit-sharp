# Hufu Biscuit integration handoff

Updated 2026-10-02. Documentation is finalized; the adapter is unimplemented.
Start with the [specification](hufu-integration-spec.md) and
[ADR 0003](decisions/0003-hufu-integration-profile.md). The original supplied
proposal is preserved [verbatim](archive/hufu-biscuit-integration-proposal-2026-10-02.md).

## Current evidence and qualification

BiscuitSharp 0.1.0-preview.1 is published and qualified on win-x64, linux-x64 and
osx-arm64 with .NET 8/10. The earlier development 0.1.0-preview.2 passed the full
19-job matrix at a67b29a / CI 36968484267. The current candidate adds typed runtime
reasons and passed all 19 cross-platform jobs at f892857 / CI 36979786333,
including six typed packaged consumers and NativeAOT on all three RIDs.
The exact CI package also passed 13 fixed-policy checks per TFM on Windows.
It is not published.
See [wrapper verification](verification.md) for exact candidate evidence.

The latest release-record commit inspected before this documentation work was
cbfbeb2. None of that wrapper evidence qualifies a Hufu adapter. The Hufu checkout
has no committed HEAD: its prototype source/docs are uncommitted local input.
The source hashes below identify that snapshot; future implementers must recheck
current contracts before adopting this profile.

The reviewed Hufu profile uses AuthenticatedAuthorityContext with TenantId,
SubjectId, RunId, RevisionId and FenceId; AuthorityRequest adds AuthorityAction,
WorkspaceId, RelativePath and RequestIdentity. Workflow/activity/audience/key/
credential/envelope contracts from the proposal are not existing Hufu APIs.
They require explicit trusted host integration rather than guessed field aliases.
ScopeId is an adapter-issued immutable reference to the registered typed Hufu
scope/exclusions; Hufu currently exposes AuthorityScope rather than that ID.

Hufu's current IAuthorityEvaluator/AuthorityDecision and Cedar adapter already
compose layered authority, validity and mandatory policy. AuthorityStatus uses
Permit/Deny/Unavailable; the specification's detailed failure codes are proposed
integration codes, not new existing enum members. Preserve the neutral status
and detailed evidence rather than pretending the public model already exposes
all component outcomes.

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

The original design activity changed documentation only. The follow-up wrapper
activity adds generic typed runtime reasons and package-consumer dogfooding;
no Hufu source, published package or root-key settings are changed. The Hufu
adapter and host/store/start/provider prerequisites remain unimplemented by this
wrapper change and are not approved for production use.

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
