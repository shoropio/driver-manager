namespace DriverManager.Services.Implementations;

/// <summary>
/// Comparación numérica por tokens de versiones como "40.26.506.2332" o "24.60.0".
/// </summary>
internal static class DriverVersions
{
    public static bool IsNewer(string? candidate, string? current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        var candidateParts = ParseVersion(candidate);
        var currentParts = ParseVersion(current);
        var count = Math.Max(candidateParts.Count, currentParts.Count);
        for (var i = 0; i < count; i++)
        {
            var c = i < candidateParts.Count ? candidateParts[i] : 0;
            var k = i < currentParts.Count ? currentParts[i] : 0;
            if (c > k)
            {
                return true;
            }

            if (c < k)
            {
                return false;
            }
        }

        return false;
    }

    public static List<long> ParseVersion(string? version)
    {
        var result = new List<long>();
        if (string.IsNullOrWhiteSpace(version))
        {
            return result;
        }

        long current = 0;
        var hasDigit = false;
        foreach (var ch in version)
        {
            if (char.IsDigit(ch))
            {
                current = current * 10 + (ch - '0');
                hasDigit = true;
            }
            else if (hasDigit)
            {
                result.Add(current);
                current = 0;
                hasDigit = false;
            }
        }

        if (hasDigit)
        {
            result.Add(current);
        }

        return result;
    }

    public static string Normalize(string? value)
    {
        return new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
