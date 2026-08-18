using DriverManager.Services.Implementations;
using Xunit;

namespace DriverManager.Tests;

public class GpuSoftwareDetectorTests
{
    [Fact]
    public async Task GetInstalledProgramNamesAsync_CombinesHivesAndDeduplicates()
    {
        var calls = new List<string>();
        var detector = new GpuSoftwareDetector(hive =>
        {
            calls.Add(hive);
            return new[] { "NVIDIA GeForce Experience", "Visual Studio Code", "nvidia geforce experience" };
        });

        var names = await detector.GetInstalledProgramNamesAsync();

        Assert.Equal(2, calls.Count);
        Assert.Equal(2, names.Count);
        Assert.Equal(new[] { "NVIDIA GeForce Experience", "Visual Studio Code" }, names);
    }

    [Fact]
    public async Task GetInstalledProgramNamesAsync_WhenNoMatches_ReturnsEmpty()
    {
        var detector = new GpuSoftwareDetector(_ => Array.Empty<string>());

        var names = await detector.GetInstalledProgramNamesAsync();

        Assert.Empty(names);
    }

    [Fact]
    public async Task GetInstalledProgramNamesAsync_IgnoresBlankEntries()
    {
        var detector = new GpuSoftwareDetector(_ => new[] { "  ", string.Empty, "AMD Software" });

        var names = await detector.GetInstalledProgramNamesAsync();

        Assert.Equal(new[] { "AMD Software" }, names);
    }

    [Fact]
    public async Task GetInstalledProgramNamesAsync_PropagatesReaderFailure()
    {
        var detector = new GpuSoftwareDetector(_ => throw new UnauthorizedAccessException("registro no accesible"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => detector.GetInstalledProgramNamesAsync());
    }
}
