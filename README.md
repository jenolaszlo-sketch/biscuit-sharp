# BiscuitSharp

[![License](https://img.shields.io/github/license/jenolaszlo-sketch/biscuit-sharp)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

**Portable, attenuable authority credentials for .NET, verified by the official Rust Biscuit engine.** BiscuitSharp calls [biscuit-auth](https://github.com/eclipse-biscuit/biscuit-rust) in process and gives C# code typed keys, tokens, attenuation, sealing, authorization decisions, revocation identifiers, and inspection. Issue a token with Datalog facts, attenuate it offline for a narrower context, and verify it anywhere with the root public key.

It is useful when authority must travel with the request: delegated access, offline attenuation, and capability-style checks that remain verifiable without a central policy call. BiscuitSharp preserves Biscuit semantics and keeps valid-token, authorized-request, and failure states distinct so the application can enforce explicitly.

> Status: M0 and the M1 functional surface (keys, tokens, authorization) are
> implemented and tested, and the M2 distribution matrix (three RIDs,
> NativeAOT, packaging, six clean consumers, Valgrind) is green in CI.
> Remaining before any release: pre-publish review, then the first preview
> publication. See [ROADMAP](ROADMAP.md) and
> [docs/implementation-plan.md](docs/implementation-plan.md). No package is published.

## Try it

```csharp
using BiscuitSharp;

using var rootKey = BiscuitPrivateKey.Generate(BiscuitKeyAlgorithm.Ed25519);

BiscuitToken token = BiscuitTokenBuilder
    .Create()
    .AddFact("""right("workspace.main", "read")""")
    .AddFact(
        "right({resource}, {operation})",
        new { resource = "workspace.main", operation = "write" })
    .Build(rootKey);

BiscuitToken child = token.Attenuate(BiscuitBlock.Create("""
    check if operation("read");
    """));

// The anonymous-object overload above uses reflection; prefer the
// `IDictionary<string, BiscuitParam>` overload for trimming and NativeAOT.

BiscuitInspection view = child.Inspect();
Console.WriteLine($"blocks={view.BlockCount} sealed={view.IsSealed}");
foreach (BiscuitRevocationId id in child.GetRevocationIds())
{
    Console.WriteLine($"revocation id: {id}");
}

BiscuitAuthorizationResult result = BiscuitAuthorizer
    .For(child)
    .AddFact("""resource("/src/Foo.cs")""")
    .AddFact("""operation("read")""")
    .AddPolicy("""allow if right("workspace.main", "read");""")
    .Authorize();
Console.WriteLine(result.IsAuthorized ? "Allowed" : "Denied or requires review");
```

Parsing with a root public key verifies the cryptographic token. A successfully parsed
token does not mean a request is authorized. `IsAuthorized` is true only for an
error-free Allow; use `RequireAuthorized()` to enforce. Policies apply
first-match-wins in the order supplied, so place `deny` policies first.

## What you get

Biscuit behavior, reviewable outcomes, safe credential handling, and
deployment choices as below — all verified on Windows x64 so far (other RIDs
await the M2 gate).

- **Biscuit behavior:** signature verification, block-chain integrity, attenuation that only narrows authority, sealing, and default-deny authorization from the pinned engine.
- **Reviewable outcomes:** typed decisions, structured authorization errors, inspection (block count, sealed state, algorithms, revocation IDs, block source), and loaded-asset identity.
- **Safe credential handling:** opaque private-key handles with explicit DER/PEM export, `ToString()` that never reveals secrets, value-equal revocation IDs without an implicit revocation store, and an explicit `AddTimeFact` for deterministic expiration checks.
- **Deployment choices:** framework-dependent, self-contained, trimmed, and NativeAOT applications on the [qualified environments](docs/deployment.md) (M2).

BiscuitSharp verifies tokens and evaluates authorization. Your application owns grant
issuance policy, revocation storage, workflow revisions, human approval, delegation
policy, auditing, and enforcement at the resource boundary. Revocation intentionally
requires external state; this library exposes the identifiers, Hufu owns the store.

## Compatibility (target)

Qualified by CI on these environments, with .NET 8 and .NET 10:

| Environment | Native asset |
| --- | --- |
| Windows x64 | `win-x64` |
| Linux x64 | `linux-x64` |
| macOS ARM64 | `osx-arm64` |

Upstream baseline: `biscuit-auth 6.0.0` (commit `0f0b4e0`), Biscuit Datalog 3.3,
Ed25519 + P-256 signatures, ABI 1. Older OS baselines and other architectures are
not qualified until built and exercised. See [docs/verification.md](docs/verification.md)
for executed release evidence.

## Learn more

- [Documentation index](docs/README.md): architecture, native boundary, and contributor material.
- [Implementation plan](docs/implementation-plan.md): milestones, matrices, and acceptance gates.
- [API contract](docs/api-contract.md): result, exception, and compatibility behavior.
- [Security](docs/security.md): invariants, privacy defaults, and disclosure.
- [Roadmap](ROADMAP.md) and [changelog](CHANGELOG.md).

## License and attribution

BiscuitSharp is an independent wrapper, unaffiliated with the Eclipse Biscuit project. The Biscuit engine and format belong to that project and its contributors. BiscuitSharp and upstream biscuit-auth use [Apache License 2.0](LICENSE); see [NOTICE](NOTICE) for attribution.
