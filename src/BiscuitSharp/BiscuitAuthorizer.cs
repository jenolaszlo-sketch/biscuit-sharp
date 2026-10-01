namespace BiscuitSharp;

public enum BiscuitDecision
{
    Allow = 0,
    Deny = 1,
}

public sealed record BiscuitAuthorizationError(string Message, string? Code = null);

/// <summary>
/// Authorization outcome. A Deny is an ordinary result, never a bridge failure.
/// No parse/verify/native/serialization failure may surface as <see cref="IsAuthorized"/> true.
/// </summary>
public sealed record BiscuitAuthorizationResult(
    BiscuitDecision Decision,
    IReadOnlyList<BiscuitAuthorizationError> Errors)
{
    public bool IsAuthorized => Decision == BiscuitDecision.Allow && Errors.Count == 0;

    public void RequireAuthorized()
    {
        if (!IsAuthorized)
        {
            throw new BiscuitAuthorizationException(
                $"Biscuit authorization denied (decision {Decision}, {Errors.Count} error(s)).",
                this);
        }
    }
}

/// <summary>Fluent authorizer over one verified token plus ambient facts and policies.</summary>
public sealed class BiscuitAuthorizer
{
    private BiscuitAuthorizer()
    {
    }

    public static BiscuitAuthorizer For(BiscuitToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new BiscuitAuthorizer();
    }

    public BiscuitAuthorizer AddFact(string datalogFact)
    {
        ArgumentNullException.ThrowIfNull(datalogFact);
        return this;
    }

    public BiscuitAuthorizer AddPolicy(string datalogPolicy)
    {
        ArgumentNullException.ThrowIfNull(datalogPolicy);
        return this;
    }

    public BiscuitAuthorizer AddCheck(string datalogCheck)
    {
        ArgumentNullException.ThrowIfNull(datalogCheck);
        return this;
    }

    public BiscuitAuthorizationResult Authorize() =>
        throw new BiscuitBridgeException("Native bridge is not implemented yet (M1). See docs/implementation-plan.md.");
}
