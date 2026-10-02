# Implementation plan

Status 2026-10-02: preview graduation is complete. 0.1.0-preview.1 is published
from 7892458 / CI 36946889887 and all six public-feed consumers passed.
The 201-entry contract is re-frozen against that published baseline.
Development 0.1.0-preview.2 passed the full 19-job matrix at a67b29a /
CI 36968484267; it has not been published. Detailed milestones below retain
their implementation history. M3.1 integration specification/handoff is complete;
M3.2 wrapper prerequisites and M3.3/M3.4 adapter qualification remain pending.
Current evidence and stable-release requirements are in ROADMAP.md and
verification.md. For scope see

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
`token_authorize` (done 2026-10-01: ambient facts/rules/checks/policies via upstream
`code`, allow/deny answers with matched-policy indices and structured
failed-check/policy errors, first-match-wins order, 9 native tests incl.
allow-matched-but-check-failed and explicit deny; `AddRule` added 2026-10-02).
Consolidation is allowed; managed callers must not depend on
Rust ABI details. Private keys live in opaque native handles; export is explicit.

### Managed surface (`src/BiscuitSharp/`)

Keys (generate/import/export over opaque handles with finalizer-backed disposal,
DER/PEM export, PEM/DER import, public-key `Parse`/hex/prefixed display forms
without the private half, `ToString` privacy — done 2026-10-01, extended
2026-10-02), builder
(textual Datalog + parameterized overloads + rules; discourage untrusted interpolation),
token (parse/verify, base64url, attenuate, seal, revocation IDs, inspection —
done 2026-10-01: canonical bytes, equality, typed params incl. reflection
convenience overload; rules added 2026-10-02),
authorizer (facts, rules, policies, checks → `BiscuitAuthorizationResult` with
matched-policy indices, structured errors, `RequireAuthorized()`, explicit time
facts, explicit limits with the wrapper default of 100k facts / 100k iterations /
5 s and an explicit upstream-parity option — done
2026-10-01, extended 2026-10-02), version discovery reporting the loaded asset (done 2026-10-01: process-lifetime
loader, strict JSON decoding, file-hash identity, load-time manifest verification), and the
`BiscuitException` taxonomy with boundary rules (bridge ≠ token ≠ Datalog ≠
enforcement failures; Deny is a result). Per-slice check counts live in the
[verification ledger](verification.md).

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

Win-x64 progress 2026-10-02 (local, single RID — the gate stays open until all
RIDs pass in CI): release-triple build, `eng/Build-Native.ps1` staging
(binary + manifest + licenses), load-time manifest verification with
child-process probes, NativeAOT publish + execute, single-RID pack +
archive verification, one isolated-cache clean consumer — all green. Linux and
macOS builds, the remaining five consumers, and `Verify-NativeStaging` /
`Verify-NuGetPackage` / `Test-PackagedConsumer` automation stay open.

Prior CI matrix 2026-10-02 (successful at the SHAs recorded in the verification
ledger; leak coverage invalidated by a filter selecting zero tests): per-OS
`managed` (build + tests), per-OS `compat` (full `Test-Compat.ps1` under pwsh),
`native-lints` (fmt + clippy), per-OS `dist` (stage + verify + AOT, artifacts
uploaded), `pack` (three-RID assembly + verification + symbols upload),
per-OS × TFM `consume` (six isolated-cache consumers), and a Linux `valgrind`
leak-probe job. The Valgrind command must be rerun with its corrected exact test
filter before the lifecycle claim is qualified. All `eng/` scripts are PowerShell 5.1/7 cross-platform
(forward-slash joins, `$HOME` cargo discovery, no `powershell.exe`
nesting); record future platform and release-gate evidence here against its
exact SHA and run, without carrying forward the invalid leak result.

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
  as `STATUS_PANIC` with an empty body; no unwind crosses the ABI). Release
  profile verified 2026-10-02 (43/43 green; matrix distribution varies run to
  run from fresh corpus randomness in both profiles — the test asserts
  invariants, not counts). Findings pinned: upstream framing tolerates trailing bytes
  (parse canonicalizes; the mutation test's byte-identical allow cases are not a
  production strict-canonical-input requirement); DER
  seed-region mutations yield different valid keys (import success requires a
  usable, destroyable handle). Release-profile repetition on all RIDs stays open
  with the M2 matrix (a `native-release` CI job now locks in the Ubuntu run).
- Leak instrumentation: rerun Linux Valgrind with the corrected exact workload
  over repeated issue/parse/attenuate/authorize/dispose cycles; prior CI selected
  zero tests and establishes no leak result.
- Diagnostics/privacy tests (safe defaults per `security.md`), strict
  malformed-input tests, SourceLink/symbols, upstream compatibility fixtures.
