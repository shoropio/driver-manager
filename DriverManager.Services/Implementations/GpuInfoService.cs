using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Management;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Enumera las tarjetas gráficas del equipo (Win32_VideoController), les asigna
/// el estado del controlador de pantalla correspondiente (Win32_PnPSignedDriver)
/// y detecta el software compañero del fabricante.
/// </summary>
public sealed class GpuInfoService : IGpuInfoService
{
    private readonly IDriverScanner _scanner;
    private readonly IGpuSoftwareDetector _softwareDetector;

    public GpuInfoService() : this(new WmiDriverScanner(), new GpuSoftwareDetector())
    {
    }

    public GpuInfoService(IDriverScanner scanner, IGpuSoftwareDetector softwareDetector)
    {
        _scanner = scanner;
        _softwareDetector = softwareDetector;
    }

    public async Task<IReadOnlyList<GpuInfo>> GetGpusAsync(CancellationToken cancellationToken = default)
    {
        var drivers = await _scanner.ScanInstalledDriversAsync(cancellationToken);
        var gpus = await Task.Run(() => QueryVideoControllers(cancellationToken), cancellationToken);
        var dxgiAdapters = await Task.Run(DxgiAdapterEnumerator.Enumerate, cancellationToken);

        var softwareDetectionFailed = false;
        IReadOnlyList<string> installedSoftware = Array.Empty<string>();
        try
        {
            installedSoftware = await _softwareDetector.GetInstalledProgramNamesAsync(cancellationToken);
        }
        catch
        {
            softwareDetectionFailed = true;
        }

        foreach (var gpu in gpus)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var driver = FindDisplayDriver(gpu, drivers);
            if (driver is not null)
            {
                gpu.Status = driver.Status;
                if (string.IsNullOrWhiteSpace(gpu.DriverVersion))
                {
                    gpu.DriverVersion = driver.InstalledVersion;
                }

                if (string.IsNullOrWhiteSpace(gpu.AdapterCompatibility))
                {
                    gpu.AdapterCompatibility = driver.Manufacturer;
                }
            }

            var software = GpuSoftwareCatalog.ForVendor(gpu.Vendor);
            if (software is not null)
            {
                software.InstallStatus = softwareDetectionFailed
                    ? SoftwareInstallStatus.Unknown
                    : GpuSoftwareCatalog.DetermineInstallStatus(gpu.Vendor, installedSoftware);
                gpu.Software = software;
            }

            var dxgiMatch = dxgiAdapters.FirstOrDefault(adapter => AdapterNamesMatch(adapter.Description, gpu.AdapterName));
            if (dxgiMatch is not null)
            {
                gpu.Luid = dxgiMatch.LuidKey;
                gpu.AdapterRamBytes = dxgiMatch.DedicatedVideoMemory;
            }
        }

        return gpus;
    }

    private static IReadOnlyList<GpuInfo> QueryVideoControllers(CancellationToken cancellationToken)
    {
        var gpus = new List<GpuInfo>();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        foreach (ManagementObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();

            gpus.Add(new GpuInfo
            {
                DeviceId = GetString(item, "PNPDeviceID"),
                AdapterCompatibility = GetString(item, "AdapterCompatibility"),
                AdapterName = GetString(item, "Name"),
                VideoProcessor = GetString(item, "VideoProcessor"),
                DriverVersion = GetString(item, "DriverVersion"),
                DriverDate = GetString(item, "DriverDate"),
                AdapterRamBytes = GetLong(item, "AdapterRAM"),
                VideoModeDescription = GetString(item, "VideoModeDescription")
            });
        }

        return gpus;
    }

    private static DriverInfo? FindDisplayDriver(GpuInfo gpu, IReadOnlyList<DriverInfo> drivers)
    {
        var vendor = GpuVendors.Detect(gpu.AdapterCompatibility);
        if (vendor == GpuVendor.Unknown)
        {
            vendor = gpu.Vendor;
        }

        if (vendor == GpuVendor.Unknown)
        {
            return null;
        }

        return drivers.FirstOrDefault(driver =>
            driver.Category.Equals("Display", StringComparison.OrdinalIgnoreCase) &&
            GpuVendors.Detect(driver.Manufacturer) == vendor);
    }

    private static string GetString(ManagementBaseObject item, string propertyName)
    {
        try
        {
            return item.GetPropertyValue(propertyName)?.ToString() ?? string.Empty;
        }
        catch (ManagementException)
        {
            return string.Empty;
        }
    }

    private static long GetLong(ManagementBaseObject item, string propertyName)
    {
        try
        {
            return Convert.ToInt64(item.GetPropertyValue(propertyName));
        }
        catch
        {
            return 0;
        }
    }

    private static bool AdapterNamesMatch(string a, string b) => DriverManager.Core.StringHelper.DeviceNamesMatch(a, b);
}
