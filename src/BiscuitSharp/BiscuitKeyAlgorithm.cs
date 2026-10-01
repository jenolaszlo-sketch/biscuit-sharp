namespace BiscuitSharp;

/// <summary>Signature algorithm for a Biscuit keypair. Mirrors upstream key types (Ed25519, P-256).</summary>
public enum BiscuitKeyAlgorithm
{
    Ed25519 = 0,
    P256 = 1,
}
