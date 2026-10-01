using System.Reflection;
using System.Runtime.InteropServices;

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

            // Publish the handle before the ABI probe so the resolver serves it.
            _handle = handle;
            uint abi;
            try
            {
                abi = NativeBridge.AbiVersion();
            }
            catch (Exception ex)
            {
                NativeLibrary.Free(handle);
                _handle = IntPtr.Zero;
                throw new BiscuitBridgeException(
                    $"Failed to query the Biscuit native ABI version: {path}.", ex);
            }

            if (abi != NativeBridge.ExpectedAbiVersion)
            {
                NativeLibrary.Free(handle);
                _handle = IntPtr.Zero;
                throw new BiscuitBridgeException(
                    $"Biscuit native ABI mismatch: expected {NativeBridge.ExpectedAbiVersion}, got {abi} ({path}).");
            }

            _loadedPath = path;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        libraryName == "biscuitsharp_native" ? _handle : IntPtr.Zero;
}
