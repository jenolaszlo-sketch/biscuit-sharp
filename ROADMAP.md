# BiscuitSharp roadmap

Updated 2026-10-01. Scaffolding only: no native behavior is qualified yet.
See [verification evidence](docs/verification.md) for executed results (currently none).

## M0: design and baseline

- [x] Establish an independent wrapper boundary and ADR 0001.
- [x] Define ABI 1, supported algorithms (Ed25519, P-256), and the managed API shape.
- [x] Pin biscuit-auth 6.0.0 at commit `0f0b4e0e6fe07220c1ba6b51bff21d450d94a975` (= upstream tag `biscuit-auth-6.0.0`), commit `Cargo.lock` (94 packages, locked fetch green on Rust 1.89.0), record the token/spec version (schema 3..6, Datalog 3.3), and inventory licenses/notices (upstream + transitive notices complete with M2 staging).
- [ ] Confirm Datalog 3.3 syntax scope, PEM/DER key encodings, and the staged feature set against the pinned source (partially done: signature/schema version constants read from source; full API confirmation lands with M1).

## M1: first usable wrapper

- [ ] Implement the native bridge: key generate/import/export, token create/parse-verify/serialize/attenuate/seal, authorize, revocation IDs, inspection, version identity.
- [ ] Implement the managed surface: keys, builder (textual + parameterized Datalog), token, attenuation, seal, authorizer, revocation IDs, inspection, version info, exception taxonomy.
- [ ] Add native differential tests (bridge vs direct Rust) and managed integration tests, including attenuation-narrows, seal, tamper/wrong-root/truncation, malformed Datalog, Unicode, invalid UTF-8, oversized inputs, concurrency, panic containment.
- [ ] Add bidirectional compatibility tests: Rust↔BiscuitSharp issue/verify/attenuate plus spec/sample-token fixtures.
- [ ] Compile and run the .NET 8 / .NET 10 solution with the real native asset.

## M2: distribution gate

- [ ] Build native assets for win-x64, linux-x64, osx-arm64 with `cargo --locked` and explicit target triples; stage manifests, hashes, licenses.
- [ ] Run .NET 8/10 tests, NativeAOT publish + execute per RID, package assembly + archive verification, and six clean packaged consumers (3 RIDs × 2 TFMs).
- [ ] Publish a preview only after the full matrix passes. Packaging stays opt-in until then.

## M2.5: graduation hardening

- [ ] Deterministic mutation/fuzz over tokens, Datalog, bridge envelopes, key encodings, and native responses; prove no panic crosses the ABI and no malformed input becomes success/authorized.
- [ ] Linux Valgrind (or equivalent) native-memory cycles over issue/parse/attenuate/authorize/dispose; record limitations honestly.
- [ ] Telemetry/privacy tests, strict malformed-input tests, public API contract + package validation baseline, SourceLink/symbols, upstream compatibility fixtures.
- [ ] Rerun the complete release matrix.

## M3: consumers and later

- [ ] Integrate separately into `Penghou.Hufu.Biscuit` (Hufu owns envelopes, grants, revocation store, workflow revisions, approval). Hufu integration is not a condition of wrapper correctness.
- [ ] Later, only on consumer need: third-party blocks, authorizer/token snapshots, performance caching, additional RIDs.

Third-party blocks and snapshots are explicitly deferred from 1.0 unless a concrete
Hufu use case requires them.
