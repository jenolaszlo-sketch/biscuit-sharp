namespace BiscuitSharp;

/// <summary>A single Biscuit block (Datalog source) appended during creation or attenuation.</summary>
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
        return new BiscuitBlock(datalogSource);
    }

    public override string ToString() => $"BiscuitBlock {{ Length = {Source.Length} }}";
}

/// <summary>Revocation identifier for one Biscuit block. Application-managed revocation state lives outside this library.</summary>
/// <param name="Value">Opaque upstream revocation id bytes (base64url-safe rendering via ToString).</param>
public sealed record BiscuitRevocationId(byte[] Value)
{
    public override string ToString() => Convert.ToBase64String(Value);
}

/// <summary>
/// Immutable Biscuit token. Parsing with a root public key verifies the cryptographic token;
/// a valid token is not an authorized request. See <see cref="BiscuitAuthorizer"/>.
/// </summary>
public sealed class BiscuitToken
{
    private readonly byte[] _bytes;

    private BiscuitToken(byte[] bytes)
    {
        _bytes = bytes;
    }

    public static BiscuitToken Parse(ReadOnlySpan<byte> token, BiscuitPublicKey root)
    {
        ArgumentNullException.ThrowIfNull(root);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public static BiscuitToken ParseBase64Url(string token, BiscuitPublicKey root)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(root);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public byte[] ToBytes() => (byte[])_bytes.Clone();

    public string ToBase64Url() => Base64Url.Encode(_bytes);

    public BiscuitToken Attenuate(BiscuitBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public BiscuitToken Seal() =>
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");

    public IReadOnlyList<BiscuitRevocationId> GetRevocationIds() =>
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");

    public BiscuitInspection Inspect() =>
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");

    internal static BiscuitToken FromVerifiedBytes(byte[] bytes) => new(bytes);
}

/// <summary>Fluent builder for token creation. Prefer parameterized overloads over string interpolation of untrusted input.</summary>
public sealed class BiscuitTokenBuilder
{
    private readonly List<string> _facts = new();
    private readonly List<string> _checks = new();

    private BiscuitTokenBuilder()
    {
    }

    public static BiscuitTokenBuilder Create() => new();

    public BiscuitTokenBuilder AddFact(string datalogFact)
    {
        ArgumentNullException.ThrowIfNull(datalogFact);
        _facts.Add(datalogFact);
        return this;
    }

    public BiscuitTokenBuilder AddFact(string template, object parameters)
    {
        // M1: render via upstream-supported parameterization; never interpolate untrusted strings.
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }

    public BiscuitTokenBuilder AddCheck(string datalogCheck)
    {
        ArgumentNullException.ThrowIfNull(datalogCheck);
        _checks.Add(datalogCheck);
        return this;
    }

    public BiscuitToken Build(BiscuitPrivateKey rootKey)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
    }
}
