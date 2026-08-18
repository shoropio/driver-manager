namespace DriverManager.Core.Models;

/// <summary>
/// Lectura en vivo del uso, temperatura y memoria de una tarjeta gráfica.
/// Cada valor es null cuando el sistema o el fabricante no lo expone.
/// </summary>
public sealed class GpuTelemetry
{
    public double? UtilizationPercent { get; set; }
    public double? TemperatureCelsius { get; set; }
    public string TemperatureSource { get; set; } = string.Empty;
    public long? DedicatedMemoryUsageBytes { get; set; }
    public long? DedicatedMemoryLimitBytes { get; set; }

    public bool HasData =>
        UtilizationPercent is not null ||
        TemperatureCelsius is not null ||
        DedicatedMemoryUsageBytes is not null;
}
