# Hufu ↔ BiscuitSharp Integration Specification

## 1. Purpose

This document defines the implementation contract between `Penghou.Hufu.Biscuit`, Hufu, and BiscuitSharp.

It removes security-sensitive implementation choices from the adapter implementation.

The responsibilities are:

- **BiscuitSharp** provides Biscuit token creation, serialization, verification, attenuation, revocation identifiers, and Datalog authorization.
- **Penghou.Hufu.Biscuit** maps Hufu authority into Biscuit tokens and maps Biscuit authorization results back into Hufu.
- **Hufu** remains authoritative for workflow state, approvals, grants, revocation storage, Cedar policy evaluation, resource canonicalization, authority envelopes, diagnostics, and actual enforcement.

Biscuit is the **delegation and capability proof layer**.

It does not replace Hufu workflow authorization or Cedar policy evaluation.

---

# 2. Security model

A request is allowed only when all applicable authorization layers allow it.

Conceptually:

```text
ALLOW =
    BiscuitAllows
    AND WorkflowAllows
    AND CedarAllows
    AND EnforcementPreconditionsSatisfied
```

A positive result from one layer MUST NOT override a denial or failure from another layer.

All authorization failures MUST fail closed.

An inability to establish authorization MUST be treated as denial for enforcement purposes, while preserving a distinct diagnostic reason.

---

# 3. Authorization pipeline

Every operation protected by Hufu authority MUST follow this order:

```text
1. Validate Hufu authority envelope
2. Extract exact Biscuit token bytes
3. Resolve Biscuit root verification key
4. Cryptographically verify Biscuit token
5. Extract Biscuit revocation identifiers
6. Query Hufu revocation store
7. Canonicalize requested resource
8. Construct trusted request facts
9. Evaluate Biscuit authorization
10. Validate current workflow/activity authority
11. Evaluate Cedar policy
12. Revalidate dynamic enforcement preconditions where required
13. Perform operation through Hufu enforcement broker
14. Record authorization/enforcement evidence
```

No information contained in an unverified Biscuit token may be treated as trusted.

Revocation MUST be checked after cryptographic verification and before Biscuit authorization.

The enforcement broker MUST NOT accept a previous `allow` result as authority to perform a materially different operation.

---

# 4. Hufu grant representation

## 4.1 Required grant identity

Every Biscuit-backed Hufu grant MUST have a stable grant identifier.

Example Hufu concept:

```text
GrantId       = "grant-123"
SubjectId     = "agent-7"
WorkflowId    = "workflow-42"
Revision      = 9
ActivityId    = "compile"
Capability    = "fs.read"
ScopeId       = "scope-55"
```

Grant identifiers MUST be immutable once issued.

Updating a grant MUST create a new grant or new grant version rather than changing the meaning of an already-issued token.

---

# 5. Biscuit authority facts

The Biscuit authority block MUST contain facts representing the authority delegated by Hufu.

The initial mapping SHALL use the following predicates:

```datalog
hufu_grant("grant-123");
hufu_subject("agent-7");
hufu_workflow("workflow-42");
hufu_revision(9);
hufu_activity("compile");
hufu_capability("fs.read");
hufu_scope("scope-55");
```

The adapter SHOULD expose constants/builders for these predicate names.

Application code MUST NOT construct these strings independently.

Future schema versions MAY introduce additional predicates, but the existing meaning of an issued predicate MUST NOT change.

---

# 6. Trusted fact sources

Facts MUST be classified by trust source.

## 6.1 Authority-block facts

These facts represent delegated authority and MUST originate from the Biscuit authority block:

```text
hufu_grant
hufu_subject
hufu_workflow
hufu_revision
hufu_activity
hufu_capability
hufu_scope
```

Facts in attenuation blocks MUST NOT increase these authorities.

An attenuated token MUST NOT be able to turn:

```text
fs.read
```

into:

```text
fs.write
```

or broaden a scope.

---

## 6.2 Request facts

