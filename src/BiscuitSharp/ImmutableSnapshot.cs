namespace BiscuitSharp;

/// <summary>Owned snapshots for public result collections, including record with-expressions.</summary>
internal static class ImmutableSnapshot
{
    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        T[] copy = values.ToArray();
        if (copy.Any(item => item is null))
            throw new ArgumentException("Collection elements must not be null.", parameter);
        return Array.AsReadOnly(copy);
    }
}