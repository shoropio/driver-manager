using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Management;

namespace DriverManager.Services.Implementations;

public sealed class WmiDriverScanner : IDriverScanner
{
    public async Task<IReadOnlyList<DriverInfo>> ScanInstalledDriversAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var errorCodes = LoadConfigManagerErrorCodes(cancellationToken);
            var drivers = new List<DriverInfo>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPSignedDriver");
            foreach (ManagementObject item in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var driver = new DriverInfo
                {
                    DeviceName = GetString(item, "DeviceName"),
                    Manufacturer = GetString(item, "Manufacturer"),
                    Category = GetString(item, "DeviceClass"),
                    InstalledVersion = GetString(item, "DriverVersion"),
                    InstallDate = ParseInstallDate(GetString(item, "InstallDate")),
                    Provider = GetString(item, "DriverProviderName"),
                    DriverPath = GetString(item, "InfName"),
                    HardwareId = JoinHardwareIds(GetValue(item, "HardwareID"))
                };

                if (IsPhantomRecord(driver, item))
                {
                    continue;
                }

                if (IsBroken(item) || HasErrorCode(driver, errorCodes, item))
                {
                    driver.Status = DriverStatus.ProblemDetected;
                }
                else if (!string.IsNullOrWhiteSpace(driver.InstalledVersion))
                {
                    driver.Status = DriverStatus.UpToDate;
                }

                drivers.Add(driver);
            }

            return drivers;
        }, cancellationToken);
    }

    private static bool IsPhantomRecord(DriverInfo driver, ManagementBaseObject item)
    {
        var name = GetString(item, "Name");
        return string.IsNullOrWhiteSpace(driver.DeviceName)
            && string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(driver.HardwareId)
            && string.IsNullOrWhiteSpace(driver.DriverPath);
    }

    private static bool IsBroken(ManagementObject item)
    {
        var status = GetString(item, "Status");
        return status.Equals("ERROR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasErrorCode(DriverInfo driver, IReadOnlyDictionary<string, uint> errorCodes, ManagementObject item)
    {
        var deviceId = GetString(item, "PNPDeviceID");
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = GetString(item, "DeviceID");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        return errorCodes.TryGetValue(deviceId, out var code) && code != 0;
    }

    private static Dictionary<string, uint> LoadConfigManagerErrorCodes(CancellationToken cancellationToken)
    {
        var codes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher("SELECT DeviceID, ConfigManagerErrorCode FROM Win32_PnPEntity");
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

    private static string GetString(ManagementBaseObject item, string propertyName)
    {
        return GetValue(item, propertyName)?.ToString() ?? string.Empty;
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

    private static string JoinHardwareIds(object? rawHardwareId)
    {
        if (rawHardwareId is null)
        {
            return string.Empty;
        }

        var ids = new List<string>();
        switch (rawHardwareId)
        {
            case string single:
                ids.Add(single);
                break;
            case System.Collections.IEnumerable enumerable:
                foreach (var value in enumerable)
                {
                    if (value?.ToString() is { Length: > 0 } text)
                    {
                        ids.Add(text);
                    }
                }
                break;
            default:
                if (rawHardwareId.ToString() is { Length: > 0 } fallback)
                {
                    ids.Add(fallback);
                }
                break;
        }

        return string.Join(";", ids.Distinct());
    }

    private static DateTime ParseInstallDate(string? rawDate)
    {
        if (string.IsNullOrWhiteSpace(rawDate) || rawDate.Length < 8)
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParseExact(rawDate.Substring(0, 8), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        return DateTime.MinValue;
    }
}
