# Security

## Invariants (must hold at 1.0)

- Tampering with a serialized token invalidates verification.
- The wrong root key invalidates verification.
- Attenuation cannot restore authority removed by an earlier check.
- Sealed tokens cannot be attenuated.
- Authorization default/deny semantics match upstream.
- Invalid token ≠ authorization denial; bridge failure ≠ authorization denial.
- No error path returns `IsAuthorized = true`.
- Private-key material is never logged; raw bearer tokens are never emitted by
  default telemetry.

## Privacy defaults

Tracing and diagnostics never export by default: private keys, raw Biscuit bearer
tokens, full Datalog containing sensitive values, ambient authorization facts.
Safe defaults include: operation, decision, block count, error category,
algorithm, engine version, duration. Token fingerprints are allowed only if
explicitly designed not to become bearer material.

## Threat notes

- Opaque native key handles reduce managed-memory exposure but do not protect
  against process dumps or a compromised host; the documentation states this.
- Hash manifests check integrity, not publisher authenticity.
- Revocation requires application-managed external state; the consumer order is
  verify → revocation-id lookup → authorize (or whatever its threat model
  requires). Hufu owns that store.
- Report vulnerabilities privately to the maintainers — prefer a GitHub private
  security advisory on this repository over a public issue — for suspected
  credential-bypass or key-exposure flaws.
