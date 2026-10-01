# Changelog

## Unreleased (scaffolding)

- Establish the repository scaffolding: solution, managed API shape (keys, token,
  builder, attenuation, seal, authorizer, revocation IDs, inspection, version info),
  native ABI 1 skeleton, docs, and roadmap. No native behavior is implemented or
  qualified; every operation fails closed with `BiscuitBridgeException` until M1.
- Pin the M0 baseline: biscuit-auth 6.0.0 (tag verified equal to the pinned
  commit, crate checksum recorded), `Cargo.lock` committed (94 packages, locked
  fetch green on Rust 1.89.0),   schema versions 3..6 / Datalog 3.3 recorded; locked check, `cdylib` link, and
  ABI exports verified on MSVC.
- M1 version slice: native `version` op with strict identity JSON and
  `{"code","message"}` error envelope (6 native tests green); managed
  process-lifetime loader with ABI check plus live `BiscuitEngine.GetVersion()`
  with   differential binary/lockfile hash verification (20 checks green on .NET 8
  and .NET 10 against the real `cdylib`).
- M1 key slice: native `key_generate`/`key_import`/`key_export_*`/`key_destroy`
  over opaque handles (Ed25519 + Secp256r1, PEM/DER with upstream
  algorithm auto-detection, `key_error` envelope code; 12 native tests green
  incl. differential round-trips vs direct upstream and 8×25 concurrency);
  managed `BiscuitPrivateKey` with finalizer-backed disposal, DER export, and
  mapped `BiscuitKeyException` failures (41 checks green on .NET 8 and .NET 10).
- M1 token slice: native `token_create`/`parse_verify`/`attenuate`/`seal`/
  `revocation_ids`/`inspect` over verified bytes (upstream `code` with typed
  str/int/bool/bytes params, canonical round-trips, stable `sealed_token`/
  `signature_error`/`format_error`/`datalog_error` codes; 26 native tests green
  incl. tamper/truncation codes and the finding that sealing flips the chain
  terminator without appending a block); managed immutable `BiscuitToken` with
  equality, `BiscuitTokenBuilder` with parameterized facts, typed
  `Biscuit*Exception` mapping (81 checks green on .NET 8 and .NET 10).
- M1 authorization slice: native `token_authorize` (verify → ambient
  facts/checks/policies via upstream `code` → allow/deny answers with
  matched-policy indices and structured failed-check/policy errors,
  first-match-wins order, default limits, no ambient time injection; 34 native
  tests green incl. allow-matched-but-check-failed and explicit deny);
  managed `BiscuitAuthorizer` with contradiction guard and extended error
  records (104 checks green on .NET 8 and .NET 10).
