# ADR 0001: wrap the official Rust implementation

Status: accepted (M0 scaffolding).

## Context

BiscuitSharp needs Biscuit cryptography, token serialization, and Datalog 3.3
semantics in .NET, with portable tokens verifiable by any conforming
implementation. A managed reimplementation would risk semantic drift in security-
critical authorization credentials.

## Decision

Wrap the official Rust `biscuit-auth` implementation (6.0.0 baseline) behind a
small versioned `cdylib` bridge with a stable C ABI, and expose a small, safe,
idiomatic .NET API over it. Do not reimplement Biscuit cryptography, token
serialization, or Datalog semantics in C#.

## Consequences

- BiscuitSharp must pin the upstream source commit, `Cargo.lock`, Rust toolchain,
  token/spec version, and bridge ABI, and verify the loaded asset identity.
- The bridge owns FFI safety (owned buffers, single free, panic containment,
  bounds); the managed layer owns typing, lifetime, and error taxonomy.
- Upgrades follow `docs/upstream-upgrade.md` and re-run the full verification gate.
- Third-party blocks and snapshots stay deferred until a concrete consumer need.
