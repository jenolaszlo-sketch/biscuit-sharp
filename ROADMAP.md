# BiscuitSharp roadmap

Updated 2026-10-02. M0 baseline pinned and verified; M1 functional surface
(keys, tokens, authorization) implemented and tested; the M2 distribution
matrix (three RIDs, NativeAOT, packaging, six clean consumers, Valgrind) is
green in CI. Remaining: pre-publish review, then the first preview publication.
See [verification evidence](docs/verification.md) for executed results.

## M0: design and baseline

- [x] Establish an independent wrapper boundary and ADR 0001.
- [x] Define ABI 1, supported algorithms (Ed25519, P-256), and the managed API shape.
- [x] Pin biscuit-auth 6.0.0 at commit `0f0b4e0e6fe07220c1ba6b51bff21d450d94a975` (= upstream tag `biscuit-auth-6.0.0`), commit `Cargo.lock` (94 packages, locked fetch green on Rust 1.89.0), record the token/spec version (schema 3..6, Datalog 3.3), and inventory licenses/notices (upstream + transitive notices complete with M2 staging).
- [x] Confirm Datalog 3.3 syntax scope, PEM/DER key encodings, and the staged feature set against the pinned source (done via implementation, differential tests, and staged manifests).

## M1: first usable wrapper

- [x] Implement the native bridge: key generate/import/export/destroy, token create/parse-verify/attenuate/seal, authorize, revocation IDs, inspection, version identity (serialize unneeded: managed tokens hold canonical bytes).
- [x] Implement the managed surface: keys, builder (textual + parameterized Datalog), token, attenuation, seal, authorizer, revocation IDs, inspection, version info, exception taxonomy.
- [x] Add native differential tests (bridge vs direct Rust) and managed integration tests, including attenuation-narrows, seal, tamper/wrong-root/truncation, malformed Datalog, Unicode, invalid UTF-8, oversized inputs, concurrency, panic containment, and bidirectional compat.
- [x] Add bidirectional compatibility tests: Rust↔BiscuitSharp issue/verify/attenuate plus committed deterministic fixtures (`eng/Test-Compat.ps1`, all qualified RIDs in CI).
- [x] Compile and run the .NET 8 / .NET 10 solution with the real native asset on every qualified RID in CI.

## M2: distribution gate

- [x] Build native assets for win-x64, linux-x64, osx-arm64 with `cargo --locked` and explicit target triples; stage manifests, hashes, licenses.
- [x] Run .NET 8/10 tests, NativeAOT publish + execute per RID, package assembly + archive verification, and six clean packaged consumers (3 RIDs × 2 TFMs).
- [ ] Publish a preview only after the full matrix passes. Packaging stays opt-in until then.

## M2.5: graduation hardening

- [x] Deterministic mutation/fuzz over tokens, Datalog, bridge envelopes, key encodings, and native responses; prove no panic crosses the ABI and no malformed input becomes success/authorized (dev + release profiles).
- [x] Linux Valgrind native-memory cycles over issue/parse/attenuate/authorize/dispose; record limitations honestly (zero definite leaks; possible-loss recorded by the job).
- [ ] Telemetry/privacy tests, strict malformed-input tests, public API contract + package validation baseline (blocked on first preview publication), SourceLink/symbols (symbols package built; SourceLink configured), upstream compatibility fixtures.
- [ ] Rerun the complete release matrix.

## M3: consumers and later

- [ ] Integrate separately into `Penghou.Hufu.Biscuit` (Hufu owns envelopes, grants, revocation store, workflow revisions, approval). Hufu integration is not a condition of wrapper correctness.
- [ ] Later, only on consumer need: third-party blocks, authorizer/token snapshots, performance caching, additional RIDs.

Third-party blocks and snapshots are explicitly deferred from 1.0 unless a concrete
Hufu use case requires them.
