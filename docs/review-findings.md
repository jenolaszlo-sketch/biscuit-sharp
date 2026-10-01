# Review findings and disposition

Updated 2026-10-02. The complete 19-job release matrix at 412f25f passed:
[run 36934972104](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36934972104).
The original review and evidence are preserved in self-audit-handoff.md.
This ledger describes current status.

## Second-audit corrections

F09–F12 are fixed in code and covered by regressions: source-bound legal
material/coverage validation, sealed factory-only BiscuitParam, public operator
and protected-member inventory coverage, and strict inspection ranges/arrays.
Process-architecture selection is corrected. The new implementation requires
a fresh complete matrix and a newly selected candidate artifact. Historical
successful runs do not qualify the changed code.

## Resolved before preview

- Loader synchronization, current-handle export binding, failed-load cleanup and
  deterministic concurrent success/failure/retry tests.
- Defensive byte ownership for public keys and revocation identifiers;
  collection snapshots in result constructors and record init setters.
- Strict JSON response/error taxonomy and contradictory policy-index rejection.
- Typed-parameter construction and duplicate-name checks; key lifetime retention
  and cleanup of newly allocated handles when response validation fails.
- Real 50-cycle Valgrind workload, empty key-store capacity release and standalone
  probe without libtest allocations. Zero definite/indirect/possible losses and
  zero memory errors; no suppressions.
- Full target-runtime legal material, checksums, inventory coverage and tamper
  rejection on staged and packaged assets.
- Three RID staging/NativeAOT gates and six clean .NET 8/10 packaged consumers.
- Public API review: ParseHex and prefixed display remain useful for configuration
  and upstream interop; raw constructors and upstream decode guarantees are
  documented. Checked surface inventory covers both frameworks.
- Preview semantics retained under authorized graduation work: wrapper default
  budgets, whole-millisecond truncation, upstream normalization and fresh chain
  keys. Synthetic ordinary/concurrent/hostile-growth evidence is recorded in
  authorizer-budget.md. Hufu-specific policy budgeting stays external.

## Release steps still open

- Full matrix on the final preview-preparation commit.
- Protected release environment, trusted publishing and preview publication.
- Public-feed consumers/content identity on all supported RID/TFM combinations.
- Published preview compatibility baseline and final contract re-freeze.
- Complete public XML documentation before 1.0 (CS1591 remains suppressed for
  the preview).

## Documented limits and deferred capabilities

- Synchronous calls have no cancellation guarantee; evaluation limits are not
  end-to-end deadlines or hard memory caps.
- Token/builders use textual upstream Datalog. Builders and authorizers require
  caller coordination during mutation; configured authorizers support parallel
  Authorize calls.
- Private key handles are process-global. Dispose promptly; abandoned handles
  remain until finalization. Panic containment covers unwinding only.
- Raw public bytes/hex/prefixed strings are supported; DER/PEM public keys and
  ambiguous raw private-secret imports are deferred.
- Result collection equality is reference-based; no structural equality promise.
- AddTimeFact uses whole seconds; unsupported older OS/glibc baselines and
  additional RIDs are unqualified.
- Third-party blocks, snapshots, caching, safe fingerprints and Hufu integration
  remain consumer-driven follow-on work.

No default tracing implementation emits secrets. Existing private-key display
regressions cover ToString privacy; explicit Export, Inspect and structured
Datalog error details are caller-requested data and must not be logged blindly.
