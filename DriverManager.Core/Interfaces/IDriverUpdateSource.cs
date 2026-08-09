using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IDriverUpdateSource
{
    Task<IReadOnlyList<DriverInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<DriverUpdateResult> DownloadAndInstallAsync(
        IReadOnlyList<DriverInfo> updates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
