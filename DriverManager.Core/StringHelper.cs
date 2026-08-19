namespace DriverManager.Core;

public static class StringHelper
{
    public static string Normalize(string value)
    {
        return new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    public static bool DeviceNamesMatch(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);
        return normalizedA.Length >= 6 && (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA));
    }
}
