namespace BiscuitSharp;

internal static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        try
        {
            return Convert.FromBase64String(s);
        }
        catch (FormatException ex)
        {
            throw new BiscuitFormatException("Invalid base64url token encoding.", ex);
        }
    }
}
