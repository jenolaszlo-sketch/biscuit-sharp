# API contract

Contract for the M1 functional surface (keys, tokens, authorization live).
This document is the compatibility baseline enforced by package validation and
integration tests from the first preview onward.

## Typed evaluation failures — development preview.2

Authorization findings expose nullable
BiscuitAuthorizationError.EvaluationFailureReason. The native engine supplies
the reason for evaluation_failure; display messages are diagnostic prose, never
a classification API.

| BiscuitEvaluationFailureReason | Meaning |
| --- | --- |
| FactLimitExceeded | Datalog fact ceiling exceeded |
| IterationLimitExceeded | Rule-application iteration ceiling exceeded |
| TimeLimitExceeded | Datalog execution time ceiling exceeded |
| ExpressionError | Runtime expression error, including division by zero |
| UnexpectedQueryResult | Upstream query result cardinality mismatch |
| Other | Another or unrecognized future runtime failure |

Only the first three values identify budget exhaustion. Failed checks, explicit
denies, no matching policy and invalid block rules have no evaluation reason.
An absent/null reason remains null and must not be guessed from the message;
unknown nonempty strings map to Other. Malformed types, empty reasons or reasons
attached to a different error code throw BiscuitBridgeException. No failure
becomes an authorized result.

For example, a consumer can classify the result using typed findings:

~~~csharp
bool budgetExceeded = result.Errors.Any(error =>
    error.Code == "evaluation_failure" &&
    error.EvaluationFailureReason is
        BiscuitEvaluationFailureReason.FactLimitExceeded or
        BiscuitEvaluationFailureReason.IterationLimitExceeded or
        BiscuitEvaluationFailureReason.TimeLimitExceeded);
~~~

This feature is in the development preview.2 candidate; the published preview.1
does not expose it. Limits, rounding, ordinary Deny behavior and exception
taxonomy are unchanged.


## Result semantics

- `BiscuitToken.Parse(token, root)` verifies the cryptographic token against the
  root public key. Success means a valid token, not an authorized request.
- `ToBytes()` / `ToBase64Url()` return the canonical serialization produced by
  the upstream parser. Upstream parsing tolerates certain trailing framing
  bytes: such input can verify, while reserialization drops the trailing bytes.
  These methods need not reproduce the exact input sequence. Retain raw
  transport bytes separately when an enclosing protocol signs or audits them.
- `Attenuate(block)` returns a new token with strictly ≤ parent authority.
- `Seal()` returns a token that cannot be further attenuated; appending to a
  sealed token throws `BiscuitSealedTokenException`, never silently new credentials.
- `GetRevocationIds()` exposes per-block revocation identifiers; no implicit store.
- `Inspect()` reports block count, sealed state, signature/root-key algorithms,
  revocation IDs, printed block source, token size, and format/spec version where
  available. Never private signing material. Malformed inspection responses,
  including managed numeric overflow and inconsistent block-related array
  lengths, throw BiscuitBridgeException.
- `BiscuitAuthorizationResult.IsAuthorized` is true only for Allow with zero
  errors. `RequireAuthorized()` throws `BiscuitAuthorizationException` (carrying
  the result) otherwise. An ordinary Deny is a result, not a bridge failure.
- `BiscuitTokenBuilder` supports facts, rules, and checks (each textual, plus
  parameterized facts); the authorizer supports facts, rules, checks, policies,
  explicit time facts, and explicit execution limits (`WithLimits`). The
  default budget is a robust 100,000 facts / 100,000 iterations / 5 s,
  deliberately larger than upstream's `RunLimits::default()` (1 ms), which is
  too small to be reliable under load; pass
  `BiscuitAuthorizerLimits.UpstreamDefault` (1,000 facts / 100 iterations /
  1 ms) for strict parity. Five seconds is
  an evaluation budget, not a request deadline, memory cap, or cancellation
  mechanism. `WithLimits` truncates fractional milliseconds; values below 1 ms
  become zero. A breached limit denies with `evaluation_failure`. Malformed
  Datalog throws `BiscuitDatalogException` before evaluation; completed
  evaluations return a result.
