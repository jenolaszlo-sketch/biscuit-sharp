# Preview package publishing

For a normal release, wait for the full CI run on the current main commit to
succeed, then open Actions → publish → Run workflow, select main, leave both
optional fields blank, and click Run workflow. The workflow pins the dispatch
commit, finds successful ci.yml coverage for that exact SHA and prints the
validated SHA/run link in its summary. It never falls back to an older commit
if the dispatched commit has no successful CI.

For an older reviewed candidate, supply only ci_run_id; the workflow derives
the SHA from that run. release_sha is an optional additional identity assertion.
Supplying only release_sha discovers successful CI for that exact commit.

All paths retain the release checks: the run must belong to the exact
.github/workflows/ci.yml, be a successful push to main, and contain all 19
successful full-matrix jobs. The publisher downloads that validated run's
immutable nupkg artifact, checks the package and symbol archive against the
checked-out SHA, and publishes from the protected GitHub environment.


Before dispatching `publish.yml`, configure these repository settings:

1. Create the `nuget-production` environment and add required reviewers. The
   environment protection rule is the human release approval gate.
2. Add repository secret `NUGET_USER` with the nuget.org profile username used
   by the trusted publishing action.
3. In NuGet.org Trusted Publishing, register this repository owner and repo,
   workflow file `publish.yml`, and environment `nuget-production`. The policy
   must permit publishing the BiscuitSharp package.
4. Dispatch on main with both optional fields blank after its CI passes. The
   workflow publishes the `.nupkg`; `dotnet nuget push` also uploads its
   matching `.snupkg` when present.

The workflow uses [NuGet/login](https://github.com/NuGet/login) to exchange the
GitHub Actions OIDC token for a short-lived NuGet API key. See Microsoft's
[NuGet Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for policy setup. These settings are configured through GitHub and NuGet.org.
The first preview was published successfully on 2026-10-02; exact release
identity and public-feed results are recorded in verification.md.

## After publication

The separate verify-published.yml workflow still takes the qualified CI run ID
and full SHA; copy them from the publisher summary when running public-feed
verification. This change automates publisher selection, not that separate step.
Its six jobs restore from NuGet.org with isolated caches, run the packaged smoke
on every supported RID/TFM and compare all archive content with the qualified
artifact (excluding the NuGet repository signature). These checks passed for
0.1.0-preview.1. The project now uses PackageValidationBaselineVersion=0.1.0-preview.1
and development version 0.1.0-preview.2. For each later publication, validate the
next candidate against the latest published baseline before re-freezing the contract.

The API inventory check is independent of NuGet availability. The pack gate also
checks portable PDB SourceLink mappings against the exact release SHA.
