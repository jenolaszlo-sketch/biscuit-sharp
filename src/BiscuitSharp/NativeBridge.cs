using System.Runtime.InteropServices;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// ABI 1 declarations + call helper. Input is borrowed for the call; native
/// output is copied to managed memory and freed exactly once in `finally`,
/// including JSON decoding failure. No raw pointers reach public callers.
/// </summary>
internal static class NativeBridge
{
    internal const uint ExpectedAbiVersion = 1;
    internal const int MaxInputBytes = 16 * 1024 * 1024;
    internal const int MaxOutputBytes = 64 * 1024 * 1024;
    internal const uint OpVersion = 0;
    internal const uint OpKeyGenerate = 1;
    internal const uint OpKeyImport = 2;
    internal const uint OpKeyExportPublic = 3;
    internal const uint OpKeyExportPrivate = 4;
    internal const uint OpKeyDestroy = 13;
    internal const uint OpKeyImportPublic = 14;
    internal const uint OpTokenCreate = 5;
    internal const uint OpTokenParseVerify = 6;
    internal const uint OpTokenAttenuate = 8;
    internal const uint OpTokenSeal = 9;
    internal const uint OpTokenRevocationIds = 11;
    internal const uint OpTokenInspect = 12;
    internal const uint OpTokenAuthorize = 10;

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

    internal static unsafe byte[] Call(
        uint operation,
        ReadOnlySpan<byte> input,
        Func<uint, string, string, BiscuitException>? mapError = null)
    {
        NativeLoader.EnsureLoaded();
        if (input.Length > MaxInputBytes)
        {
            throw new BiscuitBridgeException(
                $"Native input exceeds the {MaxInputBytes}-byte bridge bound.");
        }

        NativeBuffer output = default;
        uint status;
        fixed (byte* p = input)
        {
            status = CallV1(operation, p, (UIntPtr)input.Length, ref output);
        }

        try
        {
            ulong length = output.Length.ToUInt64();
            if (length > (ulong)MaxOutputBytes)
            {
                throw new BiscuitBridgeException(
                    "Native output exceeds the 64 MiB bridge bound.");
            }

            byte[] bytes = (output.Data == IntPtr.Zero || length == 0)
                ? Array.Empty<byte>()
                : new ReadOnlySpan<byte>((void*)output.Data, checked((int)length)).ToArray();

            if (status != 0)
            {
                throw MapError(operation, status, bytes, mapError);
            }

            return bytes;
        }
        finally
        {
            if (output.Data != IntPtr.Zero && output.Length.ToUInt64() != 0)
            {
                FreeV1(output);
            }
        }
    }

    private static BiscuitException MapError(
        uint operation,
        uint status,
        byte[] body,
        Func<uint, string, string, BiscuitException>? mapError)
    {
        (string code, string message) = ReadErrorBody(body);
        if (mapError != null)
        {
            return mapError(status, code, message);
        }

        return new BiscuitBridgeException(
            $"Biscuit native call {operation} failed with status {status} ({code}: {message}).");
    }

    internal static (string Code, string Message) ReadErrorBody(byte[] body)
    {
        if (body.Length == 0)
        {
            return ("?", "empty body");
        }

        using JsonDocument doc = BridgeJson.Parse(body, "error");
        return (
            BridgeJson.RequiredString(doc.RootElement, "code", "error"),
            BridgeJson.RequiredString(doc.RootElement, "message", "error"));
    }

    // ABI 1 (native/src/lib.rs, `extern "C"` == Cdecl on all qualified targets):
    // uint32_t biscuitsharp_abi_version(void);
    // uint32_t biscuitsharp_call_v1(uint32_t op, const uint8_t* input, size_t input_len, BiscuitSharpBuffer* output);
    // void biscuitsharp_free_v1(BiscuitSharpBuffer buffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeBuffer
    {
        public IntPtr Data;
        public UIntPtr Length;
    }

    // Resolve exports from the current handle rather than caching P/Invoke
    // addresses: a rejected provisional load can be freed and retried safely.
    internal static unsafe uint AbiVersion()
    {
        var call = (delegate* unmanaged[Cdecl]<uint>)NativeLoader.GetExport("biscuitsharp_abi_version");
        return call();
    }

    internal static unsafe uint CallV1(uint operation, byte* input, UIntPtr inputLength, ref NativeBuffer output)
    {
        var call = (delegate* unmanaged[Cdecl]<uint, byte*, UIntPtr, NativeBuffer*, uint>)
            NativeLoader.GetExport("biscuitsharp_call_v1");
        fixed (NativeBuffer* buffer = &output)
            return call(operation, input, inputLength, buffer);
    }

    internal static unsafe void FreeV1(NativeBuffer buffer)
    {
        var free = (delegate* unmanaged[Cdecl]<NativeBuffer, void>)NativeLoader.GetExport("biscuitsharp_free_v1");
        free(buffer);
    }
}
