using System.Collections.Concurrent;
using System.Text.Json;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;

namespace DriverManager.Services.Implementations;

public sealed class DownloadManager : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly string _stateFilePath;
    private readonly int _maxConcurrent;
    private readonly ILogger _logger;

    private readonly object _lock = new();
    private readonly List<DownloadItem> _items = new();
    private readonly Dictionary<string, CancellationTokenSource> _cancellations = new();
    private readonly Dictionary<string, ManualResetEventSlim> _pauses = new();
    private int _activeCount;

    public event Action<DownloadItem>? ItemChanged;
    public event Action? QueueChanged;

    public IReadOnlyList<DownloadItem> Items
    {
        get { lock (_lock) { return _items.ToList(); } }
    }

    public int ActiveCount => _activeCount;
    public int QueueCount => _items.Count(i => i.Status == DownloadStatus.Waiting);

    public DownloadManager(string stateFilePath, int maxConcurrent = 3, ILogger? logger = null)
    {
        _stateFilePath = stateFilePath;
        _maxConcurrent = Math.Max(1, maxConcurrent);
        _logger = logger ?? new FileLogger();

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriverManager/1.0");

        Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);
        LoadState();
    }

    public DownloadItem AddDownload(string url, string fileName, string outputDirectory,
        string deviceName = "", string installedVersion = "", string availableVersion = "", string source = "Manual")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL no válida.", nameof(url));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(uri.LocalPath);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"driver_{Guid.NewGuid():N}.exe";
        }

        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, fileName);
        var tempPath = filePath + ".part";
        var metaPath = filePath + ".meta";

        long resumedBytes = 0;
        if (File.Exists(metaPath))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<DownloadMeta>(File.ReadAllText(metaPath), JsonOptions);
                if (meta is not null && meta.Url == url && File.Exists(tempPath))
                {
                    resumedBytes = meta.ReceivedBytes;
                }
            }
            catch
            {
                resumedBytes = 0;
            }
        }

        var item = new DownloadItem
        {
            Url = url,
            FileName = fileName,
            FilePath = filePath,
            TempFilePath = tempPath,
            MetaFilePath = metaPath,
            TotalBytes = -1,
            ReceivedBytes = resumedBytes,
            Status = DownloadStatus.Waiting,
            Source = source,
            DeviceName = deviceName,
            InstalledVersion = installedVersion,
            AvailableVersion = availableVersion
        };

        lock (_lock)
        {
            _items.Add(item);
        }

        _logger.Log($"Descarga agregada a la cola: {fileName} ({(resumedBytes > 0 ? $"reanudando desde {resumedBytes} bytes" : "nueva")}).");
        SaveState();
        QueueChanged?.Invoke();
        ProcessQueue();
        return item;
    }

    public void Pause(string itemId)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item is null || item.Status != DownloadStatus.Downloading)
            {
                return;
            }

            item.Status = DownloadStatus.Paused;
            if (_pauses.TryGetValue(itemId, out var pause))
            {
                pause.Reset();
            }

            if (_cancellations.TryGetValue(itemId, out var cts))
            {
                cts.Cancel();
            }

            _logger.Log($"Descarga pausada: {item.FileName}.");
            SaveState();
            ItemChanged?.Invoke(item);
            QueueChanged?.Invoke();
        }
    }

    public void Resume(string itemId)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item is null || item.Status != DownloadStatus.Paused && item.Status != DownloadStatus.Failed)
            {
                return;
            }

            item.Status = DownloadStatus.Waiting;
            item.ErrorMessage = string.Empty;
            _logger.Log($"Descarga reanudada: {item.FileName}.");
            SaveState();
            ItemChanged?.Invoke(item);
            QueueChanged?.Invoke();
        }

        ProcessQueue();
    }

    public void Cancel(string itemId)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item is null)
            {
                return;
            }

            if (_cancellations.TryGetValue(itemId, out var cts))
            {
                cts.Cancel();
            }

            if (_pauses.TryGetValue(itemId, out var pause))
            {
                pause.Set();
            }

            item.Status = DownloadStatus.Cancelled;
            _logger.Log($"Descarga cancelada: {item.FileName}.");
            CleanupPartialFiles(item);
            SaveState();
            ItemChanged?.Invoke(item);
            QueueChanged?.Invoke();
        }

        ProcessQueue();
    }

    public void ClearCompleted()
    {
        lock (_lock)
        {
            var toRemove = _items.Where(i =>
                i.Status is DownloadStatus.Completed or DownloadStatus.Cancelled).ToList();
            foreach (var item in toRemove)
            {
                _items.Remove(item);
            }

            _logger.Log($"Cola limpiada: {toRemove.Count} elemento(s) eliminado(s).");
        }

        SaveState();
        QueueChanged?.Invoke();
    }

    public void Remove(string itemId)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == itemId);
            if (item is null)
            {
                return;
            }

            if (item.Status == DownloadStatus.Downloading)
            {
                Cancel(itemId);
                return;
            }

            _items.Remove(item);
        }

        SaveState();
        QueueChanged?.Invoke();
    }

    private void ProcessQueue()
    {
        List<DownloadItem> toStart;
        lock (_lock)
        {
            while (_activeCount < _maxConcurrent)
            {
                var next = _items.FirstOrDefault(i => i.Status == DownloadStatus.Waiting);
                if (next is null)
                {
                    break;
                }

                next.Status = DownloadStatus.Downloading;
                _activeCount++;

                var cts = new CancellationTokenSource();
                var pause = new ManualResetEventSlim(true);
                _cancellations[next.Id] = cts;
                _pauses[next.Id] = pause;

                toStart = new List<DownloadItem> { next };
                _ = Task.Run(() => DownloadAsync(next, cts.Token, pause));
            }
        }
    }

    private async Task DownloadAsync(DownloadItem item, CancellationToken ct, ManualResetEventSlim pause)
    {
        var attempts = 0;
        var success = false;

        while (attempts <= item.MaxRetries && !success)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
                if (item.ReceivedBytes > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(item.ReceivedBytes, null);
                }

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                if (item.ReceivedBytes == 0 || item.TotalBytes <= 0)
                {
                    item.TotalBytes = response.Content.Headers.ContentLength ?? -1;
                }

                var isResume = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                if (!isResume && item.ReceivedBytes > 0)
                {
                    item.ReceivedBytes = 0;
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                var mode = item.ReceivedBytes > 0 ? FileMode.Append : FileMode.Create;
                await using var fileStream = new FileStream(item.TempFilePath, mode, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long bytesReadSession = 0;
                var speedSamples = new Queue<(long bytes, DateTime time)>();
                var lastReport = DateTime.UtcNow;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    pause.Wait(ct);

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    item.ReceivedBytes += bytesRead;
                    bytesReadSession += bytesRead;

                    var now = DateTime.UtcNow;
                    speedSamples.Enqueue((bytesRead, now));
                    while (speedSamples.Count > 10)
                    {
                        speedSamples.Dequeue();
                    }

                    if ((now - lastReport).TotalMilliseconds >= 500 || bytesReadSession >= 1048576)
                    {
                        if (speedSamples.Count >= 2)
                        {
                            var totalBytes = speedSamples.Sum(s => s.bytes);
                            var totalTime = (speedSamples.Last().time - speedSamples.First().time).TotalSeconds;
                            if (totalTime > 0)
                            {
                                item.SpeedBytesPerSec = totalBytes / totalTime;
                            }
                        }

                        ItemChanged?.Invoke(item);
                        lastReport = now;
                    }

                    SaveMeta(item);
                }

                item.SpeedBytesPerSec = 0;
                item.Status = DownloadStatus.Completed;
                item.CompletedAt = DateTime.Now;
                success = true;

                CleanupMeta(item);
                if (File.Exists(item.TempFilePath))
                {
                    File.Move(item.TempFilePath, item.FilePath, overwrite: true);
                }

                _logger.Log($"Descarga completada: {item.FileName} ({FormatBytes(item.ReceivedBytes)}).");
                ItemChanged?.Invoke(item);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                attempts++;
                if (attempts <= item.MaxRetries)
                {
                    _logger.Log($"Error de red en {item.FileName}, reintento {attempts}/{item.MaxRetries}...");
                    item.RetryCount = attempts;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)), CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                if (item.Status != DownloadStatus.Paused)
                {
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = "Descarga cancelada.";
                    CleanupPartialFiles(item);
                }

                ItemChanged?.Invoke(item);
                break;
            }
            catch (Exception ex)
            {
                attempts++;
                item.RetryCount = attempts;
                item.ErrorMessage = ex.Message;
                _logger.LogError($"Error descargando {item.FileName}: {ex.Message}", ex);

                if (attempts <= item.MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)), CancellationToken.None);
                }
                else
                {
                    item.Status = DownloadStatus.Failed;
                    ItemChanged?.Invoke(item);
                }
            }
        }

        if (!success && item.Status == DownloadStatus.Downloading)
        {
            item.Status = DownloadStatus.Failed;
            if (string.IsNullOrWhiteSpace(item.ErrorMessage))
            {
                item.ErrorMessage = "Número máximo de reintentos alcanzado.";
            }

            ItemChanged?.Invoke(item);
        }

        lock (_lock)
        {
            _activeCount--;
            _cancellations.Remove(item.Id);
            _pauses.Remove(item.Id);
        }

        SaveState();
        QueueChanged?.Invoke();
        ProcessQueue();
    }

    private void CleanupPartialFiles(DownloadItem item)
    {
        try { if (File.Exists(item.TempFilePath)) File.Delete(item.TempFilePath); } catch { }
        CleanupMeta(item);
    }

    private void CleanupMeta(DownloadItem item)
    {
        try { if (File.Exists(item.MetaFilePath)) File.Delete(item.MetaFilePath); } catch { }
    }

    private void SaveMeta(DownloadItem item)
    {
        try
        {
            var meta = new DownloadMeta { Url = item.Url, ReceivedBytes = item.ReceivedBytes, TotalBytes = item.TotalBytes };
            File.WriteAllText(item.MetaFilePath, JsonSerializer.Serialize(meta, JsonOptions));
        }
        catch
        {
        }
    }

    private void SaveState()
    {
        try
        {
            List<DownloadItem> snapshot;
            lock (_lock)
            {
                snapshot = _items.Where(i =>
                    i.Status is DownloadStatus.Waiting or DownloadStatus.Paused or DownloadStatus.Downloading or DownloadStatus.Failed)
                    .Select(i => new DownloadItem
                    {
                        Id = i.Id,
                        Url = i.Url,
                        FileName = i.FileName,
                        FilePath = i.FilePath,
                        TempFilePath = i.TempFilePath,
                        MetaFilePath = i.MetaFilePath,
                        TotalBytes = i.TotalBytes,
                        ReceivedBytes = i.ReceivedBytes,
                        Status = i.Status == DownloadStatus.Downloading ? DownloadStatus.Waiting : i.Status,
                        ErrorMessage = i.ErrorMessage,
                        Source = i.Source,
                        DeviceName = i.DeviceName,
                        InstalledVersion = i.InstalledVersion,
                        AvailableVersion = i.AvailableVersion,
                        CreatedAt = i.CreatedAt,
                        RetryCount = i.RetryCount,
                        MaxRetries = i.MaxRetries
                    }).ToList();
            }

            File.WriteAllText(_stateFilePath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al guardar el estado de descargas.", ex);
        }
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_stateFilePath);
            var items = JsonSerializer.Deserialize<List<DownloadItem>>(json, JsonOptions);
            if (items is null)
            {
                return;
            }

            lock (_lock)
            {
                foreach (var item in items)
                {
                    if (item.Status == DownloadStatus.Downloading)
                    {
                        item.Status = DownloadStatus.Waiting;
                    }

                    if (item.Status is DownloadStatus.Waiting or DownloadStatus.Paused or DownloadStatus.Failed)
                    {
                        _items.Add(item);
                    }
                }
            }

            _logger.Log($"Cola de descargas cargada: {_items.Count} elemento(s) pendiente(s).");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al cargar el estado de descargas.", ex);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var cts in _cancellations.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _cancellations.Clear();
            _pauses.Clear();
        }

        _http.Dispose();
    }

    private sealed class DownloadMeta
    {
        public string Url { get; set; } = string.Empty;
        public long ReceivedBytes { get; set; }
        public long TotalBytes { get; set; } = -1;
    }
}
