using DriverManager.Core.Models;
using DriverManager.Core.Interfaces;
using System.ComponentModel;

namespace DriverManager.App.ViewModels;

public sealed class GpuViewModel : ObservableObject
{
    public GpuViewModel(GpuInfo gpuInfo)
    {
        GpuInfo = gpuInfo;
    }

    public GpuInfo GpuInfo { get; }
    public GpuTelemetry? Telemetry { get; set; }

    public string AdapterName => GpuInfo.AdapterName;
    public string Vendor => GpuInfo.Vendor.ToString();
    public string StatusText => GetStatusText(GpuInfo.Status);
    public string VideoProcessor => GpuInfo.VideoProcessor;
    public string VideoMode => GpuInfo.VideoModeDescription;
    public string DriverVersion => GpuInfo.DriverVersion;
    public string DriverDate => GpuInfo.DriverDate;
    public string AdapterRam => FormatBytes(GpuInfo.AdapterRamBytes);
    public string UtilizationText => Telemetry?.UtilizationPercent is double u ? $"{u:F1} %" : "—";
    public string TemperatureText => Telemetry?.TemperatureCelsius is double t ? $"{t:F1} °C" : "—";
    public string MemoryText => Telemetry?.DedicatedMemoryUsageBytes is long used && Telemetry?.DedicatedMemoryLimitBytes is long limit && limit > 0
        ? $"{FormatBytes(used)} / {FormatBytes(limit)}"
        : "—";

    public string SoftwareName => GpuInfo.Software?.Name ?? "Desconocido";
    public string SoftwareStatusText => GpuInfo.Software?.InstallStatus.ToString() ?? "Desconocido";
    public string SoftwareDownloadUrl => GpuInfo.Software?.DownloadUrl ?? string.Empty;
    public bool HasSoftware => GpuInfo.Software != null;

    private static string GetStatusText(DriverStatus status) => status switch
    {
        DriverStatus.UpdateAvailable => "Actualización disponible",
        DriverStatus.UpToDate => "Actualizado",
        DriverStatus.ProblemDetected => "Problema detectado",
        DriverStatus.BackupAvailable => "Respaldo disponible",
        DriverStatus.Installed => "Instalado",
        _ => "Desconocido"
    };

    private static string FormatBytes(long bytes) => DriverManager.Core.FormatHelper.FormatBytes(bytes, "—");

    public void RefreshTelemetry() => OnPropertyChanged(nameof(UtilizationText), nameof(TemperatureText), nameof(MemoryText));
    public void Refresh() => OnPropertyChanged(string.Empty);
}