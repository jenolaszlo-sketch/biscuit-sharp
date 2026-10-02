# BiscuitSharp roadmap

Updated 2026-10-02. BiscuitSharp 0.1.0-preview.1 is published on NuGet.org from
78924586b70f4188db6cfc47983f8171c78725f8 / CI 36946889887.
All six public-feed consumers and content comparisons passed in
[verification 36968162532](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36968162532).
The API baseline is restored; development continues at 0.1.0-preview.2.
See docs/verification.md for exact artifacts, hashes and historical evidence.

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

- [x] Qualify the second-audit fixes with a fresh complete release matrix.
- [x] Configure the release environment and NuGet trusted publishing; successful protected-environment publication verifies that this setup is operational.
- [x] Publish 0.1.0-preview.1 from the exact qualified CI artifact.
- [x] Restore from NuGet.org on all three platforms and both frameworks; compare
  published content against the qualified artifact.
- [x] Restore PackageValidationBaselineVersion to 0.1.0-preview.1 and keep the
  201-entry preview contract frozen; local development pack passes.
- [ ] Qualify the three-RID 0.1.0-preview.2 development candidate with the
  restored published baseline in full CI.

## Stable release and consumers

- [ ] Require complete public XML documentation before 1.0 and validate the
  stable candidate through the same release gates.
- [ ] Integrate separately into Penghou.Hufu.Biscuit; measure its real policies
  and choose explicit budgets. Integration is not a wrapper correctness gate.

Third-party blocks, snapshots, caching and additional RIDs remain deferred until
a concrete consumer requires them. Publication of a preview does not claim 1.0
stability or qualification of older OS/glibc versions.
