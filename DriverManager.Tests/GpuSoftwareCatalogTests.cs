using DriverManager.Core.Models;
using DriverManager.Services.Implementations;
using Xunit;

namespace DriverManager.Tests;

public class GpuSoftwareCatalogTests
{
    [Fact]
    public void ForVendor_Nvidia_ReturnsGeForceExperience()
    {
        var software = GpuSoftwareCatalog.ForVendor(GpuVendor.Nvidia);

        Assert.NotNull(software);
        Assert.Equal("NVIDIA GeForce Experience", software!.Name);
        Assert.Contains("nvidia.com", software.DownloadUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SoftwareInstallStatus.Unknown, software.InstallStatus);
    }

    [Fact]
    public void ForVendor_Amd_ReturnsAdrenalinEdition()
    {
        var software = GpuSoftwareCatalog.ForVendor(GpuVendor.Amd);

        Assert.NotNull(software);
        Assert.Equal("AMD Software: Adrenalin Edition", software!.Name);
        Assert.Contains("amd.com", software.DownloadUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForVendor_Intel_ReturnsArcControl()
    {
        var software = GpuSoftwareCatalog.ForVendor(GpuVendor.Intel);

        Assert.NotNull(software);
        Assert.Equal("Intel Arc Control", software!.Name);
        Assert.Contains("intel.com", software.DownloadUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForVendor_Unknown_ReturnsNull()
    {
        Assert.Null(GpuSoftwareCatalog.ForVendor(GpuVendor.Unknown));
    }

    [Theory]
    [InlineData(GpuVendor.Nvidia, "Microsoft Visual C++", SoftwareInstallStatus.NotInstalled)]
    [InlineData(GpuVendor.Nvidia, "NVIDIA GeForce Experience", SoftwareInstallStatus.Installed)]
    [InlineData(GpuVendor.Nvidia, "NVIDIA App", SoftwareInstallStatus.Installed)]
    [InlineData(GpuVendor.Amd, "AMD Software: Adrenalin Edition", SoftwareInstallStatus.Installed)]
    [InlineData(GpuVendor.Amd, "Visual Studio Code", SoftwareInstallStatus.NotInstalled)]
    [InlineData(GpuVendor.Intel, "Intel Graphics Command Center", SoftwareInstallStatus.Installed)]
    [InlineData(GpuVendor.Intel, "Intel Arc Control", SoftwareInstallStatus.Installed)]
    [InlineData(GpuVendor.Intel, "Notepad", SoftwareInstallStatus.NotInstalled)]
    [InlineData(GpuVendor.Unknown, "NVIDIA GeForce Experience", SoftwareInstallStatus.Unknown)]
    public void DetermineInstallStatus_MatchesByVendorTokens(GpuVendor vendor, string installed, SoftwareInstallStatus expected)
    {
        var status = GpuSoftwareCatalog.DetermineInstallStatus(vendor, new[] { installed });

        Assert.Equal(expected, status);
    }

    [Fact]
    public void DetermineInstallStatus_IsCaseInsensitive()
    {
        var status = GpuSoftwareCatalog.DetermineInstallStatus(GpuVendor.Nvidia, new[] { "nvidia geforce experience" });

        Assert.Equal(SoftwareInstallStatus.Installed, status);
    }

    [Fact]
    public void ForVendor_ReturnsFreshInstanceEachCall()
    {
        var first = GpuSoftwareCatalog.ForVendor(GpuVendor.Nvidia);
        var second = GpuSoftwareCatalog.ForVendor(GpuVendor.Nvidia);

        Assert.Same(first, first);
        Assert.NotSame(first, second);
    }
}
