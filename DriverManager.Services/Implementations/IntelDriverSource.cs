using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Diagnostics;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Detecta actualizaciones de dispositivos Intel y Killer (Wi-Fi, Ethernet y
/// Bluetooth) consultando las páginas públicas del Intel Download Center.
/// Como Intel bloquea el acceso programático (Akamai), usa un catálogo local
/// de respaldo con las últimas versiones conocidas.
/// </summary>
public sealed class IntelDriverSource : IDriverUpdateSource
{
    private readonly ILogger? _logger;
    private readonly HttpClient _http;

    public IntelDriverSource(ILogger? logger = null)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriverManager/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-ES,es;q=0.9,en;q=0.8");
    }

    public async Task<IReadOnlyList<DriverInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var installed = await new WmiDriverScanner().ScanInstalledDriversAsync(cancellationToken);
            var result = new List<DriverInfo>();
            foreach (var product in IntelProductCatalog.Products)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var matched = product.MatchedDevices(installed);
                if (matched.Count == 0)
                {
                    continue;
                }

                var (version, url) = await GetProductInfoAsync(product, cancellationToken);
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                var installedVersion = MaxVersion(matched);
                if (!DriverVersions.IsNewer(version, installedVersion))
                {
                    continue;
                }

                result.Add(new DriverInfo
                {
                    DeviceName = product.DisplayName,
                    Category = product.Category,
                    Manufacturer = "Intel",
                    InstalledVersion = installedVersion,
                    AvailableVersion = version,
                    Provider = "Intel",
                    DriverPath = string.IsNullOrWhiteSpace(url) ? product.PageUrl : url,
                    UpdateId = product.Id,
                    Status = DriverStatus.UpdateAvailable
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error al buscar actualizaciones de Intel.", ex);
            return Array.Empty<DriverInfo>();
        }
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

        try
        {
            foreach (var update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = update.DriverPath;
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    uri.Host.EndsWith("downloadmirror.intel.com", StringComparison.OrdinalIgnoreCase))
                {
                    await DownloadFileAsync(uri, update, progress, cancellationToken);
                }
                else
                {
                    progress?.Report($"Abriendo la página de descarga de {update.DeviceName}...");
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }

            return new DriverUpdateResult
            {
                Success = true,
                Message = "Los paquetes de Intel se abren en el navegador para descargarlos e instalarlos."
            };
        }
        catch (Exception ex)
        {
            return new DriverUpdateResult
            {
                Success = false,
                Message = "Error al obtener los paquetes de Intel.",
                Details = ex.Message
            };
        }
    }

    private async Task DownloadFileAsync(
        Uri url,
        DriverInfo update,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DriverManagerDownloads");
        Directory.CreateDirectory(folder);

        var fileName = Path.GetFileName(url.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"intel_{update.UpdateId}.exe";
        }

        var target = Path.Combine(folder, fileName);
        progress?.Report($"Descargando {fileName}...");
        await using var responseStream = await _http.GetStreamAsync(url, cancellationToken);
        await using var fileStream = File.Create(target);
        await responseStream.CopyToAsync(fileStream, cancellationToken);
    }

    private async Task<(string Version, string Url)> GetProductInfoAsync(IntelProduct product, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _http.GetStringAsync(product.PageUrl, cancellationToken);
            var parsed = IntelPageParser.Parse(html);
            if (!string.IsNullOrWhiteSpace(parsed.Version))
            {
                return (parsed.Version, parsed.DownloadUrl);
            }
        }
        catch (Exception ex)
        {
            _logger?.Log($"No se pudo leer la página de Intel para {product.DisplayName}: {ex.Message}. Se usa el catálogo local.");
        }

        return (product.FallbackVersion, string.Empty);
    }

    private static string MaxVersion(IReadOnlyList<DriverInfo> devices)
    {
        string best = string.Empty;
        foreach (var device in devices)
        {
            if (DriverVersions.IsNewer(device.InstalledVersion, best))
            {
                best = device.InstalledVersion;
            }
        }

        return best;
    }
}
