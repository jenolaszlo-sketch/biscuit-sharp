namespace BiscuitSharp;

/// <summary>
/// Biscuit private key. Backed by an opaque native handle after M1; until then a managed placeholder.
/// Disposal zeroes native key material as far as the underlying implementation permits.
/// ToString never reveals private material. This does not protect against process dumps
/// or a compromised host.
/// </summary>
public sealed class BiscuitPrivateKey : IDisposable
{
    private bool _disposed;

    public BiscuitPublicKey PublicKey { get; }

    public BiscuitKeyAlgorithm Algorithm { get; }

    private BiscuitPrivateKey(BiscuitPublicKey publicKey, BiscuitKeyAlgorithm algorithm)
    {
        PublicKey = publicKey;
        Algorithm = algorithm;
    }

    public static BiscuitPrivateKey Generate(BiscuitKeyAlgorithm algorithm = BiscuitKeyAlgorithm.Ed25519)
    {
        // M1: delegate to native key_generate. Scaffolding throws until the bridge lands.
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public static BiscuitPrivateKey Import(ReadOnlySpan<byte> encoded)
    {
        // M1: delegate to native key_import (supports upstream PEM/DER + raw encodings per 6.0.0).
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    /// <summary>Explicit export of private key material. Normal issuance should prefer opaque handles.</summary>
    public byte[] Export()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // M1: zero/free native key material via the bridge dispose operation.
        GC.SuppressFinalize(this);
    }

    public override string ToString() => $"BiscuitPrivateKey {{ Algorithm = {Algorithm} }}";
}

/// <summary>Public half of a Biscuit keypair. Safe to log and share.</summary>
/// <param name="Encoded">Algorithm-prefixed upstream key encoding.</param>
/// <param name="Algorithm">Signature algorithm.</param>
public sealed record BiscuitPublicKey(byte[] Encoded, BiscuitKeyAlgorithm Algorithm);
