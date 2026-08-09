using System.Diagnostics;
using System.Threading;

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

        return await Task.Run(() =>
        {
            using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("No se pudo iniciar el proceso.");
            using var outputReader = process.StandardOutput;
            using var errorReader = process.StandardError;

            while (!process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }

            var output = outputReader.ReadToEnd();
            output += errorReader.ReadToEnd();
            return (process.ExitCode, output);
        }, cancellationToken);
    }
}
