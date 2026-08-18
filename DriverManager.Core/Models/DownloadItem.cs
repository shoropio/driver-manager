namespace DriverManager.Core.Models;

public enum DownloadStatus
{
    Waiting,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed class DownloadItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string TempFilePath { get; set; } = string.Empty;
    public string MetaFilePath { get; set; } = string.Empty;
    public long TotalBytes { get; set; } = -1;
    public long ReceivedBytes { get; set; }
    public double SpeedBytesPerSec { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Waiting;
    public string ErrorMessage { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string InstalledVersion { get; set; } = string.Empty;
    public string AvailableVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
}
