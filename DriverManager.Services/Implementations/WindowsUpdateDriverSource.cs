using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Busca e instala actualizaciones de controladores usando el Agente de Windows Update (WUA).
/// Se accede a la API mediante COM dinámico para no depender de ensamblados de interoperación.
/// </summary>
public sealed class WindowsUpdateDriverSource : IDriverUpdateSource
{
    private const string SessionProgId = "Microsoft.Update.Session";
    private const string UpdateCollectionProgId = "Microsoft.Update.UpdateColl";

    private static readonly Regex VersionPattern = new(@"(\d+(?:\.\d+)+)", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<string, object> _updatesById = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<DriverInfo>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _updatesById.Clear();

        var sessionType = Type.GetTypeFromProgID(SessionProgId);
        if (sessionType is null)
        {
            return Array.Empty<DriverInfo>();
        }

        return await Task.Run(() =>
        {
            dynamic session = Activator.CreateInstance(sessionType)!;
            dynamic searcher = session.CreateUpdateSearcher();
            dynamic results = searcher.Search("IsInstalled=0 and Type='Driver'");

            var updates = new List<DriverInfo>();
            foreach (var update in (System.Collections.IEnumerable)results.Updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = ToDriverInfo(update);
                if (info is not null)
                {
                    _updatesById[info.UpdateId] = update;
                    updates.Add(info);
                }
            }

            return (IReadOnlyList<DriverInfo>)updates;
        }, cancellationToken);
    }

    public async Task<DriverUpdateResult> DownloadAndInstallAsync(
        IReadOnlyList<DriverInfo> updates,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0)
        {
            return new DriverUpdateResult { Success = true, Message = "No hay actualizaciones que instalar." };
        }

        if (!IsAdministrator())
        {
            return new DriverUpdateResult { Success = false, Message = "La instalación requiere privilegios de administrador." };
        }

        var sessionType = Type.GetTypeFromProgID(SessionProgId);
        if (sessionType is null)
        {
            return new DriverUpdateResult { Success = false, Message = "El Agente de Windows Update no está disponible." };
        }

        return await Task.Run(async () =>
        {
            try
            {
                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic collection = Activator.CreateInstance(Type.GetTypeFromProgID(UpdateCollectionProgId)!)!;

                var missing = updates.Count(info => !_updatesById.TryGetValue(info.UpdateId, out _));
                if (missing > 0)
                {
                    return new DriverUpdateResult { Success = false, Message = $"No se pudo resolver la fuente de {missing} actualizaciones." };
                }

                foreach (var info in updates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    collection.Add(_updatesById[info.UpdateId]);
                }

                progress?.Report("Descargando actualizaciones...");
                dynamic downloader = session.CreateUpdateDownloader();
                downloader.Updates = collection;
                downloader.Download();

                progress?.Report("Instalando actualizaciones...");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = collection;
                dynamic result = installer.Install();

                var code = (int)result.ResultCode;
                bool rebootRequired = result.RebootRequired == true;

                return new DriverUpdateResult
                {
                    Success = code is 2 or 3,
                    Message = MapResultCode(code),
                    Details = result.Results is System.Collections.IEnumerable results ? SummarizeResults(results) : string.Empty,
                    RequiresReboot = rebootRequired
                };
            }
            catch (Exception ex)
            {
                return new DriverUpdateResult { Success = false, Message = "Error al instalar las actualizaciones.", Details = ex.Message };
            }
        }, cancellationToken);
    }

    private static DriverInfo? ToDriverInfo(dynamic update)
    {
        var title = ReadString(update, "Title") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var updateId = ReadString(update, "Identity", "UpdateID") ?? Guid.NewGuid().ToString("N");
        var driverDate = ReadString(update, "DriverVerDate");
        var provider = ReadString(update, "DriverProvider");
        var deviceClass = ReadString(update, "DeviceClass");
        var version = ExtractVersion(title) ?? driverDate ?? string.Empty;

        return new DriverInfo
        {
            DeviceName = CleanTitle(title),
            Category = deviceClass ?? string.Empty,
            Provider = provider ?? string.Empty,
            AvailableVersion = version,
            InstalledVersion = version,
            UpdateId = updateId,
            Status = DriverStatus.UpdateAvailable
        };
    }

    private static string? ReadString(dynamic update, string propertyName, string? nestedProperty = null)
    {
        try
        {
            dynamic value = nestedProperty is null
                ? update[propertyName]
                : update[propertyName][nestedProperty];
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractVersion(string title)
    {
        var matches = VersionPattern.Matches(title);
        return matches.Count > 0 ? matches[^1].Value : null;
    }

    private static string CleanTitle(string title)
    {
        var cleaned = Regex.Replace(title, @"\s+", " ").Trim();
        var match = VersionPattern.Match(cleaned);
        if (match.Success)
        {
            cleaned = cleaned.Substring(0, match.Index).TrimEnd(' ', '-');
        }

        return cleaned;
    }

    private static string MapResultCode(int code) => code switch
    {
        1 => "La instalación no se inició.",
        3 => "Instalación completada.",
        4 => "Instalación completada con errores.",
        5 => "La instalación falló.",
        6 => "La instalación fue abortada.",
        _ => "Estado de instalación desconocido."
    };

    private static string SummarizeResults(System.Collections.IEnumerable results)
    {
        var messages = new List<string>();
        foreach (var item in results)
        {
            try
            {
                dynamic resultItem = item;
                var resultCode = (int)resultItem.ResultCode;
                var hResult = resultItem.HResult;
                var hint = hResult is not null ? $" HResult: 0x{(int)hResult:X8}" : string.Empty;
                messages.Add($"{MapResultCode(resultCode)}{hint}");
            }
            catch
            {
            }
        }

        return string.Join(Environment.NewLine, messages);
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
