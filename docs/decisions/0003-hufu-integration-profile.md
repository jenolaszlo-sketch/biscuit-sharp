# ADR 0003: optional, registered Hufu Biscuit integration profile

Status: selected design, 2026-10-02. The optional Hufu adapter and co-located SQLite start prototype are implemented locally; actual host/provider qualification remains pending. This decision does not change
the published BiscuitSharp API or Hufu's current optional-transport status.

## Context

The maintainer supplied a 43-section integration proposal and requested that
the specification and handoff be finalized. Source review found differences
between that conceptual model, published BiscuitSharp 0.1.0-preview.1 and the
current experimental Hufu contracts. Those differences affect authority, not
just class names. The original text is retained in
[the proposal archive](../archive/hufu-biscuit-integration-proposal-2026-10-02.md).

## Decision and explicit amendments

Adopt [hufu-biscuit-v1](../hufu-integration-spec.md) as an optional online profile.
Its first implementation must satisfy the complete Hufu current-authority,
Cedar, evidence and exact-operation-start boundaries. Preserve Hufu's authority
layers and deny unless every applicable component permits.

| Proposal section | Explicit amendment and reason |
| --- | --- |
| 2–3, 23–25, 34–36 | VerifyAsync authorizes/preflights; it does not execute the effect. The broker orders current checks with durable start. AlreadyStarted cannot redispatch. Required start evidence precedes dispatch. |
| 4–8, 10, 31 | Bind tenant, realm, run, fence, audience, grant version and layer as well as the original fields. Revisions use exact strings to match Hufu; absent host workflow/activity bindings reject use. A single token represents one grant version, not a cross-product of multiple grants. |
| 5–8 | Use the closed five-action Hufu mapping; PatchFile maps to fs.patch, not generic fs.write. Capability sets may repeat only within the same grant. Existing layered authority is not translated into a flat union. |
| 6–8 | Pin authorizer policy to authority-origin/authorizer facts and separate their namespaces. Predicate names are not proof of origin. Authorizer policies never trust previous blocks. |
| 7 | Reuse the existing workspace-relative resource pair and trusted provider identity. The illustrative file:/ URI is not a new canonicalization algorithm. scope_contains receives scope, workspace and path. |
| 9–11, 23 | A subject/workflow/activity-bound credential cannot change those identities through attenuation. Same-context restriction is supported; cross-context delegation needs a separate authorized issuance/lineage profile. Biscuit proves signed bearer authority, not presenter authentication. |
| 13–17 | Envelope RootKeyId is a realm-scoped bounded hint, checked against a signed hufu_root_key fact. Preview.1 cannot set the protobuf root key ID. Signing-key leases coordinate Build and rotation/disposal. At-rest storage does not imply non-exportable KMS/HSM signing support. |
| 18–20, 31–32 | Defensively snapshot bytes; ReadOnlyMemory is not ownership. Preserve received bytes and their recomputed fingerprint. This closed transport requires canonical bytes and authenticated registration; valid upstream framing variants are not accepted implicitly. |
| 9, 18–21, 39–41 | Admit only bounded issuer/adapter-generated registered logic and lineage. Choose 64 KiB token/source, 32 blocks and 256 checks. Unregistered/offline arbitrary attenuation waits for another validated profile, rather than an ad hoc textual AST parser. |
| 21–25, 35–36 | Block new starts ordered after acknowledged revocation/key retirement; already-started operations can complete. Per-block revocation must participate in the same qualified start order. Sequential checks do not establish this. |
| 26–30 | Budget classification requires a typed reason. Preview.1 conflates runtime errors under evaluation_failure, so the adapter must return AuthorizationFailure rather than guessing from prose. Reject invalid/fractional-millisecond configuration; do not promise native cancellation. |
| 3, 33, 37–38 | Mandatory issuance, authorization and start evidence must succeed before release/dispatch. Restricted credential retention is separate from telemetry. Failed outcome persistence requires reconciliation, not duplicate execution. |

These choices are explicit amendments to an unpublished integration design.
They do not retrofit transport rejection into BiscuitToken.Parse, reinterpret
upstream Datalog, or extend BiscuitSharp with Hufu policy/storage responsibilities.

## Consequences

The first profile is intentionally online and requires trusted registration and
ordered Hufu enforcement. Its scope is narrower than arbitrary offline bearer
delegation. Unknown mappings fail closed; no adapter implementation may silently
relax a documented constraint.

The published preview.1 wrapper supports the core operations. Development
preview.2 now implements precise typed budget reasons; its qualification is
tracked in verification.md. Hufu host execution bindings, registration/revocation
persistence, safe Datalog construction and start-gate integration remain
implementation prerequisites.
The [handoff](../hufu-integration-handoff.md) separates those tasks from wrapper
preview graduation. No production integration or non-exportable signing support
is claimed by this documentation decision.
