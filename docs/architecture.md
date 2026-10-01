# BiscuitSharp architecture

Scaffolding architecture for the M0 baseline. See [ADR 0001](decisions/0001-native-wrapper-boundary.md) for the binding decision, the [implementation plan](implementation-plan.md) for delivery order, and [verification](verification.md) for executed evidence (currently none).

## Layers

The managed library calls our native bridge in process. The bridge delegates to
the pinned official Biscuit engine. No network service is required for issuance,
verification, attenuation, or authorization.

| Layer | Owns |
| --- | --- |
| Managed API | Typed keys/tokens/authorizer, validated marshalling, deterministic lifetime, version discovery |
| Interop | ABI declarations, loader, byte buffers, native result ownership |
| Rust bridge | C-compatible exports, safe input decoding, structured boundary errors, delegation to biscuit-auth |
| Official biscuit-auth | Key handling, token format, Datalog 3.3 semantics, signature verification, authorization |
| Application (e.g. Hufu) | Grant policy, revocation storage, workflow revisions, approval, enforcement, auditing |

```
.NET application
      ↓
  BiscuitSharp
      ↓
versioned native bridge (ABI 1)
      ↓
 biscuit-auth 6.0.0 (Rust)
```

Hufu sits above, next to CedarSharp:

```
CedarSharp             BiscuitSharp
    │                       │
    └──────── Hufu ─────────┘
```

Cedar answers policy questions. BiscuitSharp handles portable and attenuable
authority credentials. Hufu owns the workflow authority model.

## Ownership

BiscuitSharp owns: key handling, token creation, parsing/verification,
attenuation, sealing, authorization, serialization, revocation identifiers,
token inspection, native interoperability.

BiscuitSharp does NOT own: workflow authority, grant issuance policy, revocation
storage, workflow revisions, human approval, delegation policy, Hufu authority
envelopes. Those belong to Hufu. The eventual Hufu adapter (`Penghou.Hufu.Biscuit`)
maps Hufu grants into Biscuit facts/checks; that mapping must not live here.

## Semantics and errors

Biscuit is designed around decentralized verification, offline attenuation, and
capability-style authorization. Attenuation only narrows authority; modifying or
removing prior blocks invalidates the cryptographic chain. Sealing forbids further
attenuation. Revocation intentionally requires external state: this library exposes
revocation identifiers and implements no implicit global revocation store.

These states stay distinct: valid token, invalid token, authorized request,
denied request, authorization evaluation failure. A successfully parsed token does
not mean a request is authorized. No parse, verification, native, serialization,
or transport failure may become an authorization success. An ordinary Deny is a
result, never a bridge failure.

## API scope

The generic library supports Biscuit's textual Datalog syntax (it is not
Hufu-specific); Hufu layers typed builders above it later. Parameterized builder
APIs are preferred over interpolating untrusted strings into Biscuit source.
Third-party blocks and authorizer/token snapshots are deferred from 1.0 unless a
concrete consumer use case requires them; the architecture leaves room for
third-party request/signature/append/trusted-key operations.

Calls are synchronous and instances are thread-safe. The token type is immutable;
private keys are opaque native handles with explicit export and disposal that
zeroes native material as far as the implementation permits. The loader resolves
verified assets under `AppContext.BaseDirectory` (`runtimes/<rid>/native/`) or a
verified `BISCUITSHARP_NATIVE_PATH` override; it never downloads or PATH-probes.

## References

- [Biscuit website and docs](https://www.biscuitsec.org/)
- [biscuit-auth 6.0.0 release](https://github.com/eclipse-biscuit/biscuit-rust/releases/tag/biscuit-auth-6.0.0) (Datalog 3.3, P-256, PEM/DER)
- Upstream repository: https://github.com/eclipse-biscuit/biscuit-rust

The [API contract](api-contract.md) describes what BiscuitSharp exposes.
