# Tests

`BiscuitSharp.Tests` is a dependency-free console runner (mirroring the early
CedarSharp shape): exit code 0 means all checks passed.

- Version/loader slice: fail-closed loading, relative overrides, live identity
  with differential hash checks.
- Key slice: generate/import/export round-trips (Ed25519 + P-256, DER + PEM),
  disposal, `ToString` privacy, concurrency.
- Token slice: issue/verify/attenuate/seal, tamper/truncation rejection,
  revocation growth, inspection, typed params, unicode, equality, concurrency.
- Authorization slice: allow/deny/failed-checks/explicit-deny, determinism,
  malformed-Datalog behavior, concurrency.
- Compatibility: committed fixtures always run; the generated exchange and the
  consume phase run when `artifacts/compat` / `BISCUITSHARP_COMPAT_CONSUME=1`
  are present (see `eng/Test-Compat.ps1`).
- Adversarial: mutated tokens never verify; invalid policies never authorize.
