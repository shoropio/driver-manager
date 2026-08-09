using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IDriverUpdater
{
    Task<DriverUpdateResult> DownloadDriverAsync(DriverInfo driver, string outputDirectory, CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> InstallDriverAsync(DriverInfo driver, string packagePath, CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}
