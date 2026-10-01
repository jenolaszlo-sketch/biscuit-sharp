# Changelog

## Unreleased

- Establish the repository scaffolding: solution, managed API shape (keys, token,
  builder, attenuation, seal, authorizer, revocation IDs, inspection, version info),
  native ABI 1 skeleton, docs, and roadmap.
- Pin the M0 baseline: biscuit-auth 6.0.0 (tag verified equal to the pinned
  commit, crate checksum recorded), `Cargo.lock` committed (94 packages, locked
  fetch green on Rust 1.89.0), schema versions 3..6 / Datalog 3.3 recorded; locked check, `cdylib` link, and
  ABI exports verified on MSVC.
- M1 version slice: native `version` op with strict identity JSON and
  `{"code","message"}` error envelope (6 native tests green); managed
  process-lifetime loader with ABI check plus live `BiscuitEngine.GetVersion()`
  with differential binary/lockfile hash verification (managed suite green on
  .NET 8 and .NET 10 against the real `cdylib`).
- M1 key slice: native `key_generate`/`key_import`/`key_export_*`/`key_destroy`
  over opaque handles (Ed25519 + Secp256r1, PEM/DER with upstream
  algorithm auto-detection, `key_error` envelope code; 12 native tests green
  incl. differential round-trips vs direct upstream and 8×25 concurrency);
  managed `BiscuitPrivateKey` with finalizer-backed disposal, DER export, and
  mapped `BiscuitKeyException` failures (managed suite green on .NET 8 and .NET 10).
- M1 token slice: native `token_create`/`parse_verify`/`attenuate`/`seal`/
  `revocation_ids`/`inspect` over verified bytes (upstream `code` with typed
  str/int/bool/bytes params, canonical round-trips, stable `sealed_token`/
  `signature_error`/`format_error`/`datalog_error` codes; 26 native tests green
  incl. tamper/truncation codes and the finding that sealing flips the chain
  terminator without appending a block); managed immutable `BiscuitToken` with
  equality, `BiscuitTokenBuilder` with parameterized facts, typed
  `Biscuit*Exception` mapping (managed suite green on .NET 8 and .NET 10).
- M1 authorization slice: native `token_authorize` (verify → ambient
  facts/checks/policies via upstream `code` → allow/deny answers with
  matched-policy indices and structured failed-check/policy errors,
  first-match-wins order, default limits, no ambient time injection; 34 native
  tests green incl. allow-matched-but-check-failed and explicit deny);
  managed `BiscuitAuthorizer` with contradiction guard and extended error
  records (managed suite green on .NET 8 and .NET 10).
- M1 compatibility gate: `eng/Test-Compat.ps1` exchanges fixtures between
  direct upstream Rust and the bridge in all four directions (issue, verify,
  authorize, cross-attenuate both ways) plus committed deterministic
  `fixtures/compat/genesis.json` vectors (managed suite green per TFM; exact totals in [docs/verification.md](docs/verification.md)). Finding: token bytes are never byte-stable across runs —
  issuance mints a fresh ephemeral next-key per token — so fixtures pin keys
  exactly and verify tokens structurally.
- M2.5 mutation matrix: deterministic 4,096-case native run (`src/adversarial.rs`,
  zero panics, zero unjustified successes) plus a managed 96-position token
  sweep and 7 invalid policies, all fail-closed. Findings pinned: upstream
  framing tolerates trailing bytes (parse canonicalizes; the test counted an
  allow only for byte-identical content, which is not a production strict
  canonical-input rule); DER seed-region mutations yield different valid keys.
- Second review pass: value equality for `BiscuitPublicKey`/`BiscuitRevocationId`
  (byte-based, not array identity — required for revocation lookups); the key
  store lock is no longer held across token builds; a native panic-containment
  test; `BiscuitPrivateKey.ExportPem()`; `BiscuitAuthorizer.AddTimeFact(...)`;
  CI pins Rust 1.89.0; package-validation baseline removed until the first
  preview is published.
