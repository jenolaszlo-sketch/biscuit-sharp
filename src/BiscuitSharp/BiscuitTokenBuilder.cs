using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// A typed Datalog parameter for templates like <c>right({resource}, {operation})</c>.
/// Values are substituted by upstream <c>code_with_params</c>; untrusted input is
/// never interpolated into Datalog source.
/// </summary>
public abstract record BiscuitParam
{
    public static BiscuitParam Str(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StrParam(value);
    }

    public static BiscuitParam Int(long value) => new IntParam(value);

    public static BiscuitParam Bool(bool value) => new BoolParam(value);

    public static BiscuitParam Bytes(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new BytesParam((byte[])value.Clone());
    }

    private sealed record StrParam(string Value) : BiscuitParam;

    private sealed record IntParam(long Value) : BiscuitParam;

    private sealed record BoolParam(bool Value) : BiscuitParam;

    private sealed record BytesParam(byte[] Value) : BiscuitParam;

    internal void WriteTo(Utf8JsonWriter writer, string name)
    {
        writer.WriteStartObject(name);
        switch (this)
        {
            case StrParam s:
                writer.WriteString("type", "str");
                writer.WriteString("value", s.Value);
                break;
            case IntParam i:
                writer.WriteString("type", "int");
                writer.WriteNumber("value", i.Value);
                break;
            case BoolParam b:
                writer.WriteString("type", "bool");
                writer.WriteBoolean("value", b.Value);
                break;
            case BytesParam b:
                writer.WriteString("type", "bytes");
                writer.WriteBase64String("value", b.Value);
                break;
            default:
                throw new ArgumentException($"Unsupported Biscuit parameter: {GetType()}.", nameof(name));
        }

        writer.WriteEndObject();
    }
}

/// <summary>Fluent builder for token creation. Prefer parameterized overloads over string interpolation of untrusted input.</summary>
public sealed class BiscuitTokenBuilder
{
    private readonly List<DatalogTemplate> _facts = new();
    private readonly List<DatalogTemplate> _checks = new();

    private sealed record DatalogTemplate(string Source, IReadOnlyDictionary<string, BiscuitParam>? Params);

    private BiscuitTokenBuilder()
    {
    }

    public static BiscuitTokenBuilder Create() => new();

    public BiscuitTokenBuilder AddFact(string datalogFact)
    {
        ArgumentNullException.ThrowIfNull(datalogFact);
        RejectEmpty(datalogFact, nameof(datalogFact));
        _facts.Add(new DatalogTemplate(datalogFact, null));
        return this;
    }

    /// <summary>
    /// Trim- and NativeAOT-safe parameterized fact. Values are substituted by
    /// upstream, never interpolated.
    /// </summary>
    public BiscuitTokenBuilder AddFact(
        string template,
        IEnumerable<KeyValuePair<string, BiscuitParam>> parameters)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);
        RejectEmpty(template, nameof(template));
        var copy = new Dictionary<string, BiscuitParam>(StringComparer.Ordinal);
        foreach ((string key, BiscuitParam value) in parameters)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            copy[key] = value;
        }

        _facts.Add(new DatalogTemplate(template, copy));
        return this;
    }

    /// <summary>
    /// Convenience overload accepting an anonymous object, e.g.
    /// <c>AddFact("right({resource}, {operation})", new { resource = "a", operation = "read" })</c>.
    /// Only string, int, long, bool, and byte[] properties are supported.
    /// Reflection-based: prefer the dictionary overload for trimming/NativeAOT.
    /// </summary>
    [RequiresUnreferencedCode("Enumerates the parameter object's public properties; prefer the dictionary overload for trimming.")]
    [RequiresDynamicCode("Reads the parameter object's public properties; prefer the dictionary overload for NativeAOT.")]
    public BiscuitTokenBuilder AddFact(string template, object parameters)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);
        var dict = new Dictionary<string, BiscuitParam>(StringComparer.Ordinal);
        foreach (PropertyInfo property in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            object? value = property.GetValue(parameters);
            dict[property.Name] = value switch
            {
                string s => BiscuitParam.Str(s),
                int i => BiscuitParam.Int(i),
                long l => BiscuitParam.Int(l),
                bool b => BiscuitParam.Bool(b),
                byte[] bytes => BiscuitParam.Bytes(bytes),
                null => throw new ArgumentException(
                    $"Parameter '{property.Name}' is null.", nameof(parameters)),
                _ => throw new ArgumentException(
                    $"Unsupported parameter type '{property.PropertyType}' for '{property.Name}'. " +
                    "Use string, int, long, bool, or byte[].", nameof(parameters)),
            };
        }

        return AddFact(template, dict);
    }

    public BiscuitTokenBuilder AddCheck(string datalogCheck)
    {
        ArgumentNullException.ThrowIfNull(datalogCheck);
        RejectEmpty(datalogCheck, nameof(datalogCheck));
        _checks.Add(new DatalogTemplate(datalogCheck, null));
        return this;
    }

    public BiscuitToken Build(BiscuitPrivateKey rootKey)
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        ulong handle = rootKey.NativeHandle;
        byte[] request = BridgeJson.EncodeObject(w =>
        {
            w.WriteNumber("root_handle", handle);
            WriteItems(w, "facts", _facts);
            WriteItems(w, "checks", _checks);
        });
        byte[] response = NativeBridge.Call(
            NativeBridge.OpTokenCreate,
            request,
            BiscuitErrorMapping.MapTokenError);
        using JsonDocument doc = BridgeJson.Parse(response, "token_create");
        return BiscuitToken.FromVerifiedBytes(
            BridgeJson.RequiredBase64(doc.RootElement, "token", "token_create"),
            rootKey.PublicKey);
    }

    private static void WriteItems(Utf8JsonWriter writer, string name, List<DatalogTemplate> items)
    {
        writer.WriteStartArray(name);
        foreach (DatalogTemplate item in items)
        {
            writer.WriteStartObject();
            writer.WriteString("source", item.Source);
            if (item.Params is { Count: > 0 })
            {
                writer.WriteStartObject("params");
                foreach ((string key, BiscuitParam param) in item.Params)
                {
                    param.WriteTo(writer, key);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
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
