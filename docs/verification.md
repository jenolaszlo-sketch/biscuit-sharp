# Verification

Executed release evidence. Scaffolding: nothing has been executed yet.

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Not run |
| Scaffolding tests | `dotnet run --project tests/BiscuitSharp.Tests` | Not run |
| Native tests (`--locked`) | `cargo test --locked` in `native/` | Not run (bridge unimplemented) |
| Bidirectional compatibility | Rust↔BiscuitSharp issue/verify/attenuate + spec fixtures | Not run |
| Mutation/fuzz | deterministic mutated inputs per native run | Not run |
| Leak instrumentation | Linux Valgrind issue/parse/attenuate/authorize/dispose cycles | Not run |
| NativeAOT per RID | publish + execute `BiscuitSharp.AotSmoke` on win-x64, linux-x64, osx-arm64 | Not run |
| Package verification | archive inventory/hash/ABI/version/lockfile/licenses | Not run |
| Clean consumers (3 RIDs × 2 TFMs) | isolated-cache `dotnet run` against the nupkg | Not run |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.
