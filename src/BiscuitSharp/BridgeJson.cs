using System.Text.Json;

namespace BiscuitSharp;

/// <summary>
/// Trim- and NativeAOT-safe bridge JSON: explicit writers (no reflection) and
/// strict readers. Response-shape problems are <see cref="BiscuitBridgeException"/>
/// (response decoding failure); operation failures are mapped by each caller.
/// </summary>
internal static class BridgeJson
{
    internal static byte[] EncodeObject(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static byte[] EncodeAlgorithm(string algorithm) =>
        EncodeObject(w => w.WriteString("algorithm", algorithm));

    internal static byte[] EncodeHandle(ulong handle) =>
        EncodeObject(w => w.WriteNumber("handle", handle));

    internal static byte[] EncodeKeyImportPem(string pem) =>
        EncodeObject(w =>
        {
            w.WriteString("encoding", "pem");
            w.WriteString("key", pem);
        });

    internal static byte[] EncodeKeyImportDer(ReadOnlySpan<byte> der)
    {
        // No lambda: ref-like spans cannot be captured.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("encoding", "der");
            writer.WriteBase64String("key", der);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static void WriteRoot(Utf8JsonWriter writer, BiscuitPublicKey root)
    {
        writer.WriteStartObject("root");
        writer.WriteString("algorithm", BiscuitAlgorithms.ToWireName(root.Algorithm));
        writer.WriteBase64String("public_key", root.EncodedSpan);
        writer.WriteEndObject();
    }

    internal static byte[] EncodeTokenRoot(byte[] token, BiscuitPublicKey root) =>
        EncodeObject(w =>
        {
            w.WriteBase64String("token", token);
            WriteRoot(w, root);
        });

    internal static byte[] EncodeTokenAttenuate(byte[] token, BiscuitPublicKey root, string blockSource) =>
        EncodeObject(w =>
        {
            w.WriteBase64String("token", token);
            WriteRoot(w, root);
            w.WriteStartObject("block");
            w.WriteString("source", blockSource);
            w.WriteEndObject();
        });

    internal static JsonDocument Parse(byte[] json, string operation)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(json);
            try
            {
                RequireObject(document.RootElement, operation);
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }
        catch (JsonException ex)
        {
            throw new BiscuitBridgeException(
                $"Failed to decode the native {operation} response.", ex);
        }
    }

    internal static void RequireObject(JsonElement root, string operation)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new BiscuitBridgeException($"The native {operation} response must be an object.");
    }

    internal static string RequiredString(JsonElement root, string field, string operation)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is string s
            && s.Length != 0)
        {
            return s;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required string '{field}'.");
    }

    internal static uint RequiredUInt32(JsonElement root, string field, string operation)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetUInt32(out uint n))
        {
            return n;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required number '{field}'.");
    }

    internal static ulong RequiredUInt64(JsonElement root, string field, string operation)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetUInt64(out ulong n))
        {
            return n;
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required number '{field}'.");
    }

    internal static bool RequiredBoolean(JsonElement root, string field, string operation)
    {
        if (root.TryGetProperty(field, out JsonElement value)
            && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
        {
            return value.GetBoolean();
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required boolean '{field}'.");
    }

    internal static byte[] RequiredBase64(JsonElement root, string field, string operation)
    {
        string encoded = RequiredString(root, field, operation);
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new BiscuitBridgeException(
                $"The native {operation} response field '{field}' is not valid base64.", ex);
        }
    }

    internal static string RequiredStringArray(JsonElement root, string field, string operation)
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

            return string.Join(",", items);
        }

        throw new BiscuitBridgeException(
            $"The native {operation} response is missing required array '{field}'.");
    }
}
