using DriverManager.Core.Models;
using DriverManager.App.Localization;
using Xunit;

namespace DriverManager.Tests;

public class DriverTextTests
{
    [Theory]
    [InlineData(DriverStatus.Installed, "Instalado")]
    [InlineData(DriverStatus.UpToDate, "Actualizado")]
    [InlineData(DriverStatus.UpdateAvailable, "Requiere actualización")]
    [InlineData(DriverStatus.ProblemDetected, "Error")]
    [InlineData(DriverStatus.BackupAvailable, "Respaldo disponible")]
    [InlineData(DriverStatus.Unknown, "Desconocido")]
    public void StatusText_MapsAllKnownStatuses(DriverStatus status, string expected)
    {
        Assert.Equal(expected, DriverText.StatusText(status));
    }

    [Theory]
    [InlineData("Instalado", DriverStatus.Installed)]
    [InlineData("Actualizado", DriverStatus.UpToDate)]
    [InlineData("Requiere actualización", DriverStatus.UpdateAvailable)]
    [InlineData("Error", DriverStatus.ProblemDetected)]
    [InlineData("Respaldo disponible", DriverStatus.BackupAvailable)]
    public void StatusFromText_RecognizesSpanishTexts(string text, DriverStatus expected)
    {
        Assert.Equal(expected, DriverText.StatusFromText(text));
    }

    [Theory]
    [InlineData("", "No disponible")]
    [InlineData(null, "No disponible")]
    [InlineData("  ", "No disponible")]
    [InlineData("10.0.22621.1", "10.0.22621.1")]
    public void VersionText_NeverReturnsBlank(string? version, string expected)
    {
        Assert.Equal(expected, DriverText.VersionText(version));
    }

    [Theory]
    [InlineData(null, "Dispositivo desconocido")]
    [InlineData("", "Dispositivo desconocido")]
    public void DeviceNameText_FallsBackOnBlank(string? name, string expected)
    {
        Assert.Equal(expected, DriverText.DeviceNameText(name));
    }

    [Theory]
    [InlineData("System Firmware 1.0", "Firmware del sistema 1.0")]
    [InlineData("Intel Processor 13th Gen", "Procesador Intel 13th Gen")]
    [InlineData("Intel USB xHCI Controller", "Intel USB xHCI Controlador")]
    [InlineData("Disk drive", "Unidad de disco")]
    public void DeviceNameText_TranslatesKnownNames(string name, string expected)
    {
        Assert.Equal(expected, DriverText.DeviceNameText(name));
    }

    [Theory]
    [InlineData("DISPLAY", "Pantalla")]
    [InlineData("NET", "Red")]
    [InlineData("usb", "USB")]
    [InlineData("", "Sin categoría")]
    [InlineData(null, "Sin categoría")]
    public void CategoryText_TranslatesKnownCategories(string? category, string expected)
    {
        Assert.Equal(expected, DriverText.CategoryText(category));
    }

    [Theory]
    [InlineData(null, "Fabricante desconocido")]
    [InlineData("Intel", "Intel")]
    public void ManufacturerText_FallsBackOnBlank(string? manufacturer, string expected)
    {
        Assert.Equal(expected, DriverText.ManufacturerText(manufacturer));
    }

    [Theory]
    [InlineData(GpuVendor.Nvidia, "NVIDIA")]
    [InlineData(GpuVendor.Amd, "AMD")]
    [InlineData(GpuVendor.Intel, "Intel")]
    [InlineData(GpuVendor.Unknown, "Desconocido")]
    public void GpuVendorText_MapsAllVendors(GpuVendor vendor, string expected)
    {
        Assert.Equal(expected, DriverText.GpuVendorText(vendor));
    }

    [Theory]
    [InlineData(SoftwareInstallStatus.Installed, "Instalado")]
    [InlineData(SoftwareInstallStatus.NotInstalled, "No instalado")]
    [InlineData(SoftwareInstallStatus.Unknown, "No disponible")]
    public void SoftwareStatusText_MapsAllStatuses(SoftwareInstallStatus status, string expected)
    {
        Assert.Equal(expected, DriverText.SoftwareStatusText(status));
    }

    [Theory]
    [InlineData(0, "No disponible")]
    [InlineData(-5, "No disponible")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(6_442_450_944, "6 GB")]
    [InlineData(16_777_216, "16 MB")]
    public void FormatSize_FormatsBytes(long bytes, string expected)
    {
        Assert.Equal(expected, DriverText.FormatSize(bytes));
    }

    public static TheoryData<double?, string> PercentCases => new()
    {
        { null, "No disponible" },
        { 23.0, "23 %" },
        { 23.45, "23.5 %" },
        { 100.0, "100 %" }
    };

    [Theory]
    [MemberData(nameof(PercentCases))]
    public void FormatPercent_FormatsOrFallsBack(double? percent, string expected)
    {
        Assert.Equal(expected, DriverText.FormatPercent(percent));
    }

    public static TheoryData<long?, long?, string> MemoryCases => new()
    {
        { null, null, "No disponible" },
        { 0L, 2048L, "No disponible" },
        { 1024L, null, "1 KB" },
        { 1536L, 2048L, "1.5 KB de 2 KB" },
        { 6_442_450_944L, 6_644_418_560L, "6 GB de 6.19 GB" }
    };

    [Theory]
    [MemberData(nameof(MemoryCases))]
    public void FormatMemory_FormatsUsageAndLimit(long? usage, long? limit, string expected)
    {
        Assert.Equal(expected, DriverText.FormatMemory(usage, limit));
    }
}
