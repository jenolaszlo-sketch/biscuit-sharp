# Deployment

Target deployment contract for BiscuitSharp packages (M2 gate; nothing below
is qualified until it is built and exercised per the verification ledger).

## Qualified environments (target)

| Environment | Target framework | Native asset |
| --- | --- | --- |
| Windows x64 | `net8.0`, `net10.0` | `win-x64` |
| Linux x64 (glibc) | `net8.0`, `net10.0` | `linux-x64` |
| macOS ARM64 | `net8.0`, `net10.0` | `osx-arm64` |

Older OS baselines, musl, `osx-x64`, `win-arm64`, and `linux-arm64` are not
qualified until their exact packages are built and exercised — no support is
extrapolated from upstream platform coverage.

## Package layout (target)

```
lib/net8.0/BiscuitSharp.dll
lib/net10.0/BiscuitSharp.dll
runtimes/win-x64/native/...
runtimes/linux-x64/native/...
runtimes/osx-arm64/native/...
```

plus native manifests (`biscuitsharp-native.json` per RID), XML documentation,
README, LICENSE, NOTICE, third-party notices, and a symbols package.
Packaging stays opt-in (`-p:BiscuitSharpEnablePack=true`) until the M2
distribution matrix passes; publication is a separate manual action.

## Native loading

The loader resolves the asset under `AppContext.BaseDirectory`
(package-adjacent or `runtimes/<rid>/native/`). No `PATH` probing, no network
download. `BISCUITSHARP_NATIVE_PATH` selects a self-built or vendored asset;
relative paths resolve against the current directory, and the asset must still
exist and pass the ABI check (M2 staging adds manifest/hash verification).

## Trimming and NativeAOT

The library targets `IsAotCompatible` with trim- and AOT-analyzer-clean code:
explicit `Utf8JsonWriter`/`JsonDocument` paths and annotated
`RequiresUnreferencedCode`/`RequiresDynamicCode` reflection conveniences
(currently: `BiscuitTokenBuilder.AddFact(string, object)` — prefer the
dictionary overload for trimming and NativeAOT). A NativeAOT sample
(`samples/BiscuitSharp.AotSmoke`) must be published, executed, and verified on
every qualified RID in CI before any AOT support claim. Single-file publishing
is unqualified until exercised separately.
