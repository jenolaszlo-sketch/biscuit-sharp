using System.Runtime.InteropServices;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// Loads the native asset once per process and retains it for process lifetime,
/// so concurrent calls cannot race library unloading. No arbitrary PATH probing,
/// no network download. The BISCUITSHARP_NATIVE_PATH override selects a
/// self-built or vendored asset; it is still required to exist and pass the ABI
/// check. Adjacent manifests bind the staged hash and live identity.
/// </summary>
internal static class NativeLoader
{
    private static readonly object Sync = new();
    private static IntPtr _handle;
    private static string? _loadedPath;
    private static string? _loadedSha256;
    internal static Action? BeforeVerificationForTesting { get; set; }

    internal static string LoadedSha256
    {
        get { EnsureLoaded(); return _loadedSha256!; }
    }

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
        Architecture processArchitecture = RuntimeInformation.ProcessArchitecture;
        string? rid = SelectRuntimeIdentifier(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
            processArchitecture);
        if (rid is not null)
        {
            return rid;
        }

        throw new BiscuitBridgeException(
            $"Unsupported runtime: {RuntimeInformation.OSDescription} {processArchitecture}. " +
            "Qualified RIDs: win-x64, linux-x64, osx-arm64.");
    }

    // ProcessArchitecture describes the architecture of this .NET process,
    // which determines which native asset it can load. OSArchitecture may be
    // wider when a 32-bit process runs under emulation on a 64-bit OS.
    internal static string? SelectRuntimeIdentifier(
        bool isWindows,
        bool isLinux,
        bool isOSX,
        Architecture processArchitecture)
    {
        if (isWindows && processArchitecture == Architecture.X64)
        {
            return "win-x64";
        }

        if (isLinux && processArchitecture == Architecture.X64)
        {
            return "linux-x64";
        }

        if (isOSX && processArchitecture == Architecture.Arm64)
        {
            return "osx-arm64";
        }

        return null;
    }

    internal static IntPtr GetExport(string name)
    {
        lock (Sync)
        {
            EnsureLoaded();
            try { return NativeLibrary.GetExport(_handle, name); }
            catch (Exception ex) { throw new BiscuitBridgeException($"Native export '{name}' is unavailable.", ex); }
        }
    }

    internal static void EnsureLoaded()
    {
        // All callers acquire the lock, including during provisional publication.
        // The verification version query may reenter on this same thread.
        lock (Sync)
        {
            if (_handle != IntPtr.Zero)
            {
                return;
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
            try
            {
                _loadedSha256 = BiscuitEngine.HashLoadedFile(path);
                _handle = handle;
                _loadedPath = path;
                BeforeVerificationForTesting?.Invoke();
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
                _loadedSha256 = null;
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
            BridgeJson.RequireObject(root, "native manifest");
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


}
