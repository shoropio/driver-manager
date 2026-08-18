namespace DriverManager.Core.Models;

public sealed class DriverStateSnapshot
{
    public DateTime LastScanAt { get; set; }
    public DateTime LastUpdateCheckAt { get; set; }
    public string UpdateSource { get; set; } = string.Empty;
    public List<DriverInfo> Drivers { get; set; } = new();
    public List<GpuInfo> Gpus { get; set; } = new();
}
