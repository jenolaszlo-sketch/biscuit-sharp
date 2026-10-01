# API contract

Scaffolding contract for the M1 surface. Until M1 lands, every operation throws
`BiscuitBridgeException`. After M1, this document is the compatibility baseline
enforced by package validation and integration tests.

## Result semantics

- `BiscuitToken.Parse(token, root)` verifies the cryptographic token against the
  root public key. Success means a valid token, not an authorized request.
- `ToBytes()` / `ToBase64Url()` round-trip the serialized token.
- `Attenuate(block)` returns a new token with strictly ≤ parent authority.
- `Seal()` returns a token that cannot be further attenuated; appending to a
  sealed token throws `BiscuitSealedTokenException`, never silently new credentials.
- `GetRevocationIds()` exposes per-block revocation identifiers; no implicit store.
- `Inspect()` reports block count, sealed state, signature/root-key algorithms,
  revocation IDs, printed block source, token size, and format/spec version where
  available. Never private signing material.
- `BiscuitAuthorizationResult.IsAuthorized` is true only for Allow with zero
  errors. `RequireAuthorized()` throws `BiscuitAuthorizationException` (carrying
  the result) otherwise. An ordinary Deny is a result, not a bridge failure.

## Exception semantics

```
BiscuitException
├── BiscuitBridgeException      native loading, ABI mismatch, panic status,
│                               transport/buffer corruption, response decoding
├── BiscuitTokenException       token validity / cryptographic problems
│   ├── BiscuitSignatureException
│   ├── BiscuitFormatException
│   └── BiscuitSealedTokenException
├── BiscuitDatalogException     Datalog parse/evaluation failure (not a decision)
├── BiscuitAuthorizationException  RequireAuthorized() enforcement (carries result)
└── BiscuitKeyException         key generate/import/export failure
```

No parse, verification, native, or serialization failure may become an
authorization success. Failed checks, parsing problems, token verification
failures, and ordinary denial are never flattened into one error.

## Nullability, ownership, lifetime

- All public inputs are non-null; null throws `ArgumentNullException`.
- `BiscuitToken` is immutable and thread-safe. Authorization calls are synchronous.
- `BiscuitPrivateKey` is an opaque native handle: `IDisposable`, `ToString()`
  never reveals secrets, disposal zeroes/frees native material as far as the
  implementation permits (documented honestly: no protection against dumps or a
  compromised host). Explicit `Export()` is permitted; normal issuance should not
  repeatedly copy private keys through managed memory.
- Serialization compatibility: tokens are upstream Biscuit artifacts, verifiable
  by any conforming implementation, not just this wrapper.

## Compatibility

- Entry point note: the specification sketch wrote `BiscuitSharp.GetVersion()`.
  A static class named `BiscuitSharp` inside namespace `BiscuitSharp` creates a
  C# name-resolution ambiguity at every call site, so the entry point is
  `BiscuitEngine.GetVersion()` returning `BiscuitSharpVersionInfo`. Same
  semantics, usable API.
- Target frameworks: `net8.0`, `net10.0`. Qualified RIDs: win-x64, linux-x64,
  osx-arm64. Trimming and NativeAOT analyzers are clean; a NativeAOT sample is
  published, executed, and verified per RID in CI (packaged asset, not just the
  analyzer).
- Package validation runs against the latest published preview baseline once M2
  ships the first preview.
