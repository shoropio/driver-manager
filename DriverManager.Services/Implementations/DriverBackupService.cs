using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DriverManager.Services.Implementations;

public sealed class DriverBackupService : IDriverBackupService
{
    private const string ManifestEntry = "manifest.json";

    public async Task<DriverBackupInfo> CreateBackupAsync(IReadOnlyList<DriverInfo> drivers, string destinationFolder, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationFolder);
        var backupName = $"DriverBackup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        var backupPath = Path.Combine(destinationFolder, backupName);

        var tempFolder = Path.Combine(Path.GetTempPath(), $"dm_backup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        try
        {
            var manifestItems = new List<BackupManifestItem>();
            var index = 0;
            foreach (var driver in drivers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var infPath = ResolveInfPath(driver.DriverPath);
                if (infPath is null)
                {
                    continue;
                }

                var exportFolder = Path.Combine(tempFolder, index.ToString("D3"));
                Directory.CreateDirectory(exportFolder);
                var result = await ProcessRunner.RunAsync("pnputil.exe", $"/export-driver \"{infPath}\" \"{exportFolder}\"", cancellationToken);
                if (result.ExitCode != 0)
                {
                    continue;
                }

                var exportedInf = Directory.EnumerateFiles(exportFolder, "*.inf", SearchOption.AllDirectories).FirstOrDefault();
                if (exportedInf is null)
                {
                    continue;
                }

                manifestItems.Add(new BackupManifestItem
                {
                    InfFile = Path.GetRelativePath(tempFolder, exportedInf),
                    DeviceName = driver.DeviceName,
                    Category = driver.Category,
                    Manufacturer = driver.Manufacturer,
                    InstalledVersion = driver.InstalledVersion,
                    Provider = driver.Provider,
                    HardwareId = driver.HardwareId
                });
                index++;
            }

            var manifestJson = JsonSerializer.Serialize(manifestItems);
            await File.WriteAllTextAsync(Path.Combine(tempFolder, ManifestEntry), manifestJson, Encoding.UTF8, cancellationToken);

            if (manifestItems.Count == 0)
            {
                return new DriverBackupInfo
                {
                    BackupName = backupName,
                    BackupPath = backupPath,
                    CreatedAt = DateTime.Now,
                    Drivers = Array.Empty<DriverInfo>()
                };
            }

            ZipFile.CreateFromDirectory(tempFolder, backupPath, CompressionLevel.Optimal, false);
            var fileInfo = new FileInfo(backupPath);
            return new DriverBackupInfo
            {
                BackupName = backupName,
                BackupPath = backupPath,
                CreatedAt = DateTime.Now,
                SizeBytes = fileInfo.Length,
                Drivers = manifestItems.Select(ToDriverInfo).ToArray()
            };
        }
        finally
        {
            TryDelete(tempFolder);
        }
    }

    public async Task<IReadOnlyList<DriverBackupInfo>> ListBackupsAsync(string backupFolder, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(backupFolder))
            {
                return Array.Empty<DriverBackupInfo>();
            }

