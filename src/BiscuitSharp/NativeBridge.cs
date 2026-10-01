using System.Runtime.InteropServices;

namespace BiscuitSharp;

/// <summary>
/// Native loader + ABI declarations (ABI 1). Scaffolding only: resolves the asset path
/// and verifies identity in M1/M2. No arbitrary PATH probing, no network download.
/// Honors BISCUITSHARP_NATIVE_PATH override subject to manifest/hash/ABI verification.
/// </summary>
internal static partial class NativeBridge
{
    internal const uint ExpectedAbiVersion = 1;
    internal const int MaxInputBytes = 16 * 1024 * 1024;
    internal const int MaxOutputBytes = 64 * 1024 * 1024;

    internal static string GetNativeAssetPath(string rid)
    {
        string? overridePath = Environment.GetEnvironmentVariable("BISCUITSHARP_NATIVE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            string normalized = Path.IsPathRooted(overridePath)
                ? overridePath
                : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, overridePath));
            return normalized;
        }

        string fileName = GetNativeFileName(rid);
        string baseDir = AppContext.BaseDirectory;
        string ridPath = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        if (File.Exists(ridPath))
        {
            return ridPath;
        }

        return Path.Combine(baseDir, fileName);
    }

    internal static string GetNativeFileName(string rid) =>
        rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "biscuitsharp_native.dll" :
        rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase) ? "libbiscuitsharp_native.dylib" :
        "libbiscuitsharp_native.so";

    // ABI 1 declarations (implemented in native/src/lib.rs during M1):
    // uint32_t biscuitsharp_abi_version(void);
    // uint32_t biscuitsharp_call_v1(uint32_t op, const uint8_t* input, size_t input_len, BiscuitSharpBuffer* output);
    // void biscuitsharp_free_v1(BiscuitSharpBuffer buffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeBuffer
    {
        public IntPtr Data;
        public UIntPtr Length;
    }

    [LibraryImport("biscuitsharp_native", EntryPoint = "biscuitsharp_abi_version")]
    internal static partial uint AbiVersion();

    [LibraryImport("biscuitsharp_native", EntryPoint = "biscuitsharp_call_v1")]
    internal static partial uint CallV1(uint operation, in byte input, UIntPtr inputLength, ref NativeBuffer output);

    [LibraryImport("biscuitsharp_native", EntryPoint = "biscuitsharp_free_v1")]
    internal static partial void FreeV1(NativeBuffer buffer);
}
