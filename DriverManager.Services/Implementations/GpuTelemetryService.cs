using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Globalization;
using System.Management;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Lee uso de GPU desde los contadores de rendimiento de Windows
/// (Win32_PerfFormattedData_GPUPerformanceCounters), agregados por LUID, y
/// combina la temperatura proporcionada por los proveedores del fabricante.
/// </summary>
public sealed class GpuTelemetryService : IGpuTelemetryService
{
    private readonly IGpuTemperatureProvider[] _temperatureProviders;

    public GpuTelemetryService() : this(new NvidiaTemperatureProvider(), new AmdTemperatureProvider(), new IntelTemperatureProvider())
    {
    }

    public GpuTelemetryService(params IGpuTemperatureProvider[] temperatureProviders)
    {
        _temperatureProviders = temperatureProviders ?? Array.Empty<IGpuTemperatureProvider>();
    }

    public async Task<IReadOnlyList<GpuTelemetry>> ReadAsync(
        IReadOnlyList<GpuInfo> gpus,
        CancellationToken cancellationToken = default)
    {
        var utilizationByLuid = await Task.Run(() => QueryUtilizationByLuid(cancellationToken), cancellationToken);
        var memoryByLuid = await Task.Run(() => QueryMemoryByLuid(cancellationToken), cancellationToken);

        var temperaturesByProvider = new Dictionary<string, IReadOnlyDictionary<string, double>>();
        foreach (var provider in _temperatureProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            temperaturesByProvider[provider.Name] = await provider.ReadTemperaturesAsync(cancellationToken);
        }

        var result = new List<GpuTelemetry>(gpus.Count);
        foreach (var gpu in gpus)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(BuildTelemetry(gpu, utilizationByLuid, memoryByLuid, temperaturesByProvider));
        }

        return result;
    }

    private static GpuTelemetry BuildTelemetry(
        GpuInfo gpu,
        IReadOnlyDictionary<string, double> utilizationByLuid,
        IReadOnlyDictionary<string, GpuMemoryReading> memoryByLuid,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> temperaturesByProvider)
    {
        var telemetry = new GpuTelemetry();

        if (!string.IsNullOrWhiteSpace(gpu.Luid))
        {
            if (utilizationByLuid.TryGetValue(gpu.Luid, out var utilization))
            {
                telemetry.UtilizationPercent = Math.Min(100, utilization);
            }

            if (memoryByLuid.TryGetValue(gpu.Luid, out var memory))
            {
                telemetry.DedicatedMemoryUsageBytes = memory.Usage;
                if (gpu.AdapterRamBytes > 0)
                {
                    telemetry.DedicatedMemoryLimitBytes = gpu.AdapterRamBytes;
                }
            }
        }

        foreach (var provider in temperaturesByProvider)
        {
            if (TryMatchTemperature(gpu.AdapterName, provider.Value, out var temperature))
            {
                telemetry.TemperatureCelsius = temperature;
                telemetry.TemperatureSource = provider.Key;
                break;
            }
        }

        return telemetry;
    }

    private static IReadOnlyDictionary<string, double> QueryUtilizationByLuid(CancellationToken cancellationToken)
    {
        var byLuid = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            foreach (ManagementObject item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Convert.ToString(item.GetPropertyValue("Name"));
                if (!TryParseLuid(name, out var luid))
                {
                    continue;
                }

                var utilization = Convert.ToDouble(item.GetPropertyValue("UtilizationPercentage"), CultureInfo.InvariantCulture);
                byLuid[luid] = byLuid.TryGetValue(luid, out var current) ? current + utilization : utilization;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }

        foreach (var luid in byLuid.Keys.ToArray())
        {
            byLuid[luid] = Math.Min(100, byLuid[luid]);
        }

        return byLuid;
    }

    private static IReadOnlyDictionary<string, GpuMemoryReading> QueryMemoryByLuid(CancellationToken cancellationToken)
    {
        var byLuid = new Dictionary<string, GpuMemoryReading>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DedicatedUsage, SharedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory");
            foreach (ManagementObject item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Convert.ToString(item.GetPropertyValue("Name"));
                if (!TryParseLuid(name, out var luid))
                {
                    continue;
                }

                var dedicated = Convert.ToInt64(item.GetPropertyValue("DedicatedUsage"), CultureInfo.InvariantCulture);
                var shared = Convert.ToInt64(item.GetPropertyValue("SharedUsage"), CultureInfo.InvariantCulture);
                byLuid[luid] = new GpuMemoryReading(dedicated + shared);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }

        return byLuid;
    }

    /// <summary>
    /// Extrae la clave LUID de un nombre de instancia de contador como
    /// "luid_0x00000000_0x00013DA6_phys_0_eng_0_engtype_3D".
    /// </summary>
    internal static bool TryParseLuid(string? counterName, out string luidKey)
    {
        luidKey = string.Empty;
        if (string.IsNullOrWhiteSpace(counterName))
        {
            return false;
        }

        const string marker = "luid_";
        var start = counterName.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += marker.Length;
        var end = counterName.IndexOf("_phys_", start, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var key = counterName.Substring(start, end - start);
        if (key.Length == 0)
        {
            return false;
        }

        luidKey = key;
        return true;
    }

    internal static bool TryMatchTemperature(
        string? adapterName,
        IReadOnlyDictionary<string, double> temperaturesByDevice,
        out double temperature)
    {
        temperature = 0;
        if (string.IsNullOrWhiteSpace(adapterName))
        {
            return false;
        }

        var normalized = Normalize(adapterName);
        foreach (var (deviceName, value) in temperaturesByDevice)
        {
            var other = Normalize(deviceName);
            if (other.Length >= 6 && (normalized.Contains(other) || other.Contains(normalized)))
            {
                temperature = value;
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value) => DriverManager.Core.StringHelper.Normalize(value);

    internal readonly record struct GpuMemoryReading(long Usage);
}