            return Directory.EnumerateFiles(backupFolder, "DriverBackup_*.zip")
                .Select(path => new DriverBackupInfo
                {
                    BackupName = Path.GetFileName(path),
                    BackupPath = path,
                    CreatedAt = File.GetCreationTime(path),
                    SizeBytes = new FileInfo(path).Length,
                    Drivers = ReadManifest(path, cancellationToken)
                })
                .ToArray();
        }, cancellationToken);
    }

    public async Task<DriverUpdateResult> RestoreBackupAsync(DriverBackupInfo backup, IReadOnlyList<DriverInfo> drivers, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backup.BackupPath))
        {
            return new DriverUpdateResult { Success = false, Message = "Copia de seguridad no encontrada." };
        }

        if (!IsAdministrator())
        {
            return new DriverUpdateResult { Success = false, Message = "La restauración requiere privilegios de administrador." };
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), $"dm_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        try
        {
            ZipFile.ExtractToDirectory(backup.BackupPath, tempFolder, true);
            cancellationToken.ThrowIfCancellationRequested();

            var manifest = await ReadManifestFileAsync(Path.Combine(tempFolder, ManifestEntry), cancellationToken);
            if (manifest.Count == 0)
            {
                return new DriverUpdateResult { Success = false, Message = "El respaldo no contiene controladores exportados." };
            }

            var installed = 0;
            var errors = new List<string>();
            foreach (var item in manifest)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var infPath = Path.Combine(tempFolder, item.InfFile);
                if (!File.Exists(infPath))
                {
                    errors.Add($"INF no encontrado: {item.InfFile}");
                    continue;
                }

                var result = await ProcessRunner.RunAsync("pnputil.exe", $"/add-driver \"{infPath}\" /install", cancellationToken);
                if (IsPnpSuccess(result.ExitCode))
                {
                    installed++;
                }
                else
                {
                    errors.Add($"{item.DeviceName}: {result.Output.Trim()}");
                }
            }

            if (installed == manifest.Count)
            {
                return new DriverUpdateResult { Success = true, Message = $"Restauración completada: {installed} controlador(es).", RequiresReboot = true };
            }

            return new DriverUpdateResult
            {
                Success = installed > 0,
                Message = $"Restaurados {installed} de {manifest.Count} controladores.",
                Details = string.Join(Environment.NewLine, errors),
                RequiresReboot = true
            };
        }
        catch (Exception ex)
        {
            return new DriverUpdateResult { Success = false, Message = "Error al restaurar", Details = ex.Message };
        }
        finally
        {
            TryDelete(tempFolder);
        }
    }

    public Task<int> EnforceRetentionAsync(string backupFolder, int maxBackups, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (maxBackups < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBackups), "El número máximo de respaldos debe ser al menos 1.");
            }

            if (!Directory.Exists(backupFolder))
            {
                return 0;
            }

            var backups = Directory.EnumerateFiles(backupFolder, "DriverBackup_*.zip")
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            var deleted = 0;
            while (backups.Count > maxBackups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(backups[0]);
                backups.RemoveAt(0);
                deleted++;
            }

            return deleted;
        }, cancellationToken);
    }

    private static IReadOnlyList<DriverInfo> ReadManifest(string backupPath, CancellationToken cancellationToken)
    {
        try
        {
            using var archive = ZipFile.OpenRead(backupPath);
            var entry = archive.GetEntry(ManifestEntry);
            if (entry is null)
            {
                return Array.Empty<DriverInfo>();
            }

            using var stream = entry.Open();
            var items = JsonSerializer.Deserialize<List<BackupManifestItem>>(stream);
            return items?.Select(ToDriverInfo).ToArray() ?? Array.Empty<DriverInfo>();
        }
        catch
        {
            return Array.Empty<DriverInfo>();
        }
    }

    private static async Task<List<BackupManifestItem>> ReadManifestFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new List<BackupManifestItem>();
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<BackupManifestItem>>(stream, cancellationToken: cancellationToken)
            ?? new List<BackupManifestItem>();
    }

    private static DriverInfo ToDriverInfo(BackupManifestItem item)
    {
        return new DriverInfo
        {
            DeviceName = item.DeviceName,
            Category = item.Category,
            Manufacturer = item.Manufacturer,
            InstalledVersion = item.InstalledVersion,
            Provider = item.Provider,
            HardwareId = item.HardwareId,
            DriverPath = item.InfFile,
            Status = DriverStatus.BackupAvailable
        };
    }

    private static string? ResolveInfPath(string? infName)
    {
        if (string.IsNullOrWhiteSpace(infName))
        {
            return null;
        }

        if (Path.IsPathRooted(infName) && File.Exists(infName))
        {
            return infName;
        }

        var fileName = Path.GetFileName(infName);
        var infFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
        var candidate = Path.Combine(infFolder, fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        var storeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "DriverStore", "FileRepository");
        if (!Directory.Exists(storeRoot))
        {
            return null;
        }

        foreach (var packageFolder in Directory.EnumerateDirectories(storeRoot))
        {
            var storeCandidate = Path.Combine(packageFolder, fileName);
            if (File.Exists(storeCandidate))
            {
                return storeCandidate;
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

    private static bool IsPnpSuccess(int exitCode)
    {
        return exitCode is 0 or 259;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private sealed class BackupManifestItem
    {
        public string InfFile { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string InstalledVersion { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string HardwareId { get; set; } = string.Empty;
    }
}
