# Preview package publishing

The manual workflow accepts a successful `ci.yml` run ID and the exact full
commit SHA tested by that run. It checks that the run belongs to
`.github/workflows/ci.yml`, was a successful `push` to `main`, and that every
job in the full 19-job matrix succeeded. It downloads the immutable `nupkg`
artifact from that run, checks the package and symbol archive against the
checked-out SHA, then publishes from a protected GitHub environment.

Before dispatching `publish.yml`, configure these repository settings:

1. Create the `nuget-production` environment and add required reviewers. The
   environment protection rule is the human release approval gate.
2. Add repository secret `NUGET_USER` with the nuget.org profile username used
   by the trusted publishing action.
3. In NuGet.org Trusted Publishing, register this repository owner and repo,
   workflow file `publish.yml`, and environment `nuget-production`. The policy
   must permit publishing the BiscuitSharp package.
4. Dispatch with the successful full CI run ID and its 40-character SHA. The
   workflow publishes the `.nupkg`; `dotnet nuget push` also uploads its
   matching `.snupkg` when present.

The workflow uses [NuGet/login](https://github.com/NuGet/login) to exchange the
GitHub Actions OIDC token for a short-lived NuGet API key. See Microsoft's
[NuGet Trusted Publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
for policy setup. This repository change does not create the environment,
secret, or NuGet.org policy and does not publish a package.
