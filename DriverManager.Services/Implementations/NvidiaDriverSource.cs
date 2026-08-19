using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Text.Json;
using System.Xml.Linq;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Busca el controlador más reciente para la GPU NVIDIA del equipo usando los
/// servicios públicos de NVIDIA (búsqueda de producto + AjaxDriverService).
/// </summary>
public sealed class NvidiaDriverSource : IDriverUpdateSource
{
    private const string LookupUrl = "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3";
    private const string DriverApiUrl = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php";
    private const int Windows10Or11X64OsId = 57;

    private readonly IDriverScanner _scanner;
    private readonly HttpClient _http;

    public NvidiaDriverSource() : this(new WmiDriverScanner())
    {
    }

    public NvidiaDriverSource(IDriverScanner scanner)
    {
        _scanner = scanner;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriverManager/1.0");
    }

    public async Task<IReadOnlyList<DriverInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var installed = await _scanner.ScanInstalledDriversAsync(cancellationToken);
        var gpu = installed.FirstOrDefault(d =>
            d.Category.Equals("Display", StringComparison.OrdinalIgnoreCase) &&
            (d.Manufacturer.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
             d.DeviceName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)));

        if (gpu is null)
        {
            return Array.Empty<DriverInfo>();
        }

        var productId = await FindProductIdAsync(gpu.DeviceName, cancellationToken);
        if (productId is null)
        {
            return Array.Empty<DriverInfo>();
        }

        var latest = await GetLatestDriverAsync(productId, cancellationToken);
        if (latest is null)
        {
            return Array.Empty<DriverInfo>();
        }

        if (IsNewer(latest.Version, gpu.InstalledVersion))
        {
            return new[]
            {
                new DriverInfo
                {
                    DeviceName = gpu.DeviceName,
                    Category = "Display",
                    Manufacturer = "NVIDIA",
                    InstalledVersion = gpu.InstalledVersion,
                    AvailableVersion = latest.Version,
                    Provider = "NVIDIA",
                    DriverPath = latest.DownloadUrl,
                    UpdateId = latest.Version,
                    Status = DriverStatus.UpdateAvailable
                }
            };
        }

        return Array.Empty<DriverInfo>();
    }

    public async Task<DriverUpdateResult> DownloadAndInstallAsync(
        IReadOnlyList<DriverInfo> updates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0)
        {
            return new DriverUpdateResult { Success = true, Message = "No hay actualizaciones que descargar." };
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DriverManagerDownloads");
        Directory.CreateDirectory(folder);

        try
        {
            foreach (var update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(update.DriverPath) ||
                    !Uri.TryCreate(update.DriverPath, UriKind.Absolute, out var url))
                {
                    continue;
                }

                var fileName = Path.GetFileName(url.LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = $"nvidia_{update.AvailableVersion}.exe";
                }

                var target = Path.Combine(folder, fileName);
                progress?.Report($"Descargando {fileName}...");
                await using var responseStream = await _http.GetStreamAsync(url, cancellationToken);
                await using var fileStream = File.Create(target);
                await responseStream.CopyToAsync(fileStream, cancellationToken);
            }

            return new DriverUpdateResult
            {
                Success = true,
                Message = $"Controlador descargado en {folder}. Ejecuta el instalador manualmente.",
                Details = "Los instaladores de NVIDIA no se ejecutan automáticamente por seguridad."
            };
        }
        catch (Exception ex)
        {
            return new DriverUpdateResult { Success = false, Message = "Error al descargar el controlador de NVIDIA.", Details = ex.Message };
        }
    }

    private async Task<string?> FindProductIdAsync(string gpuName, CancellationToken cancellationToken)
    {
        var xml = await GetStringWithRetryAsync(LookupUrl, cancellationToken);
        if (xml is null)
        {
            return null;
        }

        var document = XDocument.Parse(xml);
        var candidates = document.Descendants("LookupValue")
            .Where(e => e.Element("Name") is not null && e.Element("Value") is not null)
            .Select(e => new
            {
                Name = e.Element("Name")!.Value,
                Value = e.Element("Value")!.Value
            })
            .ToList();

        var normalized = Normalize(gpuName);
        var best = candidates
            .Where(c => normalized.Contains(Normalize(c.Name)) && Normalize(c.Name).Length >= 6)
            .OrderByDescending(c => Normalize(c.Name).Length)
            .FirstOrDefault();

        return best?.Value;
    }

    private async Task<NvidiaDriverInfo?> GetLatestDriverAsync(string productId, CancellationToken cancellationToken)
    {
        var url = $"{DriverApiUrl}?func=DriverManualLookup&pfid={Uri.EscapeDataString(productId)}&osID={Windows10Or11X64OsId}&languageCode=1033&isWHQL=1&dch=1&sort1=0&numberOfResults=1";
        var json = await GetStringWithRetryAsync(url, cancellationToken);
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("IDS", out var ids) || ids.GetArrayLength() == 0)
        {
            return null;
        }

        var downloadInfo = ids[0].GetProperty("downloadInfo");
        var version = GetString(downloadInfo, "Version");
        var downloadUrl = GetString(downloadInfo, "DownloadURL");
        if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        return new NvidiaDriverInfo
        {
            Version = Uri.UnescapeDataString(version),
            DownloadUrl = Uri.UnescapeDataString(downloadUrl),
            FileSize = GetString(downloadInfo, "DownloadURLFileSize"),
            ReleaseDate = GetString(downloadInfo, "ReleaseDateTime")
        };
    }

    private async Task<string?> GetStringWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await _http.GetStringAsync(url, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }

        return null;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static bool IsNewer(string candidate, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return true;
        }

        var candidateParts = ParseVersion(candidate);
        var currentParts = ParseVersion(current);
        var count = Math.Max(candidateParts.Count, currentParts.Count);
        for (var i = 0; i < count; i++)
        {
            var c = i < candidateParts.Count ? candidateParts[i] : 0;
            var k = i < currentParts.Count ? currentParts[i] : 0;
            if (c > k)
            {
                return true;
            }

            if (c < k)
            {
                return false;
            }
        }

        return false;
    }

    private static List<long> ParseVersion(string version)
    {
        var result = new List<long>();
        var current = 0L;
        var hasDigit = false;
        foreach (var ch in version)
        {
            if (char.IsDigit(ch))
            {
                current = current * 10 + (ch - '0');
                hasDigit = true;
            }
            else if (hasDigit)
            {
                result.Add(current);
                current = 0;
                hasDigit = false;
            }
        }

        if (hasDigit)
        {
            result.Add(current);
        }

        return result;
    }

    private static string Normalize(string value) => DriverManager.Core.StringHelper.Normalize(value);

    private sealed class NvidiaDriverInfo
    {
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
    }
}
