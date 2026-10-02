# Hufu ↔ BiscuitSharp integration specification

Status: finalized design and implementation handoff, 2026-10-02. The adapter is
not implemented or qualified. This specifies an optional Penghou.Hufu.Biscuit
integration, without making Biscuit a dependency of Hufu core. It does not make
Hufu's experimental prototype a production authorization host.

The [original 43-section proposal](archive/hufu-biscuit-integration-proposal-2026-10-02.md)
is preserved. [ADR 0003](decisions/0003-hufu-integration-profile.md) records the
explicit amendments below. The [handoff](hufu-integration-handoff.md) records
current API compatibility, prerequisites and acceptance tests. MUST and MUST NOT
are normative requirements for an implementation claiming this profile.

## 1. Responsibilities and decision rule

BiscuitSharp owns token creation, verification, serialization, attenuation,
sealing, revocation identifiers and upstream Datalog evaluation.
Penghou.Hufu.Biscuit owns the closed, versioned Hufu token mapping and Biscuit
decision projection. Hufu owns authenticated execution context, current grants,
workflow/activity authority, scope resolution, approval, Cedar composition,
durable evidence and enforcement. Application code cannot supply arbitrary
Datalog, trusted facts, signing keys, policy text or evaluator budgets.

For an operation using this profile:

~~~text
Permit = BiscuitAllows AND CurrentHufuAuthorityAllows
         AND CedarAllows AND EnforcementPreconditionsSatisfied
         AND RequiredEvidenceRecorded
~~~

A denial, unknown result or unavailable dependency in any applicable layer
blocks the operation. An earlier Allow cannot override another layer. Hufu's
existing evaluator already composes its authority layers with Cedar; integration
MUST preserve that composition, not flatten its grants into a union or invent a
second permissive evaluator. It MUST retain each layer's attributable evidence.

## 2. Supported profile and authenticated identity

Profile identity is hufu-biscuit-v1; mapping and envelope versions are explicit.
Only online, host-issued or host-attenuated, registered credentials are admitted.
Offline verification alone cannot establish current Hufu authority.

A Biscuit is a bearer credential, not proof that its presenter is the named
subject. Before interpreting an envelope, the trusted host MUST authenticate the
invoking workload and derive tenant, authority realm, subject, run, workflow,
revision, activity, runtime fence and audience. Agent-supplied metadata cannot
choose these values. Cross-boundary use requires an authenticated protected
channel bound to that workload and audience; a subject string in Datalog is
insufficient. No signing key or reusable credential is exposed to a model.

The profile carries one immutable Hufu grant version in one named authority
layer, one subject/run/workflow/revision/activity binding, and a nonempty set of
explicit capabilities for that grant. It does not merge separate grants.
The current complete Hufu snapshot still independently constrains the operation
through every required layer and mandatory denial.

The grant version record binds the issuer, context, selected layer/grant,
capabilities, scope/exclusions, validity interval, profile version and parent
lineage. It is authenticated trusted-store state. Any change to that meaning
requires a new immutable grant version and credential; unrelated current-state
changes do not justify rewriting an existing credential. Current applicability
is checked on every protected operation.

## 3. Authority predicates and exact request binding

The approved issuer creates exactly one value for each singleton predicate:

~~~datalog
hufu_profile("hufu-biscuit-v1");
hufu_realm("production");
hufu_tenant("tenant-1");
hufu_root_key("hufu-prod-2026-01");
hufu_grant("grant-123");
hufu_grant_version("version-1");
hufu_layer("layer-1");
hufu_subject("agent-7");
hufu_run("run-8");
hufu_workflow("workflow-42");
hufu_revision("revision-9");
hufu_activity("compile");
hufu_fence("fence-2");
hufu_audience("local-broker-1");
hufu_scope("scope-55");
hufu_capability("fs.read");
~~~

hufu_capability may repeat for an explicit capability set belonging to the same
grant version. Singleton values cannot be omitted, duplicated with different
values, inferred as wildcards, or populated from separate grants. Revisions are
opaque strings, matching AuthenticatedAuthorityContext.RevisionId, not integers
with inferred ordering. SubjectId, RunId, TenantId and FenceId map directly from
that context. WorkflowId and ActivityId require trusted host execution bindings:
the present Hufu AuthorityRequest does not supply those fields.

Capability mapping is closed and versioned:

| Hufu AuthorityAction | Biscuit capability |
| --- | --- |
| ReadFile | fs.read |
| ListDirectory | fs.list |
| ReadMetadata | fs.metadata |
| PatchFile | fs.patch |
| Release | data.release |

