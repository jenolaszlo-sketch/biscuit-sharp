# Native ABI and distribution contract

Baseline (pinned 2026-10-01): biscuit-auth 6.0.0, bridge 0.1.0, ABI 1,
Rust 1.89.0 via `rust-toolchain.toml` (auto-installs through rustup).
Upstream source commit: `0f0b4e0e6fe07220c1ba6b51bff21d450d94a975` —
verified equal to upstream tag `biscuit-auth-6.0.0` via `git ls-remote`.
`Cargo.lock` is committed (94 packages, `cargo fetch --locked` green).
Do not follow floating Cargo versions.

Upstream identity details:
- biscuit-auth crate checksum (Cargo.lock):
  `d5884fc86b3e21f5649ef4326e17ef729b3096e6502deaf13db7b7fb05bb992b`.
- The published crate declares no `rust-version` (no upstream MSRV); the 1.89.0
  pin is the bridge's own choice and stays frozen until an upgrade re-runs the gate.
- Token/spec versions from the pinned source (`src/token/mod.rs`,
  `src/format/mod.rs`): `MIN_SCHEMA_VERSION = 3`, `MAX_SCHEMA_VERSION = 6`,
  `DATALOG_3_3_SIGNATURE_VERSION = 1`. The staged `biscuitsharp-native.json`
  (M2) records these alongside the ABI/bridge/toolchain identity.

## ABI 1

```c
typedef struct { uint8_t *data; size_t len; } BiscuitSharpBuffer;
uint32_t biscuitsharp_abi_version(void);
uint32_t biscuitsharp_call_v1(uint32_t operation, const uint8_t *input,
                              size_t input_len, BiscuitSharpBuffer *output);
void biscuitsharp_free_v1(BiscuitSharpBuffer buffer);
```

C calling convention; pointer-sized lengths; sequential two-field output layout.
The caller owns readable input for the call and a writable output struct. Null
input is valid only for length zero. UTF-8 must be valid where text applies.
Input limit 16 MiB, output limit 64 MiB. The limits bound bridge messages, not
intermediate allocations or evaluation time.

The bridge initializes output before processing. Nonempty output is one owned
Rust boxed byte slice with no terminator; the caller frees it exactly once using
the originating library and the unmodified pointer/length. A null/zero buffer
is safe to free. Foreign pointers, double-free, unreadable input, and invalid
output addresses violate the C contract and are not recoverable validation errors.