Request facts MUST be supplied by the Hufu authorizer, not by the token holder.

Predicates:

```datalog
request_subject("agent-7");
request_workflow("workflow-42");
request_revision(9);
request_activity("compile");
request_capability("fs.read");
request_resource("file:/repo/docs/design.md");
```

These values MUST be derived from the actual Hufu execution context.

The caller MUST NOT be permitted to override them.

---

## 6.3 Resource relationship facts

Resource scope evaluation remains owned by Hufu.

For example:

```datalog
scope_contains(
    "scope-55",
    "file:/repo/docs/design.md"
);
```

This fact MUST only be inserted after Hufu has canonicalized the requested resource and evaluated Hufu's resource/scope rules.

Biscuit Datalog MUST NOT independently implement:

- filesystem path normalization,
- symlink semantics,
- junction handling,
- case sensitivity rules,
- operating-system path rules,
- repository boundary detection.

These remain Hufu responsibilities.

---

# 7. Resource canonicalization

Hufu MUST canonicalize resources before authorization.

The canonical form MUST be the same form used by:

- Hufu scopes,
- Biscuit request facts,
- Cedar resource mapping,
- enforcement checks,
- diagnostics,
- audit evidence.

Example:

```text
file:/repo/src/example.cs
```

rather than mixing:

```text
C:\Repo\src\example.cs
/repo/src/example.cs
file:///repo/src/example.cs
```

The canonicalization component MUST be shared or centrally defined.

Authorization MUST NOT rely on string-prefix comparison for filesystem containment.

Symlinks, junctions and equivalent redirections MUST be resolved according to Hufu's filesystem enforcement rules.

---

# 8. Biscuit authorization rules

The adapter MUST require the token authority to match the current request.

Conceptually, authorization requires:

```text
grant subject == request subject
grant workflow == request workflow
grant revision == request revision
grant activity == request activity
grant capability == request capability
grant scope contains request resource
```

Where Hufu deliberately supports broader grants, such as workflow-level rather than activity-level authority, that MUST be represented explicitly.

The adapter MUST NOT infer wildcard semantics from absent facts.

Absence means no authority unless an explicitly defined rule states otherwise.

---

# 9. Attenuation

Derived Biscuit tokens MAY add restrictions.

Examples include:

```text
restrict to one activity
restrict to one resource subtree
restrict to read-only operations
restrict to an expiration time
restrict to a child workflow
```

Attenuation MUST never create authority absent from the parent token.

Hufu MUST treat attenuation as restriction only.

If an attenuation block attempts to represent broader Hufu authority, authorization MUST still be constrained by the authority block and trusted authorizer facts.

---

# 10. Workflow binding

Biscuit authority MUST be bound to a workflow.

Default behavior:

```text
WorkflowId MUST match.
Revision MUST match.
ActivityId MUST match.
```

This is intentionally strict.

A workflow revision that materially changes authority requirements therefore invalidates previously issued activity authority unless Hufu explicitly reissues or rebinds authority.

Future support for revision ranges or revision-independent grants MUST be introduced as an explicit feature. It MUST NOT be inferred by the adapter.

---

# 11. Delegation

An agent may delegate only authority it currently holds.

Delegated authority MUST be represented by attenuation where possible.

The adapter MUST NOT mint a new root-authority Biscuit merely because an agent requests delegation.

Root issuance is an Hufu authority operation.

Delegation MUST satisfy both:

```text
delegated authority ⊆ token authority
```

and:

```text
delegated authority ⊆ Hufu authority currently available to delegator
```

If either cannot be proven, delegation fails.

---

# 12. Root key algorithm

The initial Hufu Biscuit implementation SHALL use:

```text
Ed25519
```

for Biscuit root signing.

The algorithm MUST NOT be selected dynamically by agent input.

---

# 13. Root key abstraction

The integration SHALL define a root key provider equivalent to:

