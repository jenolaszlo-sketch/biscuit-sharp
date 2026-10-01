# Tests

`BiscuitSharp.Tests` is a dependency-free console runner (mirroring the early
CedarSharp shape): exit code 0 means all checks passed.

- Scaffolding: asserts every operation fails closed with `BiscuitBridgeException`
  and checks native filenames per RID.
- M1 replaces these with the native/managed matrices in
  `docs/implementation-plan.md` (differential, bidirectional compatibility,
  tamper, seal, privacy, loader, concurrency).
