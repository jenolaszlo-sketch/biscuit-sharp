# Changelog

## Unreleased (scaffolding)

- Establish the repository scaffolding: solution, managed API shape (keys, token,
  builder, attenuation, seal, authorizer, revocation IDs, inspection, version info),
  native ABI 1 skeleton, docs, and roadmap. No native behavior is implemented or
  qualified; every operation fails closed with `BiscuitBridgeException` until M1.
- Pin the M0 baseline: biscuit-auth 6.0.0 (tag verified equal to the pinned
  commit, crate checksum recorded), `Cargo.lock` committed (94 packages, locked
  fetch green on Rust 1.89.0), schema versions 3..6 / Datalog 3.3 recorded.
  Local `cargo check` awaits an MSVC linker install (VS C++ workload).