- Rerun the complete M2 release matrix.

## M3: consumers and later

Status 2026-10-02: the optional Hufu integration design is finalized at 69ff07a;
the adapter is not implemented. Use the [specification](hufu-integration-spec.md),
[source-reviewed handoff](hufu-integration-handoff.md) and
[ADR 0003](decisions/0003-hufu-integration-profile.md). This selects an online,
registered-token design, not a production Hufu host or a dependency of Hufu core.

### M3.1: integration contract and handoff — complete

- [x] Preserve the supplied proposal and define exact predicates, authenticated
  context, key selection/rotation/lifetime, registered canonical bytes,
  same-context attenuation, revocation ordering and structured failure mapping.
- [x] Check the design against published BiscuitSharp and the current experimental
  Hufu source; record missing host contracts and wrapper limitations.
- [x] Exercise the exact fixed policy through the real win-x64 bridge on net8/net10:
  11 cases pass on each, including authority/request/scope fact pollution and
  read-only attenuation. This evidence does not qualify the complete adapter.

### M3.2: generic wrapper prerequisites — implemented, qualification in progress

- [x] Add machine-readable reasons for fact, iteration and time limit exhaustion;
  distinguish expression/query/other evaluation failures and keep failures closed.
- [x] Review the new enum/property and update the API inventory to 210 entries.
  The maintainer has explicitly deprioritized additional compatibility work
  during early adoption; the existing package gate remains in place.
- [x] Run native and both-TFM managed reason-mapping regressions on Windows.
- [ ] Complete isolated package/NativeAOT consumer and full release-matrix checks;
  record exact evidence in verification.md before qualifying this candidate.

Gate: the adapter can produce AuthorizationBudgetExceeded from typed evidence.
Until then evaluation_failure maps to AuthorizationFailure. Typed request/
attenuation substitution is a separately reviewed wrapper option; a qualified
centralized adapter literal writer is also permitted by the specification.

### M3.3: Hufu adapter and authoritative host composition — pending

- [ ] Define trusted workflow/activity/realm/audience bindings, grant-version/
  layer selection, exact effect identity and provider-object bindings.
- [ ] Supply Ed25519 signing-key leases and realm-scoped verification lookup;
  implement bounded envelopes, protected credential custody and authenticated
  issuance/derivation registration.
- [ ] Implement only the closed profile's fixed facts/policy and structured
  same-context restrictions. Reject unknown logic, profile or lineage.
- [ ] Compose current authority, every revocation ID, scope/exclusions, time,
  Biscuit and complete Hufu/Cedar layer checks with mandatory durable evidence.
- [ ] Set host-owned evaluation limits using real policy/concurrency measurements.
- [ ] Order credential revocation and key retirement with final Hufu operation
  start, current versions/fences/policy and exact resource/effect checks.
- [ ] Retain AlreadyStarted as historical/idempotent evidence, without redispatch.

Owner: Penghou.Hufu.Biscuit and trusted Hufu host/store/enforcement composition.
Keep workflow policy, grants, approval, resource canonicalization, evidence
persistence and broker execution outside BiscuitSharp. Hufu's immediate governed
Luban start/outcome work remains its own prerequisite; this optional transport
does not replace or postpone it.

### M3.4: consumer qualification and adapter freeze — pending

- [ ] Begin with Hufu's supported Windows read profile, including concrete
  metadata/traversal/release checks and required evidence.
- [ ] Run the proposal and handoff conformance cases: no amplification or layer
  override, workload binding, key/registration/budget failures, ancestor/child
  revocation, exact start races and no replayed dispatch.
- [ ] Exercise actual provider/object/link races and start/evidence failures;
  mock-only tests do not establish confinement or ordered revocation.
- [ ] Record exact consumer source, native/package/profile identities, platforms,
  configured budgets and tested limits; freeze/publish the adapter separately.

Gate: a real Hufu consumer passes complete integration conformance. Wrapper
cross-platform CI does not qualify Hufu providers on other platforms. Mutation
support requires its own qualified governed host and outcome-recovery protocol.

Later, only on concrete need: cross-context or offline delegation profiles,
third-party blocks, snapshots, performance caching, non-exportable signing and
additional RIDs. Unsupported mappings remain explicitly rejected.

## Stable-release acceptance gate

Public XML documentation remains an open requirement before 1.0. Stable release
qualification and publication are also pending.

Before publishing 1.0.0, complete that documentation and qualify the exact stable
candidate through the existing release gates: native assets built and exercised
on every advertised RID; clean packaged consumers on both frameworks; per-RID
NativeAOT execution; bidirectional direct-upstream compatibility; malformed-input
and native-memory hardening; reviewed API contract and published-baseline checks;
independently verifiable package/native identity.
