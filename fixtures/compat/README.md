# Compatibility fixtures

`genesis.json`: a deterministic interop vector generated with direct upstream
calls (no bridge): an Ed25519 root from a fixed seed (seed, PKCS#8 DER,
public key) and one genesis token over two facts (including a Datalog 3.3 set).

- Verify (runs in CI): `cargo test --locked --test committed_fixtures` from `native/`.
- Regenerate: `cargo test --locked --test committed_fixtures -- --ignored`.

Token bytes vary per run by design: issuance generates a fresh ephemeral
next-key per token (`BiscuitBuilder::build` → `build_with_rng`), so every token
and its revocation IDs are unique. Seed-derived keys are byte-pinned by
`fixtures_match_committed`; the committed token is verified semantically
(parse, block sources, authorize).