```csharp
public interface IBiscuitRootKeyProvider
{
    ValueTask<BiscuitSigningKey> GetCurrentSigningKeyAsync(
        CancellationToken cancellationToken = default);

    ValueTask<BiscuitVerificationKey?> GetVerificationKeyAsync(
        string rootKeyId,
        CancellationToken cancellationToken = default);
}
```

Exact types MAY follow BiscuitSharp's existing API.

The semantic contract MUST remain the same.

---

# 14. Root key identifiers

Every issued token MUST identify the root key used for signing.

Root key IDs MUST:

- be stable,
- contain no secret material,
- uniquely identify a verification key within the Hufu authority realm.

Suggested representation:

```text
hufu-prod-2026-01
```

or an opaque generated identifier.

Code MUST NOT assume chronological meaning in the identifier.

---

# 15. Root key storage

Private root signing keys MUST NOT be stored directly in:

- the Hufu grant database,
- authority envelopes,
- logs,
- workflow definitions,
- source control.

Key persistence MUST be hidden behind a key-store abstraction.

Initial implementations MAY use operating-system protected secret storage.

The design MUST permit later providers such as:

- Windows DPAPI/CNG,
- macOS Keychain,
- Linux secret/key services,
- cloud KMS,
- HSM-backed signing.

Only public verification material may be freely persisted with Hufu configuration.

---

# 16. Environment separation

Development, test and production environments MUST NOT share Biscuit root private keys.

Tests SHOULD use ephemeral generated keys.

Production code MUST NOT contain a built-in default root private key.

---

# 17. Key rotation

Rotation SHALL operate as follows:

```text
Old key:
    no longer used for issuance
    public verification key retained

New key:
    used for all new issuance
```

Tokens signed with old keys MAY continue to verify while their root key remains trusted.

Removing an old verification key invalidates all tokens requiring that key.

Key rotation MUST therefore be independent from revocation.

Normal authority revocation SHOULD use Biscuit/Hufu revocation rather than premature deletion of an old verification key.

---

# 18. Token representation

The authoritative Biscuit representation inside Hufu is the exact serialized binary token.

The authority envelope SHALL store:

```csharp
ReadOnlyMemory<byte> TokenBytes
```

or an equivalent immutable byte representation.

Base64URL is a transport encoding only.

The system MUST NOT treat a Base64 string as the canonical identity of a token.

---

# 19. Received versus normalized bytes

Hufu MUST retain the exact token bytes received for verification and audit purposes.

If BiscuitSharp performs parse/reserialize normalization, those bytes MUST be treated separately.

Conceptually:

```text
ReceivedTokenBytes
NormalizedTokenBytes
```

The received bytes MUST NOT be silently replaced before evidence is recorded.

---

# 20. Token fingerprint

Hufu MAY calculate:

```text
SHA-256(ReceivedTokenBytes)
```

as a stable audit fingerprint.

Example field:

```text
TokenFingerprint
```

This fingerprint is for:

- tracing,
- diagnostics,
- audit evidence,
- correlation.

It MUST NOT replace Biscuit revocation identifiers.

---

# 21. Revocation

After successful Biscuit verification, Hufu MUST obtain all Biscuit revocation identifiers represented by the verified token.

Each relevant revocation identifier MUST be queried against the Hufu revocation store.

Conceptually:

```csharp
foreach (var revocationId in token.RevocationIds)
{
    if (await revocationStore.IsRevokedAsync(revocationId))
        return AuthorityRevoked;
}
```

Exact BiscuitSharp API names MAY differ.

The behavior MUST NOT.

If the revocation store cannot be consulted, authorization MUST fail closed.

---

# 22. Hufu revocation records

Hufu owns revocation persistence.

A revocation record SHOULD contain at least:

```text
RevocationId
RevokedAt
Reason
Source
WorkflowId where applicable
GrantId where applicable
Evidence/provenance reference
```

BiscuitSharp MUST NOT become the persistence layer for Hufu revocations.

---

# 23. Biscuit, workflow and Cedar responsibilities

The systems have distinct responsibilities.

