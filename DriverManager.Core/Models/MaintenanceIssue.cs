namespace DriverManager.Core.Models;

public enum MaintenanceIssueType
{
    Obsolete,
    Missing,
    Broken,
    Unsigned
}

public enum MaintenanceSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class MaintenanceIssue
{
    public MaintenanceIssueType IssueType { get; init; }
    public MaintenanceSeverity Severity { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string HardwareId { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string InstalledVersion { get; init; } = string.Empty;
    public string AvailableVersion { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public uint ErrorCode { get; init; }
    public DriverInfo? RelatedDriver { get; init; }
}
