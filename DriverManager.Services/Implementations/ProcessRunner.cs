using System.Diagnostics;

namespace DriverManager.Services.Implementations;

internal static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("No se pudo iniciar el proceso.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        output += await errorTask;
        return (process.ExitCode, output);
    }
}