## Biscuit

Answers:

```text
Was this authority cryptographically delegated to this holder,
with these restrictions?
```

## Hufu workflow authority

Answers:

```text
Is this authority valid for the currently executing workflow,
revision and activity?
```

## Cedar

Answers:

```text
Does current system/organizational policy permit this operation?
```

## Hufu enforcement broker

Answers:

```text
Can this exact authorized operation be safely performed now?
```

These concerns MUST remain separate.

---

# 24. Decision combination

Default combination:

```text
Biscuit        ALLOW
Workflow       ALLOW
Cedar          ALLOW
-------------------
Final          ALLOW
```

Any denial produces final denial.

Any authorization-system failure produces final denial with a distinct failure classification.

Cedar MUST NOT grant authority absent from Biscuit/Hufu delegation.

Biscuit MUST NOT override a Cedar forbid.

Workflow authority MUST NOT override either.

---

# 25. Preflight versus enforcement-time checks

Hufu MAY preflight authority before execution.

Preflight is advisory with respect to dynamic conditions.

The actual enforcement broker MUST perform all checks that can become stale between planning and execution.

Examples:

- revocation,
- resource canonicalization,
- symlink target,
- current workflow revision,
- temporary authority,
- dynamic Cedar policy,
- scope resolution where filesystem state matters.

A successful preflight MUST NOT be treated as an irrevocable permit.

---

# 26. Failure model

The adapter SHALL map implementation-specific failures into stable Hufu failure codes.

Required codes:

```text
InvalidAuthorityEnvelope
InvalidCredential
UnknownRootKey
AuthorityRevoked
AuthorityConstraintFailed
AuthorityDenied
WorkflowAuthorityDenied
PolicyDenied
AuthorizationBudgetExceeded
AuthorizationUnavailable
AuthorizationFailure
EnforcementPreconditionFailed
```

---

# 27. Failure semantics

## InvalidAuthorityEnvelope

Envelope structure or required metadata is invalid.

## InvalidCredential

The Biscuit token is malformed, cryptographically invalid or otherwise cannot be trusted.

## UnknownRootKey

The token references a root key not trusted by the current Hufu authority realm.

## AuthorityRevoked

At least one applicable Biscuit revocation identifier is revoked.

## AuthorityConstraintFailed

The token is valid but one of its attenuation/check constraints fails.

## AuthorityDenied

The Biscuit authorization system successfully evaluated the request and did not authorize it.

## WorkflowAuthorityDenied

The token does not match the active workflow/revision/activity authority.

## PolicyDenied

Cedar evaluated the request and denied it.

## AuthorizationBudgetExceeded

Biscuit Datalog evaluation exceeded an explicitly configured resource budget.

## AuthorizationUnavailable

A required authorization dependency, such as revocation storage, could not be consulted.

## AuthorizationFailure

Unexpected internal authorization failure.

## EnforcementPreconditionFailed

Authorization succeeded, but a security-sensitive condition changed or could not be established at actual execution time.

---

# 28. Exception handling

Security denials MUST be represented as structured results rather than relying solely on exceptions.

Exceptions MAY be used internally but MUST be normalized before they cross the Hufu authorization boundary.

Diagnostic information MUST NOT leak:

- private signing keys,
- secret configuration,
- raw security-sensitive credentials.

---

# 29. Authorization budgets

Biscuit authorization MUST use explicit Hufu-owned evaluation limits.

The adapter SHALL expose configuration conceptually equivalent to:

```csharp
public sealed record BiscuitAuthorizationBudget(
    int MaxFacts,
    int MaxIterations,
    TimeSpan MaxEvaluationTime);
```

If BiscuitSharp exposes additional relevant limits, they SHOULD also be configurable.

Production code MUST NOT depend silently on library defaults.

---

# 30. Budget behavior

Exceeding any authorization budget MUST:

1. stop evaluation,
2. return `AuthorizationBudgetExceeded`,
3. deny the operation,
4. record diagnostic evidence.

