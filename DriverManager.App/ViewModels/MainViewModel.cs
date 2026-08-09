using DriverManager.App.Localization;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using DriverManager.Services.Implementations;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace DriverManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDriverBackupService _backupService;
    private readonly IDriverUpdater _updater;
    private readonly FileLogger _logger;
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private IDriverUpdateSource _updateSource;
    private bool _updateSourceInstalls;

    private AppPage _selectedPage = AppPage.Dashboard;
    private bool _isBusy;
    private string _statusMessage = "Listo";
    private int _outdatedDriversCount;
    private int _attentionCount;
    private string _latestBackup = "Nunca";
    private DriverBackupInfo? _selectedBackup;
    private string _downloadName = string.Empty;
    private string _downloadUrl = string.Empty;
    private string _historyLog = string.Empty;
    private string _driverFilterText = string.Empty;
    private string _selectedStatusFilter = "Todos";
    private string _selectedUpdateSource = "Windows Update";

    public MainViewModel()
    {
        _backupService = new DriverBackupService();
        _logger = new FileLogger();
        _updater = new DriverUpdaterService(_logger);
        _settingsService = new SettingsService();
        _settings = _settingsService.Load();
        _updateSource = DriverUpdateSourceFactory.Create(_settings, _logger);
        _updateSourceInstalls = DriverUpdateSourceFactory.InstallsPackages(_settings);

        BackupFolder = _settings.ResolveBackupFolder();
        DownloadsFolder = _settings.ResolveDownloadsFolder();
        Drivers = new ObservableCollection<DriverViewModel>();
        PendingUpdates = new ObservableCollection<DriverViewModel>();
        Backups = new ObservableCollection<DriverBackupInfo>();
        DriversView = CollectionViewSource.GetDefaultView(Drivers);
        DriversView.Filter = FilterDrivers;

        UpdateSourceOptions = new ObservableCollection<string>
        {
            "Windows Update",
            "NVIDIA (GeForce)",
            "Dell (Service Tag)"
        };
        StatusFilterOptions = new ObservableCollection<string>
        {
            "Todos",
            "Actualizado",
            "Requiere actualización",
            "Error",
            "Respaldo disponible",
            "Desconocido"
        };
        SelectedUpdateSource = MapUpdateSourceToDisplay(_settings.UpdateSource);
        SelectedStatusFilter = "Todos";

        ScanDriversCommand = new AsyncRelayCommand(ScanDriversAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy);
        InstallUpdatesCommand = new AsyncRelayCommand(InstallUpdatesAsync, () => !IsBusy);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RefreshBackupsCommand = new AsyncRelayCommand(RefreshBackupsAsync, () => !IsBusy);
        DeleteBackupCommand = new AsyncRelayCommand(DeleteSelectedBackupAsync, () => !IsBusy && SelectedBackup is not null);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync, () => !IsBusy && SelectedBackup is not null);
        CreateRestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync, () => !IsBusy);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => !IsBusy);
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        NavigateCommand = new RelayCommand(Navigate);
    }

    public string BackupFolder
    {
        get => _backupFolder;
        set => SetProperty(ref _backupFolder, value);
    }
    private string _backupFolder = string.Empty;

    public string DownloadsFolder
    {
        get => _downloadsFolder;
        set => SetProperty(ref _downloadsFolder, value);
    }
    private string _downloadsFolder = string.Empty;

    public string LogFilePath => _logger.LogFilePath;

    public ObservableCollection<DriverViewModel> Drivers { get; }
    public ICollectionView DriversView { get; }
    public ObservableCollection<DriverViewModel> PendingUpdates { get; }
    public ObservableCollection<DriverBackupInfo> Backups { get; }
    public ObservableCollection<string> UpdateSourceOptions { get; }
    public ObservableCollection<string> StatusFilterOptions { get; }

    public AppPage SelectedPage
    {
        get => _selectedPage;
        private set => SetProperty(ref _selectedPage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int OutdatedDriversCount
    {
        get => _outdatedDriversCount;
        private set => SetProperty(ref _outdatedDriversCount, value);
    }

    public int AttentionCount
    {
        get => _attentionCount;
        private set => SetProperty(ref _attentionCount, value);
    }

    public string LatestBackup
    {
        get => _latestBackup;
        private set => SetProperty(ref _latestBackup, value);
    }

    public DriverBackupInfo? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value))
            {
                RestoreBackupCommand.RaiseCanExecuteChanged();
                DeleteBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string DownloadName
    {
        get => _downloadName;
        set => SetProperty(ref _downloadName, value);
    }

    public string DownloadUrl
    {
        get => _downloadUrl;
        set => SetProperty(ref _downloadUrl, value);
    }

    public string HistoryLog
    {
        get => _historyLog;
        private set => SetProperty(ref _historyLog, value);
    }

    public string DriverFilterText
    {
        get => _driverFilterText;
        set
        {
            if (SetProperty(ref _driverFilterText, value))
            {
                DriversView.Refresh();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                DriversView.Refresh();
            }
        }
    }

    public string SelectedUpdateSource
    {
        get => _selectedUpdateSource;
        set => SetProperty(ref _selectedUpdateSource, value);
    }

    public AppSettings Settings => _settings;

    public IAsyncCommand ScanDriversCommand { get; }
    public IAsyncCommand CheckUpdatesCommand { get; }
    public IAsyncCommand InstallUpdatesCommand { get; }
    public IAsyncCommand CreateBackupCommand { get; }
    public IAsyncCommand RefreshBackupsCommand { get; }
    public IAsyncCommand DeleteBackupCommand { get; }
    public IAsyncCommand RestoreBackupCommand { get; }
    public IAsyncCommand CreateRestorePointCommand { get; }
    public IAsyncCommand DownloadCommand { get; }
    public IAsyncCommand RefreshHistoryCommand { get; }
    public IAsyncCommand SaveSettingsCommand { get; }
    public ICommand NavigateCommand { get; }

    private void Navigate(object? parameter)
    {
        if (parameter is string pageName && Enum.TryParse<AppPage>(pageName, true, out var page))
        {
            SelectedPage = page;
        }
    }

    private bool FilterDrivers(object item)
    {
        if (item is not DriverViewModel driver)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(DriverFilterText))
        {
            var search = DriverFilterText.Trim();
            if (!driver.DriverInfo.DeviceName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !driver.DriverInfo.Manufacturer.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !driver.DriverInfo.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && !SelectedStatusFilter.Equals("Todos", StringComparison.Ordinal))
        {
            var targetStatus = DriverText.StatusFromText(SelectedStatusFilter);
            return targetStatus is null || driver.DriverInfo.Status == targetStatus;
        }

        return true;
    }

    private async Task ScanDriversAsync()
    {
        if (IsBusy)
        {
            return;
        }

        using (BusyScope())
        {
            StatusMessage = "Escaneando controladores...";
            _logger.Log("Iniciando escaneo de controladores.");
            try
            {
                var driverInfos = await new WmiDriverScanner().ScanInstalledDriversAsync();
                Drivers.Clear();
                PendingUpdates.Clear();

                foreach (var driverInfo in driverInfos)
                {
                    Drivers.Add(new DriverViewModel(driverInfo));
                }

                DriversView.Refresh();
                RefreshCounters();
                StatusMessage = $"Escaneo completado: {Drivers.Count} controladores encontrados.";
                _logger.Log($"Escaneo completado con {Drivers.Count} controladores.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al escanear controladores.";
                _logger.LogError("Error en el escaneo de controladores.", ex);
            }
        }
    }

    private async Task CheckUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (DriverUpdateSourceFactory.RequiresConfiguration(_settings, out var configError))
        {
            StatusMessage = configError;
            SelectedPage = AppPage.Settings;
            return;
        }

        using (BusyScope())
        {
            StatusMessage = $"Buscando actualizaciones ({DescribeSource(_settings.UpdateSource)})...";
            _logger.Log($"Buscando actualizaciones de controladores ({_settings.UpdateSource}).");
            try
            {
                var updates = await _updateSource.CheckForUpdatesAsync();
                foreach (var update in updates)
                {
                    var existing = Drivers.FirstOrDefault(d => DeviceNamesMatch(d.DriverInfo.DeviceName, update.DeviceName));
                    if (existing is not null)
                    {
                        existing.DriverInfo.AvailableVersion = update.AvailableVersion;
                        existing.DriverInfo.UpdateId = update.UpdateId;
                        existing.DriverInfo.DriverPath = update.DriverPath;
                        existing.DriverInfo.Provider = update.Provider;
                        existing.DriverInfo.Status = DriverStatus.UpdateAvailable;
                        existing.Refresh();
                        PendingUpdates.Add(existing);
                    }
                    else
                    {
                        var viewModel = new DriverViewModel(update);
                        Drivers.Add(viewModel);
                        PendingUpdates.Add(viewModel);
                    }
                }

                DriversView.Refresh();
                RefreshCounters();
                StatusMessage = PendingUpdates.Count == 0
                    ? "Tu equipo está al día: no se encontraron actualizaciones."
                    : $"Se encontraron {PendingUpdates.Count} actualización(es) disponible(s).";
                _logger.Log($"Actualizaciones encontradas: {PendingUpdates.Count}.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"No se pudo consultar {DescribeSource(_settings.UpdateSource)}.";
                _logger.LogError("Error al buscar actualizaciones.", ex);
            }
        }
    }

    private async Task InstallUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (PendingUpdates.Count == 0)
        {
            StatusMessage = "No hay actualizaciones pendientes. Busca actualizaciones primero.";
            return;
        }

        using (BusyScope())
        {
            var progress = new Progress<string>(message => StatusMessage = message);
            try
            {
                if (_updateSourceInstalls)
                {
                    StatusMessage = "Creando punto de restauración...";
                    var restorePoint = await _updater.CreateRestorePointAsync("DriverManager: antes de actualizar controladores");
                    if (!restorePoint.Success)
                    {
                        _logger.Log($"Aviso: no se pudo crear el punto de restauración ({restorePoint.Message}).");
                    }
                }

                var pending = PendingUpdates.Select(vm => vm.DriverInfo).ToArray();
                var result = await _updateSource.DownloadAndInstallAsync(pending, progress);

                if (result.Success && _updateSourceInstalls)
                {
                    foreach (var viewModel in PendingUpdates.ToArray())
                    {
                        viewModel.DriverInfo.InstalledVersion = viewModel.DriverInfo.AvailableVersion;
                        viewModel.DriverInfo.Status = DriverStatus.UpToDate;
                        viewModel.Refresh();
                        PendingUpdates.Remove(viewModel);
                    }

                    DriversView.Refresh();
                    RefreshCounters();
                    StatusMessage = result.RequiresReboot
                        ? $"{result.Message} Se recomienda reiniciar el equipo."
                        : result.Message;
                    _logger.Log("Instalación de actualizaciones completada.");
                }
                else
                {
                    StatusMessage = result.Message;
                    _logger.LogError(result.Details is { Length: > 0 } details ? $"{result.Message} | {details}" : result.Message);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al instalar las actualizaciones.";
                _logger.LogError("Error al instalar actualizaciones.", ex);
            }
        }
    }

    private async Task CreateBackupAsync()
    {
        if (IsBusy)
        {
            return;
        }

        using (BusyScope())
        {
            StatusMessage = "Creando respaldo...";
            try
            {
                var driversToBackup = Drivers.Select(d => d.DriverInfo).ToArray();
                if (driversToBackup.Length == 0)
                {
                    StatusMessage = "No hay controladores disponibles para respaldar.";
                    return;
                }

                var backup = await _backupService.CreateBackupAsync(driversToBackup, BackupFolder);
                if (backup.Drivers.Count == 0)
                {
                    StatusMessage = "No se pudo exportar ningún controlador para el respaldo.";
                    return;
                }

                LatestBackup = backup.CreatedAt.ToString("g");
                StatusMessage = $"Respaldo creado ({backup.Drivers.Count} controladores): {backup.BackupPath}.";
                _logger.Log($"Respaldo de controladores creado: {backup.BackupPath}.");
                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al crear el respaldo.";
                _logger.LogError("Error al crear respaldo de controladores.", ex);
            }
        }
    }

    private async Task RefreshBackupsAsync()
    {
        try
        {
            var backups = await _backupService.ListBackupsAsync(BackupFolder);
            Backups.Clear();
            foreach (var backup in backups)
            {
                Backups.Add(backup);
            }

            if (Backups.Count > 0)
            {
                SelectedBackup = Backups[^1];
            }
            else
            {
                SelectedBackup = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error al listar los respaldos.";
            _logger.LogError("Error al listar respaldos.", ex);
        }
    }

    private async Task DeleteSelectedBackupAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedBackup is null)
        {
            StatusMessage = "Selecciona un respaldo para eliminar.";
            return;
        }

        using (BusyScope())
        {
            try
            {
                if (File.Exists(SelectedBackup.BackupPath))
                {
                    File.Delete(SelectedBackup.BackupPath);
                    StatusMessage = $"Respaldo eliminado: {SelectedBackup.BackupName}.";
                    _logger.Log($"Respaldo eliminado: {SelectedBackup.BackupPath}.");
                }

                await RefreshBackupsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al eliminar el respaldo.";
                _logger.LogError("Error al eliminar respaldo.", ex);
            }
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedBackup is null)
        {
            StatusMessage = "Selecciona un respaldo para restaurar.";
            return;
        }

        using (BusyScope())
        {
            StatusMessage = $"Restaurando {SelectedBackup.BackupName}...";
            _logger.Log($"Restaurando respaldo {SelectedBackup.BackupPath}.");
            try
            {
                var result = await _backupService.RestoreBackupAsync(SelectedBackup, SelectedBackup.Drivers);
                StatusMessage = result.RequiresReboot
                    ? $"{result.Message} Se recomienda reiniciar el equipo."
                    : result.Message;
                if (!result.Success && !string.IsNullOrWhiteSpace(result.Details))
                {
                    _logger.LogError(result.Details);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al restaurar el respaldo.";
                _logger.LogError("Error al restaurar respaldo.", ex);
            }
        }
    }

    private async Task CreateRestorePointAsync()
    {
        if (IsBusy)
        {
            return;
        }

        using (BusyScope())
        {
            StatusMessage = "Creando punto de restauración...";
            try
            {
                var result = await _updater.CreateRestorePointAsync("DriverManager: punto de restauración manual");
                StatusMessage = result.Message;
                if (!result.Success)
                {
                    _logger.LogError(result.Details);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al crear el punto de restauración.";
                _logger.LogError("Error al crear punto de restauración.", ex);
            }
        }
    }

    private async Task DownloadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(DownloadUrl))
        {
            StatusMessage = "Ingresa una URL de descarga para el controlador.";
            return;
        }

        using (BusyScope())
        {
            StatusMessage = "Descargando...";
            try
            {
                Directory.CreateDirectory(DownloadsFolder);
                var driver = new DriverInfo
                {
                    DeviceName = string.IsNullOrWhiteSpace(DownloadName) ? "Descarga manual" : DownloadName.Trim(),
                    Provider = DownloadUrl.Trim(),
                    AvailableVersion = DownloadUrl.Trim()
                };

                var result = await _updater.DownloadDriverAsync(driver, DownloadsFolder);
                StatusMessage = result.Success
                    ? $"Descargado en {DownloadsFolder}."
                    : result.Message;
                _logger.Log($"Descarga manual: {result.Success} -> {DownloadUrl}");
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al descargar el controlador.";
                _logger.LogError("Error en descarga manual.", ex);
            }
        }
    }

    private async Task RefreshHistoryAsync()
    {
        try
        {
            if (!File.Exists(LogFilePath))
            {
                HistoryLog = "(El log aún no se ha creado. Realiza alguna operación.)";
                return;
            }

            var lines = await Task.Run(() => File.ReadAllLines(LogFilePath).Reverse().Take(500).Reverse().ToArray());
            HistoryLog = lines.Length == 0 ? "(El log está vacío.)" : string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            HistoryLog = $"No se pudo leer el log: {ex.Message}";
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        using (BusyScope())
        {
            try
            {
                _settings.UpdateSource = MapUpdateSourceToCode(SelectedUpdateSource);
                _settings.BackupFolder = BackupFolder;
                _settings.DownloadsFolder = DownloadsFolder;
                _settingsService.Save(_settings);

                BackupFolder = _settings.ResolveBackupFolder();
                DownloadsFolder = _settings.ResolveDownloadsFolder();
                _updateSource = DriverUpdateSourceFactory.Create(_settings, _logger);
                _updateSourceInstalls = DriverUpdateSourceFactory.InstallsPackages(_settings);
                PendingUpdates.Clear();

                StatusMessage = "Configuración guardada. La próxima búsqueda usará la nueva fuente.";
                _logger.Log($"Configuración guardada. Fuente de actualizaciones: {_settings.UpdateSource}.");
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al guardar la configuración.";
                _logger.LogError("Error al guardar configuración.", ex);
            }
        }
    }

    private static string MapUpdateSourceToCode(string display) => display switch
    {
        "NVIDIA (GeForce)" => "Nvidia",
        "Dell (Service Tag)" => "Dell",
        _ => "WindowsUpdate"
    };

    private static string MapUpdateSourceToDisplay(string code) => code switch
    {
        "Nvidia" => "NVIDIA (GeForce)",
        "Dell" => "Dell (Service Tag)",
        _ => "Windows Update"
    };

    private static string DescribeSource(string code) => code switch
    {
        "Nvidia" => "NVIDIA",
        "Dell" => "Dell",
        _ => "Windows Update"
    };

    private void RefreshCounters()
    {
        OutdatedDriversCount = Drivers.Count(d => d.DriverInfo.Status == DriverStatus.UpdateAvailable);
        AttentionCount = Drivers.Count(d => d.DriverInfo.Status == DriverStatus.ProblemDetected);
    }

    private static bool DeviceNamesMatch(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);
        return normalizedA.Length >= 6 && (normalizedA.Contains(normalizedB) || normalizedB.Contains(normalizedA));
    }

    private static string Normalize(string value)
    {
        return new string(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private IDisposable BusyScope()
    {
        IsBusy = true;
        RaiseAllCanExecute();
        return new BusyToken(() =>
        {
            IsBusy = false;
            RaiseAllCanExecute();
        });
    }

    private void RaiseAllCanExecute()
    {
        ScanDriversCommand.RaiseCanExecuteChanged();
        CheckUpdatesCommand.RaiseCanExecuteChanged();
        InstallUpdatesCommand.RaiseCanExecuteChanged();
        CreateBackupCommand.RaiseCanExecuteChanged();
        RefreshBackupsCommand.RaiseCanExecuteChanged();
        DeleteBackupCommand.RaiseCanExecuteChanged();
        RestoreBackupCommand.RaiseCanExecuteChanged();
        CreateRestorePointCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
        SaveSettingsCommand.RaiseCanExecuteChanged();
    }

    private sealed class BusyToken : IDisposable
    {
        private readonly Action _onDispose;

        public BusyToken(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}
