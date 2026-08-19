using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using DriverManager.Services.Implementations;

namespace DriverManager.App.ViewModels;

public sealed class DownloadsViewModel : ObservableObject
{
    private readonly DownloadManager _downloadManager;
    private readonly ILogger _logger;
    private readonly string _downloadsFolder;
    private readonly Dictionary<string, DownloadItemViewModel> _vmMap = new();
    private string _addName = string.Empty;
    private string _addUrl = string.Empty;

    public DownloadsViewModel(DownloadManager downloadManager, ILogger logger, string downloadsFolder)
    {
        _downloadManager = downloadManager;
        _logger = logger;
        _downloadsFolder = downloadsFolder;

        ActiveDownloads = new ObservableCollection<DownloadItemViewModel>();
        CompletedDownloads = new ObservableCollection<DownloadItemViewModel>();

        AddDownloadCommand = new AsyncRelayCommand(AddDownloadAsync, () => !string.IsNullOrWhiteSpace(AddUrl));
        PauseDownloadCommand = new RelayCommand(p => PauseDownload(p as string));
        ResumeDownloadCommand = new RelayCommand(p => ResumeDownload(p as string));
        CancelDownloadCommand = new RelayCommand(p => CancelDownload(p as string));
        RemoveDownloadCommand = new RelayCommand(p => RemoveDownload(p as string));
        ClearCompletedCommand = new RelayCommand(_ => ClearCompleted());

        _downloadManager.ItemChanged += OnItemChanged;
        _downloadManager.QueueChanged += OnQueueChanged;

        SyncFromManager();
    }

    public ObservableCollection<DownloadItemViewModel> ActiveDownloads { get; }
    public ObservableCollection<DownloadItemViewModel> CompletedDownloads { get; }

    public string AddName
    {
        get => _addName;
        set => SetProperty(ref _addName, value);
    }

    public string AddUrl
    {
        get => _addUrl;
        set => SetProperty(ref _addUrl, value);
    }

    public string TotalSpeedText
    {
        get
        {
            var total = ActiveDownloads.Where(d => d.IsDownloading).Sum(d => d.Item.SpeedBytesPerSec);
            return total > 0 ? $"Velocidad total: {DownloadItemViewModel.FormatBytes((long)total)}/s" : string.Empty;
        }
    }

    public int ActiveCount => ActiveDownloads.Count(d => d.IsActive);
    public bool HasActive => ActiveCount > 0;
    public bool HasCompleted => CompletedDownloads.Count > 0;
    public bool HasAny => ActiveDownloads.Count > 0 || CompletedDownloads.Count > 0;
    public string EmptyMessage => "No hay descargas en cola. Agrega una URL o instala desde Actualizaciones.";

    public ICommand AddDownloadCommand { get; }
    public ICommand PauseDownloadCommand { get; }
    public ICommand ResumeDownloadCommand { get; }
    public ICommand CancelDownloadCommand { get; }
    public ICommand RemoveDownloadCommand { get; }
    public ICommand ClearCompletedCommand { get; }

    public string DownloadsFolder => _downloadsFolder;

    public void EnqueueFromUpdate(string url, string fileName, string deviceName,
        string installedVersion, string availableVersion, string source)
    {
        _downloadManager.AddDownload(url, fileName, DownloadsFolder,
            deviceName, installedVersion, availableVersion, source);
    }

    private async Task AddDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(AddUrl))
        {
            return;
        }

        try
        {
            var uri = new Uri(AddUrl.Trim());
            var name = string.IsNullOrWhiteSpace(AddName)
                ? Path.GetFileName(uri.LocalPath)
                : AddName.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"driver_{Guid.NewGuid():N}.exe";
            }

            _downloadManager.AddDownload(AddUrl.Trim(), name, DownloadsFolder, source: "Manual");
            AddName = string.Empty;
            AddUrl = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al agregar descarga: {ex.Message}", ex);
        }
    }

    private void PauseDownload(string? id)
    {
        if (id is not null)
        {
            _downloadManager.Pause(id);
        }
    }

    private void ResumeDownload(string? id)
    {
        if (id is not null)
        {
            _downloadManager.Resume(id);
        }
    }

    private void CancelDownload(string? id)
    {
        if (id is not null)
        {
            _downloadManager.Cancel(id);
        }
    }

    private void RemoveDownload(string? id)
    {
        if (id is not null)
        {
            _downloadManager.Remove(id);
        }
    }

    private void ClearCompleted()
    {
        _downloadManager.ClearCompleted();
    }

    private void OnItemChanged(DriverManager.Core.Models.DownloadItem item)
    {
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_vmMap.TryGetValue(item.Id, out var vm))
            {
                vm.Refresh();
                RefreshCollections();
            }
        });
    }

    private void OnQueueChanged()
    {
        App.Current.Dispatcher.BeginInvoke(SyncFromManager);
    }

    private void SyncFromManager()
    {
        var items = _downloadManager.Items;

        var currentIds = new HashSet<string>(items.Select(i => i.Id));
        foreach (var key in _vmMap.Keys.Where(k => !currentIds.Contains(k)).ToList())
        {
            _vmMap.Remove(key);
        }

        foreach (var item in items)
        {
            if (!_vmMap.TryGetValue(item.Id, out var vm))
            {
                vm = new DownloadItemViewModel(item);
                _vmMap[item.Id] = vm;
            }
        }

        ActiveDownloads.Clear();
        CompletedDownloads.Clear();

        foreach (var item in items.Where(i => i.Status is
            DownloadStatus.Waiting or DownloadStatus.Downloading or
            DownloadStatus.Paused or DownloadStatus.Failed))
        {
            if (_vmMap.TryGetValue(item.Id, out var vm))
            {
                ActiveDownloads.Add(vm);
            }
        }

        foreach (var item in items.Where(i => i.Status is
            DownloadStatus.Completed or DownloadStatus.Cancelled).Reverse())
        {
            if (_vmMap.TryGetValue(item.Id, out var vm))
            {
                CompletedDownloads.Add(vm);
            }
        }

        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(TotalSpeedText));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void RefreshCollections()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(HasActive));
        OnPropertyChanged(nameof(HasCompleted));
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(TotalSpeedText));
    }
}
