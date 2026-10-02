# BiscuitSharp documentation

New to BiscuitSharp? Start with the [README](../README.md) for the purpose and first token call, then use these guides as you build an application.

## Use the library

| Guide | When to read it |
| --- | --- |
| [API contract](api-contract.md) | Exact result, exception, ownership, and compatibility behavior |
| [Hufu integration specification](hufu-integration-spec.md) | Optional transport profile, authority mapping, keys, revocation and exact enforcement |
| [Hufu integration handoff](hufu-integration-handoff.md) | Current API gaps, source snapshot, implementation sequence and acceptance tests |
| [Deployment](deployment.md) | Qualified platforms, package assets, trimming, and NativeAOT |
| [Security](security.md) | Invariants, privacy defaults, and disclosure |

## Build and understand it

| Document | Purpose |
| --- | --- |
| [Architecture](architecture.md) | Layer ownership and Biscuit semantics |
| [Native boundary](native-boundary.md) | ABI, native ownership, asset identity, and distribution |
| [Upstream upgrade](upstream-upgrade.md) | How to move the pinned biscuit-auth baseline |
| [Verification](verification.md) | Executed tests and release evidence |
| [Publishing](publishing.md) | Protected preview release setup and trusted NuGet publishing |
| [Pre-publish review](prepublish-review.md) | Checklist for the reviews before first publication |
| [Review findings](review-findings.md) | Open gaps, known limits, and follow-on work |
| [Implementation plan](implementation-plan.md) | Milestones, matrices, and acceptance gates (current plan) |
| [ADR 0001](decisions/0001-native-wrapper-boundary.md) | Why BiscuitSharp wraps the official engine |
| [ADR 0002](decisions/0002-wrapper-semantics.md) | Authorization budgets, token normalization, and issuance identity |
| [ADR 0003](decisions/0003-hufu-integration-profile.md) | Explicit amendments for the optional registered Hufu profile |
| [Changelog](../CHANGELOG.md) and [roadmap](../ROADMAP.md) | Released changes and planned work |
