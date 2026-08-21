using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using DriverManager.Core;
using System.Management;

namespace DriverManager.Services.Implementations;

public sealed class MaintenanceService : IMaintenanceService
{
    private readonly IDriverScanner _driverScanner;
    private readonly ILogger _logger;

    public MaintenanceService(IDriverScanner driverScanner, ILogger logger)
    {
        _driverScanner = driverScanner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MaintenanceIssue>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<MaintenanceIssue>();
        var errorCodes = await Task.Run(() => LoadConfigManagerErrorCodes(cancellationToken), cancellationToken);
        var installedDrivers = await _driverScanner.ScanInstalledDriversAsync(cancellationToken);

        await Task.Run(() =>
        {
            ScanBrokenDevices(issues, errorCodes, cancellationToken);
            ScanMissingDrivers(issues, errorCodes, cancellationToken);
            ScanUnsignedDrivers(issues, installedDrivers, cancellationToken);
            ScanObsoleteDrivers(issues, installedDrivers, cancellationToken);
        }, cancellationToken);

        _logger.Log($"Mantenimiento: escaneo completado. {issues.Count} problema(s) encontrado(s).");
        return issues;
    }

    public async Task<DriverUpdateResult> RepairIssueAsync(MaintenanceIssue issue, CancellationToken cancellationToken = default)
    {
        if (issue is null)
        {
            return new DriverUpdateResult { Success = false, Message = "Problema no válido." };
        }

        return issue.IssueType switch
        {
            MaintenanceIssueType.Broken => await RepairBrokenDriverAsync(issue, cancellationToken),
            MaintenanceIssueType.Missing => await RescanHardwareChangesAsync(cancellationToken)
                ? new DriverUpdateResult { Success = true, Message = "Reescaneo de hardware completado. El sistema detectará los controladores faltantes.", Source = "Maintenance" }
                : new DriverUpdateResult { Success = false, Message = "No se pudo reescanear el hardware.", Source = "Maintenance" },
            MaintenanceIssueType.Obsolete => new DriverUpdateResult { Success = true, Message = $"Visita la pestaña Actualizaciones para descargar la versión {issue.AvailableVersion}.", Source = "Maintenance" },
            MaintenanceIssueType.Unsigned => new DriverUpdateResult { Success = true, Message = "Busca una versión firmada del controlador en el sitio del fabricante.", Source = "Maintenance" },
            _ => new DriverUpdateResult { Success = false, Message = "Tipo de problema no soportado para reparación automática.", Source = "Maintenance" }
        };
    }

