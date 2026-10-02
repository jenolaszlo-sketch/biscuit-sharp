namespace BiscuitSharp;

/// <summary>
/// Machine-readable cause of an authorization evaluation failure.
/// Only the three limit-exceeded values identify resource-budget exhaustion.
/// </summary>
public enum BiscuitEvaluationFailureReason
{
    /// <summary>The Datalog fact limit was exceeded.</summary>
    FactLimitExceeded = 1,
    /// <summary>The Datalog rule-application iteration limit was exceeded.</summary>
    IterationLimitExceeded = 2,
    /// <summary>The Datalog execution time limit was exceeded.</summary>
    TimeLimitExceeded = 3,
    /// <summary>An expression could not be evaluated, for example division by zero.</summary>
    ExpressionError = 4,
    /// <summary>An upstream query returned an unexpected number of results.</summary>
    UnexpectedQueryResult = 5,
    /// <summary>Another evaluation failure, including an unrecognized future reason.</summary>
    Other = 0,
}
