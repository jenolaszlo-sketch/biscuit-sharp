# Engineering scripts (all PowerShell 5.1-compatible, ASCII-only)

| Script | Purpose |
| --- | --- |
| `Build-Native.ps1 -Rid <rid>` | Release-triple `cargo build --locked`, stage to `native/staging/<rid>/` with `biscuitsharp-native.json` manifest + licenses |
| `Verify-NativeStaging.ps1 [-Rid <rid>]` | Exact inventory, manifest schema, recomputed hashes, pinned versions, legal notices |
| `Verify-NuGetPackage.ps1 -Package <nupkg> -Rids <rids>` | Archive inventory, per-RID asset/manifest/hash checks, nuspec, symbols package |
| `Test-PackagedConsumer.ps1 -Package <nupkg> -TargetFramework <tfm>` | Isolated-cache external consumer exercising the full credential flow |
| `Test-Compat.ps1` | Bidirectional compatibility gate (unit, gen, fixtures, managed, consume) |
| `Test-Dist.ps1 -Rid <rid>` | Per-RID distribution gate: staging, staging verification, NativeAOT publish + execute |

Still open for M2: `Test-Native.ps1` equivalent per-RID automation beyond what CI
jobs already run, and `Test-NativeMemory.sh` (Valgrind, needs Linux).
