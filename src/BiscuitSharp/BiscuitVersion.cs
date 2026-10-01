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

    public static BiscuitSharpVersionInfo GetVersion() =>
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
}
