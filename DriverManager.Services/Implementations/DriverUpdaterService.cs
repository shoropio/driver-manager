using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.Runtime.InteropServices;

namespace DriverManager.Services.Implementations;

public sealed class DriverUpdaterService : IDriverUpdater
{
    private readonly ILogger _logger;

    public DriverUpdaterService() : this(new FileLogger())
    {
    }

    public DriverUpdaterService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<DriverUpdateResult> DownloadDriverAsync(DriverInfo driver, string outputDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var sourceUri = TryGetSourceUri(driver);
        if (sourceUri is null)
        {
            return new DriverUpdateResult
            {
                Success = false,
                Message = "No hay una fuente de descarga válida para el controlador.",
                Source = driver.Provider
            };
        }

        Directory.CreateDirectory(outputDirectory);
        var targetFileName = Path.GetFileName(sourceUri.LocalPath);
        if (string.IsNullOrWhiteSpace(targetFileName))
        {
            targetFileName = $"driver_{Guid.NewGuid()}.cab";
        }

        var targetPath = Path.Combine(outputDirectory, targetFileName);
        _logger.Log($"Descargando controlador desde {sourceUri} a {targetPath}.");

        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(sourceUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = File.Create(targetPath);

            var buffer = new byte[81920];
            long bytesReadTotal = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesReadTotal += bytesRead;
                if (totalBytes is > 0)
                {
                    progress?.Report((double)bytesReadTotal / totalBytes.Value);
                }
            }

            progress?.Report(1.0);

            return new DriverUpdateResult
            {
                Success = true,
                Message = "Descarga completada.",
                Source = sourceUri.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al descargar el controlador.", ex);
            return new DriverUpdateResult
            {
                Success = false,
                Message = "Error al descargar el controlador.",
                Details = ex.Message,
                Source = sourceUri.ToString()
            };
        }
    }

    public async Task<DriverUpdateResult> InstallDriverAsync(DriverInfo driver, string packagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            return new DriverUpdateResult { Success = false, Message = "El paquete de controlador no existe." };
        }

        if (!IsAdministrator())
        {
            return new DriverUpdateResult { Success = false, Message = "La instalación requiere privilegios de administrador." };
        }

        try
        {
            var arguments = $"/add-driver \"{packagePath}\" /install";
            var result = await ProcessRunner.RunAsync("pnputil.exe", arguments, cancellationToken);
            _logger.Log($"Instalación de controlador ejecutada: {arguments}.");

            return new DriverUpdateResult
            {
                Success = IsPnpSuccess(result.ExitCode),
                Message = IsPnpSuccess(result.ExitCode) ? "Instalación completada." : "La instalación falló.",
                Details = result.Output,
                RequiresReboot = RequiresReboot(result.Output)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al instalar el controlador.", ex);
            return new DriverUpdateResult { Success = false, Message = "Error al instalar el controlador.", Details = ex.Message };
        }
    }

    public async Task<DriverUpdateResult> CreateRestorePointAsync(string description, CancellationToken cancellationToken = default)
    {
        if (!IsAdministrator())
        {
            return new DriverUpdateResult { Success = false, Message = "Crear punto de restauración requiere privilegios de administrador." };
        }

        try
        {
            var script = $"Checkpoint-Computer -Description '{EscapePowerShellString(description)}' -RestorePointType 'Modify_Settings'";
            var result = await ProcessRunner.RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"", cancellationToken);
            var success = result.ExitCode == 0 && !result.Output.Contains("Error", StringComparison.OrdinalIgnoreCase);

            return new DriverUpdateResult
            {
                Success = success,
                Message = success ? "Punto de restauración creado." : "No se pudo crear el punto de restauración.",
                Details = result.Output
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al crear punto de restauración.", ex);
            return new DriverUpdateResult { Success = false, Message = "Error al crear punto de restauración.", Details = ex.Message };
        }
    }

    private static Uri? TryGetSourceUri(DriverInfo driver)
    {
        var candidates = new[] { driver.AvailableVersion, driver.Provider };
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                return uri;
            }
        }

        return null;
    }

    private static bool IsAdministrator()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        return false;
    }

    private static string EscapePowerShellString(string text)
    {
        return text.Replace("'", "''");
    }

    private static bool IsPnpSuccess(int exitCode)
    {
        return exitCode is 0 or 259;
    }

    private static bool RequiresReboot(string output)
    {
        return output.Contains("restart", StringComparison.OrdinalIgnoreCase)
            || output.Contains("reiniciar", StringComparison.OrdinalIgnoreCase)
            || output.Contains("reboot", StringComparison.OrdinalIgnoreCase);
    }
}
