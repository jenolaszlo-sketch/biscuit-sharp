# Pre-publish review checklist

For the reviews before the first preview publication. Check each item, record
the outcome in the release notes or `docs/verification.md`.

## API review
- [ ] Public surface reviewed member by member (`BiscuitPrivateKey`,
      `BiscuitPublicKey`, `BiscuitToken`, `BiscuitTokenBuilder`, `BiscuitBlock`,
      `BiscuitAuthorizer`, `BiscuitAuthorizationResult`,
      `BiscuitAuthorizationError`, `BiscuitRevocationId`, `BiscuitInspection`,
      `BiscuitEngine`, `BiscuitSharpVersionInfo`, `BiscuitParam`,
      `BiscuitAuthorizerLimits`, exception hierarchy). Naming, nullability,
      ownership, and thread-safety as documented in `docs/api-contract.md`.
- [ ] Decide: freeze as the 1.0 baseline, or adjust before the preview (cheaper now).
- [ ] `ToString()` outputs audited for secret material (keys, tokens, Datalog).

## Behavior review
- [ ] Re-run the full CI matrix green on the release commit (managed, compat,
      lints, dist ×3, pack, consume ×6, valgrind, native-release).
- [ ] Confirm the 1 ms → robust-default limits change reads correctly in code,
      docs, and tests (it was a behavior fix, not just a test fix).
- [ ] Confirm trailing-bytes canonicalization + ephemeral-key nondeterminism are
      documented where consumers could be surprised (`api-contract.md`).
- [ ] Privacy pass: inspection prints block sources; confirm no default path
      logs keys, tokens, or ambient facts.

## Packaging review
- [ ] Implement `publish.yml` (manual trusted publish; NuGet credentials).
- [ ] Decide the first version (`0.1.0-preview.1` as configured, or otherwise).
- [ ] After publication: restore `PackageValidationBaselineVersion`, freeze the
      API baseline, record the release in `CHANGELOG.md` and `docs/verification.md`.
- [ ] Confirm README/ROADMAP status lines describe a published package, not a
      pre-release repo.

## Provenance review
- [ ] Upstream pin re-verified (tag = commit, checksum, `Cargo.lock` diff
      reviewed, license inventory current).
- [ ] `NOTICE` attribution complete; no copied code without attribution.
- [ ] Secrets audit: no tokens/keys in the repo, logs, or fixtures
      (`fixtures/` holds a fixed test seed by design — confirm that stays
      test-only and clearly labeled).
