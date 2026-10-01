# ADR 0002: wrapper authorization budget and token normalization

Status: accepted for the preview contract under the maintainer-authorized graduation work on 2026-10-02. Hufu-specific workload qualification remains a consumer responsibility.
Date: 2026-10-01.

## Context

BiscuitSharp wraps upstream parsing and authorization. Two observable choices
need a stable contract because consumers such as Hufu may use these tokens and
budgets in their own protocols:

- Upstream `RunLimits::default()` is intentionally small (1,000 facts, 100
  iterations, 1 ms). The one-millisecond ceiling can produce `evaluation_failure`
  under ordinary scheduler load.
- Upstream token framing can accept trailing bytes. Parsing verifies the token
  and serializes its canonical form, which can differ from the supplied bytes.
- Every token build generates fresh ephemeral chain keys. Rebuilding equivalent
  source does not produce a stable token byte string or revocation identifiers.

## Decision

Keep a BiscuitSharp-specific default evaluation budget of 100,000 facts,
100,000 iterations, and 5 seconds. Keep `UpstreamDefault` as an explicit
strict-parity option. The five-second setting bounds upstream evaluation only;
it is not an end-to-end deadline, memory cap, or cancellation guarantee.
`WithLimits` converts `TimeSpan` to whole milliseconds by truncating fractional
milliseconds, including sub-millisecond values to zero.

Preserve upstream parse behavior. When upstream accepts trailing framing bytes,
`Parse` returns a verified token whose `ToBytes()` output is the canonical
upstream serialization; it need not equal the original transport bytes. Protocols
that sign, cache, or audit the raw input must retain those bytes separately or
choose the canonical serialization deliberately.

Continue generating fresh chain keys on every build. Token bytes and revocation
IDs identify a particular issuance; equivalent authorization behavior does not
imply byte or revocation-ID equality across builds.

## Consequences

- `Authorize` is safe for concurrent calls after configuration is complete, as
  long as the authorizer is not mutated concurrently.
- Callers must choose their budget explicitly when strict upstream parity is
  needed, and account for synchronous evaluation occupying a calling thread.
- Regression tests pin tolerated suffix normalization against the canonical
  original and verify equivalent authorization and revocation behavior.
- Synthetic release measurements are recorded in authorizer-budget.md. They
  exercise ordinary requests and hostile growth through the real bridge; they
  do not establish a universal Hufu latency/memory budget. Integration should
  set explicit limits after measuring its actual policies.