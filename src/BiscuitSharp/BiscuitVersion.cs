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
        using JsonDocument doc = BridgeJson.Parse(json, "version");
        JsonElement root = doc.RootElement;

        string biscuitAuth = BridgeJson.RequiredString(root, "biscuit_auth_version", "version");
        uint minSchema = BridgeJson.RequiredUInt32(root, "min_schema_version", "version");
        uint maxSchema = BridgeJson.RequiredUInt32(root, "max_schema_version", "version");
        string datalog = BridgeJson.RequiredString(root, "datalog", "version");
        string bridge = BridgeJson.RequiredString(root, "bridge_version", "version");
        uint abi = BridgeJson.RequiredUInt32(root, "abi_version", "version");
        string rust = BridgeJson.RequiredString(root, "rust_version", "version");
        string triple = BridgeJson.RequiredString(root, "target_triple", "version");
        string features = BridgeJson.RequiredStringArray(root, "enabled_features", "version");
        string commit = BridgeJson.RequiredString(root, "upstream_commit", "version");
        string lockHash = BridgeJson.RequiredString(root, "cargo_lock_sha256", "version");

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

    internal static string HashLoadedFile(string path)
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