| Operation | Concept |
| --- | --- |
| 0 | version / loaded-asset identity (implemented: strict JSON payload with biscuit-auth version, schema min/max, Datalog marker, bridge/ABI versions, Rust version, target triple, enabled features, upstream commit, Cargo.lock SHA-256) |
| 1 | key_generate (implemented: `{"algorithm": "ed25519" \| "secp256r1"}` → handle + public half) |
| 2 | key_import (implemented: PEM string or base64 DER, algorithm auto-detected → handle + public half) |
| 3 | key_export_public (implemented: handle → algorithm + raw public bytes) |
| 4 | key_export_private (implemented: handle → base64 PKCS#8 DER) |
| 5 | token_create (implemented: root handle + `{source, params?}` facts/rules/checks through upstream `code`/`code_with_params` → canonical token) |
| 6 | token_parse_verify (implemented: verify against root → canonical token) |
| 7 | token_serialize (reserved; unneeded — managed tokens already hold canonical bytes) |
| 8 | token_attenuate (implemented: verify → append `{source, params?}` block; sealed append fails `sealed_token`) |
| 9 | token_seal (implemented: verify → seal; reseal fails `sealed_token`. Sealing flips the chain terminator, it does not append a block) |
| 10 | token_authorize (implemented: verify → ambient facts/rules/checks/policies through upstream `code` → allow/deny answer with structured errors; first-match-wins in policy order; robust default limits 100k facts / 100k iterations / 5 s unless overridden; no ambient time fact injected) |
| 11 | token_revocation_ids (implemented: verify → per-block ids) |
| 12 | token_inspect (implemented: verify → block count/sources/versions, seal probe, root key id, verified root algorithms, size; refuses tokens with more than 4,096 blocks to bound inspection output) |
| 13 | key_destroy (implemented: handle → drop native key; unknown handles error) |
| 14 | key_import_public (implemented: algorithm + raw public bytes → validated canonical public key; validation is upstream decode, lenient about non-canonical encodings) |

The exact ABI may consolidate operations, but managed callers must not depend on
Rust ABI details. No Rust-owned pointers reach the public .NET API. Recoverable
Rust unwinding is caught; abort, OOM abort, stack overflow, and memory faults are
not promised recoverable.

Upstream token failures map to stable codes: `sealed_token` (append/seal on
sealed), `signature_error`, `format_error`, `datalog_error` (upstream language
failures), `token_error` (catch-all), alongside `key_error` for key operations.
Status 0 means a complete Biscuit answer, including a Deny; denial is not a
boundary failure. Status 1 is invalid boundary input, 2 unsupported operation,
3 caught panic, 4 oversized output. Key operations reuse status 1 with distinct
codes: `invalid_input` for malformed requests/handles and `key_error` for
upstream key failures (bad encoding, undecodable key) so managed callers map
them to `BiscuitKeyException` instead of `BiscuitBridgeException`.
Every nonzero status except the panic path
also carries a JSON `{"code","message"}` body (`invalid_input`,
`unsupported_operation`, `oversized_output`); the panic path leaves the
pre-initialized empty buffer. Managed calls copy the output and free it in
`finally`, including response-decoding failure. Version decoding is strict:
every documented field is required. A lazy singleton verifies and retains
the native library for process lifetime so concurrent calls cannot race
unloading. Public APIs expose no raw pointers.

## Asset identity and distribution

| RID | Target | Native CI |
| --- | --- | --- |
| win-x64 | x86_64-pc-windows-msvc | Windows x64 |
| linux-x64 | x86_64-unknown-linux-gnu | Linux x64/glibc |
| osx-arm64 | aarch64-apple-darwin | macOS ARM64 |

Qualified environments are the CI runner environments for those RIDs with .NET 8
and .NET 10; see [verification](verification.md). No musl, osx-x64, win-arm64, or
linux-arm64 assets are selected until built and exercised. Older OS and glibc
baselines are not qualified.

Build with `cargo --locked` and explicit target triples. Each staged asset carries
`biscuitsharp-native.json`: ABI/bridge/toolchain, biscuit-auth version, token/spec
version, target/RID, binary SHA-256, source commit, lock/source hashes, features.
Packages carry upstream and transitive dependency license notices. When a
manifest sits next to the asset (staged and packaged layouts), the loader
verifies it: the file hash must match, and the live version identity reported
by the loaded binary (engine, bridge, ABI, commit, lockfile, target, RID, Rust,
features) must match the manifest field by field; any mismatch frees the
library and fails the load. Assets without an adjacent manifest (local
`target/debug` loop) keep the ABI-check-only behavior. The loader resolves the
asset under `AppContext.BaseDirectory` (package-adjacent or its
`runtimes/<rid>/native/` directory). `BISCUITSHARP_NATIVE_PATH` selects a
self-built or vendored asset; relative paths are normalized against the current
directory before the adjacent manifest is located, and the asset is still
hash- and identity-verified. There is no automatic download or global search.

Packaging is disabled by default (`BiscuitSharpEnablePack`). The normal opt-in
build requires all three RID manifests; CI additionally verifies actual package
content and clean consumers on all three platforms and .NET 8/10. A manifest alone
does not constitute qualification. Publication requires those gates and a separate
release action.

Re-run diagnostics, native safety, concurrency, package, and platform gates on
upgrades. Record old identity so consumers can audit historical decisions.
