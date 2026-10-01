namespace BiscuitSharp;

/// <summary>Signature algorithm for a Biscuit keypair. Mirrors upstream key types (Ed25519, P-256).</summary>
public enum BiscuitKeyAlgorithm
{
    Ed25519 = 0,
    P256 = 1,
}

/// <summary>Upstream wire names for key algorithms ("ed25519", "secp256r1").</summary>
internal static class BiscuitAlgorithms
{
    internal static string ToWireName(BiscuitKeyAlgorithm algorithm) => algorithm switch
    {
        BiscuitKeyAlgorithm.Ed25519 => "ed25519",
        BiscuitKeyAlgorithm.P256 => "secp256r1",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown Biscuit key algorithm."),
    };

    internal static BiscuitKeyAlgorithm FromWireName(string name, string operation) => name switch
    {
        "ed25519" => BiscuitKeyAlgorithm.Ed25519,
        "secp256r1" => BiscuitKeyAlgorithm.P256,
        _ => throw new BiscuitBridgeException(
            $"The native {operation} response reported unknown algorithm '{name}'."),
    };
}
