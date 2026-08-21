using DriverManager.App.Localization;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;
using DriverManager.Services.Implementations;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;

namespace DriverManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDriverBackupService _backupService;
    private readonly IDriverUpdater _updater;
    private readonly IGpuInfoService _gpuService;
    private readonly IGpuTelemetryService _telemetryService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly FileLogger _logger;
    private readonly SettingsService _settingsService;
    private readonly DriverStateStore _stateStore;
    private readonly DownloadManager _downloadManager;
    private AppSettings _settings;
    private IDriverUpdateSource _updateSource;
    private bool _updateSourceInstalls;

    private AppPage _selectedPage = AppPage.Dashboard;
    private bool _isBusy;
    private string _statusMessage = "Listo";
    private bool _isStatusVisible;
    private string _statusForeground = "#FF9ED27A";
    private int _outdatedDriversCount;
    private int _attentionCount;
    private int _gpuAttentionCount;
    private string _latestBackup = "Nunca";
    private DriverBackupInfo? _selectedBackup;
    private string _historyLog = string.Empty;
    private string _driverFilterText = string.Empty;
    private string _selectedStatusFilter = "Todos";
    private string _selectedUpdateSource = "Windows Update";
    private DateTime _lastScanAt;
    private DateTime _lastCheckAt;
    private CancellationTokenSource? _searchCts;

    public MainViewModel()
    {
        _backupService = new DriverBackupService();
        _logger = new FileLogger();
        _updater = new DriverUpdaterService(_logger);
        _gpuService = new GpuInfoService();
        _telemetryService = new GpuTelemetryService();
        _systemInfoService = new SystemInfoService();
        _settingsService = new SettingsService();
        _stateStore = new DriverStateStore();
        _settings = _settingsService.Load();
        _updateSource = DriverUpdateSourceFactory.Create(_settings, _logger);
        _updateSourceInstalls = DriverUpdateSourceFactory.InstallsPackages(_settings);

        var downloadsDir = _settings.ResolveDownloadsFolder();
        var stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverManager");
        Directory.CreateDirectory(stateDir);
        _downloadManager = new DownloadManager(Path.Combine(stateDir, "downloads.json"), 3, _logger);
        DownloadsVM = new DownloadsViewModel(_downloadManager, _logger, downloadsDir);
        MaintenanceVM = new MaintenanceViewModel(new MaintenanceService(new WmiDriverScanner(), _logger), _updateSource, _logger);

        BackupFolder = _settings.ResolveBackupFolder();
        DownloadsFolder = downloadsDir;
        Drivers = new ObservableCollection<DriverViewModel>();
        PendingUpdates = new ObservableCollection<DriverViewModel>();
        Backups = new ObservableCollection<DriverBackupInfo>();
        Gpus = new ObservableCollection<GpuViewModel>();
        DriversView = CollectionViewSource.GetDefaultView(Drivers);
        DriversView.Filter = FilterDrivers;

        UpdateSourceOptions = new ObservableCollection<string>
        {
            "Windows Update",
            "NVIDIA (GeForce)",
            "Dell (Service Tag)",
            "Intel & Killer"
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
        ScanGpusCommand = new AsyncRelayCommand(ScanGpusAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(() => CheckUpdatesAsync(), () => !IsBusy);
        InstallUpdatesCommand = new AsyncRelayCommand(InstallUpdatesAsync, () => !IsBusy);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        RefreshBackupsCommand = new AsyncRelayCommand(RefreshBackupsAsync, () => !IsBusy);
        DeleteBackupCommand = new AsyncRelayCommand(DeleteSelectedBackupAsync, () => !IsBusy);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync, () => !IsBusy);
        CreateRestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync, () => !IsBusy);
        InstallDriverCommand = new AsyncRelayCommand<DriverViewModel>(InstallDriverAsync, _ => !IsBusy);
        OpenUrlCommand = new RelayCommand(OpenUrl);
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        NavigateCommand = new RelayCommand(Navigate);
        RefreshSystemInfoCommand = new AsyncRelayCommand(RefreshSystemInfoAsync);

        InitializationTask = InitializeAsync();
    }

    public Task InitializationTask { get; }

    private async Task InitializeAsync()
    {
        var snapshot = await _stateStore.LoadAsync();
        if (snapshot?.Drivers is not { Count: > 0 })
        {
            return;
        }

        using (BusyScope())
        {
            try
            {
                _lastScanAt = snapshot.LastScanAt;
                _lastCheckAt = snapshot.LastUpdateCheckAt;
                Drivers.Clear();
                PendingUpdates.Clear();
                foreach (var info in snapshot.Drivers)
                {
                    var viewModel = new DriverViewModel(info);
                    Drivers.Add(viewModel);
                    if (info.Status == DriverStatus.UpdateAvailable)
                    {
                        PendingUpdates.Add(viewModel);
                    }
                }

                Gpus.Clear();
                foreach (var gpu in snapshot.Gpus)
                {
                    Gpus.Add(new GpuViewModel(gpu));
                }

                DriversView.Refresh();
                RefreshCounters();

                var lastScan = snapshot.LastScanAt == default ? string.Empty : $" (último escaneo: {snapshot.LastScanAt:g})";
                if (PendingUpdates.Count > 0)
                {
                    StatusMessage = $"Hay {PendingUpdates.Count} actualización(es) disponible(s) desde la última búsqueda. Revisa la pestaña Actualizaciones.";
                    _logger.Log($"Estado restaurado: {Drivers.Count} controladores, {Gpus.Count} GPUs, {PendingUpdates.Count} actualizaciones pendientes.");
                }
                else
                {
                    StatusMessage = $"Lista de controladores cargada de la última sesión{lastScan}.";
                    _logger.Log($"Estado restaurado: {Drivers.Count} controladores, {Gpus.Count} tarjeta(s) gráfica(s).");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "No se pudo cargar el estado guardado.";
                _logger.LogError("Error al cargar el estado guardado.", ex);
            }
        }

        if (_settings.CheckUpdatesOnStartup)
        {
            await CheckUpdatesAsync(fromStartup: true);
        }

        await RefreshTelemetryAsync();
        await RefreshSystemInfoAsync();
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
    public ObservableCollection<GpuViewModel> Gpus { get; }
    public ObservableCollection<string> UpdateSourceOptions { get; }
    public ObservableCollection<string> StatusFilterOptions { get; }
    public ObservableCollection<string> HistoryEntries { get; } = new();
    public ObservableCollection<SystemInfoSectionViewModel> SystemInfoSections { get; } = new();

    public int FilteredDriversCount => DriversView.Cast<object>().Count();

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
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                IsStatusVisible = !string.IsNullOrEmpty(value) && value != "Listo";
                StatusForeground = value.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                                   value.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                   value.Contains("No se pudo", StringComparison.OrdinalIgnoreCase)
                    ? "#FFFF6B6B" : "#FF9ED27A";
            }
        }
    }

    public bool IsStatusVisible
    {
        get => _isStatusVisible;
        private set => SetProperty(ref _isStatusVisible, value);
    }

    public string StatusForeground
    {
        get => _statusForeground;
        private set => SetProperty(ref _statusForeground, value);
    }

    public string SystemInfoStatus
    {
        get => _systemInfoStatus;
        private set => SetProperty(ref _systemInfoStatus, value);
    }
    private string _systemInfoStatus = string.Empty;

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

    public int GpuAttentionCount
    {
        get => _gpuAttentionCount;
        private set => SetProperty(ref _gpuAttentionCount, value);
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

    public DownloadsViewModel DownloadsVM { get; }
    public MaintenanceViewModel MaintenanceVM { get; }

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
                OnPropertyChanged(nameof(FilteredDriversCount));
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
                OnPropertyChanged(nameof(FilteredDriversCount));
            }
        }
    }

    public string SelectedUpdateSource
    {
        get => _selectedUpdateSource;
        set
        {
            if (SetProperty(ref _selectedUpdateSource, value))
            {
                var code = MapUpdateSourceToCode(value);
                _settings.UpdateSource = code;
                _updateSource = DriverUpdateSourceFactory.Create(_settings, _logger);
                _updateSourceInstalls = DriverUpdateSourceFactory.InstallsPackages(_settings);

                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = null;

                DriversView.Refresh();
                RefreshCounters();

                OnPropertyChanged(nameof(UpdateSearchMessage));
                OnPropertyChanged(nameof(EmptyUpdateMessage));
                OnPropertyChanged(nameof(UpdateLoadingIconData));
                OnPropertyChanged(nameof(CurrentSourceName));
            }
        }
    }

    public string CurrentSourceName => DescribeSource(_settings.UpdateSource);

    public string UpdateSearchMessage => $"Buscando actualizaciones ({DescribeSource(_settings.UpdateSource)})...";

    public string EmptyUpdateMessage => $"No hay actualizaciones pendientes de {DescribeSource(_settings.UpdateSource)}. Busca actualizaciones primero.";

    public string UpdateLoadingIconData => DescribeSource(_settings.UpdateSource) switch
    {
        "NVIDIA" => "M23 4 v6 h-6 M1 20 v-6 h6 M3.51 9 a9 9 0 0 1 14.85 -3.36 L23 10 M1 14 l4.64 4.36 A9 9 0 0 0 20.49 15",
        "Intel & Killer" => "M21 16 V8 a2 2 0 0 0 -1 -1.73 l-7 -4 a2 2 0 0 0 -2 0 l-7 4 A2 2 0 0 0 3 8 v8 a2 2 0 0 0 1 1.73 l7 4 a2 2 0 0 0 2 0 l7 -4 A2 2 0 0 0 21 16 Z M3.27 6.96 L12 12.01 L20.73 6.96 M12 22.08 V12",
        "Dell" => "M2 5 a2 2 0 0 1 2 -2 h16 a2 2 0 0 1 2 2 v10 a2 2 0 0 1 -2 2 H4 a2 2 0 0 1 -2 -2 Z M8 21 h8 M12 17 v4",
        _ => "M12 2 l8 3 v6 c0 5 -3.5 9.5 -8 11 c-4.5 -1.5 -8 -6 -8 -11 V5 Z",
    };

    public AppSettings Settings => _settings;

    public IAsyncCommand ScanDriversCommand { get; }
    public IAsyncCommand ScanGpusCommand { get; }
    public IAsyncCommand CheckUpdatesCommand { get; }
    public IAsyncCommand InstallUpdatesCommand { get; }
    public IAsyncCommand CreateBackupCommand { get; }
    public IAsyncCommand RefreshBackupsCommand { get; }
    public IAsyncCommand DeleteBackupCommand { get; }
    public IAsyncCommand RestoreBackupCommand { get; }
    public IAsyncCommand CreateRestorePointCommand { get; }
    public IAsyncCommand InstallDriverCommand { get; }
    public ICommand OpenUrlCommand { get; }
    public IAsyncCommand RefreshHistoryCommand { get; }
    public IAsyncCommand SaveSettingsCommand { get; }
    public IAsyncCommand RefreshSystemInfoCommand { get; }
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
                await PersistStateAsync(DateTime.Now, null);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al escanear controladores.";
                _logger.LogError("Error en el escaneo de controladores.", ex);
            }
        }
    }

    private async Task ScanGpusAsync()
    {
        if (IsBusy)
        {
            return;
        }

        using (BusyScope())
        {
            StatusMessage = "Analizando tarjetas gráficas...";
            _logger.Log("Iniciando análisis de tarjetas gráficas.");
            try
            {
                var gpus = await _gpuService.GetGpusAsync();
                Gpus.Clear();
                foreach (var gpu in gpus)
                {
                    Gpus.Add(new GpuViewModel(gpu));
                }

                await RefreshTelemetryAsync();
                RefreshCounters();
                StatusMessage = $"Análisis completado: {Gpus.Count} tarjeta(s) gráfica(s) encontrada(s).";
                _logger.Log($"Análisis de tarjetas gráficas completado con {Gpus.Count} adaptador(es).");
                await PersistStateAsync(DateTime.Now, null);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al analizar las tarjetas gráficas.";
                _logger.LogError("Error al analizar tarjetas gráficas.", ex);
            }
        }
    }

    private int _telemetryRefreshInProgress;

    public async Task RefreshTelemetryAsync()
    {
        if (Gpus.Count == 0 || Interlocked.CompareExchange(ref _telemetryRefreshInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var targets = Gpus.ToArray();
            var telemetry = await _telemetryService.ReadAsync(targets.Select(g => g.GpuInfo).ToArray());
            for (var i = 0; i < targets.Length && i < telemetry.Count; i++)
            {
                targets[i].Telemetry = telemetry[i];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error al actualizar la telemetría de las tarjetas gráficas.", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _telemetryRefreshInProgress, 0);
        }
    }

    private async Task CheckUpdatesAsync(bool fromStartup = false)
    {
        if (IsBusy)
        {
            return;
        }

        var currentSource = _updateSource;
        var sourceCode = _settings.UpdateSource;

        if (DriverUpdateSourceFactory.RequiresConfiguration(_settings, out var configError))
        {
            StatusMessage = configError;
            if (!fromStartup)
            {
                SelectedPage = AppPage.Settings;
            }

            return;
        }

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        using (BusyScope())
        {
            StatusMessage = $"Buscando actualizaciones ({DescribeSource(sourceCode)})...";
            _logger.Log($"Buscando actualizaciones de controladores ({sourceCode}).");
            try
            {
                var updates = await currentSource.CheckForUpdatesAsync(ct);

                ct.ThrowIfCancellationRequested();

                PendingUpdates.Clear();
                ResetDriverUpdateStatuses();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var update in updates)
                {
                    ct.ThrowIfCancellationRequested();

                    var dedupKey = string.Join("|",
                        Normalize(update.DeviceName),
                        Normalize(update.AvailableVersion),
                        Normalize(update.Provider),
                        Normalize(update.UpdateId));

                    if (!string.IsNullOrWhiteSpace(dedupKey) && !seen.Add(dedupKey))
                    {
                        continue;
                    }

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
                    ? $"No se encontraron actualizaciones de {DescribeSource(sourceCode)}."
                    : $"Se encontraron {PendingUpdates.Count} actualización(es) desde {DescribeSource(sourceCode)}.";
                _logger.Log($"Actualizaciones encontradas: {PendingUpdates.Count}.");
                await PersistStateAsync(null, DateTime.Now);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Búsqueda cancelada por cambio de proveedor.";
                _logger.Log("Búsqueda de actualizaciones cancelada.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"No se pudo consultar {DescribeSource(sourceCode)}.";
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

        var enqueued = 0;
        foreach (var update in PendingUpdates.ToArray())
        {
            var downloadUrl = update.DriverInfo.DriverPath;
            if (string.IsNullOrWhiteSpace(downloadUrl) ||
                !Uri.TryCreate(downloadUrl, UriKind.Absolute, out _))
            {
                continue;
            }

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{update.DriverInfo.DeviceName}_{update.DriverInfo.AvailableVersion}.exe";
            }

            DownloadsVM.EnqueueFromUpdate(
                downloadUrl,
                fileName,
                update.DriverInfo.DeviceName,
                update.DriverInfo.InstalledVersion,
                update.DriverInfo.AvailableVersion,
                DescribeSource(_settings.UpdateSource));

            enqueued++;
        }

        StatusMessage = enqueued > 0
            ? $"{enqueued} descarga(s) encolada(s). Revisa la pestaña Descargas."
            : "No se encontraron URLs de descarga válidas.";
        _logger.Log($"Actualizaciones encoladas: {enqueued}.");

        if (enqueued > 0)
        {
            try
            {
                StatusMessage = "Creando punto de restauración antes de instalar actualizaciones...";
                await _updater.CreateRestorePointAsync("DriverManager: antes de instalar actualizaciones");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error al crear punto de restauración automático.", ex);
            }
        }

        await Task.CompletedTask;
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

                if (_settings.MaxBackups > 0)
                {
                    var deleted = await _backupService.EnforceRetentionAsync(BackupFolder, _settings.MaxBackups);
                    if (deleted > 0)
                    {
                        _logger.Log($"Retención de respaldos: {deleted} respaldo(s) antiguo(s) eliminado(s).");
                    }
                }

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

    private async Task InstallDriverAsync(DriverViewModel? driver)
    {
        if (IsBusy || driver is null)
        {
            return;
        }

        var downloadUrl = driver.DriverInfo.DriverPath;
        if (string.IsNullOrWhiteSpace(downloadUrl) ||
            !Uri.TryCreate(downloadUrl, UriKind.Absolute, out _))
        {
            StatusMessage = "No hay una fuente de descarga válida para este controlador.";
            return;
        }

        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{driver.DriverInfo.DeviceName}_{driver.DriverInfo.AvailableVersion}.exe";
        }

        DownloadsVM.EnqueueFromUpdate(
            downloadUrl,
            fileName,
            driver.DriverInfo.DeviceName,
            driver.DriverInfo.InstalledVersion,
            driver.DriverInfo.AvailableVersion,
            DescribeSource(_settings.UpdateSource));

        StatusMessage = $"Descarga encolada: {fileName}. Revisa la pestaña Descargas.";
        _logger.Log($"Descarga encolada desde Actualizaciones: {fileName}.");

        await Task.CompletedTask;
    }

    private void OpenUrl(object? parameter)
    {
        if (parameter is not string url || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusMessage = "No se pudo abrir la página de descarga.";
            _logger.LogError($"No se pudo abrir la URL {url}.", ex);
        }
    }

    private async Task RefreshSystemInfoAsync()
    {
        try
        {
            SystemInfoStatus = "Obteniendo información del equipo...";
            var info = await _systemInfoService.GetSystemInfoAsync();
            SystemInfoSections.Clear();
            foreach (var section in info.Sections)
            {
                SystemInfoSections.Add(new SystemInfoSectionViewModel(section));
            }

            SystemInfoStatus = $"Actualizado: {info.CapturedAt:g}";
        }
        catch (Exception ex)
        {
            SystemInfoStatus = "No se pudo obtener la información del sistema.";
            _logger.LogError("Error al obtener la información del sistema.", ex);
        }
    }

    private async Task RefreshHistoryAsync()
    {
        try
        {
            HistoryEntries.Clear();
            if (!File.Exists(LogFilePath))
            {
                HistoryLog = "(El log aún no se ha creado. Realiza alguna operación.)";
                HistoryEntries.Add(HistoryLog);
                return;
            }

            var lines = await Task.Run(() => File.ReadAllLines(LogFilePath).Reverse().Take(500).Reverse().ToArray());
            if (lines.Length == 0)
            {
                HistoryLog = "(El log está vacío.)";
                HistoryEntries.Add(HistoryLog);
                return;
            }

            HistoryLog = string.Join(Environment.NewLine, lines);
            foreach (var line in lines)
            {
                HistoryEntries.Add(line);
            }
        }
        catch (Exception ex)
        {
            HistoryLog = $"No se pudo leer el log: {ex.Message}";
            HistoryEntries.Clear();
            HistoryEntries.Add(HistoryLog);
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
                DriversView.Refresh();
                RefreshCounters();
                await PersistStateAsync(null, null);

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
        "Intel & Killer" => "Intel",
        _ => "WindowsUpdate"
    };

    private static string MapUpdateSourceToDisplay(string code) => code switch
    {
        "Nvidia" => "NVIDIA (GeForce)",
        "Dell" => "Dell (Service Tag)",
        "Intel" => "Intel & Killer",
        _ => "Windows Update"
    };

    private static string DescribeSource(string code) => code switch
    {
        "Nvidia" => "NVIDIA",
        "Dell" => "Dell",
        "Intel" => "Intel & Killer",
        _ => "Windows Update"
    };

    private void RefreshCounters()
    {
        OutdatedDriversCount = Drivers.Count(d => d.DriverInfo.Status == DriverStatus.UpdateAvailable);
        AttentionCount = Drivers.Count(d => d.DriverInfo.Status == DriverStatus.ProblemDetected);
        GpuAttentionCount = Gpus.Count(g => g.GpuInfo.HasProblem);
        OnPropertyChanged(nameof(FilteredDriversCount));
    }

    private async Task PersistStateAsync(DateTime? scanAt, DateTime? checkAt)
    {
        if (scanAt is not null)
        {
            _lastScanAt = scanAt.Value;
        }

        if (checkAt is not null)
        {
            _lastCheckAt = checkAt.Value;
        }

        var snapshot = new DriverStateSnapshot
        {
            LastScanAt = _lastScanAt,
            LastUpdateCheckAt = _lastCheckAt,
            UpdateSource = _settings.UpdateSource,
            Drivers = Drivers.Select(d => d.DriverInfo).ToList(),
            Gpus = Gpus.Select(g => g.GpuInfo).ToList()
        };

        await _stateStore.SaveAsync(snapshot);
    }

    private static bool DeviceNamesMatch(string a, string b) => DriverManager.Core.StringHelper.DeviceNamesMatch(a, b);

    private static string Normalize(string value) => DriverManager.Core.StringHelper.Normalize(value);

    private void ResetDriverUpdateStatuses()
    {
        foreach (var driver in Drivers)
        {
            if (driver.DriverInfo.Status == DriverStatus.UpdateAvailable)
            {
                driver.DriverInfo.Status = DriverStatus.Installed;
                driver.Refresh();
            }
        }
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
        InstallDriverCommand.RaiseCanExecuteChanged();
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
