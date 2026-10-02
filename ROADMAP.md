# BiscuitSharp roadmap

Updated 2026-10-02. BiscuitSharp 0.1.0-preview.1 is published on NuGet.org from
78924586b70f4188db6cfc47983f8171c78725f8 / CI 36946889887.
All six public-feed consumers and content comparisons passed in
[verification 36968162532](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36968162532).
The API baseline is restored. All 19 jobs passed for the earlier development
0.1.0-preview.2 at a67b29a / CI 36968484267. The current development candidate
adds typed evaluation reasons; its reviewed API inventory has 210 entries.
The new implementation is qualified at f892857 by all 19 jobs in
[CI 36979786333](https://github.com/jenolaszlo-sketch/biscuit-sharp/actions/runs/36979786333).
Preview graduation is complete; 0.1.0-preview.2 has not been published.
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
- [x] Configure the release environment and NuGet trusted publishing;
  successful publication verifies that this setup is operational.
- [x] Publish 0.1.0-preview.1 from the exact qualified CI artifact.
- [x] Restore from NuGet.org on all three platforms and both frameworks; compare
  published content against the qualified artifact.
- [x] Restore PackageValidationBaselineVersion to 0.1.0-preview.1 and keep the
  201-entry preview contract frozen; local development pack passes.
- [x] Qualify the three-RID 0.1.0-preview.2 development candidate with the
  restored published baseline in full CI (19/19 at a67b29a / 36968484267).

## Stable release

- [ ] Complete public XML documentation before 1.0.
- [ ] Qualify and publish the stable candidate through the same API-baseline,
  distribution, NativeAOT, packaged-consumer and native-safety gates.

## M3: optional Hufu integration

Design activity completed on 2026-10-02 in commit 69ff07a. See the
[specification](docs/hufu-integration-spec.md), [handoff](docs/hufu-integration-handoff.md)
and [ADR 0003](docs/decisions/0003-hufu-integration-profile.md).
The [implementation plan](docs/implementation-plan.md#m3-consumers-and-later)
defines the delivery gates. The optional adapter and SQLite start prototype are implemented locally; real provider qualification remains open.

- [x] Finalize the source-reviewed specification and handoff; archive the original
  proposal and document security-sensitive amendments.
- [x] Exercise the fixed Datalog policy through the real Windows native bridge:
  11 cases pass on each managed framework, including attenuation fact pollution.
- [x] Implement machine-readable fact/iteration/time budget-exhaustion reasons;
  expression/query/other failures stay distinct. Full CI passed 19/19 at
  f892857 / 36979786333, including typed packaged-consumer/NativeAOT checks.
  The exact CI package passed 13 Hufu fixed-policy checks per TFM on Windows.
  Development preview.2 remains unpublished.
- [x] Define Hufu's trusted workflow/activity/realm/audience contracts, immutable
  grant versions and exact request/provider identities.
- [x] Implement signing-lease contracts, bounded owned envelopes, authenticated
  issuance/derivation registration and realm-scoped revocation storage.
- [x] Implement and test bounded literal construction, positive host-owned budgets
  and complete authority-layer/Cedar/required-evidence composition locally.
- [ ] Measure actual host policy/concurrency budgets and qualify key custody.
- [x] Order per-block revocation and key retirement with exact durable operation
  start; prove receipt replay never redispatches.
- [ ] Qualify one real Hufu Windows read consumer and the integration conformance
  suite before freezing or publishing the adapter separately.

The local Windows adapter suite passes 63 cases per TFM using the exact reviewed
CI package, real Cedar and disk SQLite; the 95 existing Hufu cases also pass per
TFM. The runtime start participant is a SQL fixture. See the
[handoff](docs/hufu-integration-handoff.md#m33-local-implementation-and-next-handoff)
for the remaining real-provider, authentication/custody and release gates.
Hufu source remains an uncommitted prototype; no adapter package was published.

Hufu owns adapter and host/provider delivery. BiscuitSharp owns only generic
wrapper enhancements needed by that consumer. This optional integration does
not block wrapper preview qualification or replace Hufu's immediate governed
Luban operation-start gate. Hufu policy/concurrency budget measurements remain
a consumer requirement.

Third-party blocks, snapshots, caching and additional RIDs remain deferred until
a concrete consumer requires them. Publication of a preview does not claim 1.0
stability or qualification of older OS/glibc versions.