- Policies apply first-match-wins in the order supplied: place `deny` policies
  before the `allow` policies they must override. The result reports matched
  policy indices and structured errors (`failed_check` with block/check/rule,
  `allow_policy_matched`, `deny_policy_matched`, `no_matching_policy`,
  `invalid_block_rule`, `evaluation_failure`). Malformed facts/policies/checks
  throw `BiscuitDatalogException` before evaluation; a completed evaluation
  returns a result. No ambient time fact is injected.

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

- BiscuitParam is a sealed immutable factory-created class. Str/Int/Bool/Bytes
  are its only construction paths; Bytes snapshots its input. Parameters use
  reference equality; no record inheritance or structural equality is promised.
- All public inputs are non-null; null throws `ArgumentNullException`.
- `BiscuitToken` is immutable and thread-safe. `BiscuitTokenBuilder` and
  `BiscuitAuthorizer` are mutable accumulators; do not mutate them concurrently.
  Concurrent `Authorize()` calls are supported only after configuration is
  complete and while no thread mutates that authorizer. Authorization calls are
  synchronous. Each `Build()` mints fresh ephemeral chain keys, so rebuilding
  from the same builder yields equally valid tokens with different serialized
  bytes and revocation IDs. Compare behavior, not bytes or IDs, across builds.
- `BiscuitPublicKey` and `BiscuitRevocationId` implement value equality over
  their bytes. Constructors copy supplied arrays; `Encoded` and `Value` return
  copies, so callers cannot mutate equality or hash identity through input or
  output arrays.
- Datalog syntax supplied through builder/authorizer methods is validated when
  the native operation executes (for example, `Build`, `Attenuate`, or
  `Authorize`), not when `Create` or an `Add*` method stores the source. Managed
  argument and parameter validation can still fail when an `Add*` method is
  called.
- `BiscuitPrivateKey.Export()` returns PKCS#8 DER; `ExportPem()` returns the
  armored PEM form; both are secret material (do not log). `Import()` accepts
  either.
- `BiscuitAuthorizer.AddTimeFact(DateTimeOffset)` adds an explicit RFC 3339
  `time(...)` fact so expiration checks evaluate deterministically; time is
  never injected implicitly.
- `BiscuitPrivateKey` is an opaque native handle: `IDisposable`, `ToString()`
  never reveals secrets, disposal zeroes/frees native material as far as the
  implementation permits (documented honestly: no protection against dumps or a
  compromised host). Concurrent disposal and key operations require caller
  coordination. Explicit `Export()` is permitted; normal issuance should not
  repeatedly copy private keys through managed memory. `Export()` emits PKCS#8
  DER; `ExportPem()` the armored PEM form; `Import()` accepts PKCS#8 PEM (detected by armor) or DER with upstream
  algorithm auto-detection, and rejects ambiguous raw secrets. `BiscuitPublicKey.Parse`
  validates raw public bytes for an algorithm without needing the private half
  (validation is upstream decode: size plus successful decode);
  `ParseHex`/`ParsePrefixed` accept the hex and `ed25519/<hex>` display forms,
  and `ToPrefixedString()` renders them (public keys are safe to log).
  Key-operation failures surface as `BiscuitKeyException`; only native/ABI/transport problems
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
  published and executed against staging per RID; CI also publishes/runs clean
  package-reference NativeAOT consumers on net10 for each RID (not just the
  analyzer).
- Package validation checks development packages against the published
  0.1.0-preview.1 binary baseline on both target frameworks.

## Checked surface inventory

[public-api.txt](public-api.txt) records exported types, constructors, public and externally accessible protected
members, operators, nullable signatures, optional defaults, record init accessors, generic
constraints and trimming annotations. CI runs BiscuitSharp.ApiSurface on net8.0
and net10.0 and fails on drift. Regenerate with --write only when the new surface
has been reviewed. The tool records selected contract attributes (Obsolete, RequiresUnreferencedCode
and RequiresDynamicCode), not every CLR attribute. This readable inventory is a
readable surface gate; package validation
against published 0.1.0-preview.1 is the binary compatibility gate.
