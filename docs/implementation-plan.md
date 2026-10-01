# Implementation plan

Current delivery plan for BiscuitSharp. Status 2026-10-01: M0 complete, M1 functional
surface and bidirectional gate green on Windows x64; M2 distribution and M2.5
hardening open. For release status see [verification](verification.md); for scope see
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

`version` (done 2026-10-01, with `{"code","message"}` error envelope and 6
native boundary tests), key `generate`/`import`/`export`/`destroy` over opaque
native handles (done 2026-10-01: Ed25519 + Secp256r1, PEM/DER with upstream
auto-detection, DER export for round-trips, 6 native tests incl. differential
vs direct upstream and concurrency), token `create`/`parse_verify`/`attenuate`/
`seal`/`revocation_ids`/`inspect` over verified bytes (done 2026-10-01: upstream
`code`/`code_with_params` with typed str/int/bool/bytes params, canonical
round-trips, `sealed_token`/`signature_error`/`format_error`/`datalog_error`
codes, 14 native tests incl. tamper/truncation and seal semantics),
`token_authorize` (done 2026-10-01: ambient facts/checks/policies via upstream
`code`, allow/deny answers with matched-policy indices and structured
failed-check/policy errors, first-match-wins order, 8 native tests incl.
allow-matched-but-check-failed and explicit deny).
Consolidation is allowed; managed callers must not depend on
Rust ABI details. Private keys live in opaque native handles; export is explicit.

### Managed surface (`src/BiscuitSharp/`)

Keys (generate/import/export over opaque handles with finalizer-backed disposal,
DER export, PEM/DER import, `ToString` privacy — done 2026-10-01, 21 managed
key checks × 2 TFMs), builder
(textual Datalog + parameterized overloads; discourage untrusted interpolation),
token (parse/verify, base64url, attenuate, seal, revocation IDs, inspection —
done 2026-10-01: canonical bytes, equality, typed params incl. reflection
convenience overload, 38 managed token checks × 2 TFMs),
authorizer (facts, policies, checks → `BiscuitAuthorizationResult` with
matched-policy indices, structured errors, `RequireAuthorized()` — done
2026-10-01, 23 managed checks × 2 TFMs), version discovery reporting the loaded asset (done 2026-10-01: process-lifetime
loader, strict JSON decoding, file-hash identity, 17 managed checks × 2 TFMs), and the
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

Cross-implementation compatibility — done 2026-10-01 via `eng/Test-Compat.ps1`
(unit → compat_gen → committed fixtures → managed generate → compat_consume →
managed consume), proving all four directions: Rust-issues→bridge-verifies/
authorizes; bridge-issues→direct-Rust-verifies/authorizes;
bridge-attenuates-Rust-token→direct-Rust-verifies; direct-Rust-attenuates-
bridge-token→bridge-verifies/authorizes. Committed `fixtures/compat/genesis.json`
(fixed seed) is verified semantically. Finding: token bytes are never
byte-stable across runs by design — issuance mints a fresh ephemeral next-key
per token (`build` → `build_with_rng`), so fixtures pin seed-derived keys
exactly and verify tokens structurally. Goal met: BiscuitSharp creates Biscuit
tokens, not just self-readable ones.

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
  Done 2026-10-01 on Windows x64 dev profile (`src/adversarial.rs`, runs inside
  `cargo test --lib`): 4,096 cases — rejected 4074, proved-legitimate 19,
  denies 0, allows 3 (all byte-identical canonical content), panics 0 — plus a
  managed 96-position mutation sweep and 7 invalid policies (fail-fast or deny,
  never allow) and a native panic-containment test (a real Rust panic surfaces
  as `STATUS_PANIC` with an empty body; no unwind crosses the ABI). Findings pinned: upstream framing tolerates trailing bytes
  (parse canonicalizes; authorization requires byte-identical content); DER
  seed-region mutations yield different valid keys (import success requires a
  usable, destroyable handle). Release-profile + all-RID repetition stays open
  with the M2 matrix.
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
