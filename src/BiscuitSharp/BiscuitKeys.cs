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
            BiscuitErrorMapping.MapKeyError);
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
        byte[] response = NativeBridge.Call(NativeBridge.OpKeyImport, request, BiscuitErrorMapping.MapKeyError);
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
            BiscuitErrorMapping.MapKeyError);
        using JsonDocument doc = BridgeJson.Parse(response, "key_export_private");
        return BridgeJson.RequiredBase64(doc.RootElement, "private_key", "key_export_private");
    }

    /// <summary>
    /// Explicit export as a PKCS#8 PEM string (<c>-----BEGIN PRIVATE KEY-----</c>
    /// armored DER), matching upstream's PEM format so <see cref="Import"/>
    /// round-trips it. The returned string holds secret material; do not log it.
    /// </summary>
    public string ExportPem()
    {
        byte[] der = Export();
        try
        {
            string base64 = Convert.ToBase64String(der);
            var sb = new StringBuilder(base64.Length + (base64.Length / 64 * 2) + 64);
            sb.Append("-----BEGIN PRIVATE KEY-----\n");
            for (int i = 0; i < base64.Length; i += 64)
            {
                sb.Append(base64, i, Math.Min(64, base64.Length - i));
                sb.Append('\n');
            }

            sb.Append("-----END PRIVATE KEY-----\n");
            return sb.ToString();
        }
        finally
        {
            Array.Clear(der);
        }
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
            NativeBridge.Call(NativeBridge.OpKeyDestroy, BridgeJson.EncodeHandle(handle), BiscuitErrorMapping.MapKeyError);
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
}

/// <summary>
/// Public half of a Biscuit keypair. Safe to log and share. Value equality is
/// over the algorithm and the encoded bytes (not array identity), so keys
/// rebuilt from the same material compare equal and can key dictionaries.
/// </summary>
public sealed class BiscuitPublicKey : IEquatable<BiscuitPublicKey>
{
    /// <summary>Raw upstream public-key bytes, interpreted with <see cref="Algorithm"/>.</summary>
    public byte[] Encoded { get; }

    public BiscuitKeyAlgorithm Algorithm { get; }

    public BiscuitPublicKey(byte[] encoded, BiscuitKeyAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        Encoded = (byte[])encoded.Clone();
        Algorithm = algorithm;
    }

    public bool Equals(BiscuitPublicKey? other) =>
        other is not null
            && Algorithm == other.Algorithm
            && Encoded.AsSpan().SequenceEqual(other.Encoded);

    public override bool Equals(object? obj) => Equals(obj as BiscuitPublicKey);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add((int)Algorithm);
        hash.AddBytes(Encoded);
        return hash.ToHashCode();
    }

    public static bool operator ==(BiscuitPublicKey? left, BiscuitPublicKey? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BiscuitPublicKey? left, BiscuitPublicKey? right) => !(left == right);

    public override string ToString() =>
        $"BiscuitPublicKey {{ Algorithm = {Algorithm}, Length = {Encoded.Length} }}";

    /// <summary>
    /// Validates raw public-key bytes for the algorithm through the native
    /// bridge (size plus upstream decode) and returns the canonical key. This
    /// is the verification-only entry point: a caller holding only a root
    /// public key validates it here without needing the private half.
    /// </summary>
    public static BiscuitPublicKey Parse(ReadOnlySpan<byte> encoded, BiscuitKeyAlgorithm algorithm)
    {
        if (encoded.IsEmpty)
        {
            throw new BiscuitKeyException("Cannot parse an empty public key.");
        }

        // Copied once: ref-like spans cannot be captured by the JSON writer.
        byte[] bytes = encoded.ToArray();
        byte[] request = BridgeJson.EncodeObject(w =>
        {
            w.WriteString("algorithm", BiscuitAlgorithms.ToWireName(algorithm));
            w.WriteBase64String("public_key", bytes);
        });
        byte[] response = NativeBridge.Call(
            NativeBridge.OpKeyImportPublic,
            request,
            BiscuitErrorMapping.MapKeyError);
        using JsonDocument doc = BridgeJson.Parse(response, "key_import_public");
        JsonElement root = doc.RootElement;
        var parsedAlgorithm = BiscuitAlgorithms.FromWireName(
            BridgeJson.RequiredString(root, "algorithm", "key_import_public"),
            "key_import_public");
        if (parsedAlgorithm != algorithm)
        {
            throw new BiscuitBridgeException(
                $"The native key_import_public response claimed {parsedAlgorithm} for a {algorithm} request.");
        }

        return new BiscuitPublicKey(
            BridgeJson.RequiredBase64(root, "public_key", "key_import_public"),
            parsedAlgorithm);
    }
}
