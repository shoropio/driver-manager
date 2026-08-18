using System.IO;
using DriverManager.Core.Models;
using DriverManager.Services.Implementations;
using Xunit;

namespace DriverManager.Tests;

public class DriverStateStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileMissing_ReturnsNull()
    {
        var folder = CreateTempFolder();
        try
        {
            var store = new DriverStateStore(folder);
            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsSnapshot()
    {
        var folder = CreateTempFolder();
        try
        {
            var store = new DriverStateStore(folder);
            var expected = new DriverStateSnapshot
            {
                LastScanAt = new DateTime(2026, 1, 15, 10, 30, 0),
                UpdateSource = "Test",
                Drivers = new List<DriverInfo>
                {
                    new() { DeviceName = "Test Device", Category = "NET", Status = DriverStatus.UpToDate }
                }
            };

            await store.SaveAsync(expected);
            Assert.True(File.Exists(store.StatePath));

            var loaded = await store.LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(expected.LastScanAt, loaded.LastScanAt);
            Assert.Equal(expected.UpdateSource, loaded.UpdateSource);
            var driver = Assert.Single(loaded.Drivers);
            Assert.Equal("Test Device", driver.DeviceName);
            Assert.Equal(DriverStatus.UpToDate, driver.Status);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsGpus()
    {
        var folder = CreateTempFolder();
        try
        {
            var store = new DriverStateStore(folder);
            var expected = new DriverStateSnapshot
            {
                Gpus = new List<GpuInfo>
                {
                    new()
                    {
                        AdapterName = "NVIDIA GeForce RTX 2060",
                        AdapterCompatibility = "NVIDIA",
                        DriverVersion = "32.0.15.9636",
                        Luid = "0x00000000_0x0001424E",
                        AdapterRamBytes = 6_244_270_080,
                        Status = DriverStatus.UpToDate,
                        Software = new GpuSoftware
                        {
                            Name = "NVIDIA GeForce Experience",
                            Vendor = "Nvidia",
                            InstallStatus = SoftwareInstallStatus.Installed
                        }
                    }
                }
            };

            await store.SaveAsync(expected);
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            var gpu = Assert.Single(loaded.Gpus);
            Assert.Equal("NVIDIA GeForce RTX 2060", gpu.AdapterName);
            Assert.Equal("0x00000000_0x0001424E", gpu.Luid);
            Assert.Equal(6_244_270_080, gpu.AdapterRamBytes);
            Assert.Equal(DriverStatus.UpToDate, gpu.Status);
            Assert.Equal(GpuVendor.Nvidia, gpu.Vendor);
            Assert.Equal("NVIDIA GeForce Experience", gpu.Software?.Name);
            Assert.Equal(SoftwareInstallStatus.Installed, gpu.Software?.InstallStatus);
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task SaveAndClear_RemovesStateFile()
    {
        var folder = CreateTempFolder();
        try
        {
            var store = new DriverStateStore(folder);
            await store.SaveAsync(new DriverStateSnapshot());
            Assert.True(File.Exists(store.StatePath));

            store.Clear();
            Assert.False(File.Exists(store.StatePath));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsNull()
    {
        var folder = CreateTempFolder();
        try
        {
            var store = new DriverStateStore(folder);
            await File.WriteAllTextAsync(store.StatePath, "{{ not json");
            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dm_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, true);
        }
        catch
        {
        }
    }
}