fs.write is not an alias for PatchFile. Unknown actions fail closed.
Representing PatchFile does not qualify a mutation provider or grant permission
to dispatch it. Concrete metadata, traversal, content and release operations
retain the distinct checks required by Hufu's Luban profile.

Request predicates mirror the context with request_ prefixes, including profile,
realm, tenant, root_key, grant, grant_version, layer, subject, run, workflow,
revision, activity, fence, audience, capability and scope. Their values are
derived from current host context and the authenticated issuance/derivation
record. They are supplied once per evaluation, never copied from unverified
token text. Grant selection is not a caller-controlled permission selector.

The authorizer requires equality of every authority/request binding and joins
the selected capability and scope against the exact concrete resource:

~~~datalog
allow if
  hufu_profile($p), request_profile($p),
  hufu_realm($realm), request_realm($realm),
  hufu_tenant($tenant), request_tenant($tenant),
  hufu_root_key($key), request_root_key($key),
  hufu_grant($grant), request_grant($grant),
  hufu_grant_version($version), request_grant_version($version),
  hufu_layer($layer), request_layer($layer),
  hufu_subject($subject), request_subject($subject),
  hufu_run($run), request_run($run),
  hufu_workflow($workflow), request_workflow($workflow),
  hufu_revision($revision), request_revision($revision),
  hufu_activity($activity), request_activity($activity),
  hufu_fence($fence), request_fence($fence),
  hufu_audience($audience), request_audience($audience),
  hufu_capability($capability), request_capability($capability),
  hufu_scope($scope), request_scope($scope),
  request_workspace($workspace), request_resource($path),
  scope_contains($scope, $workspace, $path)
  trusting authority;
~~~

