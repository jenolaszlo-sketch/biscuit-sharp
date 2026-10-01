# Tests

`BiscuitSharp.Tests` is a dependency-free console runner (mirroring the early
CedarSharp shape): exit code 0 means all checks passed.

- Version/loader slice: fail-closed loading, relative overrides, live identity
  with differential hash checks.
- Key slice: generate/import/export round-trips (Ed25519 + P-256, DER + PEM),
  public-key import (raw, hex, prefixed display), disposal, `ToString` privacy,
  concurrency.
- Token slice: issue/verify/attenuate/seal, rules, tamper/truncation/garbage
  rejection, revocation growth, inspection, typed params, unicode, equality,
  concurrency.
- Authorization slice: allow/deny/failed-checks/explicit-deny, rules, limits,
  determinism, malformed-Datalog behavior, concurrency.
- Loader manifest probes: verified load succeeds, tampered manifest refuses
  (child processes, since the loader caches per process).
- Compatibility: committed fixtures always run; the generated exchange and the
  consume phase run when `artifacts/compat` / `BISCUITSHARP_COMPAT_CONSUME=1`
  are present (see `eng/Test-Compat.ps1`).
- Adversarial: mutated tokens never verify; invalid policies never authorize.
