using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface IGpuInfoService
{
    Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken = default);
}
