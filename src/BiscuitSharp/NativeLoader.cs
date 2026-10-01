using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// Loads the native asset once per process and retains it for process lifetime,
/// so concurrent calls cannot race library unloading. No arbitrary PATH probing,
/// no network download. The BISCUITSHARP_NATIVE_PATH override selects a
/// self-built or vendored asset; it is still required to exist and pass the ABI
/// check. Manifest/hash verification arrives with M2 staging.
/// </summary>
internal static class NativeLoader
{
    private static readonly object Sync = new();
    private static IntPtr _handle;
    private static string? _loadedPath;
    private static bool _resolverSet;

    internal static string LoadedPath
    {
        get
        {
            EnsureLoaded();
            return _loadedPath!;
        }
    }

    internal static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            return "win-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            return "linux-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            && RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            return "osx-arm64";
        }

        throw new BiscuitBridgeException(
            $"Unsupported runtime: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}. " +
            "Qualified RIDs: win-x64, linux-x64, osx-arm64.");
    }

    internal static void EnsureLoaded()
    {
        if (_handle != IntPtr.Zero)
        {
            return;
        }

        lock (Sync)
        {
            if (_handle != IntPtr.Zero)
            {
                return;
            }

            if (!_resolverSet)
            {
                NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, Resolve);
                _resolverSet = true;
            }

            string rid = GetRuntimeIdentifier();
            string path = NativeBridge.GetNativeAssetPath(rid);
            if (!File.Exists(path))
            {
                throw new BiscuitBridgeException(
                    $"Biscuit native asset not found: {path} (RID {rid}). " +
                    "Set BISCUITSHARP_NATIVE_PATH to a built bridge for local development.");
            }

            IntPtr handle;
            try
            {
                handle = NativeLibrary.Load(path);
            }
            catch (Exception ex)
            {
                throw new BiscuitBridgeException(
                    $"Failed to load the Biscuit native asset: {path}.", ex);
            }

            // Publish the handle (and path, so the version query inside manifest
            // verification can resolve it) before probing; every failure below
            // frees the handle and resets both, so a failed load never leaves a
            // retained library behind. The lock is held throughout, so no other
            // thread can observe the half-verified state.
            _handle = handle;
            _loadedPath = path;
            try
            {
                uint abi;
                try
                {
                    abi = NativeBridge.AbiVersion();
                }
                catch (Exception ex)
                {
                    throw new BiscuitBridgeException(
                        $"Failed to query the Biscuit native ABI version: {path}.", ex);
                }

                if (abi != NativeBridge.ExpectedAbiVersion)
                {
                    throw new BiscuitBridgeException(
                        $"Biscuit native ABI mismatch: expected {NativeBridge.ExpectedAbiVersion}, got {abi} ({path}).");
                }

                VerifyManifestIfPresent(path);
            }
            catch
            {
                NativeLibrary.Free(handle);
                _handle = IntPtr.Zero;
                _loadedPath = null;
                throw;
            }
        }
    }

    /// <summary>
    /// Verifies the adjacent <c>biscuitsharp-native.json</c> manifest when one
    /// is staged next to the asset: the file hash must match, and the live
    /// version identity reported by the loaded binary must match the manifest.
    /// Absent manifests (local dev loop) keep the ABI-check-only behavior.
    /// </summary>
    private static void VerifyManifestIfPresent(string assetPath)
    {
        string? directory = Path.GetDirectoryName(assetPath);
        if (directory is null)
        {
            return;
        }

        string manifestPath = Path.Combine(directory, "biscuitsharp-native.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        JsonDocument manifest;
        try
        {
            manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
        {
            throw new BiscuitBridgeException($"Failed to read native manifest: {manifestPath}.", ex);
        }

        using (manifest)
        {
            JsonElement root = manifest.RootElement;
            string expectedHash = ManifestString(root, manifestPath, "binary_sha256");
            string actualHash = BiscuitEngine.HashLoadedFile(assetPath);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new BiscuitBridgeException(
                    $"Native manifest hash mismatch for {assetPath}: manifest expects {expectedHash}.");
            }

            BiscuitSharpVersionInfo live = BiscuitEngine.GetVersion();
            ManifestStringEquals(root, manifestPath, "biscuit_auth", live.BiscuitAuthVersion);
            ManifestStringEquals(root, manifestPath, "bridge", live.BridgeVersion);
            ManifestStringEquals(root, manifestPath, "upstream_commit", live.UpstreamCommit);
            ManifestStringEquals(root, manifestPath, "cargo_lock_sha256", live.CargoLockHash);
            ManifestStringEquals(root, manifestPath, "target", live.TargetTriple);
            ManifestStringEquals(root, manifestPath, "rid", GetRuntimeIdentifier());
            ManifestStringEquals(root, manifestPath, "rust", live.RustVersion);
            if (!root.TryGetProperty("abi", out JsonElement abi)
                || abi.ValueKind != JsonValueKind.Number
                || !abi.TryGetUInt32(out uint manifestAbi)
                || manifestAbi != live.AbiVersion
                || manifestAbi != NativeBridge.ExpectedAbiVersion)
            {
                throw new BiscuitBridgeException(
                    $"Native manifest ABI mismatch for {assetPath} (manifest: {manifestPath}).");
            }

            if (root.TryGetProperty("enabled_features", out JsonElement features)
                && features.ValueKind == JsonValueKind.Array)
            {
                var names = new List<string>();
                foreach (JsonElement feature in features.EnumerateArray())
                {
                    if (feature.ValueKind != JsonValueKind.String || feature.GetString() is not string name)
                    {
                        throw new BiscuitBridgeException(
                            $"Native manifest field 'enabled_features' must be an array of strings ({manifestPath}).");
                    }

                    names.Add(name);
                }

                if (string.Join(",", names) != live.EnabledFeatures)
                {
                    throw new BiscuitBridgeException(
                        $"Native manifest feature mismatch for {assetPath} (manifest: {manifestPath}).");
                }
            }
            else
            {
                throw new BiscuitBridgeException(
                    $"Native manifest is missing required array 'enabled_features' ({manifestPath}).");
            }
        }
    }

    private static string ManifestString(JsonElement root, string manifestPath, string field)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is string s
            && s.Length != 0)
        {
            return s;
        }

        throw new BiscuitBridgeException(
            $"Native manifest is missing required string '{field}' ({manifestPath}).");
    }

    private static void ManifestStringEquals(JsonElement root, string manifestPath, string field, string actual)
    {
        if (ManifestString(root, manifestPath, field) != actual)
        {
            throw new BiscuitBridgeException(
                $"Native manifest field '{field}' does not match the loaded asset ({manifestPath}).");
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == "biscuitsharp_native" ? _handle : IntPtr.Zero;
}
