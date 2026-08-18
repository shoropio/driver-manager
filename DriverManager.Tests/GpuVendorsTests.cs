using DriverManager.Core.Models;
using Xunit;

namespace DriverManager.Tests;

public class GpuVendorsTests
{
    [Theory]
    [InlineData("NVIDIA", GpuVendor.Nvidia)]
    [InlineData("NVIDIA Corporation", GpuVendor.Nvidia)]
    [InlineData("Advanced Micro Devices, Inc.", GpuVendor.Amd)]
    [InlineData("AMD", GpuVendor.Amd)]
    [InlineData("ATI Radeon HD 5670", GpuVendor.Amd)]
    [InlineData("Intel Corporation", GpuVendor.Intel)]
    [InlineData("Intel(R) UHD Graphics", GpuVendor.Intel)]
    [InlineData("", GpuVendor.Unknown)]
    [InlineData(null, GpuVendor.Unknown)]
    public void Detect_ClassifiesKnownStrings(string? value, GpuVendor expected)
    {
        Assert.Equal(expected, GpuVendors.Detect(value));
    }

    [Fact]
    public void Detect_IntelCorporation_IsNotAmd()
    {
        Assert.Equal(GpuVendor.Intel, GpuVendors.Detect("Intel Corporation"));
    }
}
