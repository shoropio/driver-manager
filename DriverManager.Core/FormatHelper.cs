namespace DriverManager.Core;

public static class FormatHelper
{
    public static string FormatBytes(long bytes, string fallback = "—")
    {
        if (bytes <= 0)
        {
            return fallback;
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
