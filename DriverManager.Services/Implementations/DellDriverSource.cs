using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Text.Json;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Busca controladores de un equipo Dell mediante el Service Tag usando la API
/// pública de soporte de Dell (apigtwb2c). Dell exige un App ID, que se solicita
/// de forma gratuita en developer.dell.com (TechDirect).
/// </summary>
public sealed class DellDriverSource : IDriverUpdateSource
{
    private const string ApiBase = "https://apigtwb2c.us.dell.com/PROD/sbil/eim/v1";
    private readonly string _serviceTag;
    private readonly string _appId;
    private readonly ILogger? _logger;
    private readonly HttpClient _http;

    public DellDriverSource(string serviceTag, string appId, ILogger? logger = null)
    {
        _serviceTag = serviceTag?.Trim() ?? string.Empty;
        _appId = appId?.Trim() ?? string.Empty;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DriverManager/1.0");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_serviceTag);

    public string ConfigError =>
        string.IsNullOrWhiteSpace(_serviceTag)
            ? "Configura el Service Tag de tu equipo Dell en Configuración."
            : string.Empty;

    public async Task<IReadOnlyList<DriverInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<DriverInfo>();
        }

        try
        {
            var query = new List<string> { $"servicetag={Uri.EscapeDataString(_serviceTag)}" };
            if (!string.IsNullOrWhiteSpace(_appId))
            {
                query.Add($"appid={Uri.EscapeDataString(_appId)}");
            }

            var url = $"{ApiBase}/softwares?{string.Join("&", query)}";
            var json = await _http.GetStringAsync(url, cancellationToken);
            return ParseSoftware(json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error al consultar la API de Dell.", ex);
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
                    fileName = $"dell_{update.UpdateId}.exe";
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
                Message = $"Controlador(es) descargado(s) en {folder}. Ejecuta los instaladores manualmente."
            };
        }
        catch (Exception ex)
        {
            return new DriverUpdateResult { Success = false, Message = "Error al descargar controladores de Dell.", Details = ex.Message };
        }
    }

    private static IReadOnlyList<DriverInfo> ParseSoftware(string json, CancellationToken cancellationToken)
    {
        var result = new List<DriverInfo>();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var array = root.ValueKind == JsonValueKind.Array ? root : FindProperty(root, "software").value;
        if (array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = GetString(item, "name");
            var version = GetString(item, "version");
            var url = GetString(item, "downloadUrl");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            result.Add(new DriverInfo
            {
                DeviceName = string.IsNullOrWhiteSpace(name) ? "Controlador Dell" : name,
                Category = GetString(item, "category"),
                Manufacturer = "Dell",
                InstalledVersion = string.Empty,
                AvailableVersion = version,
                Provider = "Dell",
                DriverPath = url,
                UpdateId = version,
                Status = DriverStatus.UpdateAvailable
            });
        }

        return result;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        var (value, ok) = FindProperty(element, propertyName);
        if (ok && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static (JsonElement value, bool ok) FindProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (default, false);
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return (property.Value, true);
            }
        }

        return (default, false);
    }
}
