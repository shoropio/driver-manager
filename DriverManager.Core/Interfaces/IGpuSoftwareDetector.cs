namespace DriverManager.Core.Interfaces;

public interface IGpuSoftwareDetector
{
    /// <summary>
    /// Devuelve los nombres de programas instalados (DisplayName de las claves
    /// Uninstall del registro). Lanza una excepción si el registro no se puede leer.
    /// </summary>
    Task<IReadOnlyList<string>> GetInstalledProgramNamesAsync(CancellationToken cancellationToken = default);
}
