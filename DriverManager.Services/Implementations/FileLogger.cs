using DriverManager.Core.Interfaces;
using System.IO;

namespace DriverManager.Services.Implementations;

public sealed class FileLogger : ILogger
{
    private readonly object _lock = new();

    public string LogFilePath { get; }

    public FileLogger()
    {
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverManager");
        Directory.CreateDirectory(appFolder);
        LogFilePath = Path.Combine(appFolder, "DriverManager.log");
    }

    public void Log(string message)
    {
        WriteEntry("INFO", message);
    }

    public void LogError(string message, Exception? exception = null)
    {
        WriteEntry("ERROR", message + (exception is null ? string.Empty : $" | {exception.Message}"));
    }

    private void WriteEntry(string level, string message)
    {
        lock (_lock)
        {
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}{Environment.NewLine}");
        }
    }
}
