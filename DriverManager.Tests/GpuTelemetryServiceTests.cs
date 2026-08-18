using DriverManager.Services.Implementations;
using Xunit;

namespace DriverManager.Tests;

public class GpuTelemetryServiceTests
{
    [Theory]
    [InlineData("luid_0x00000000_0x00013DA6_phys_0_eng_0_engtype_3D", "0x00000000_0x00013DA6")]
    [InlineData("luid_0x00000000_0x0001424E_phys_0", "0x00000000_0x0001424E")]
    [InlineData("luid_0x00000000_0x0001421A_phys_1_eng_2_engtype_Copy", "0x00000000_0x0001421A")]
    public void TryParseLuid_ExtractsKeyBetweenMarkers(string counterName, string expected)
    {
        var parsed = GpuTelemetryService.TryParseLuid(counterName, out var luidKey);

        Assert.True(parsed);
        Assert.Equal(expected, luidKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin_marcador_phys_0")]
    [InlineData("luid_0x00000000_0x00013DA6")]
    public void TryParseLuid_RejectsInvalidNames(string counterName)
    {
        var parsed = GpuTelemetryService.TryParseLuid(counterName, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryMatchTemperature_MatchesNormalizedNames()
    {
        var temperatures = new Dictionary<string, double>
        {
            ["NVIDIA GeForce RTX 2060"] = 58
        };

        var matched = GpuTelemetryService.TryMatchTemperature("NVIDIA GeForce RTX 2060", temperatures, out var temperature);

        Assert.True(matched);
        Assert.Equal(58, temperature);
    }

    [Fact]
    public void TryMatchTemperature_WhenNoDeviceMatches_ReturnsFalse()
    {
        var temperatures = new Dictionary<string, double>
        {
            ["NVIDIA GeForce RTX 2060"] = 58
        };

        var matched = GpuTelemetryService.TryMatchTemperature("Intel(R) UHD Graphics", temperatures, out _);

        Assert.False(matched);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryMatchTemperature_WhenAdapterNameBlank_ReturnsFalse(string? adapterName)
    {
        var matched = GpuTelemetryService.TryMatchTemperature(
            adapterName,
            new Dictionary<string, double> { ["NVIDIA GeForce RTX 2060"] = 58 },
            out _);

        Assert.False(matched);
    }

    [Fact]
    public async Task NvidiaTemperatureProvider_NeverThrowsAndReturnsDictionary()
    {
        var provider = new NvidiaTemperatureProvider();

        var temperatures = await provider.ReadTemperaturesAsync();

        Assert.NotNull(temperatures);
        Assert.All(temperatures, pair => Assert.InRange(pair.Value, 0, 125));
    }
}
