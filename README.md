# BiscuitSharp

[![License](https://img.shields.io/github/license/jenolaszlo-sketch/biscuit-sharp)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

**Portable, attenuable authority credentials for .NET, verified by the official Rust Biscuit engine.** BiscuitSharp calls [biscuit-auth](https://github.com/eclipse-biscuit/biscuit-rust) in process and gives C# code typed keys, tokens, attenuation, sealing, authorization decisions, revocation identifiers, and inspection. Issue a token with Datalog facts, attenuate it offline for a narrower context, and verify it anywhere with the root public key.

It is useful when authority must travel with the request: delegated access, offline attenuation, and capability-style checks that remain verifiable without a central policy call. BiscuitSharp preserves Biscuit semantics and keeps valid-token, authorized-request, and failure states distinct so the application can enforce explicitly.

> Status: [0.1.0-preview.1 is published](https://www.nuget.org/packages/BiscuitSharp/0.1.0-preview.1).
> The complete 19-job release matrix and six NuGet.org consumers passed on
> win-x64, linux-x64 and osx-arm64 (.NET 8/10), including package identity checks.
> NativeAOT, full legal inventories and the 50-cycle Valgrind probe are qualified.
> The published API baseline is restored; development is at 0.1.0-preview.2.
> See [verification](docs/verification.md), [roadmap](ROADMAP.md) and the
> checked [public API inventory](docs/public-api.txt).

## Try it

Install the published preview:

```sh
dotnet add package BiscuitSharp --version 0.1.0-preview.1
```

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

Development preview.2 adds typed evaluation failure reasons on authorization
findings: fact, iteration and time exhaustion are distinct from expression,
query and other errors. See the [API contract](docs/api-contract.md#typed-evaluation-failures--development-preview2)
for classification; published preview.1 has generic evaluation_failure only.

A verifier holding only the root public key needs no private material:

```csharp
using BiscuitSharp;

BiscuitPublicKey root = BiscuitPublicKey.ParsePrefixed("ed25519/<hex of the root public key>");
BiscuitToken token = BiscuitToken.Parse(tokenBytes, root);
```

## What you get

Biscuit behavior, reviewable outcomes, safe credential handling, and
deployment choices as below — verified on Windows x64, Linux x64, and macOS
ARM64 with .NET 8 and .NET 10 (see [verification](docs/verification.md)).

- **Biscuit behavior:** signature verification, block-chain integrity, attenuation that only narrows authority, sealing, and default-deny authorization from the pinned engine.
- **Reviewable outcomes:** typed decisions, structured authorization errors, inspection (block count, sealed state, algorithms, revocation IDs, block source), and loaded-asset identity.
- **Safe credential handling:** opaque private-key handles with explicit DER/PEM export, `ToString()` that never reveals secrets, value-equal revocation IDs without an implicit revocation store, and an explicit `AddTimeFact` for deterministic expiration checks.
- **Deployment choices:** framework-dependent, self-contained, trimmed, and NativeAOT applications on the [qualified environments](docs/deployment.md).

BiscuitSharp verifies tokens and evaluates authorization. Your application owns grant
issuance policy, revocation storage, workflow revisions, human approval, delegation
policy, auditing, and enforcement at the resource boundary. Revocation intentionally
requires external state; this library exposes the identifiers, Hufu owns the store.
The [Hufu integration specification](docs/hufu-integration-spec.md) defines the
optional adapter profile; its [handoff](docs/hufu-integration-handoff.md) records
implementation prerequisites and acceptance tests. The adapter remains unimplemented.

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
