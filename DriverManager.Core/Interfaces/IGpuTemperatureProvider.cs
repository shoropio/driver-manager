namespace DriverManager.Core.Interfaces;

/// <summary>
/// Provee temperaturas de GPU mediante una API propietaria del fabricante
/// (p. ej. NVML de NVIDIA). Devuelve un diccionario con nombre de dispositivo
/// -> temperatura en grados Celsius.
/// </summary>
public interface IGpuTemperatureProvider
{
    string Name { get; }

    Task<IReadOnlyDictionary<string, double>> ReadTemperaturesAsync(
        CancellationToken cancellationToken = default);
}
