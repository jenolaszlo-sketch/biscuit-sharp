# BiscuitSharp roadmap

Updated 2026-10-02. The complete 19-job release matrix passed at
5dd9e199b32ea18d1499a5e055cbe658c9e0b0a1
([CI run 36940126702](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36940126702)).
This includes corrected legal inventories and the real 50-cycle Valgrind probe.
The checked preview API, budget probes and SourceLink checks are included in this run. Publication awaits protected environment approval configuration.

## Implemented and qualified

- [x] Official biscuit-auth 6.0.0 wrapper; pinned upstream commit, Cargo.lock,
  Rust 1.89.0, ABI 1, schema 3..6 and Datalog 3.3.
- [x] Keys, token creation/verification/attenuation/sealing, authorization,
  revocation identifiers, inspection and version identity.
- [x] Managed .NET 8/10 validation, Rust differential and bidirectional
  compatibility, malformed inputs, concurrency and mutation hardening.
- [x] win-x64, linux-x64 and osx-arm64 assets with full redistribution material,
  source checksums, manifest identity and legal tamper rejection.
- [x] NativeAOT execution on all three platforms; six isolated packaged consumers.
- [x] Linux Valgrind: 50 lifecycle cycles, zero memory errors and zero
  definite/indirect/possible leaks.
- [x] Privacy regression coverage for private-key display; no default telemetry
  exports keys, bearer tokens or Datalog.
- [x] Public API inventory and nullable/annotation checks on both frameworks.
- [x] Synthetic ordinary/concurrent/hostile-growth budget probe; preview retains
  documented defaults and upstream normalization (ADR 0002).

## Preview publication and contract freeze

- [x] Complete the full release matrix after adding the inventory/budget/SourceLink gates.
- [ ] Configure the protected release environment and NuGet trusted publishing.
- [ ] Publish 0.1.0-preview.1 from the exact qualified CI artifact.
- [ ] Restore from NuGet.org on all three platforms and both frameworks; compare
  published content against the qualified artifact.
- [ ] Restore PackageValidationBaselineVersion to 0.1.0-preview.1 after it is
  publicly available, validate the next candidate against it and re-freeze.

## Stable release and consumers

- [ ] Require complete public XML documentation before 1.0 and validate the
  stable candidate through the same release gates.
- [ ] Integrate separately into Penghou.Hufu.Biscuit; measure its real policies
  and choose explicit budgets. Integration is not a wrapper correctness gate.

Third-party blocks, snapshots, caching and additional RIDs remain deferred until
a concrete consumer requires them. Publication of a preview does not claim 1.0
stability or qualification of older OS/glibc versions.
