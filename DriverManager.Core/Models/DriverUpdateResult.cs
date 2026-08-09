namespace DriverManager.Core.Models;

public sealed class DriverUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public bool RequiresReboot { get; init; }
    public string Source { get; init; } = string.Empty;
}
