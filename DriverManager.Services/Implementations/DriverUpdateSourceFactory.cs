using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;

namespace DriverManager.Services.Implementations;

public static class DriverUpdateSourceFactory
{
    public static IDriverUpdateSource Create(AppSettings settings, ILogger logger)
    {
        return settings.UpdateSource switch
        {
            "Nvidia" => new NvidiaDriverSource(),
            "Dell" => new DellDriverSource(settings.DellServiceTag, settings.DellAppId, logger),
            _ => new WindowsUpdateDriverSource()
        };
    }

    public static bool InstallsPackages(AppSettings settings)
    {
        return !settings.UpdateSource.Equals("Nvidia", StringComparison.OrdinalIgnoreCase)
            && !settings.UpdateSource.Equals("Dell", StringComparison.OrdinalIgnoreCase);
    }

    public static bool RequiresConfiguration(AppSettings settings, out string error)
    {
        error = string.Empty;
        if (settings.UpdateSource.Equals("Dell", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(settings.DellServiceTag))
        {
            error = "Configura el Service Tag de tu equipo Dell en Configuración antes de buscar actualizaciones.";
            return true;
        }

        return false;
    }
}
