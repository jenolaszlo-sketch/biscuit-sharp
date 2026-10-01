# Verification

Executed evidence for the M0 baseline and the M1 version/loader slice
(slices 2+: keys, tokens, authorizer — still open).

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Pass 2026-10-01 (0 warnings, 0 errors) |
| M1 version-slice tests | `dotnet run --project tests/BiscuitSharp.Tests` (`net8.0`, `net10.0`) | Pass 2026-10-01 (20/20 each): live identity from the real `cdylib`, differential SHA-256 checks of the loaded binary and `Cargo.lock`, loader fail-closed, relative override, pending ops fail closed |
| Locked dependency fetch | `cargo fetch --locked` in `native/` (Rust 1.89.0) | Pass 2026-10-01 (94 packages) |
| Native check (`--locked`) | `cargo check --locked` in `native/` (Rust 1.89.0, MSVC) | Pass 2026-10-01 |
| Native link (`--locked`) | `cargo build --locked` in `native/` + `dumpbin /EXPORTS` | Pass 2026-10-01: `biscuitsharp_native.dll` links; exports `biscuitsharp_abi_version`, `biscuitsharp_call_v1`, `biscuitsharp_free_v1` with undecorated C names |
| Native tests (`--locked`) | `cargo test --locked` in `native/` | Pass 2026-10-01 (6/6): version identity, unsupported op envelope, input rejection, null-output safety, oversized rejection, free safety |
| Bidirectional compatibility | Rust↔BiscuitSharp issue/verify/attenuate + spec fixtures | Not run |
| Mutation/fuzz | deterministic mutated inputs per native run | Not run |
| Leak instrumentation | Linux Valgrind issue/parse/attenuate/authorize/dispose cycles | Not run |
| NativeAOT per RID | publish + execute `BiscuitSharp.AotSmoke` on win-x64, linux-x64, osx-arm64 | Not run |
| Package verification | archive inventory/hash/ABI/version/lockfile/licenses | Not run |
| Clean consumers (3 RIDs × 2 TFMs) | isolated-cache `dotnet run` against the nupkg | Not run |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.
