using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IDriverUpdater
{
    Task<DriverUpdateResult> DownloadDriverAsync(DriverInfo driver, string outputDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> InstallDriverAsync(DriverInfo driver, string packagePath, CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default);
}
