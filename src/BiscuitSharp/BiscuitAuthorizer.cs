using System.Globalization;
using System.Text.Json;

namespace BiscuitSharp;

public enum BiscuitDecision
{
    Allow = 0,
    Deny = 1,
}

/// <summary>One structured authorization finding: a failed check, a matched policy note, or an evaluation failure.</summary>
/// <param name="Message">Human-readable description (upstream pretty-print).</param>
/// <param name="Code">Stable code: <c>failed_check</c>, <c>allow_policy_matched</c>, <c>deny_policy_matched</c>, <c>no_matching_policy</c>, <c>invalid_block_rule</c>, <c>evaluation_failure</c>.</param>
/// <param name="BlockId">Token block owning a failed check, if any.</param>
/// <param name="CheckId">Index of the failed check, if any.</param>
/// <param name="Rule">Pretty-printed rule that failed, if any.</param>
public sealed record BiscuitAuthorizationError(
    string Message,
    string? Code = null,
    uint? BlockId = null,
    uint? CheckId = null,
    string? Rule = null);

/// <summary>
/// Authorization outcome. A Deny is an ordinary result, never a bridge failure.
/// <see cref="IsAuthorized"/> holds only for an error-free Allow; determinism
/// note: upstream evaluates policies first-match-wins in the order supplied.
/// No parse/verify/native/serialization failure may surface as <see cref="IsAuthorized"/> true.
/// </summary>
public sealed record BiscuitAuthorizationResult(
    BiscuitDecision Decision,
    IReadOnlyList<BiscuitAuthorizationError> Errors,
    uint? AllowPolicyIndex = null,
    uint? DenyPolicyIndex = null)
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

/// <summary>
/// Datalog execution limits for one authorization, mirroring upstream
/// <c>RunLimits</c>. A breached limit denies with an
/// <c>evaluation_failure</c> error — it never allows.
/// </summary>
/// <param name="MaxFacts">Maximum Datalog facts (memory usage).</param>
/// <param name="MaxIterations">Maximum rule-application iterations (prevents degenerate rules).</param>
/// <param name="MaxTime">Maximum execution time.</param>
public sealed record BiscuitAuthorizerLimits(ulong MaxFacts, ulong MaxIterations, TimeSpan MaxTime)
{
    /// <summary>
    /// Robust default applied when the caller sets no limits: 100,000 facts,
    /// 100,000 iterations, 5 seconds. Deliberately larger than upstream's
    /// <see cref="UpstreamDefault"/> (1 ms), which is too small to be reliable
    /// under scheduler load and can deny a trivial request with
    /// <c>evaluation_failure</c>. The budget stays bounded for resource
    /// protection. Must stay identical to the bridge fallback in
    /// <c>native/src/authorizer.rs</c> (`default_limits`); both are pinned by
    /// tests on each side.
    /// </summary>
    public static BiscuitAuthorizerLimits Default { get; } =
        new(100_000, 100_000, TimeSpan.FromSeconds(5));

    /// <summary>
    /// Upstream <c>RunLimits::default()</c>: 1,000 facts, 100 iterations,
    /// 1 millisecond. Restore strict parity (and its flakiness risk) with
    /// <see cref="BiscuitAuthorizer.WithLimits"/>.
    /// </summary>
    public static BiscuitAuthorizerLimits UpstreamDefault { get; } =
        new(1000, 100, TimeSpan.FromMilliseconds(1));
}

/// <summary>
/// Fluent authorizer over one verified token plus ambient facts, checks, and
/// policies. Malformed Datalog throws <see cref="BiscuitDatalogException"/>
/// (no evaluation ran); a completed evaluation — allow or deny — returns a
/// <see cref="BiscuitAuthorizationResult"/>. No ambient time fact is injected;
/// time-dependent policies need an explicit <c>time(...)</c> fact.
/// </summary>
public sealed class BiscuitAuthorizer
{
    private readonly BiscuitToken _token;
    private readonly List<string> _facts = new();
    private readonly List<string> _rules = new();
    private readonly List<string> _checks = new();
    private readonly List<string> _policies = new();
    private BiscuitAuthorizerLimits? _limits;

    private BiscuitAuthorizer(BiscuitToken token)
    {
        _token = token;
    }

    public static BiscuitAuthorizer For(BiscuitToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return new BiscuitAuthorizer(token);
    }

    public BiscuitAuthorizer AddFact(string datalogFact)
    {
        ArgumentNullException.ThrowIfNull(datalogFact);
        RejectEmpty(datalogFact, nameof(datalogFact));
        _facts.Add(datalogFact);
        return this;
    }

    public BiscuitAuthorizer AddPolicy(string datalogPolicy)
    {
        ArgumentNullException.ThrowIfNull(datalogPolicy);
        RejectEmpty(datalogPolicy, nameof(datalogPolicy));
        _policies.Add(datalogPolicy);
        return this;
    }

    public BiscuitAuthorizer AddCheck(string datalogCheck)
    {
        ArgumentNullException.ThrowIfNull(datalogCheck);
        RejectEmpty(datalogCheck, nameof(datalogCheck));
        _checks.Add(datalogCheck);
        return this;
    }