The system MUST NOT retry automatically with a larger authorization budget.

Agents MUST NOT be able to raise their own authorization budgets.

---

# 31. Authority envelope

A Hufu Biscuit authority envelope SHOULD conceptually contain:

```text
EnvelopeVersion
TokenBytes
TokenFingerprint
Issuer/AuthorityRealm
Grant reference where applicable
CreatedAt
Supporting provenance/evidence metadata
```

Workflow values inside the envelope are metadata.

They MUST NOT override the cryptographically verified token or live Hufu execution context.

---

# 32. Authority-envelope versioning

The envelope MUST have an explicit version.

Example:

```text
EnvelopeVersion = 1
```

Unknown versions MUST fail closed.

Parsing code MUST NOT guess how to interpret unknown fields or future envelope formats.

---

# 33. Issuance API

`Penghou.Hufu.Biscuit` SHOULD expose an issuance operation conceptually equivalent to:

```csharp
IssueAsync(
    HufuGrant grant,
    BiscuitIssuanceOptions options,
    CancellationToken cancellationToken)
```

Issuance MUST:

1. validate the Hufu grant,
2. obtain the current root signing key,
3. generate authority facts,
4. add required initial checks,
5. issue the Biscuit,
6. serialize exact token bytes,
7. calculate audit fingerprint,
8. create the Hufu authority envelope,
9. record issuance evidence.

---

# 34. Verification API

The adapter SHOULD expose a verification operation conceptually equivalent to:

```csharp
VerifyAsync(
    AuthorityEnvelope envelope,
    HufuAuthorizationRequest request,
    CancellationToken cancellationToken)
```

Verification MUST perform the complete authorization pipeline defined by this specification.

Callers MUST NOT have to manually compose:

```text
verify
revocation
Biscuit authorization
workflow check
Cedar
```

in different orders.

There should be one safe orchestration path.

Lower-level APIs MAY exist for testing and specialized infrastructure but SHOULD NOT be the default public integration API.

---

# 35. Enforcement API boundary

Authorization MUST produce a structured permit/result tied to the exact operation.

A permit SHOULD include enough immutable information to detect mismatches between authorization and enforcement.

For example:

```text
RequestId
Subject
WorkflowId
Revision
ActivityId
Capability
CanonicalResource
AuthorizationTimestamp
PolicyVersion where applicable
TokenFingerprint
Decision evidence reference
```

The enforcement broker MUST reject use of a permit for another operation.

---

# 36. TOCTOU handling

Hufu MUST assume that resource state may change between authorization and execution.

Security-sensitive filesystem operations MUST revalidate conditions at enforcement time where necessary.

Examples:

- symbolic link target changed,
- destination appeared,
- path canonicalization changed,
- mount/junction changed,
- workflow revision changed,
- grant revoked.

The exact mechanics belong to Hufu enforcement, not BiscuitSharp.

---

# 37. Logging and evidence

Every authorization attempt SHOULD be traceable.

Evidence SHOULD include:

```text
RequestId
GrantId
TokenFingerprint
Subject
WorkflowId
Revision
ActivityId
Capability
CanonicalResource
BiscuitDecision
WorkflowDecision
CedarDecision
FinalDecision
FailureCode
Revocation result
Evaluation duration
Budget usage where available
```

Raw private keys MUST never be logged.

Raw Biscuit tokens SHOULD NOT be logged by default.

---

# 38. Authority debugger integration

The adapter MUST preserve sufficient structured information for Hufu to explain denials.

Example:

```text
Requested capability:
    fs.write

Delegated capability:
    fs.read

Result:
    AuthorityDenied
```

The adapter SHOULD expose machine-readable failure details rather than building human prose itself.

Hufu's authority debugger or AI explanation layer may convert these facts into explanations.

The AI explanation MUST NOT participate in the authorization decision.

---

# 39. Integration tests

The integration test suite MUST contain at least the following cases.

### Basic authority

