# Verification

Executed evidence for the M0 baseline and the M1 version/loader slice
(slices 2+: keys, tokens, authorizer — still open).

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Pass 2026-10-01 (0 warnings, 0 errors) |
| M1 version+key+token tests | `dotnet run --project tests/BiscuitSharp.Tests` (`net8.0`, `net10.0`) | Pass 2026-10-01 (81/81 each): version identity + differential hashes, loader gates, key round-trips (DER + PEM), token issue/verify/attenuate/seal, tamper/truncation/garbage rejection, revocation growth, inspection, typed params, unicode, disposal, privacy, concurrency |
| Locked dependency fetch | `cargo fetch --locked` in `native/` (Rust 1.89.0) | Pass 2026-10-01 (94 packages) |
| Native check (`--locked`) | `cargo check --locked` in `native/` (Rust 1.89.0, MSVC) | Pass 2026-10-01 |
| Native link (`--locked`) | `cargo build --locked` in `native/` + `dumpbin /EXPORTS` | Pass 2026-10-01: `biscuitsharp_native.dll` links; exports `biscuitsharp_abi_version`, `biscuitsharp_call_v1`, `biscuitsharp_free_v1` with undecorated C names |
| Native tests (`--locked`) | `cargo test --locked` in `native/` | Pass 2026-10-01 (26/26): boundary envelope, key round-trips (differential vs direct upstream) + concurrency, token create/parse canonical round-trips, typed params, tamper/truncation/garbage codes, attenuate/seal semantics (seal adds no block), revocation growth, inspection fields, malformed envelopes, oversized rejection, concurrency |
| Bidirectional compatibility | Rust↔BiscuitSharp issue/verify/attenuate + spec fixtures | Not run |
| Mutation/fuzz | deterministic mutated inputs per native run | Not run |
| Leak instrumentation | Linux Valgrind issue/parse/attenuate/authorize/dispose cycles | Not run |
| NativeAOT per RID | publish + execute `BiscuitSharp.AotSmoke` on win-x64, linux-x64, osx-arm64 | Not run |
| Package verification | archive inventory/hash/ABI/version/lockfile/licenses | Not run |
| Clean consumers (3 RIDs × 2 TFMs) | isolated-cache `dotnet run` against the nupkg | Not run |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.
