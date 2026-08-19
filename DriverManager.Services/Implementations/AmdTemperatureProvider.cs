using DriverManager.Core.Interfaces;
using System.Runtime.InteropServices;
using System.Text;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Lee temperaturas de GPU AMD mediante ADL (AMD Display Library / atiadlxx.dll).
/// Devuelve un diccionario vacío sin lanzar excepciones cuando la biblioteca
/// no está disponible o la consulta falla.
/// </summary>
public sealed class AmdTemperatureProvider : IGpuTemperatureProvider
{
    public string Name => "ADL";

    public Task<IReadOnlyDictionary<string, double>> ReadTemperaturesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var temperatures = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            IntPtr context = IntPtr.Zero;

            try
            {
                if (ADL2_Main_Control_Create(ADL2_Callback, ref context) != ADL_OK || context == IntPtr.Zero)
                {
                    return temperatures;
                }

                try
                {
                    if (ADL2_Adapter_NumberOfAdapters_Get(context, out int adapterCount) != ADL_OK || adapterCount <= 0)
                    {
                        return temperatures;
                    }

                    for (int i = 0; i < adapterCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (ADL2_Adapter_AdapterInfo_Get(context, out var info, Marshal.SizeOf<ADLAdapterInfo>()) != ADL_OK)
                        {
                            continue;
                        }

                        var adapterInfo = Marshal.PtrToStructure<ADLAdapterInfo>(info);
                        Marshal.FreeCoTaskMem(info);

                        if (ADL2_Overdrive5_Temperature_Get(context, adapterInfo.AdapterIndex, out int temp) == ADL_OK)
                        {
                            var name = adapterInfo.AdapterName;
                            if (!string.IsNullOrWhiteSpace(name) && temp > 0)
                            {
                                temperatures[name] = temp / 1000.0;
                            }
                        }
                    }
                }
                finally
                {
                    if (context != IntPtr.Zero)
                    {
                        ADL2_Main_Control_Destroy(context);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }

            return (IReadOnlyDictionary<string, double>)temperatures;
        }, cancellationToken);
    }

    private const int ADL_OK = 0;

    private delegate void ADL2CallbackProc(int context, int message, IntPtr param1, IntPtr param2);

    [DllImport("atiadlxx.dll", EntryPoint = "ADL2_Main_Control_Create")]
    private static extern int ADL2_Main_Control_Create(ADL2CallbackProc callback, ref IntPtr context);

    [DllImport("atiadlxx.dll", EntryPoint = "ADL2_Main_Control_Destroy")]
    private static extern int ADL2_Main_Control_Destroy(IntPtr context);

    [DllImport("atiadlxx.dll", EntryPoint = "ADL2_Adapter_NumberOfAdapters_Get")]
    private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int adapterCount);

    [DllImport("atiadlxx.dll", EntryPoint = "ADL2_Adapter_AdapterInfo_Get")]
    private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, out IntPtr info, int size);

    [DllImport("atiadlxx.dll", EntryPoint = "ADL2_Overdrive5_Temperature_Get")]
    private static extern int ADL2_Overdrive5_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

    private static readonly ADL2CallbackProc ADL2_Callback = (_, _, _, _) => { };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ADLAdapterInfo
    {
        public int AdapterIndex;
        public int AdapterID;
        public int BusNumber;
        public int DeviceNumber;
        public int FunctionNumber;
        public int VendorID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string AdapterName;
    }
}
