using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

/// <summary>
/// Lee telemetría en vivo (uso, temperatura y memoria) de las tarjetas
/// gráficas indicadas. Devuelve una lectura por cada GPU, en el mismo orden.
/// </summary>
public interface IGpuTelemetryService
{
    Task<IReadOnlyList<GpuTelemetry>> ReadAsync(
        IReadOnlyList<GpuInfo> gpus,
        CancellationToken cancellationToken = default);
}
