using System.Text.Json;

namespace BiscuitSharp;

/// <summary>A single Biscuit block (Datalog source) appended during attenuation.</summary>
public sealed class BiscuitBlock
{
    public string Source { get; }

    private BiscuitBlock(string source)
    {
        Source = source;
    }

    public static BiscuitBlock Create(string datalogSource)
    {
        ArgumentNullException.ThrowIfNull(datalogSource);
        if (string.IsNullOrWhiteSpace(datalogSource))
        {
            throw new ArgumentException("Datalog block source must not be empty.", nameof(datalogSource));
        }

        return new BiscuitBlock(datalogSource);
    }

    public override string ToString() => $"BiscuitBlock {{ Length = {Source.Length} }}";
}

/// <summary>
/// Revocation identifier for one Biscuit block. Application-managed revocation
/// state lives outside this library. Value equality is over the opaque bytes
/// (not array identity), so identifiers from separate parses of the same token
/// are equal — required for revocation lookups keyed by these ids.
/// </summary>
public sealed class BiscuitRevocationId : IEquatable<BiscuitRevocationId>
{
    /// <summary>A defensive copy of opaque upstream revocation id bytes.</summary>
    private readonly byte[] _value;
    public byte[] Value => (byte[])_value.Clone();

    public BiscuitRevocationId(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = (byte[])value.Clone();
    }

    public bool Equals(BiscuitRevocationId? other) =>
        other is not null && _value.AsSpan().SequenceEqual(other._value);

    public override bool Equals(object? obj) => Equals(obj as BiscuitRevocationId);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_value);
        return hash.ToHashCode();
    }

    public static bool operator ==(BiscuitRevocationId? left, BiscuitRevocationId? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BiscuitRevocationId? left, BiscuitRevocationId? right) => !(left == right);

    public override string ToString() => Convert.ToBase64String(_value);
}

/// <summary>
/// Immutable Biscuit token holding canonical upstream-serialized bytes plus the
/// root public key it verified against. Parsing verifies the cryptographic
/// token; a valid token is still not an authorized request.
/// </summary>
public sealed class BiscuitToken : IEquatable<BiscuitToken>
{
    private readonly byte[] _bytes;

    public BiscuitPublicKey Root { get; }

    private BiscuitToken(byte[] bytes, BiscuitPublicKey root)
    {
        _bytes = bytes;
        Root = root;
    }

