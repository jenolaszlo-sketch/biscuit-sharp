# Verification

Executed evidence for the M0 baseline, the complete M1 functional surface
(version, keys, tokens, authorization), and the bidirectional compatibility
gate on Windows x64. Open: other RIDs, packaging, hardening.

## Ledger

| Gate | Command | Result |
| --- | --- | --- |
| Managed build (`net8.0;net10.0`) | `dotnet build BiscuitSharp.slnx` | Pass 2026-10-01 (0 warnings, 0 errors) |
| M1 functional tests | `dotnet run --project tests/BiscuitSharp.Tests` (`net8.0`, `net10.0`) | Pass 2026-10-02 (147/147 each on a clean tree; gate runs add 4 generated-exchange and 3 consume checks): version identity + differential hashes, loader gates + manifest probes, key round-trips (DER/PEM export, public/hex/prefixed import), value equality, token issue/verify/attenuate/seal, rules (builder + authorizer), authorizer limits, tamper/truncation/garbage rejection, revocation growth, inspection, typed params, unicode, time-fact expiry, disposal, privacy, allow/deny/failed-checks/explicit-deny, determinism, adversarial sweeps, concurrency |
| Locked dependency fetch | `cargo fetch --locked` in `native/` (Rust 1.89.0) | Pass 2026-10-01 (94 packages) |
| Native check (`--locked`) | `cargo check --locked` in `native/` (Rust 1.89.0, MSVC) | Pass 2026-10-01 |
| Native link (`--locked`) | `cargo build --locked` in `native/` + `dumpbin /EXPORTS` | Pass 2026-10-01: `biscuitsharp_native.dll` links; exports `biscuitsharp_abi_version`, `biscuitsharp_call_v1`, `biscuitsharp_free_v1` with undecorated C names |
| Native tests (`--locked`) | `cargo test --locked --lib` in `native/` | Pass 2026-10-02 (41/41: previous 39 + 2 authorizer-limits tests): boundary envelope, panic containment (`catch_unwind` → status 3, empty body), key round-trips (differential vs direct upstream) + concurrency, token create/parse canonical round-trips, typed params, rules, tamper/truncation/garbage codes, attenuate/seal semantics (seal adds no block), revocation growth, inspection fields, malformed envelopes/limits, oversized rejection, allow/deny/failed-checks/explicit-deny, first-match-wins order, determinism, concurrency, 4,096-case mutation matrix |
| Bidirectional compatibility | `eng/Test-Compat.ps1`: unit → compat_gen → committed fixtures → managed generate → compat_consume → managed consume | Pass 2026-10-02 on Windows x64: all four directions verified + authorized across the boundary; committed genesis fixture verified |
| Windows x64 distribution | `eng/Test-Dist.ps1 -Rid win-x64` (stage, verify staging, AOT) + pack + `eng/Verify-NuGetPackage.ps1` + consumers net8.0/net10.0 | Pass 2026-10-02, local: release asset staged with manifest + licenses; AOT executes; single-RID pack verified; both clean consumers green. Linux/macOS open. |
| Mutation/fuzz | `cargo test --locked --lib adversarial` + managed adversarial section | Pass 2026-10-01 (Windows x64, dev profile): 4,096 deterministic cases (rejected 4074, proved-legitimate 19, denies 0, allows 3 byte-identical, panics 0); managed 96-position token sweep + 7 invalid policies, all fail-closed. Release-profile and all-RID repetition open with M2. |
| Leak instrumentation | Linux Valgrind issue/parse/attenuate/authorize/dispose cycles | Not run |
| NativeAOT (win-x64, local) | `dotnet publish samples/BiscuitSharp.AotSmoke -r win-x64 -c Release` + execute with staged asset | Pass 2026-10-02: NativeAOT exe reports the manifest-verified release bridge (exit 0). Other RIDs open with M2. |
| Package verification (win-x64, local) | `dotnet pack` (local smoke) + archive inventory | Pass 2026-10-02: lib/net8.0+net10.0, runtimes/win-x64/native (dll + manifest), legal notices, README/LICENSE/NOTICE, symbols package. Other RIDs open with M2. |
| Clean consumer (win-x64 × net10, local) | isolated-cache console against the local nupkg | Pass 2026-10-02: generate/import, issue, serialize, parse/verify, attenuate, allow + deny, seal, revocation IDs, version query. Remaining five matrix cells open with M2. |
| Package verification | archive inventory/hash/ABI/version/lockfile/licenses | Not run |
| Clean consumers (3 RIDs × 2 TFMs) | isolated-cache `dotnet run` against the nupkg | Not run |

Record exact commands, commit SHAs, runner versions, and hashes here when each
gate passes. Planned gates are not qualified packages.