- Review fixes part 3: hex/prefixed public-key display forms
  (`ParseHex`/`ParsePrefixed`/`ToPrefixedString`, managed-side over the
  validated raw path — no ABI change); explicit `BiscuitAuthorizerLimits` /
  `WithLimits` wired to upstream `set_limits`. Finding: upstream's default
  `max_time` is 1 ms, which made the default path deny with
  `evaluation_failure` under scheduler load (surfaced in CI); BiscuitSharp now
  applies a robust default (100k facts / 100k iterations / 5 s) and exposes
  `UpstreamDefault` for strict parity. `eng/Verify-NativeStaging.ps1`,
  `eng/Verify-NuGetPackage.ps1`, `eng/Test-PackagedConsumer.ps1`, and
  `eng/Test-Dist.ps1` (per-RID orchestrator) implemented with CI
  `dist`/`pack`/`consume`/`valgrind` jobs.
- Review fixes part 2: `BiscuitPublicKey.Parse` over a new `key_import_public`
  bridge op (verification without the private half); `AddRule` on the token
  builder and the authorizer; `unsafe extern "C"` FFI entry with documented
  safety; rustfmt + clippy clean with CI lint jobs; `BiscuitSharp.AotSmoke`
  added to the solution.
- Third review pass: documented open items in [docs/review-findings.md](docs/review-findings.md)
  (several since fixed: public-key import, authorizer rules, load-time manifest
  verification, clippy/CI coverage).
- Reliability fix: upstream's default 1 ms authorization budget made the
  default path deny with `evaluation_failure` under CI load. BiscuitSharp now
  applies a robust default (100k facts / 100k iterations / 5 s) at the bridge
  and in `BiscuitAuthorizerLimits.Default`, with `UpstreamDefault` for strict
  parity, plus regression guards (native default-limits test, 128-way parallel
  managed authorizations).
- Self-audit fixes: explicit Cdecl on all P/Invokes; unbound template
  placeholders and empty authority blocks pinned by test (both fail closed or
  behave harmlessly); builder-reuse semantics pinned; inspect 4,096-block print
  cap documented; managed/native default-limits coupling cross-referenced;
  verify-only README snippet.
- Release-profile hardening: full native suite green under
  `cargo test --release` (43/43); the mutation matrix holds its invariants in
  both profiles (per-run distribution varies from fresh corpus randomness, so
  the test asserts invariants, not counts). Locked in with a CI
  `native-release` job.
- M2 win-x64 staging (local): release-triple build via `eng/Build-Native.ps1`,
  `eng/Verify-NativeStaging.ps1`, load-time manifest verification with
  child-process probes, NativeAOT publish + execute, single-RID pack +
  `eng/Verify-NuGetPackage.ps1`, and `eng/Test-PackagedConsumer.ps1` on
  net8.0/net10.0 via `eng/Test-Dist.ps1 -Rid win-x64` — all green. Linux/macOS still open.
- CI distribution matrix: per-OS managed/compat/dist jobs, three-RID pack +
  six-consumer jobs, Valgrind leak-probe job, and a pwsh-cross-platform `eng/`
  suite (`$HOME` cargo discovery, forward-slash joins, no `powershell.exe`
  nesting); prior CI passed at the SHAs recorded in `docs/verification.md`.
  Audit found its Valgrind filter selected zero tests, so leak coverage remains
  unverified pending a corrected Linux run. Added a native `leak_probe_cycles`
  workload (50 full-lifecycle bridge cycles) for that gate.
- Contract corrections: document canonical parse output for tolerated trailing
  framing, fresh chain keys per issuance, completed-configuration concurrency
  for `Authorize`, whole-millisecond limit truncation, validation timing, and
  caller-coordinated key disposal. ADR 0002 records the chosen semantics; team
  acceptance remains pending. Arrays and result collections now use defensive
  snapshots in the implementation.
