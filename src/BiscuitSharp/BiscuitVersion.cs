using System.Security.Cryptography;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>Identity of the loaded native asset. Reports the loaded binary, never managed constants alone.</summary>
public sealed record BiscuitSharpVersionInfo(
    string BiscuitAuthVersion,
    string? TokenSpecVersion,
    string BridgeVersion,
    uint AbiVersion,
    string RustVersion,
    string TargetTriple,
    string RuntimeIdentifier,
    string EnabledFeatures,
    string NativeSha256,
    string UpstreamCommit,
    string CargoLockHash);

/// <summary>Entry point for engine-level operations (version discovery).</summary>
public static class BiscuitEngine
{
    public const uint AbiVersion = 1;
    public const string BridgeVersion = "0.1.0";

    /// <summary>
    /// Reports the identity of the loaded native asset: engine, bridge, ABI,
    /// toolchain, target, features, hashes, and upstream pin. Never managed
    /// constants alone (the file hash is computed from the loaded binary).
    /// </summary>
    public static BiscuitSharpVersionInfo GetVersion()
    {
        byte[] json = NativeBridge.Call(NativeBridge.OpVersion, ReadOnlySpan<byte>.Empty);
        using JsonDocument doc = ParseVersionResponse(json);
        JsonElement root = doc.RootElement;

        string biscuitAuth = RequiredString(root, "biscuit_auth_version");
        uint minSchema = RequiredUInt32(root, "min_schema_version");
        uint maxSchema = RequiredUInt32(root, "max_schema_version");
        string datalog = RequiredString(root, "datalog");
        string bridge = RequiredString(root, "bridge_version");
        uint abi = RequiredUInt32(root, "abi_version");
        string rust = RequiredString(root, "rust_version");
        string triple = RequiredString(root, "target_triple");
        string features = RequiredStringArray(root, "enabled_features");
        string commit = RequiredString(root, "upstream_commit");
        string lockHash = RequiredString(root, "cargo_lock_sha256");

        if (abi != AbiVersion)
        {
            throw new BiscuitBridgeException(
                $"Native ABI drift: the version response claims ABI {abi}, expected {AbiVersion}.");
        }

        string path = NativeLoader.LoadedPath;
        string rid = NativeLoader.GetRuntimeIdentifier();
        return new BiscuitSharpVersionInfo(
            biscuitAuth,
            $"schema {minSchema}..{maxSchema} (Datalog {datalog})",
            bridge,
            abi,
            rust,
            triple,
            rid,
            features,
            HashLoadedFile(path),
            commit,
            lockHash);
    }

    private static JsonDocument ParseVersionResponse(byte[] json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new BiscuitBridgeException(
                "Failed to decode the native version response.", ex);
        }
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is string s
            && s.Length != 0)
        {
            return s;
        }

        throw new BiscuitBridgeException(
            $"The native version response is missing required string '{name}'.");
    }

    private static uint RequiredUInt32(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetUInt32(out uint n))
        {
            return n;
        }

        throw new BiscuitBridgeException(
            $"The native version response is missing required number '{name}'.");
    }

    private static string RequiredStringArray(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || item.GetString() is not string s)
                {
                    throw new BiscuitBridgeException(
                        $"The native version response field '{name}' must be an array of strings.");
                }

                items.Add(s);
            }

            return string.Join(",", items);
        }

        throw new BiscuitBridgeException(
            $"The native version response is missing required array '{name}'.");
    }

    private static string HashLoadedFile(string path)
    {
        try
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            throw new BiscuitBridgeException(
                $"Failed to hash the loaded native asset: {path}.", ex);
        }
    }
}
