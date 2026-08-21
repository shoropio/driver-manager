using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IMaintenanceService
{
    Task<IReadOnlyList<MaintenanceIssue>> ScanAsync(CancellationToken cancellationToken = default);
    Task<DriverUpdateResult> RepairIssueAsync(MaintenanceIssue issue, CancellationToken cancellationToken = default);
    Task<bool> RescanHardwareChangesAsync(CancellationToken cancellationToken = default);
}
