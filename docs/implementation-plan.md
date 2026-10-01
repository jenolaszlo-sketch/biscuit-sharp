# Implementation plan

Current delivery plan for BiscuitSharp. Status: scaffolding (M0, 2026-10-01).
For release status see [verification](verification.md); for scope see
[architecture](architecture.md), [native boundary](native-boundary.md),
[API contract](api-contract.md), and [security](security.md).

Baseline: `biscuit-auth =6.0.0` at commit `0f0b4e0e6fe07220c1ba6b51bff21d450d94a975`,
bridge 0.1.0, ABI 1, Rust 1.89.0 (confirm MSRV against upstream CI during M0),
Datalog 3.3, Ed25519 + P-256, `net8.0` + `net10.0`,
RIDs win-x64, linux-x64, osx-arm64. Initial consumer: Penghou.Hufu
(via a separate `Penghou.Hufu.Biscuit` adapter; not a correctness condition).

## M0: design and baseline

1. Establish the wrapper boundary ([ADR 0001](decisions/0001-native-wrapper-boundary.md)):
   BiscuitSharp owns keys, issuance, verification, attenuation, sealing,
   authorization, serialization, revocation IDs, inspection, interop. Hufu owns
   workflow authority, grants, revocation storage, revisions, approval, envelopes.
2. Pin the baseline: exact `biscuit-auth` version + source commit, committed
   `Cargo.lock`, pinned Rust toolchain, recorded token/spec version, bridge 0.1.0,
   ABI 1. Status 2026-10-01: `cargo generate-lockfile` + `cargo fetch --locked`
   green on Rust 1.89.0 (94 packages); tag `biscuit-auth-6.0.0` verified equal to
   the pinned commit; crate checksum and schema versions recorded in
   `docs/native-boundary.md`. The published crate declares no `rust-version`, so
   1.89.0 is the bridge's own frozen choice. Status 2026-10-01 (evening):
   `cargo check --locked` and `cargo build --locked` green on MSVC after the
   VS C++ workload install; `dumpbin` confirms the three ABI exports
   (see verification ledger).
3. Confirm the toolchain against upstream MSRV/CI; record crate checksums and the
   staged feature set (PEM/DER, P-256 per 6.0.0). Toolchain confirmed working
   (rustup auto-installs 1.89.0 from `rust-toolchain.toml`); full checksum +
   feature inventory completes with the M2 staging gate.
4. Inventory licenses/notices into `native/legal/` + `NOTICE`.
5. Define the managed API shape (done in scaffolding: `BiscuitPrivateKey`,
   `BiscuitPublicKey`, `BiscuitToken`, `BiscuitTokenBuilder`, `BiscuitBlock`,
   `BiscuitAuthorizer`, `BiscuitAuthorizationResult`, `BiscuitRevocationId`,
   `BiscuitInspection`, `BiscuitSharp.GetVersion()`, exception taxonomy).

Exit: `cargo fetch --locked` succeeds, lockfile committed, identity recorded in
`native-boundary.md`, scaffolding builds on .NET 8/10.

## M1: first usable wrapper

### Native bridge (`native/src/lib.rs`)

Implement ABI 1 operations over `biscuit-auth` 6.0.0 with pointer+length inputs,
native-owned outputs + single free, caught panics, bounded I/O, version identity:

`version`, `key_generate`, `key_import`, `key_export_public`,
`key_export_private`, `token_create`, `token_parse_verify`, `token_serialize`,
`token_attenuate`, `token_seal`, `token_authorize`, `token_revocation_ids`,
`token_inspect`. Consolidation is allowed; managed callers must not depend on
Rust ABI details. Private keys live in opaque native handles; export is explicit.

### Managed surface (`src/BiscuitSharp/`)

Keys (generate/import/export, disposal zeroing, `ToString` privacy), builder
(textual Datalog + parameterized overloads; discourage untrusted interpolation),
token (parse/verify, base64url, attenuate, seal, revocation IDs, inspection),
authorizer (facts, policies, checks → `BiscuitAuthorizationResult` with
`RequireAuthorized()`), version discovery reporting the loaded asset, and the
`BiscuitException` taxonomy with boundary rules (bridge ≠ token ≠ Datalog ≠
enforcement failures; Deny is a result).

