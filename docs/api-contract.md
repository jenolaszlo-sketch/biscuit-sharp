# API contract

Contract for the M1 functional surface (keys, tokens, authorization live).
This document is the compatibility baseline enforced by package validation and
integration tests from the first preview onward.

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
- Policies apply first-match-wins in the order supplied: place `deny` policies
  before the `allow` policies they must override. The result reports matched
  policy indices and structured errors (`failed_check` with block/check/rule,
  `allow_policy_matched`, `deny_policy_matched`, `no_matching_policy`,
  `invalid_block_rule`, `evaluation_failure`). Malformed facts/policies/checks
  throw `BiscuitDatalogException` — no evaluation ran — while a completed
  evaluation always returns a result. Upstream default execution limits apply;
  no ambient time fact is injected.

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
- `BiscuitToken` is immutable and thread-safe; `BiscuitTokenBuilder` and
  `BiscuitAuthorizer` are mutable single-threaded accumulators (one instance
  per request; `Authorize()` itself may be called repeatedly and concurrently).
  Authorization calls are synchronous. Each `Build()` mints fresh ephemeral
  chain keys, so rebuilding from the same builder yields distinct but equally
  valid tokens — compare behavior, not bytes, across issuances.
- `BiscuitPublicKey` and `BiscuitRevocationId` implement value equality over
  their bytes (not array identity), so keys and revocation ids from separate
  constructions compare equal and can key dictionaries/sets — required for
  revocation lookups keyed by ids.
- `BiscuitPrivateKey.Export()` returns PKCS#8 DER; `ExportPem()` returns the
  armored PEM form; both are secret material (do not log). `Import()` accepts
  either.
- `BiscuitAuthorizer.AddTimeFact(DateTimeOffset)` adds an explicit RFC 3339
  `time(...)` fact so expiration checks evaluate deterministically; time is
  never injected implicitly.
- `BiscuitPrivateKey` is an opaque native handle: `IDisposable`, `ToString()`
  never reveals secrets, disposal zeroes/frees native material as far as the
  implementation permits (documented honestly: no protection against dumps or a
  compromised host). Explicit `Export()` is permitted; normal issuance should not
  repeatedly copy private keys through managed memory. `Export()` emits PKCS#8
  DER; `Import()` accepts PKCS#8 PEM (detected by armor) or DER with upstream
  algorithm auto-detection, and rejects ambiguous raw secrets. Key-operation
  failures surface as `BiscuitKeyException`; only native/ABI/transport problems
  surface as `BiscuitBridgeException`.
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
