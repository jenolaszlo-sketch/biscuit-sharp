using System.Text;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// Biscuit private key over an opaque native handle. The secret itself lives in
/// the native key store; managed code only holds the handle id. Disposal
/// destroys the native key as far as the underlying implementation permits
/// (upstream zeroization-on-drop applies where implemented; this does not
/// protect against process dumps or a compromised host).
/// <see cref="ToString"/> never reveals key material.
/// </summary>
public sealed class BiscuitPrivateKey : IDisposable
{
    private ulong _handle; // 0 = destroyed
    private int _disposed; // 0/1 via Interlocked

    public BiscuitPublicKey PublicKey { get; }

    public BiscuitKeyAlgorithm Algorithm { get; }

    private BiscuitPrivateKey(ulong handle, BiscuitPublicKey publicKey, BiscuitKeyAlgorithm algorithm)
    {
        _handle = handle;
        PublicKey = publicKey;
        Algorithm = algorithm;
    }

    ~BiscuitPrivateKey() => DestroyHandle(throwOnError: false);

    public static BiscuitPrivateKey Generate(BiscuitKeyAlgorithm algorithm = BiscuitKeyAlgorithm.Ed25519)
    {
        byte[] response = NativeBridge.Call(
            NativeBridge.OpKeyGenerate,
            BridgeJson.EncodeAlgorithm(BiscuitAlgorithms.ToWireName(algorithm)),
            MapKeyError);
        using JsonDocument doc = BridgeJson.Parse(response, "key_generate");
        return FromKeyResponse(doc.RootElement, "key_generate", algorithm);
    }

    /// <summary>
    /// Imports PKCS#8 PEM (detected by its <c>-----BEGIN</c> armor) or PKCS#8
    /// DER otherwise. The algorithm is auto-detected by upstream in both cases;
    /// raw fixed-size secrets are ambiguous across algorithms and are rejected
    /// with a clear error — wrap them in DER first.
    /// </summary>
    public static BiscuitPrivateKey Import(ReadOnlySpan<byte> encoded)
    {
        if (encoded.IsEmpty)
        {
            throw new BiscuitKeyException("Cannot import an empty key encoding.");
        }

        byte[] request = LooksLikePem(encoded)
            ? BridgeJson.EncodeKeyImportPem(Encoding.UTF8.GetString(encoded))
            : BridgeJson.EncodeKeyImportDer(encoded);
        byte[] response = NativeBridge.Call(NativeBridge.OpKeyImport, request, MapKeyError);
        using JsonDocument doc = BridgeJson.Parse(response, "key_import");
        return FromKeyResponse(doc.RootElement, "key_import");
    }

    /// <summary>
    /// Explicit export of private key material as PKCS#8 DER (self-describing,
    /// so <see cref="Import"/> round-trips it without an out-of-band algorithm).
    /// Zero the returned array when done; normal issuance should prefer opaque
    /// handles instead of repeatedly copying secrets through managed memory.
    /// </summary>
    public byte[] Export()
    {
        ulong handle = RequireHandle();
        byte[] response = NativeBridge.Call(
            NativeBridge.OpKeyExportPrivate,
            BridgeJson.EncodeHandle(handle),
            MapKeyError);
        using JsonDocument doc = BridgeJson.Parse(response, "key_export_private");
        return BridgeJson.RequiredBase64(doc.RootElement, "private_key", "key_export_private");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            DestroyHandle(throwOnError: true);
            GC.SuppressFinalize(this);
        }
    }

    public override string ToString() => $"BiscuitPrivateKey {{ Algorithm = {Algorithm} }}";

    internal ulong NativeHandle => RequireHandle();

    private ulong RequireHandle()
    {
        ulong handle = _handle;
        if (handle == 0 || Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(BiscuitPrivateKey));
        }

        return handle;
    }

    private void DestroyHandle(bool throwOnError)
    {
        ulong handle = Interlocked.Exchange(ref _handle, 0ul);
        if (handle == 0)
        {
            return;
        }

        try
        {
            NativeBridge.Call(NativeBridge.OpKeyDestroy, BridgeJson.EncodeHandle(handle), MapKeyError);
        }
        catch (Exception) when (!throwOnError)
        {
        }
    }

    private static BiscuitPrivateKey FromKeyResponse(
        JsonElement root,
        string operation,
        BiscuitKeyAlgorithm? expected = null)
    {
        ulong handle = BridgeJson.RequiredUInt64(root, "handle", operation);
        if (handle == 0)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response reported an invalid zero handle.");
        }

        BiscuitKeyAlgorithm algorithm =
            BiscuitAlgorithms.FromWireName(BridgeJson.RequiredString(root, "algorithm", operation), operation);
        if (expected.HasValue && algorithm != expected.Value)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response claimed {algorithm} for a {expected.Value} request.");
        }

        byte[] publicKey = BridgeJson.RequiredBase64(root, "public_key", operation);
        return new BiscuitPrivateKey(handle, new BiscuitPublicKey(publicKey, algorithm), algorithm);
    }

    private static bool LooksLikePem(ReadOnlySpan<byte> encoded) =>
        encoded.IndexOf("-----BEGIN"u8) >= 0;

    private static BiscuitException MapKeyError(uint status, string code, string message) =>
        (status == 3 || code is "panic" or "oversized_output" or "unsupported_operation")
            ? new BiscuitBridgeException(
                $"Biscuit native key call failed with status {status} ({code}: {message}).")
            : new BiscuitKeyException($"Biscuit key operation failed ({code}): {message}.");
}

/// <summary>Public half of a Biscuit keypair. Safe to log and share.</summary>
/// <param name="Encoded">Raw upstream public-key bytes, interpreted with <paramref name="Algorithm"/>.</param>
/// <param name="Algorithm">Signature algorithm.</param>
public sealed record BiscuitPublicKey(byte[] Encoded, BiscuitKeyAlgorithm Algorithm);
