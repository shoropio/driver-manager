namespace DriverManager.Core.Models;

/// <summary>
/// Representa una tarjeta gráfica detectada en el equipo (Win32_VideoController)
/// junto con el estado de su controlador y su software compañero.
/// </summary>
public sealed class GpuInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string AdapterCompatibility { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string VideoProcessor { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public long AdapterRamBytes { get; set; }
    public string VideoModeDescription { get; set; } = string.Empty;
    public string Luid { get; set; } = string.Empty;
    public DriverStatus Status { get; set; } = DriverStatus.Unknown;
    public GpuSoftware? Software { get; set; }

    public GpuVendor Vendor
    {
        get
        {
            var fromCompatibility = GpuVendors.Detect(AdapterCompatibility);
            return fromCompatibility != GpuVendor.Unknown ? fromCompatibility : GpuVendors.Detect(AdapterName);
        }
    }

    public bool HasUpdate => Status == DriverStatus.UpdateAvailable;
    public bool HasProblem => Status == DriverStatus.ProblemDetected;
}
