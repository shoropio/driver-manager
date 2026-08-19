using System.Globalization;
using DriverManager.Core;
using Xunit;

namespace DriverManager.Tests;

public sealed class FormatHelperTests
{
    [Theory]
    [InlineData(0, "—")]
    [InlineData(-1, "—")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void FormatBytes_ReturnsCorrectFormat(long bytes, string expected)
    {
        var culture = CultureInfo.InvariantCulture;
        var result = FormatHelper.FormatBytes(bytes, "—");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytes_ZeroBytes_ReturnsFallback()
    {
        Assert.Equal("N/A", FormatHelper.FormatBytes(0, "N/A"));
    }
}

public sealed class StringHelperTests
{
    [Theory]
    [InlineData("NVIDIA GeForce RTX 4090", "nvidiageforcertx4090")]
    [InlineData("Intel(R) UHD Graphics 630", "intelruhdgraphics630")]
    [InlineData("AMD Radeon RX 7900 XTX", "amdradeonrx7900xtx")]
    public void Normalize_RemovesNonAlphanumericAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, StringHelper.Normalize(input));
    }

    [Fact]
    public void DeviceNamesMatch_ContainedNames_ReturnsTrue()
    {
        Assert.True(StringHelper.DeviceNamesMatch("NVIDIA GeForce RTX 4090", "geforce rtx 4090"));
    }

    [Fact]
    public void DeviceNamesMatch_DifferentNames_ReturnsFalse()
    {
        Assert.False(StringHelper.DeviceNamesMatch("NVIDIA GeForce RTX 4090", "AMD Radeon RX 7900"));
    }

    [Fact]
    public void DeviceNamesMatch_ShortNames_ReturnsFalse()
    {
        Assert.False(StringHelper.DeviceNamesMatch("GPU", "GPU"));
    }
}
