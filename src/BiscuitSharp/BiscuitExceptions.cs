namespace BiscuitSharp;

/// <summary>
/// Base BiscuitSharp exception. All managed Biscuit failures derive from this type.
/// </summary>
public class BiscuitException : Exception
{
    public BiscuitException(string message)
        : base(message)
    {
    }

    public BiscuitException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>Native loading, ABI mismatch, panic status, transport or response-decoding failure.</summary>
public sealed class BiscuitBridgeException : BiscuitException
{
    public BiscuitBridgeException(string message)
        : base(message)
    {
    }

    public BiscuitBridgeException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>Token validity or cryptographic failure (parse/verify/serialize/attenuate/seal).</summary>
public class BiscuitTokenException : BiscuitException
{
    public BiscuitTokenException(string message)
        : base(message)
    {
    }

    public BiscuitTokenException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

public sealed class BiscuitSignatureException : BiscuitTokenException
{
    public BiscuitSignatureException(string message)
        : base(message)
    {
    }

    public BiscuitSignatureException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

public sealed class BiscuitFormatException : BiscuitTokenException
{
    public BiscuitFormatException(string message)
        : base(message)
    {
    }

    public BiscuitFormatException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

public sealed class BiscuitSealedTokenException : BiscuitTokenException
{
    public BiscuitSealedTokenException(string message)
        : base(message)
    {
    }

    public BiscuitSealedTokenException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>Datalog parse/evaluation failure that is not an authorization decision.</summary>
public sealed class BiscuitDatalogException : BiscuitException
{
    public BiscuitDatalogException(string message)
        : base(message)
    {
    }

    public BiscuitDatalogException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}

/// <summary>Enforcement helper failure. An ordinary Deny is a result, not this exception.</summary>
public sealed class BiscuitAuthorizationException : BiscuitException
{
    public BiscuitAuthorizationResult? Result { get; }

    public BiscuitAuthorizationException(string message, BiscuitAuthorizationResult? result = null, Exception? inner = null)
        : base(message, inner)
    {
        Result = result;
    }
}

/// <summary>Key generation, import, or export failure.</summary>
public sealed class BiscuitKeyException : BiscuitException
{
    public BiscuitKeyException(string message)
        : base(message)
    {
    }

    public BiscuitKeyException(string message, Exception? inner)
        : base(message, inner)
    {
    }
}
