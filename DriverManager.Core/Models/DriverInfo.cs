namespace DriverManager.Core.Models;

public enum DriverStatus
{
    Installed,
    UpToDate,
    UpdateAvailable,
    ProblemDetected,
    BackupAvailable,
    Unknown
}

public sealed class DriverInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public string AvailableVersion { get; set; } = string.Empty;
    public DateTime InstallDate { get; set; }
    public DriverStatus Status { get; set; } = DriverStatus.Unknown;
    public string Provider { get; set; } = string.Empty;
    public string DriverPath { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string UpdateId { get; set; } = string.Empty;
    public bool HasUpdate => Status == DriverStatus.UpdateAvailable;
}
