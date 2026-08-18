using DriverManager.Core.Interfaces;
using Microsoft.Win32;

namespace DriverManager.Services.Implementations;

/// <summary>
/// Lee los nombres de programas instalados desde las claves Uninstall del
/// registro (64 y 32 bits). Acepta un lector inyectable para pruebas.
/// </summary>
public sealed class GpuSoftwareDetector : IGpuSoftwareDetector
{
    private static readonly string[] Hives =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    private readonly Func<string, IEnumerable<string>> _registryReader;

    public GpuSoftwareDetector() : this(ReadRegistryDisplayNames)
    {
    }

    public GpuSoftwareDetector(Func<string, IEnumerable<string>> registryReader)
    {
        _registryReader = registryReader ?? throw new ArgumentNullException(nameof(registryReader));
    }

    public Task<IReadOnlyList<string>> GetInstalledProgramNamesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var names = new List<string>();
            foreach (var hive in Hives)
            {
                names.AddRange(_registryReader(hive));
            }

            return (IReadOnlyList<string>)names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }, cancellationToken);
    }

    private static IEnumerable<string> ReadRegistryDisplayNames(string hive)
    {
        using var root = Registry.LocalMachine.OpenSubKey(hive);
        if (root is null)
        {
            yield break;
        }

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var subKey = root.OpenSubKey(subKeyName);
            if (subKey?.GetValue("DisplayName") is string displayName && !string.IsNullOrWhiteSpace(displayName))
            {
                yield return displayName;
            }
        }
    }
}
