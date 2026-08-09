using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IDriverScanner
{
    Task<IReadOnlyList<DriverInfo>> ScanInstalledDriversAsync(CancellationToken cancellationToken = default);
}
