using System.Windows.Input;
using DriverManager.Core.Models;

namespace DriverManager.App.ViewModels;

public sealed class DownloadItemViewModel : ObservableObject
{
    private readonly DownloadItem _item;

    public DownloadItemViewModel(DownloadItem item)
    {
        _item = item;
    }

    public DownloadItem Item => _item;

    public string Id => _item.Id;
    public string FileName => _item.FileName;
    public string Url => _item.Url;
    public string DeviceName => _item.DeviceName;
    public string Source => _item.Source;

    public double Progress =>
        _item.TotalBytes > 0 ? Math.Min(100.0, (double)_item.ReceivedBytes / _item.TotalBytes * 100) : 0;

    public string ReceivedText =>
        _item.TotalBytes > 0
            ? $"{FormatBytes(_item.ReceivedBytes)} / {FormatBytes(_item.TotalBytes)}"
            : FormatBytes(_item.ReceivedBytes);

    public string SpeedText =>
        _item.Status == DownloadStatus.Downloading && _item.SpeedBytesPerSec > 0
            ? $"{FormatBytes((long)_item.SpeedBytesPerSec)}/s"
            : string.Empty;

    public string EtaText
    {
        get
        {
            if (_item.Status != DownloadStatus.Downloading || _item.SpeedBytesPerSec <= 0 || _item.TotalBytes <= 0)
            {
                return string.Empty;
            }

            var remaining = _item.TotalBytes - _item.ReceivedBytes;
            var seconds = (int)(remaining / _item.SpeedBytesPerSec);
            if (seconds < 60) return $"{seconds}s";
            if (seconds < 3600) return $"{seconds / 60}m {seconds % 60}s";
            return $"{seconds / 3600}h {(seconds % 3600) / 60}m";
        }
    }

    public DownloadStatus Status => _item.Status;

    public string StatusText => _item.Status switch
    {
        DownloadStatus.Waiting => "En cola",
        DownloadStatus.Downloading => "Descargando",
        DownloadStatus.Paused => "Pausado",
        DownloadStatus.Completed => "Completado",
        DownloadStatus.Failed => "Error",
        DownloadStatus.Cancelled => "Cancelado",
        _ => "Desconocido"
    };

    public bool IsDownloading => _item.Status == DownloadStatus.Downloading;
    public bool IsPaused => _item.Status == DownloadStatus.Paused;
    public bool IsWaiting => _item.Status == DownloadStatus.Waiting;
    public bool IsCompleted => _item.Status == DownloadStatus.Completed;
    public bool IsFailed => _item.Status == DownloadStatus.Failed;
    public bool IsCancelled => _item.Status == DownloadStatus.Cancelled;
    public bool IsActive => _item.Status is DownloadStatus.Downloading or DownloadStatus.Waiting or DownloadStatus.Paused;
    public bool IsFinished => _item.Status is DownloadStatus.Completed or DownloadStatus.Cancelled;
    public bool IsPausedOrFailed => _item.Status is DownloadStatus.Paused or DownloadStatus.Failed;

    public string StatusForeground => _item.Status switch
    {
        DownloadStatus.Downloading => "#FF3B82F6",
        DownloadStatus.Paused => "#FFFFB454",
        DownloadStatus.Completed => "#FF9ED27A",
        DownloadStatus.Failed => "#FFFF6B6B",
        DownloadStatus.Cancelled => "#FF8B8FA6",
        _ => "#FF8B8FA6"
    };

    public void Refresh()
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ReceivedText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(EtaText));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(IsPausedOrFailed));
        OnPropertyChanged(nameof(StatusForeground));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
