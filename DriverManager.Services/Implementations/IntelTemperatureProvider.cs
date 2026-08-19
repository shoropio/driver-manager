using DriverManager.Core.Interfaces;
using System.Management;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Lee temperaturas de GPU Intel mediante WMI (Intel Graphics Performance Monitor).
/// Devuelve un diccionario vacío sin lanzar excepciones cuando no hay GPU Intel
/// o la consulta falla.
/// </summary>
public sealed class IntelTemperatureProvider : IGpuTemperatureProvider
{
    public string Name => "IntelWMI";

    public Task<IReadOnlyDictionary<string, double>> ReadTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temperatures = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Temperature FROM Win32_PerfFormattedData_IntelGraphics_IrisXeGraphics");
                foreach (ManagementObject item in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = Convert.ToString(item.GetPropertyValue("Name"));
                    var tempObj = item.GetPropertyValue("Temperature");
                    if (!string.IsNullOrWhiteSpace(name) && tempObj is not null)
                    {
                        var temp = Convert.ToDouble(tempObj);
                        if (temp > 0)
                        {
                            temperatures[name] = temp;
                        }
                    }
                }
            }
            catch
            {
            }

            if (temperatures.Count == 0)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT Name, CurrentTemperature FROM Win32_VideoController WHERE Name LIKE '%Intel%'");
                    foreach (ManagementObject item in searcher.Get())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var name = Convert.ToString(item.GetPropertyValue("Name"));
                        var tempObj = item.GetPropertyValue("CurrentTemperature");
                        if (!string.IsNullOrWhiteSpace(name) && tempObj is not null)
                        {
                            var tempDeci = Convert.ToInt64(tempObj);
                            if (tempDeci > 0)
                            {
                                temperatures[name] = tempDeci / 10.0;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            return (IReadOnlyDictionary<string, double>)temperatures;
        }, cancellationToken);
    }
}