```text
Granted read -> read succeeds.
Granted read -> write fails.
Granted resource -> unrelated resource fails.
Granted workflow -> another workflow fails.
Granted activity -> another activity fails.
```

### Workflow revision

```text
Grant revision N -> revision N succeeds.
Grant revision N -> revision N+1 fails by default.
```

### Attenuation

```text
Parent read/write -> child read succeeds.
Child read -> child write fails.
Child cannot restore authority removed through attenuation.
Child cannot broaden resource scope.
```

### Revocation

```text
Non-revoked token succeeds.
Revoked token fails.
Revoked ancestor causes derived token to fail where Biscuit semantics require it.
Revoking a child does not incorrectly revoke unrelated authority.
Revocation-store failure fails closed.
```

### Keys

```text
Valid current key succeeds.
Unknown root key fails.
Invalid signature fails.
Old verification key succeeds during rotation window.
New issuance uses new signing key.
Removed verification key fails.
Dev key cannot validate production authority.
```

### Cedar composition

```text
Biscuit allow + Workflow allow + Cedar allow -> allow.
Biscuit deny + Cedar allow -> deny.
Biscuit allow + Workflow deny + Cedar allow -> deny.
Biscuit allow + Workflow allow + Cedar deny -> deny.
```

### Envelope

```text
Malformed envelope fails.
Unknown envelope version fails.
Changed token bytes fail verification.
Base64 transport round-trip preserves exact token bytes.
Token fingerprint is calculated from received binary bytes.
```

### Resource security

```text
Resource outside scope fails.
Path traversal cannot escape scope.
Symlink escape fails.
Junction/mount equivalent escape fails where supported.
Canonical aliases produce consistent authorization.
```

### Budgets

```text
Normal policy stays inside budget.
Maximum fact count exceeded -> AuthorizationBudgetExceeded.
Maximum iterations exceeded -> AuthorizationBudgetExceeded.
Evaluation time exceeded -> AuthorizationBudgetExceeded.
Budget failure denies operation.
Agent cannot modify budget.
```

### Enforcement

```text
Authorized operation cannot be reused for another resource.
Authorized operation cannot be reused for another capability.
Revocation between preflight and enforcement prevents execution.
Workflow revision change between preflight and enforcement prevents execution where applicable.
Dynamic filesystem escape discovered at enforcement prevents execution.
```

---

# 40. Conformance tests

A small security conformance suite SHOULD be reusable by every Hufu enforcement adapter.

At minimum it should verify:

```text
No authority amplification.
No delegation beyond parent authority.
No policy layer can override another layer's denial.
Revocation fails closed.
Unknown identity/key/version fails closed.
Resource canonicalization is consistent.
Authorization results cannot be replayed for another operation.
```

---

# 41. Explicit non-goals for the first implementation

The initial implementation MUST NOT attempt to implement:

- distributed root-key synchronization,
- HSM integration,
- remote Biscuit introspection,
- custom cryptographic algorithms,
- Datalog-based filesystem canonicalization,
- Datalog replacement for Cedar,
- Datalog replacement for Hufu workflow authority,
- automatic authorization-budget increases,
- AI-generated authorization rules executed without deterministic validation,
- transparent migration between incompatible authority-envelope versions.

These can be added independently later.

---

# 42. Implementation rule

When this specification does not define a security-sensitive behavior, implementation MUST NOT silently invent one.

The developer should:

1. preserve the secure fail-closed behavior,
2. expose the missing choice explicitly,
3. add a design decision or specification amendment before implementing behavior that changes authority semantics.

Ordinary implementation details that do not affect authority semantics, such as internal class names or allocation strategies, do not require specification amendments.

---

# 43. Architectural invariant

The central invariant is:

> A Biscuit proves that authority was delegated. Hufu proves that the authority still applies to the current workflow and operation. Cedar determines whether policy permits its use. The enforcement broker performs only the exact operation authorized by all three.

No component may use another component's `allow` decision to create authority that was not already available to it.