This is the only Allow policy in the base profile. All token checks must also
pass. Hufu policy denies remain independent required decisions. The authorizer
MUST NOT use trusting previous or trust arbitrary third-party blocks.
Biscuit scopes also trust authorizer-origin facts; therefore predicate namespace
separation is mandatory. The authorizer cannot issue hufu_* facts or rules, and
the issuer cannot issue request_* or scope_contains facts/rules. Attenuation
blocks cannot add trusted authority or request facts. See the
[upstream Datalog reference](https://doc.biscuitsec.org/reference/datalog.html)
for origin semantics; predicate names alone do not establish provenance.

No user value is interpolated into Datalog. Issuance uses BiscuitParam dictionaries
with fixed templates. Request/attenuation construction needs typed substitution
or a centralized, grammar-correct literal writer tested against pinned upstream
parsing. JSON escaping is not assumed to be Datalog escaping. There is no
arbitrary-source public adapter overload.

## 4. Resource and time semantics

The canonical resource is Hufu's typed pair (WorkspaceId, RelativePath), together
with the trusted provider's resolved object identity where required.
request_workspace and request_resource carry those existing canonical values.
The illustrative file:/repo URI from the proposal is not a new wire format.

The host resolves the actual resource using Hufu/Luban's registered provider.
It independently checks the selected grant scope, exclusions, validity,
mandatory denials and current authority. Only then may it insert
scope_contains(scopeId, workspaceId, relativePath) for this exact operation.
Biscuit does not normalize filesystem paths, implement symlink/junction rules,
or infer containment from raw string prefixes.

Hufu's structural segment-boundary containment is distinct from physical
filesystem confinement. The adapter preserves current ASCII case folding,
Unicode validation and provider-specific final-object checks; it does not apply
a general URI or Unicode normalization algorithm.

Trusted host time controls NotBefore <= now < ExpiresAt and snapshot validity.
Grant/derivation validity is checked in current Hufu state at authorization and
again at the start boundary. Optional token expiry checks use the host-supplied
AddTimeFact value; its whole-second precision cannot weaken higher-precision
Hufu expiry checks. No client clock is trusted.

## 5. Root keys and rotation

The first profile uses Ed25519. Algorithm, authority realm and key provider are
host configuration, never agent input.

Each envelope requires RootKeyId as a bounded lookup hint. Resolve exactly one
verification key in the host-selected realm, then call
BiscuitToken.Parse(receivedBytes, publicKey). The returned key must be Ed25519;
an incompatible provider key is AuthorizationFailure. Unknown hints produce UnknownRootKey;
an unavailable provider produces AuthorizationUnavailable; failed verification
produces InvalidCredential. Do not try all keys or select a realm from token
claims. After verification, the fixed authorization policy binds the signed
hufu_root_key and hufu_realm values to the selected key and authenticated realm.

Preview.1 cannot set the Biscuit protobuf root_key_id at issuance and Inspect
only exposes its optional uint32 value after verification. This profile does
not require that field. Its string key ID resides in envelope metadata and a
signed authority fact. These are distinct namespaces; Inspect is not a
pre-verification key resolver.

A provider exposes current signing key plus stable ID, and a verification-key
lookup by (trusted realm, ID). Its contract explicitly states ownership:

- Verification keys are immutable public values.
- Issuance acquires a signing-key lease. Rotation cannot dispose its native
  BiscuitPrivateKey while Build is in progress.
- The lease/provider coordinates lifetime and disposal; the adapter must not
  dispose a shared current key or export it for normal issuance.
- Issuance failure records no usable credential or permit.

Private persistence belongs to a protected secret-store provider, separate from
grants, envelopes, logs and source control. No built-in production key exists.
Development/test/production use distinct keys; tests use ephemeral keys.
Protected at-rest DER/PEM storage followed by native import is supported by the
current wrapper. Non-exportable CNG/KMS/HSM signing is a separate future
capability; returning a native private-key handle does not implement it.

Rotation stops old-key issuance, retains its public verification key while
trusted, and uses the new key for new issuance. Root-key retirement blocks new
operations at the ordered start boundary. Revocation and key retirement are
distinct operations; reintroducing a key cannot silently restore terminally
revoked grant or derivation records.

## 6. Envelope, bytes, registration and bounds

Envelope v1 requires EnvelopeVersion, TokenProfile, RootKeyId, immutable
ReceivedTokenBytes, recomputed TokenFingerprint, issuer/realm metadata, grant
version reference, issuance-record reference and CreatedAt. Metadata supplies
no authority until validated against trusted context, registration and signed
facts. Unknown versions, profiles, required fields or key algorithms reject.
Version-specific extensions must be registered; no guessed future semantics.

ReadOnlyMemory<byte> is not inherently immutable. Envelope construction takes a
defensive snapshot and exposes copies or an owned immutable abstraction.
Transport Base64URL decoding preserves exact bytes.

ReceivedTokenBytes and NormalizedTokenBytes are separately retained in restricted
credential storage for verification/evidence needs, not general logs.
TokenFingerprint is lower-case SHA-256 hex of received bytes. It is an audit
correlation value, not a revocation ID, authenticity proof or sole grant identity.
Fingerprints are computed locally, never trusted from supplied metadata.

The host maintains authenticated immutable issuance/derivation registrations
binding exact token length/digest, profile, grant version, key ID, context,
constraints and lineage. A digest selects a trusted record; it does not
authenticate one. Verify the token and match all bindings before using that
record as authority. Store lookup failure fails closed.

Only registered canonical issuer/adapter output is admitted in v1. After Parse,
received bytes must equal ToBytes(), and the recomputed digest must match the
trusted registration. An otherwise valid token with an extra trailing suffix
is InvalidCredential for this transport profile. BiscuitSharp Parse itself
continues preserving upstream normalization semantics. Never replace raw input
before recording its received-byte fingerprint.

This closed registration profile prevents unknown token logic from being
accepted without a verified AST validator. Do not parse Inspect().BlockSources
with regex/string splitting to recognize a security profile. Unregistered
offline holder-created attenuation is unsupported; future offline/third-party
logic needs its own explicit profile and qualification.

Initial adapter ceilings are explicit security amendments: at most 64 KiB of
received token bytes, 32 total blocks including authority, 64 KiB total generated
UTF-8 Datalog source and 256 total generated checks per derivation chain.
Identity strings and paths additionally obey Hufu's current validation bounds.
Validate receipt length before Parse. The authenticated registration supplies
expected source/check counts; verified Inspect block count must match that
record and ceiling before authorization. These ceilings are profile limits,
not claims about native allocation peaks. Issuance or attenuation that would
exceed them fails without registering a usable credential.

## 7. Issuance and attenuation

IssueAsync is a host-only authority operation. It validates issuer ceilings,
approval/provenance, current selected grant version, exact context, capabilities,
scope/exclusions and validity. It leases the configured signing key, generates
the closed authority predicates, builds and serializes the token, and records
its registration, root revocation ID and required issuance evidence before
releasing the envelope. Failed mandatory registration/evidence prevents release.

AttenuateAsync accepts a registered verified parent and structured restrictions,
not arbitrary Datalog. It proves requested rights remain within both the parent
restriction record and the delegator's current Hufu authority. It then appends
checks, preserves the root binding, registers exact child bytes and all child
revocation IDs, and records lineage/evidence before release.

The first profile supports narrowing capability sets, scope within the same
workspace and earlier expiry within the same authenticated subject/run/workflow/
revision/activity/fence/audience. Allowed-capability restrictions are disjunctions
of fixed request_capability equality checks. Resource restrictions use a trusted
host-resolved restriction ID and scope relationship facts, never string-prefix
Datalog checks; their typed mapping must be included in the registration.

An activity-bound token cannot be attenuated into authority for another activity.
A subject-bound or workflow-bound token cannot be relabeled to another subject
or child workflow. Such delegation requires a separately authorized Hufu
issuance/rebinding profile with current parent lineage and revocation rules;
it is outside v1. Agent requests cannot cause unrestricted root issuance.
A sealed token cannot be attenuated; unsupported restrictions fail closed.

## 8. Authorization and enforcement sequence

VerifyAsync means authorization/preflight, not performing the resource effect.
It returns a structured result bound to an exact immutable request, rather than
a generic reusable Boolean permit. A separate trusted Hufu broker executes.

1. Authenticate the workload and derive the exact typed execution context.
2. Validate bounded envelope/version/profile; snapshot received bytes.
3. Resolve the configured realm/key hint and cryptographically verify.
4. Validate canonical bytes, authenticated registration and exact profile/lineage.
5. Obtain all GetRevocationIds() and check every ID in realm-scoped Hufu storage.
6. Fetch current Hufu state and resolve the concrete resource through its provider.
7. Construct fixed trusted request/scope/time facts and explicit evaluation limits.
8. Evaluate the fixed Biscuit policy and every token check; preserve its result.
9. Validate current grant, subject/run/workflow/revision/activity/fence/audience.
10. Obtain the required current Hufu/Cedar decision with all layer restrictions.
11. Persist required authorization evidence before a result can be used for start.
12. At the authoritative start gate, revalidate revocation, key trust, exact
    authority version/fence, expiry, evaluator/policy identity and required
    provider preconditions; serialize those checks with durable operation start.
13. Only a newly authorized exact start may dispatch through the trusted broker.
14. Record outcome evidence and reconcile incomplete/ambiguous effects.

Checking a revocation store and later starting an operation is insufficient.
All block revocation and key-trust changes applicable to this profile must be
ordered with operation start, either in the same authority transaction or a
qualified equivalent protocol. Existing Hufu SQLite start support does not
automatically add Biscuit revocation storage or credential verification.

The selected Hufu semantics block new starts ordered after acknowledged
revocation; already-started operations may complete. No drain, rollback or
no-I/O-after-acknowledgement guarantee is inferred. A stream/chunk profile must
define its own protected-operation boundaries.

The operation binding includes RequestIdentity, tenant/subject/run/workflow/
revision/activity/fence/audience, capability, canonical workspace/path, physical
object/effect arguments where applicable, credential fingerprint, lineage,
current snapshot sequence, evaluator/policy/profile identities and evidence
references. A changed argument set requires a new authorization/start.
An AlreadyStarted receipt is historical/idempotent evidence; it cannot trigger
a second dispatch. Provider locks/object identity must close relevant filesystem
races; independent path rechecks alone do not establish confinement.

## 9. Failure mapping and evaluation budgets

Every non-permit is a structured result. Preserve denial versus unavailable
state and machine-readable evidence. Never turn exceptions into permission.

| Condition | Integration failure code |
| --- | --- |
| Invalid envelope/version/required metadata | InvalidAuthorityEnvelope |
| Bad signature/format, unregistered profile or noncanonical transport | InvalidCredential |
| No trusted key for the supplied bounded hint | UnknownRootKey |
| Any applicable ancestor/child revocation ID revoked | AuthorityRevoked |
| Confirmed token check failure | AuthorityConstraintFailed |
| Completed Biscuit evaluation with no allow or explicit deny | AuthorityDenied |
| Current workflow/context/grant/revision/fence does not match | WorkflowAuthorityDenied |
| Completed Cedar decision denies | PolicyDenied |
| Machine-confirmed fact/iteration/time limit breach | AuthorizationBudgetExceeded |
| Required key/store/evaluator dependency unavailable | AuthorizationUnavailable |
| Unexpected native/bridge/Datalog mapping failure or unclassified evaluation failure | AuthorizationFailure |
| Required exact-start/provider condition cannot be established | EnforcementPreconditionFailed |

Confirmed native failures take precedence over ordinary denials in diagnostic
classification; retain every available component finding. Token-check failures
are checked before no-matching-policy; policy-match notes do not erase checks.
When a prior stage blocks, later stages are NotEvaluated, not reported as Allow.

The adapter MUST NOT infer typed reasons by parsing exception messages or printed
Datalog. Workflow mismatch attribution comes from trusted binding validation;
it cannot reliably be inferred from a generic no-matching-policy result.

Preview.1 uses evaluation_failure for budget exhaustion and other runtime
errors. Map it conservatively to AuthorizationFailure until a wrapper supplies
a machine-readable reason. An implementation claiming all budget-specific
conformance cases MUST first qualify that wrapper enhancement. Do not label
all evaluation failures AuthorizationBudgetExceeded.

Production configuration requires positive finite fact/iteration/time ceilings
chosen and measured by the host. MaxFacts/MaxIterations map by checked conversion
to BiscuitAuthorizerLimits ulong fields. Time is an integral millisecond value
of at least 1 ms; reject sub-millisecond/fractional/overflowing configuration.
Always call WithLimits. Agents cannot select or increase the budget.
No automatic retry with larger limits is permitted.

BiscuitSharp authorization is synchronous. CancellationToken applies to awaited
host dependencies and checkpoints before/after the native call; it does not
interrupt native evaluation, parse, verification or inspection. A cancelled
authorization cannot issue a usable permit/start. Evaluation budgets do not
promise hard memory caps or an end-to-end cancellation deadline.

## 10. Evidence and confidentiality

Mandatory decision/start evidence must be acknowledged before protected dispatch.
If it cannot be persisted, return an unavailable/non-permit result; a secondary
best-effort log cannot substitute for required evidence. Outcome evidence follows
execution; its failure triggers reconciliation, not a retrospective claim that
an effect never occurred or an automatic duplicate retry.

Evidence includes exact request/context/profile/version bindings, grant/parent
references, received-byte fingerprint, revocation findings, component decisions
and NotEvaluated states, failure classification, measured duration, configured
limits, snapshot/evaluator/policy identities, start identity and outcome receipt.
Budget usage is included only if the engine exposes it; preview.1 does not
expose counters.

Raw token/key material, arbitrary Inspect output, Datalog error text and full
host evidence stay out of agent-visible logs. Restricted credential retention,
access controls, integrity and retention limits belong to Hufu's host/store.
Human or AI explanation uses authorized structured projections and never
participates in a permit decision.

## 11. Acceptance and non-goals

Every case in section 39 of the [original proposal](archive/hufu-biscuit-integration-proposal-2026-10-02.md)
is retained, subject to this profile's explicit amendments:

- Revision identifiers are exact opaque strings.
- Read/write attenuation uses explicit parent capability sets; fs.patch is the
  only initial mutation action mapping.
- Changed signed bytes fail signature checks; tolerated trailing framing must
  fail this canonical registered transport profile, even when Parse succeeds.
- Cross-subject/activity/workflow attenuation is rejected as unsupported.
- Revocation races assert ordered block-new-start semantics, not rollback of
  already-started effects.

Additional mandatory cases cover authority/request predicate pollution from
attenuation blocks, grant Cartesian-product amplification, tenant/realm/run/
fence/audience mismatch, unknown logic/profile/derivation records, mutable byte
aliasing, duplicate singleton authority, malformed Datalog values, cancelled
native-call results, invalid bounds, mandatory evidence failure, key lease
rotation/disposal races, key retirement at start, all-layer Hufu composition,
AlreadyStarted non-dispatch, and typed budget errors versus other runtime errors.
Qualified provider-object tests must exercise actual link/junction races;
mock zero-I/O tests alone do not establish physical containment.

The first profile does not implement offline authorization, distributed key
sync, non-exportable signing, third-party blocks, arbitrary token logic,
Cedar-to-Datalog translation, filesystem canonicalization in Datalog, a sandbox,
automatic budget escalation, AI-generated authorization policy, or silent
version migration. Hufu lifecycle and enforcement qualification remain required.

## 12. Unspecified behavior

No implementation may silently invent authority semantics. Unsupported choices
fail closed and require a versioned design amendment. Internal class names or
allocation strategies do not require one.

The final invariant is: Biscuit proves the signed delegation and restrictions;
authenticated current Hufu state determines their applicability; Cedar applies
current policy; the trusted broker starts only the exact operation permitted
by all required layers and durably recorded.