    public static BiscuitToken Parse(ReadOnlySpan<byte> token, BiscuitPublicKey root)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (token.IsEmpty)
        {
            throw new BiscuitFormatException("Cannot parse an empty token.");
        }

        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenParseVerify,
            BridgeJson.EncodeTokenRoot(token.ToArray(), root),
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_parse_verify");
        return new BiscuitToken(
            BridgeJson.RequiredBase64(doc.RootElement, "token", "token_parse_verify"),
            root);
    }

    public static BiscuitToken ParseBase64Url(string token, BiscuitPublicKey root)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(root);
        return Parse(Base64Url.Decode(token), root);
    }

    public byte[] ToBytes() => (byte[])_bytes.Clone();

    public string ToBase64Url() => Base64Url.Encode(_bytes);

    public BiscuitToken Attenuate(BiscuitBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenAttenuate,
            BridgeJson.EncodeTokenAttenuate(_bytes, Root, block.Source),
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_attenuate");
        return new BiscuitToken(
            BridgeJson.RequiredBase64(doc.RootElement, "token", "token_attenuate"),
            Root);
    }

    /// <summary>
    /// Seals the token: the result cannot be further attenuated. Sealing a
    /// sealed token throws <see cref="BiscuitSealedTokenException"/>.
    /// </summary>
    public BiscuitToken Seal()
    {
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenSeal,
            BridgeJson.EncodeTokenRoot(_bytes, Root),
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_seal");
        return new BiscuitToken(
            BridgeJson.RequiredBase64(doc.RootElement, "token", "token_seal"),
            Root);
    }

    public IReadOnlyList<BiscuitRevocationId> GetRevocationIds()
    {
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenRevocationIds,
            BridgeJson.EncodeTokenRoot(_bytes, Root),
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_revocation_ids");
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("revocation_ids", out JsonElement ids)
            || ids.ValueKind != JsonValueKind.Array)
        {
            throw new BiscuitBridgeException(
                "The native token_revocation_ids response is missing required array 'revocation_ids'.");
        }

        var result = new List<BiscuitRevocationId>();
        foreach (JsonElement id in ids.EnumerateArray())
        {
            if (id.ValueKind != JsonValueKind.String || id.GetString() is not string s)
            {
                throw new BiscuitBridgeException(
                    "The native token_revocation_ids response must be an array of base64 strings.");
            }

            try
            {
                result.Add(new BiscuitRevocationId(Convert.FromBase64String(s)));
            }
            catch (FormatException ex)
            {
                throw new BiscuitBridgeException(
                    "The native token_revocation_ids response contains invalid base64.", ex);
            }
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Safe structural inspection after verification. Reports block count,
    /// sealed state, the verified root's algorithms, revocation IDs, printed
    /// block sources, token size, and the highest block schema version.
    /// Never private signing material.
    /// </summary>
    public BiscuitInspection Inspect()
    {
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenInspect,
            BridgeJson.EncodeTokenRoot(_bytes, Root),
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_inspect");
        return ParseInspection(doc.RootElement);
    }

    internal static BiscuitInspection ParseInspection(JsonElement root)
    {
        const string operation = "token_inspect";
        if (root.ValueKind != JsonValueKind.Object)
            throw new BiscuitBridgeException("The native token_inspect response must be an object.");

        uint countValue = BridgeJson.RequiredUInt32(root, "block_count", operation);
        if (countValue > int.MaxValue)
            throw new BiscuitBridgeException("The native token_inspect block_count exceeds the managed range.");
        int blockCount = (int)countValue;
        bool isSealed = BridgeJson.RequiredBoolean(root, "is_sealed", operation);
        var signatureAlgorithm = BiscuitAlgorithms.FromWireName(
            BridgeJson.RequiredString(root, "signature_algorithm", operation), operation);
        var rootKeyAlgorithm = BiscuitAlgorithms.FromWireName(
            BridgeJson.RequiredString(root, "root_key_algorithm", operation), operation);

        uint? rootKeyId = null;
        if (root.TryGetProperty("root_key_id", out JsonElement keyId))
        {
            if (keyId.ValueKind == JsonValueKind.Number && keyId.TryGetUInt32(out uint id))
            {
                rootKeyId = id;
            }
            else if (keyId.ValueKind != JsonValueKind.Null)
            {
                throw new BiscuitBridgeException(
                    $"The native {operation} response field 'root_key_id' must be a number or null.");
            }
        }

        var revocationIds = new List<BiscuitRevocationId>();
        if (root.TryGetProperty("revocation_ids", out JsonElement ids)
            && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement id in ids.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.String || id.GetString() is not string s)
                {
                    throw new BiscuitBridgeException(
                        $"The native {operation} response revocation IDs must be base64 strings.");
                }

                try
                {
                    revocationIds.Add(new BiscuitRevocationId(Convert.FromBase64String(s)));
                }
                catch (FormatException ex)
                {
                    throw new BiscuitBridgeException(
                        $"The native {operation} response contains invalid base64 revocation IDs.", ex);
                }
            }
        }
        else
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response is missing required array 'revocation_ids'.");
        }

        if (revocationIds.Count != blockCount)
            throw new BiscuitBridgeException("The native token_inspect revocation_ids length does not match block_count.");

        var sources = RequiredStringList(root, "block_sources", operation);
        if (sources.Count != blockCount)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response block_sources length does not match block_count.");
        }

        uint maxVersion = 0;
        if (root.TryGetProperty("block_versions", out JsonElement versions)
            && versions.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            foreach (JsonElement version in versions.EnumerateArray())
            {
                count++;
                if (version.ValueKind != JsonValueKind.Number || !version.TryGetUInt32(out uint v))
                {
                    throw new BiscuitBridgeException(
                        $"The native {operation} response block_versions must be numbers.");
                }

                maxVersion = Math.Max(maxVersion, v);
            }

            if (count != blockCount)
            {
                throw new BiscuitBridgeException(
                    $"The native {operation} response block_versions length does not match block_count.");
            }
        }
        else
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response is missing required array 'block_versions'.");
        }

        ulong sizeValue = BridgeJson.RequiredUInt64(root, "token_size", operation);
        if (sizeValue > long.MaxValue)
            throw new BiscuitBridgeException("The native token_inspect token_size exceeds the managed range.");
        long tokenSize = (long)sizeValue;

        return new BiscuitInspection(
            blockCount,
            isSealed,
            signatureAlgorithm,
            rootKeyAlgorithm,
            revocationIds,
            sources,
            tokenSize,
            maxVersion.ToString(),
            rootKeyId);
    }

    public bool Equals(BiscuitToken? other) =>
        other is not null && _bytes.SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => Equals(obj as BiscuitToken);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    public static bool operator ==(BiscuitToken? left, BiscuitToken? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(BiscuitToken? left, BiscuitToken? right) => !(left == right);

    internal static BiscuitToken FromVerifiedBytes(byte[] bytes, BiscuitPublicKey root) =>
        new(bytes, root);

    private static List<string> RequiredStringList(JsonElement root, string field, string operation)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String || item.GetString() is not string s)
                {
                    throw new BiscuitBridgeException(
                        $"The native {operation} response field '{field}' must be an array of strings.");
                }

                items.Add(s);
            }

            return items;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required array '{field}'.");
    }
}

/// <summary>Maps native token-operation failures to the typed exception taxonomy.</summary>
internal static class BiscuitErrorMapping
{
    // Status 1 carries the operation's own failure codes; any other status is a
    // native/ABI/transport problem regardless of the (possibly empty) body.
    internal static BiscuitException MapKeyError(uint status, string code, string message) =>
        status != 1 || (code != "key_error" && code != "invalid_input")
            ? new BiscuitBridgeException(
                $"Biscuit native key call failed with status {status} ({code}: {message}).")
            : new BiscuitKeyException($"Biscuit key operation failed ({code}): {message}.");

    internal static BiscuitException MapTokenError(uint status, string code, string message) =>
        status != 1
            ? Bridge(status, code, message)
            : code switch
            {
                "signature_error" => new BiscuitSignatureException(
                    $"Biscuit signature verification failed: {message}."),
                "sealed_token" => new BiscuitSealedTokenException(
                    $"Biscuit sealed-token violation: {message}."),
                "format_error" => new BiscuitFormatException(
                    $"Malformed Biscuit token or encoding: {message}."),
                "datalog_error" => new BiscuitDatalogException(
                    $"Invalid Biscuit Datalog: {message}."),
                "token_error" or "invalid_input" => new BiscuitTokenException(
                    $"Biscuit token operation failed ({code}): {message}."),
                _ => Bridge(status, code, message),
            };

    private static BiscuitBridgeException Bridge(uint status, string code, string message) =>
        new($"Biscuit native token call failed with status {status} ({code}: {message}).");
}
