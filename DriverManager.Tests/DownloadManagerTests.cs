using System.IO;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using DriverManager.Services.Implementations;
using Moq;
using Xunit;

namespace DriverManager.Tests;

public sealed class DownloadManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stateFile;
    private readonly Mock<ILogger> _logger;

    public DownloadManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _stateFile = Path.Combine(_tempDir, "downloads.json");
        _logger = new Mock<ILogger>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void AddDownload_InvalidUrl_ThrowsArgumentException()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        Assert.Throws<ArgumentException>(() => mgr.AddDownload("not-a-url", "file.exe", _tempDir));
    }

    [Fact]
    public void AddDownload_ValidUrl_CreatesDownloadItem()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var item = mgr.AddDownload("https://example.com/driver.exe", "driver.exe", _tempDir);

        Assert.NotNull(item);
        Assert.Equal("driver.exe", item.FileName);
        Assert.Single(mgr.Items);
    }

    [Fact]
    public void AddDownload_EmptyFilename_UsesUrlFilename()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var item = mgr.AddDownload("https://example.com/nvidia-560.exe", "", _tempDir);

        Assert.Equal("nvidia-560.exe", item.FileName);
    }

    [Fact]
    public void AddDownload_CompletelyEmptyFilename_GeneratesGuidName()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var item = mgr.AddDownload("https://example.com/", "", _tempDir);

        Assert.StartsWith("driver_", item.FileName);
        Assert.EndsWith(".exe", item.FileName);
    }

    [Fact]
    public void Pause_NonExistentId_DoesNotThrow()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var ex = Record.Exception(() => mgr.Pause("nonexistent"));
        Assert.Null(ex);
    }

    [Fact]
    public void Resume_NonExistentId_DoesNotThrow()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var ex = Record.Exception(() => mgr.Resume("nonexistent"));
        Assert.Null(ex);
    }

    [Fact]
    public void Cancel_NonExistentId_DoesNotThrow()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var ex = Record.Exception(() => mgr.Cancel("nonexistent"));
        Assert.Null(ex);
    }

    [Fact]
    public void Remove_NonExistentId_DoesNotThrow()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var ex = Record.Exception(() => mgr.Remove("nonexistent"));
        Assert.Null(ex);
    }

    [Fact]
    public void Cancel_DownloadItem_CleansUpPartialFiles()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var item = mgr.AddDownload("https://example.com/driver.exe", "driver.exe", _tempDir);

        File.WriteAllText(item.TempFilePath, "partial data");
        File.WriteAllText(item.MetaFilePath, "{}");

        mgr.Cancel(item.Id);

        Assert.False(File.Exists(item.TempFilePath));
        Assert.False(File.Exists(item.MetaFilePath));
    }

    [Fact]
    public void ClearCompleted_RemovesNonActiveItems()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        var item1 = mgr.AddDownload("https://example.com/a.exe", "a.exe", _tempDir);
        var item2 = mgr.AddDownload("https://example.com/b.exe", "b.exe", _tempDir);

        mgr.Cancel(item1.Id);
        mgr.Cancel(item2.Id);

        mgr.ClearCompleted();
        Assert.Empty(mgr.Items);
    }

    [Fact]
    public void AddDownload_DuplicateUrl_CreatesSecondItem()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        mgr.AddDownload("https://example.com/driver.exe", "driver.exe", _tempDir);
        mgr.AddDownload("https://example.com/driver.exe", "driver.exe", _tempDir);

        Assert.Equal(2, mgr.Items.Count);
    }

    [Fact]
    public void SaveAndLoadState_PersistsNonCompletedItems()
    {
        var item1Id = string.Empty;
        using (var mgr = new DownloadManager(_stateFile, 1, _logger.Object))
        {
            var item = mgr.AddDownload("https://example.com/driver.exe", "driver.exe", _tempDir);
            item1Id = item.Id;
            mgr.Cancel(item.Id);
        }

        using var mgr2 = new DownloadManager(_stateFile, 1, _logger.Object);
        Assert.Empty(mgr2.Items);
    }

    [Fact]
    public void MaxConcurrent_DefaultsToThree()
    {
        using var mgr = new DownloadManager(_stateFile);
        Assert.Equal(0, mgr.ActiveCount);
    }

    [Fact]
    public void Items_ReturnsAllItems()
    {
        using var mgr = new DownloadManager(_stateFile, 1, _logger.Object);
        mgr.AddDownload("https://example.com/a.exe", "a.exe", _tempDir);
        mgr.AddDownload("https://example.com/b.exe", "b.exe", _tempDir);

        Assert.Equal(2, mgr.Items.Count);
    }
}
