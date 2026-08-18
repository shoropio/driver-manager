using System.IO;
using DriverManager.Services.Implementations;
using Xunit;

namespace DriverManager.Tests;

public class DriverBackupServiceTests
{
    private readonly DriverBackupService _service = new();

    [Fact]
    public async Task EnforceRetention_DeletesOldestBeyondMax()
    {
        var folder = CreateTempFolder();
        try
        {
            var names = new[]
            {
                "DriverBackup_20260101_100000.zip",
                "DriverBackup_20260102_100000.zip",
                "DriverBackup_20260103_100000.zip",
                "DriverBackup_20260104_100000.zip",
                "DriverBackup_20260105_100000.zip"
            };
            foreach (var name in names)
            {
                await File.WriteAllTextAsync(Path.Combine(folder, name), "dummy");
            }

            var deleted = await _service.EnforceRetentionAsync(folder, maxBackups: 3);

            Assert.Equal(2, deleted);
            Assert.False(File.Exists(Path.Combine(folder, names[0])));
            Assert.False(File.Exists(Path.Combine(folder, names[1])));
            Assert.True(File.Exists(Path.Combine(folder, names[2])));
            Assert.True(File.Exists(Path.Combine(folder, names[3])));
            Assert.True(File.Exists(Path.Combine(folder, names[4])));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task EnforceRetention_WhenWithinLimit_DeletesNothing()
    {
        var folder = CreateTempFolder();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "DriverBackup_20260101_100000.zip"), "dummy");

            var deleted = await _service.EnforceRetentionAsync(folder, maxBackups: 5);

            Assert.Equal(0, deleted);
            Assert.Single(Directory.EnumerateFiles(folder, "DriverBackup_*.zip"));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task EnforceRetention_WhenFolderMissing_ReturnsZero()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"dm_missing_{Guid.NewGuid():N}");

        var deleted = await _service.EnforceRetentionAsync(folder, maxBackups: 3);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public async Task EnforceRetention_IgnoresForeignFiles()
    {
        var folder = CreateTempFolder();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "DriverBackup_20260101_100000.zip"), "dummy");
            await File.WriteAllTextAsync(Path.Combine(folder, "not-a-backup.txt"), "keep");

            var deleted = await _service.EnforceRetentionAsync(folder, maxBackups: 1);

            Assert.Equal(0, deleted);
            Assert.True(File.Exists(Path.Combine(folder, "not-a-backup.txt")));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public async Task EnforceRetention_InvalidMaxBackups_Throws()
    {
        var folder = CreateTempFolder();
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _service.EnforceRetentionAsync(folder, maxBackups: 0));
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
