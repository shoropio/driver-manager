namespace DriverManager.Core.Models;

public sealed class AppSettings
{
    public string UpdateSource { get; set; } = "WindowsUpdate";
    public string DellServiceTag { get; set; } = string.Empty;
    public string DellAppId { get; set; } = string.Empty;
    public string BackupFolder { get; set; } = string.Empty;
    public string DownloadsFolder { get; set; } = string.Empty;
    public bool CheckUpdatesOnStartup { get; set; }
    public int MaxBackups { get; set; } = 10;

    public string ResolveBackupFolder()
    {
        if (!string.IsNullOrWhiteSpace(BackupFolder))
        {
            return BackupFolder;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DriverManagerBackups");
    }

    public string ResolveDownloadsFolder()
    {
        if (!string.IsNullOrWhiteSpace(DownloadsFolder))
        {
            return DownloadsFolder;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DriverManagerDownloads");
    }
}