### Test matrices

Native tests (real asset, each qualified RID): key generation; Ed25519 and P-256
issuance; key round-trips; issuance; serialization round-trips; wrong root key;
tampered/truncated tokens; attenuation + multiple blocks; sealing +
append-after-seal failure; allow/deny; failed checks; revocation IDs; malformed
Datalog; Unicode; invalid UTF-8 at the bridge; oversized inputs; concurrent
calls; panic containment. Strongest tests run the same scenario through direct
Rust vs the bridge and compare semantic results.

Cross-implementation compatibility (required — the token is a portable
artifact): Rust-issues→BiscuitSharp-verifies/authorizes; BiscuitSharp-issues→
Rust-verifies/authorizes; BiscuitSharp-attenuates-Rust-token→Rust-verifies;
Rust-attenuates-BiscuitSharp-token→BiscuitSharp-verifies; plus spec/sample-token
fixtures. Goal: BiscuitSharp creates Biscuit tokens, not just self-readable ones.

Managed tests (.NET 8 + .NET 10, real asset, each RID): immutability,
nullability, exception contracts, base64url, binary round-trips, concurrency,
repeated authorization, equality/fingerprints if exposed, loader behavior
(missing/tampered asset, wrong ABI/version/manifest, relative override),
diagnostic privacy. Mock-only native tests are insufficient.

Exit: full solution green on .NET 8/10 with the real asset; attenuation-narrows,
seal, tamper, and privacy proofs recorded in `verification.md`.

## M2: distribution gate

1. Build native assets with `cargo --locked` and explicit triples for win-x64,
   linux-x64, osx-arm64; stage `biscuitsharp-native.json` manifests, SHA-256
   hashes, licenses per RID (`eng/Build-Native.ps1`, `eng/Verify-NativeStaging.ps1`).
2. Run .NET 8/10 suites, NativeAOT publish + execute per RID
   (`samples/BiscuitSharp.AotSmoke`), package assembly + archive verification
   (`eng/Verify-NuGetPackage.ps1`: RID inventory, filenames, hashes, ABI,
   upstream version/commit, lockfile identity, license inventory, no surprises).
3. Run six clean external consumers from an isolated NuGet cache (3 RIDs ×
   net8/net10: generate/import key, issue, serialize, parse/verify, attenuate,
   authorize, seal, revocation IDs) (`eng/Test-PackagedConsumer.ps1`).
4. Publish a preview only after the matrix passes. Packaging stays opt-in
   (`-p:BiscuitSharpEnablePack=true`) until then; publication is a separate
   manual action (`.github/workflows/publish.yml`).

Exit: preview published; `verification.md` holds the exact evidence.

## M2.5: graduation hardening

- Public API contract: result/exception/nullability/ownership/lifetime/thread-
  safety/immutability/disposal/serialization semantics frozen; package validation
  against the latest preview baseline.
- Mutation/fuzz: deterministic mutation of serialized tokens, Datalog source,
  bridge envelopes, key encodings, and structured native responses (thousands of
  cases per native run). Prove: no panic crosses the ABI, no malformed input
  becomes success, no malformed signature verifies, no malformed token authorizes.
- Leak instrumentation: Linux Valgrind (or equivalent) over repeated
  issue/parse/attenuate/authorize/dispose cycles; record limitations honestly.
- Diagnostics/privacy tests (safe defaults per `security.md`), strict
  malformed-input tests, SourceLink/symbols, upstream compatibility fixtures.
- Rerun the complete M2 release matrix.

## M3: consumers and later

Integrate separately into `Penghou.Hufu.Biscuit` (Hufu maps envelopes/grants
into Biscuit facts/checks and owns revocation/Cedar policy/revisions/deltas/
re-admission/approval). Later, only on concrete need: third-party blocks
(request/signature/append/trusted-key), snapshots, performance caching,
additional RIDs.

## Stable-release acceptance gate

1.0.0 ships only when: the exact NuGet native assets were built and exercised on
every advertised RID; every target passed a clean packaged consumer; NativeAOT
executed per RID; token semantics were compared against direct upstream Rust
bidirectionally; malformed/tampered inputs hardened; the public API contract
frozen with package/native identity independently verifiable.