    /// <summary>
    /// Adds a Datalog rule (e.g. <c>right("a", "read") &lt;- role("admin");</c>)
    /// to the ambient authorizer scope. Rules derive facts at evaluation time.
    /// </summary>
    public BiscuitAuthorizer AddRule(string datalogRule)
    {
        ArgumentNullException.ThrowIfNull(datalogRule);
        RejectEmpty(datalogRule, nameof(datalogRule));
        _rules.Add(datalogRule);
        return this;
    }

    /// <summary>
    /// Adds an explicit ambient <c>time(...)</c> fact (RFC 3339, UTC) so
    /// expiration checks like <c>check if time($t), $t &lt; 2030-01-01T00:00:00Z;</c>
    /// evaluate deterministically. Time is never injected implicitly.
    /// </summary>
    public BiscuitAuthorizer AddTimeFact(DateTimeOffset value)
    {
        string rfc3339 = value.ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return AddFact($"time({rfc3339})");
    }

    /// <summary>
    /// Overrides the execution limits for this authorizer. Absent limits use
    /// <see cref="BiscuitAuthorizerLimits.Default"/> (a robust budget), not
    /// upstream's 1 ms default; pass
    /// <see cref="BiscuitAuthorizerLimits.UpstreamDefault"/> for strict parity.
    /// </summary>
    public BiscuitAuthorizer WithLimits(BiscuitAuthorizerLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaxTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "MaxTime must not be negative.");
        }

        _limits = limits;
        return this;
    }

    public BiscuitAuthorizationResult Authorize()
    {
        BiscuitAuthorizerLimits limits = _limits ?? BiscuitAuthorizerLimits.Default;
        byte[] request = BridgeJson.EncodeObject(w =>
        {
            w.WriteBase64String("token", _token.ToBytes());
            BridgeJson.WriteRoot(w, _token.Root);
            WriteSources(w, "facts", _facts);
            WriteSources(w, "rules", _rules);
            WriteSources(w, "checks", _checks);
            WriteSources(w, "policies", _policies);
            w.WriteStartObject("limits");
            w.WriteNumber("max_facts", limits.MaxFacts);
            w.WriteNumber("max_iterations", limits.MaxIterations);
            // TimeSpan values fit in ulong milliseconds; checked for safety.
            w.WriteNumber("max_time_ms", checked((ulong)limits.MaxTime.TotalMilliseconds));
            w.WriteEndObject();
        });
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenAuthorize,
            request,
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_authorize");
        return ParseResult(doc.RootElement);
    }

    private static BiscuitAuthorizationResult ParseResult(JsonElement root)
    {
        const string operation = "token_authorize";
        BiscuitDecision decision = BridgeJson.RequiredString(root, "decision", operation) switch
        {
            "allow" => BiscuitDecision.Allow,
            "deny" => BiscuitDecision.Deny,
            string other => throw new BiscuitBridgeException(
                $"The native {operation} response reported unknown decision '{other}'."),
        };

        uint? allowIndex = OptionalUInt32(root, "allow_policy_index", operation);
        uint? denyIndex = OptionalUInt32(root, "deny_policy_index", operation);

        if (!root.TryGetProperty("errors", out JsonElement errors)
            || errors.ValueKind != JsonValueKind.Array)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response is missing required array 'errors'.");
        }

        var findings = new List<BiscuitAuthorizationError>();
        foreach (JsonElement error in errors.EnumerateArray())
        {
            if (error.ValueKind != JsonValueKind.Object)
            {
                throw new BiscuitBridgeException(
                    $"The native {operation} response errors must be objects.");
            }

            findings.Add(new BiscuitAuthorizationError(
                BridgeJson.RequiredString(error, "message", operation),
                OptionalString(error, "code", operation),
                OptionalUInt32(error, "block_id", operation),
                OptionalUInt32(error, "check_id", operation),
                OptionalString(error, "rule", operation)));
        }

        if (decision == BiscuitDecision.Allow && findings.Count != 0)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response contradicts itself: Allow with {findings.Count} error(s).");
        }

        return new BiscuitAuthorizationResult(decision, findings, allowIndex, denyIndex);
    }

    private static uint? OptionalUInt32(JsonElement root, string field, string operation)
    {
        if (!root.TryGetProperty(field, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out uint n))
        {
            return n;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response field '{field}' must be a number or null.");
    }

    private static string? OptionalString(JsonElement root, string field, string operation)
    {
        if (!root.TryGetProperty(field, out JsonElement value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && value.GetString() is string s)
        {
            return s;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response field '{field}' must be a string or null.");
    }

    private static void WriteSources(Utf8JsonWriter writer, string name, List<string> sources)
    {
        writer.WriteStartArray(name);
        foreach (string source in sources)
        {
            writer.WriteStringValue(source);
        }

        writer.WriteEndArray();
    }

    private static void RejectEmpty(string source, string paramName)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Datalog source must not be empty.", paramName);
        }
    }
}