    public async Task<bool> RescanHardwareChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, output) = await ProcessRunner.RunAsync("pnputil.exe", "/scan-devices", cancellationToken);
            _logger.Log($"Reescaneo de hardware: código {exitCode}. {output.Trim()}");
            return PlatformHelper.IsPnpSuccess(exitCode);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al reescanear hardware.", ex);
            return false;
        }
    }

    private async Task<DriverUpdateResult> RepairBrokenDriverAsync(MaintenanceIssue issue, CancellationToken cancellationToken)
    {
        try
        {
            var (exitCode, output) = await ProcessRunner.RunAsync("pnputil.exe", "/scan-devices", cancellationToken);
            var success = PlatformHelper.IsPnpSuccess(exitCode);
            _logger.Log($"Reparación de driver roto ({issue.DeviceName}): código {exitCode}.");

            return new DriverUpdateResult
            {
                Success = success,
                Message = success
                    ? $"Reescaneo completado para '{issue.DeviceName}'. El sistema reintentará cargar el controlador."
                    : $"No se pudo reescanear el hardware para '{issue.DeviceName}'.",
                Details = output.Trim(),
                Source = "Maintenance"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al reparar driver roto: {issue.DeviceName}", ex);
            return new DriverUpdateResult { Success = false, Message = $"Error: {ex.Message}", Source = "Maintenance" };
        }
    }

    private void ScanBrokenDevices(List<MaintenanceIssue> issues, IReadOnlyDictionary<string, uint> errorCodes, CancellationToken cancellationToken)
    {
        var brokenErrorCodes = new HashSet<uint> { 1, 3, 10, 12, 14, 18, 19, 24, 28, 31, 32, 37, 39, 40, 43 };

        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID, Name, Manufacturer, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0");

        foreach (ManagementObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deviceId = GetString(item, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId) || !errorCodes.TryGetValue(deviceId, out var code))
            {
                continue;
            }

            if (!brokenErrorCodes.Contains(code))
            {
                continue;
            }

            var severity = code switch
            {
                1 => MaintenanceSeverity.Critical,
                10 => MaintenanceSeverity.Critical,
                3 => MaintenanceSeverity.Critical,
                12 => MaintenanceSeverity.Warning,
                31 => MaintenanceSeverity.Critical,
                _ => MaintenanceSeverity.Warning
            };

            issues.Add(new MaintenanceIssue
            {
                IssueType = MaintenanceIssueType.Broken,
                Severity = severity,
                DeviceName = GetString(item, "Name"),
                HardwareId = deviceId,
                Category = GetString(item, "DeviceClass"),
                Manufacturer = GetString(item, "Manufacturer"),
                ErrorCode = code,
                Description = $"Error del dispositivo (código {code}): {GetErrorCodeDescription(code)}",
                SuggestedAction = "Reescanear hardware o reinstalar el controlador desde el sitio del fabricante."
            });
        }
    }

    private void ScanMissingDrivers(List<MaintenanceIssue> issues, IReadOnlyDictionary<string, uint> errorCodes, CancellationToken cancellationToken)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID, Name, Manufacturer, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE Service IS NULL AND ConfigManagerErrorCode = 24");

        foreach (ManagementObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deviceId = GetString(item, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            issues.Add(new MaintenanceIssue
            {
                IssueType = MaintenanceIssueType.Missing,
                Severity = MaintenanceSeverity.Warning,
                DeviceName = GetString(item, "Name"),
                HardwareId = deviceId,
                Category = GetString(item, "DeviceClass"),
                Manufacturer = GetString(item, "Manufacturer"),
                Description = "El dispositivo no tiene un controlador asignado.",
                SuggestedAction = "Buscar e instalar el controlador desde el sitio del fabricante o Windows Update."
            });
        }
    }

    private void ScanUnsignedDrivers(List<MaintenanceIssue> issues, IReadOnlyList<DriverInfo> installedDrivers, CancellationToken cancellationToken)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID, DeviceName, DeviceClass, Manufacturer, InfName FROM Win32_PnPSignedDriver");

        foreach (ManagementObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deviceName = GetString(item, "DeviceName");
            if (string.IsNullOrWhiteSpace(deviceName))
            {
                continue;
            }

            var infName = GetString(item, "InfName");
            if (string.IsNullOrWhiteSpace(infName))
            {
                continue;
            }

            var driverPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "INF", infName);

            var isSigned = File.Exists(driverPath) &&
                File.Exists(driverPath + ".sig");

            if (isSigned)
            {
                continue;
            }

            issues.Add(new MaintenanceIssue
            {
                IssueType = MaintenanceIssueType.Unsigned,
                Severity = MaintenanceSeverity.Info,
                DeviceName = deviceName,
                HardwareId = GetString(item, "DeviceID"),
                Category = GetString(item, "DeviceClass"),
                Manufacturer = GetString(item, "Manufacturer"),
                InstalledVersion = GetString(item, "InfName"),
                Description = "El controlador no está firmado digitalmente.",
                SuggestedAction = "Buscar una versión firmada del controlador para mayor seguridad y compatibilidad."
            });
        }
    }

    private void ScanObsoleteDrivers(List<MaintenanceIssue> issues, IReadOnlyList<DriverInfo> installedDrivers, CancellationToken cancellationToken)
    {
        foreach (var driver in installedDrivers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(driver.AvailableVersion) || string.IsNullOrWhiteSpace(driver.InstalledVersion))
            {
                continue;
            }

            if (!DriverVersions.IsNewer(driver.AvailableVersion, driver.InstalledVersion))
            {
                continue;
            }

            issues.Add(new MaintenanceIssue
            {
                IssueType = MaintenanceIssueType.Obsolete,
                Severity = MaintenanceSeverity.Info,
                DeviceName = driver.DeviceName,
                HardwareId = driver.HardwareId,
                Category = driver.Category,
                Manufacturer = driver.Manufacturer,
                InstalledVersion = driver.InstalledVersion,
                AvailableVersion = driver.AvailableVersion,
                RelatedDriver = driver,
                Description = $"Hay una versión más reciente disponible: {driver.AvailableVersion} (instalada: {driver.InstalledVersion}).",
                SuggestedAction = "Actualizar desde la pestaña Actualizaciones."
            });
        }
    }

    private static Dictionary<string, uint> LoadConfigManagerErrorCodes(CancellationToken cancellationToken)
    {
        var codes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher("SELECT DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0");
        foreach (ManagementObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deviceId = GetString(item, "DeviceID");
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            if (GetValue(item, "ConfigManagerErrorCode") is ushort errorCode && errorCode != 0)
            {
                codes[deviceId] = errorCode;
            }
        }

        return codes;
    }

    private static string GetErrorCodeDescription(uint code) => code switch
    {
        1 => "Este dispositivo no puede iniciarse.",
        3 => "El controlador de este dispositivo está dañado.",
        10 => "Este dispositivo no puede iniciarse.",
        12 => "Este dispositivo tiene un conflicto de recursos.",
        14 => "Este dispositivo no puede funcionar correctamente hasta que se reinicie.",
        18 => "Este dispositivo no está configurado correctamente.",
        19 => "Windows no puede cargar el controlador de este dispositivo.",
        22 => "Este dispositivo está deshabilitado.",
        24 => "Este dispositivo no está configurado correctamente.",
        28 => "El controlador de este dispositivo no está instalado.",
        31 => "Este dispositivo no funciona correctamente.",
        32 => "Windows requiere una firma digital para el controlador.",
        37 => "Windows no puede aplicar la restricción de hardware a este dispositivo.",
        39 => "Windows no puede inicializar el controlador del dispositivo.",
        40 => "Windows no puede acceder a este hardware porque el servicio de hardware anticipó un error.",
        43 => "Windows deshabilita este dispositivo porque ha reportado problemas.",
        _ => $"Código de error {code}."
    };

    private static string GetString(ManagementBaseObject item, string propertyName)
    {
        return GetValue(item, propertyName)?.ToString() ?? string.Empty;
    }

    private static bool GetBool(ManagementBaseObject item, string propertyName)
    {
        return GetValue(item, propertyName) is true;
    }

    private static object? GetValue(ManagementBaseObject item, string propertyName)
    {
        try
        {
            return item.GetPropertyValue(propertyName);
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
