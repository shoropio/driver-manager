using System.Text.RegularExpressions;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Extrae la versión más reciente y la URL directa de descarga de una página del
/// Intel Download Center (HTML renderizado en el servidor).
/// </summary>
internal static class IntelPageParser
{
    public static (string Version, string DownloadUrl) Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return (string.Empty, string.Empty);
        }

        var version = string.Empty;
        var match = Regex.Match(html, @"([0-9]+(?:\.[0-9]+){1,3})(?:<[^>]*>)*\s*\(Latest\)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            version = match.Groups[1].Value;
        }

        var downloadUrl = string.Empty;
        var mirror = Regex.Match(html, @"https://downloadmirror\.intel\.com/[0-9]+/[^""'\s<>]+\.exe", RegexOptions.IgnoreCase);
        if (mirror.Success)
        {
            downloadUrl = mirror.Value;
        }

        return (version, downloadUrl);
    }
}
