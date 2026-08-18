using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IDriverBackupService
{
    Task<DriverBackupInfo> CreateBackupAsync(IReadOnlyList<DriverInfo> drivers, string destinationFolder, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverBackupInfo>> ListBackupsAsync(string backupFolder, CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> RestoreBackupAsync(DriverBackupInfo backup, IReadOnlyList<DriverInfo> drivers, CancellationToken cancellationToken = default);
    Task<int> EnforceRetentionAsync(string backupFolder, int maxBackups, CancellationToken cancellationToken = default);
}
