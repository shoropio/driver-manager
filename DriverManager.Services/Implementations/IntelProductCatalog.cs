using DriverManager.Core.Models;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Catálogo de productos Intel que la app vigila (Intel & Killer). Cada producto
/// se detecta a partir de los dispositivos instalados (Win32_PnPSignedDriver) y
/// se compara contra la versión más reciente de su página de descarga.
/// </summary>
internal sealed class IntelProduct
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Clase de dispositivo (DeviceClass) que se usará como categoría.</summary>
    public required string Category { get; init; }

    /// <summary>Página de descarga de Intel para el producto.</summary>
    public required string PageUrl { get; init; }

    /// <summary>Versión de respaldo usada cuando no se puede leer la página (Akamai/offline).</summary>
    public required string FallbackVersion { get; init; }

    /// <summary>Tokens que deben aparecer en DeviceName o Manufacturer (normalizados).</summary>
    public required string[] MatchTokens { get; init; }

    /// <summary>Token obligatorio en el fabricante (normalizado); opcional.</summary>
    public string? RequireManufacturerToken { get; init; }

    public IReadOnlyList<DriverInfo> MatchedDevices(IReadOnlyList<DriverInfo> drivers)
    {
        var matched = new List<DriverInfo>();
        foreach (var driver in drivers)
        {
            var name = DriverVersions.Normalize($"{driver.DeviceName} {driver.Manufacturer}");
            if (!MatchTokens.Any(token => name.Contains(token)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(RequireManufacturerToken) &&
                !DriverVersions.Normalize(driver.Manufacturer).Contains(RequireManufacturerToken))
            {
                continue;
            }

            matched.Add(driver);
        }

        return matched;
    }
}

internal static class IntelProductCatalog
{
    public static IReadOnlyList<IntelProduct> Products { get; } = new[]
    {
        new IntelProduct
        {
            Id = "killer-suite",
            DisplayName = "Intel Killer Performance Suite",
            Category = "NET",
            PageUrl = "https://www.intel.com/content/www/us/en/download/19779/intel-killer-performance-suite.html",
            FallbackVersion = "40.26.506.2332",
            MatchTokens = new[] { "killer" }
        },
        new IntelProduct
        {
            Id = "intel-bluetooth",
            DisplayName = "Intel Wireless Bluetooth",
            Category = "BLUETOOTH",
            PageUrl = "https://www.intel.com/content/www/us/en/download/18649/intel-wireless-bluetooth-for-windows-10-and-windows-11.html",
            FallbackVersion = "24.60.0",
            MatchTokens = new[] { "bluetooth" },
            RequireManufacturerToken = "intel"
        }
    };
}
