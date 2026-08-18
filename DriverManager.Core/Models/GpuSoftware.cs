namespace DriverManager.Core.Models;

public enum SoftwareInstallStatus
{
    Installed,
    NotInstalled,
    Unknown
}

/// <summary>
/// Software compañero asociado a una tarjeta gráfica (por ejemplo,
/// GeForce Experience, AMD Adrenalin o Intel Arc Control) y su enlace
/// oficial de descarga.
/// </summary>
public sealed class GpuSoftware
{
    public string Name { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public SoftwareInstallStatus InstallStatus { get; set; } = SoftwareInstallStatus.Unknown;
}
