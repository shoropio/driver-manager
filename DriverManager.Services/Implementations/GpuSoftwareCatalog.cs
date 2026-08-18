using DriverManager.Core.Models;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Catálogo del software compañero oficial de cada fabricante de tarjetas
/// gráficas, junto con las marcas (tokens) usadas para detectar si está
/// instalado en el equipo.
/// </summary>
public static class GpuSoftwareCatalog
{
    private static readonly Dictionary<GpuVendor, IReadOnlyList<string>> MatchTokens = new()
    {
        [GpuVendor.Nvidia] = new[] { "GeForce Experience", "NVIDIA App", "NVIDIA Graphics Driver" },
        [GpuVendor.Amd] = new[] { "Adrenalin", "AMD Software", "Radeon Software" },
        [GpuVendor.Intel] = new[] { "Arc Control", "Graphics Command Center", "Intel Graphics" }
    };

    public static GpuSoftware? ForVendor(GpuVendor vendor)
    {
        return vendor switch
        {
            GpuVendor.Nvidia => new GpuSoftware
            {
                Vendor = "NVIDIA",
                Name = "NVIDIA GeForce Experience",
                DownloadUrl = "https://www.nvidia.com/es-la/geforce/geforce-experience/"
            },
            GpuVendor.Amd => new GpuSoftware
            {
                Vendor = "AMD",
                Name = "AMD Software: Adrenalin Edition",
                DownloadUrl = "https://www.amd.com/es/support"
            },
            GpuVendor.Intel => new GpuSoftware
            {
                Vendor = "Intel",
                Name = "Intel Arc Control",
                DownloadUrl = "https://www.intel.com/content/www/us/en/download-center/home.html"
            },
            _ => null
        };
    }

    public static SoftwareInstallStatus DetermineInstallStatus(GpuVendor vendor, IReadOnlyList<string> installedProgramNames)
    {
        if (!MatchTokens.TryGetValue(vendor, out var tokens))
        {
            return SoftwareInstallStatus.Unknown;
        }

        return installedProgramNames.Any(installed => tokens.Any(token => installed.Contains(token, StringComparison.OrdinalIgnoreCase)))
            ? SoftwareInstallStatus.Installed
            : SoftwareInstallStatus.NotInstalled;
    }
}
