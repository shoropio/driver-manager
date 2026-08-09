namespace DriverManager.Core.Models;

public sealed class DriverBackupInfo
{
    public string BackupName { get; init; } = string.Empty;
    public string BackupPath { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
    public IReadOnlyList<DriverInfo> Drivers { get; init; } = Array.Empty<DriverInfo>();
}
