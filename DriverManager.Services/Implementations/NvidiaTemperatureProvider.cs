using DriverManager.Core.Interfaces;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Lee temperaturas de GPU NVIDIA mediante NVML (nvml.dll). Devuelve un
/// diccionario vacío sin lanzar excepciones cuando la biblioteca no existe,
/// no hay GPU NVIDIA o la consulta falla.
/// </summary>
public sealed class NvidiaTemperatureProvider : IGpuTemperatureProvider
{
    public string Name => "NVML";

    public Task<IReadOnlyDictionary<string, double>> ReadTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temperatures = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (NvmlInit() != 0)
                {
                    return temperatures;
                }

                try
                {
                    if (NvmlDeviceGetCount(out uint count) != 0)
                    {
                        return temperatures;
                    }

                    for (uint i = 0; i < count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (NvmlDeviceGetHandleByIndex(i, out var device) != 0)
                        {
                            continue;
                        }

                        var name = new StringBuilder(256);
                        if (NvmlDeviceGetName(device, name, 256) == 0 &&
                            NvmlDeviceGetTemperature(device, 0, out uint temperature) == 0)
                        {
                            temperatures[name.ToString()] = temperature;
                        }
                    }
                }
                finally
                {
                    NvmlShutdown();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }

            return (IReadOnlyDictionary<string, double>)temperatures;
        }, cancellationToken);
    }

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    private static extern int NvmlInit();

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetCount_v2")]
    private static extern int NvmlDeviceGetCount(out uint count);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetName")]
    private static extern int NvmlDeviceGetName(IntPtr device, StringBuilder name, uint length);

    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
    private static extern int NvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temperature);

    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
    private static extern int NvmlShutdown();
}
