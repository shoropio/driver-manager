using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverManager.App.Localization;
using DriverManager.Core.Interfaces;
using DriverManager.Core.Models;

namespace DriverManager.App.ViewModels;

public sealed class MaintenanceViewModel : ObservableObject
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly IDriverUpdateSource? _updateSource;
    private readonly ILogger _logger;
    private bool _isBusy;
    private bool _hasScanned;
    private string _statusMessage = string.Empty;
    private int _totalIssues;
    private int _obsoleteCount;
    private int _missingCount;
    private int _brokenCount;
    private int _unsignedCount;
    private string _selectedFilter = "Todos";
    private CancellationTokenSource? _scanCts;

    public MaintenanceViewModel(IMaintenanceService maintenanceService, IDriverUpdateSource? updateSource, ILogger logger)
    {
        _maintenanceService = maintenanceService;
        _updateSource = updateSource;
        _logger = logger;

        Issues = new ObservableCollection<MaintenanceItemViewModel>();
        FilteredIssues = new ObservableCollection<MaintenanceItemViewModel>();

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        RepairAllCommand = new AsyncRelayCommand(RepairAllAsync, () => !IsBusy && Issues.Count > 0);
        RepairSelectedCommand = new AsyncRelayCommand<MaintenanceItemViewModel>(RepairSingleAsync, _ => !IsBusy);

        FilterOptions = new ObservableCollection<string>
        {
            "Todos",
            DriverText.MaintenanceIssueTypeText(MaintenanceIssueType.Broken),
            DriverText.MaintenanceIssueTypeText(MaintenanceIssueType.Missing),
            DriverText.MaintenanceIssueTypeText(MaintenanceIssueType.Obsolete),
            DriverText.MaintenanceIssueTypeText(MaintenanceIssueType.Unsigned)
        };
    }

    public ObservableCollection<MaintenanceItemViewModel> Issues { get; }
    public ObservableCollection<MaintenanceItemViewModel> FilteredIssues { get; }
    public ObservableCollection<string> FilterOptions { get; }

    public ICommand ScanCommand { get; }
    public ICommand RepairAllCommand { get; }
    public ICommand RepairSelectedCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ((AsyncRelayCommand)ScanCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)RepairAllCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasScanned
    {
        get => _hasScanned;
        private set => SetProperty(ref _hasScanned, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int TotalIssues
    {
        get => _totalIssues;
        private set => SetProperty(ref _totalIssues, value);
    }

    public int ObsoleteCount
    {
        get => _obsoleteCount;
        private set => SetProperty(ref _obsoleteCount, value);
    }

    public int MissingCount
    {
        get => _missingCount;
        private set => SetProperty(ref _missingCount, value);
    }

    public int BrokenCount
    {
        get => _brokenCount;
        private set => SetProperty(ref _brokenCount, value);
    }

    public int UnsignedCount
    {
        get => _unsignedCount;
        private set => SetProperty(ref _unsignedCount, value);
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    private async Task ScanAsync()
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        using (new BusyScope(this))
        {
            StatusMessage = "Escaneando el sistema en busca de problemas de controladores...";
            Issues.Clear();
            FilteredIssues.Clear();

            try
            {
                var results = await _maintenanceService.ScanAsync(ct);
                foreach (var issue in results)
                {
                    Issues.Add(new MaintenanceItemViewModel(issue));
                }

                RefreshCounters();
                ApplyFilter();
                HasScanned = true;

                StatusMessage = TotalIssues == 0
                    ? "No se encontraron problemas. El sistema está operando correctamente."
                    : $"Escaneo completado: {TotalIssues} problema(s) encontrado(s).";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Escaneo cancelado.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error durante el escaneo: {ex.Message}";
                _logger.LogError("Error en escaneo de mantenimiento.", ex);
            }
        }
    }

    private async Task RepairAllAsync()
    {
        using (new BusyScope(this))
        {
            StatusMessage = "Reparando problemas...";
            var repaired = 0;
            var failed = 0;

            foreach (var issue in Issues.ToList())
            {
                try
                {
                    var result = await _maintenanceService.RepairIssueAsync(issue.Issue);
                    if (result.Success)
                    {
                        repaired++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError($"Error al reparar: {issue.DeviceName}", ex);
                }
            }

            StatusMessage = $"Reparación completada: {repaired} exitosa(s), {failed} fallida(s).";
            await ScanAsync();
        }
    }

    private async Task RepairSingleAsync(MaintenanceItemViewModel? item)
    {
        if (item is null) return;

        using (new BusyScope(this))
        {
            StatusMessage = $"Reparando '{item.DeviceName}'...";

            try
            {
                var result = await _maintenanceService.RepairIssueAsync(item.Issue);
                StatusMessage = result.Success
                    ? $"Reparado: {result.Message}"
                    : $"No se pudo reparar: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger.LogError($"Error al reparar: {item.DeviceName}", ex);
            }
        }
    }

    private void RefreshCounters()
    {
        TotalIssues = Issues.Count;
        ObsoleteCount = Issues.Count(i => i.IssueType == MaintenanceIssueType.Obsolete);
        MissingCount = Issues.Count(i => i.IssueType == MaintenanceIssueType.Missing);
        BrokenCount = Issues.Count(i => i.IssueType == MaintenanceIssueType.Broken);
        UnsignedCount = Issues.Count(i => i.IssueType == MaintenanceIssueType.Unsigned);
    }

    private void ApplyFilter()
    {
        FilteredIssues.Clear();

        var filtered = string.IsNullOrWhiteSpace(SelectedFilter) || SelectedFilter == "Todos"
            ? Issues
            : new ObservableCollection<MaintenanceItemViewModel>(
                Issues.Where(i => i.IssueTypeText == SelectedFilter));

        foreach (var item in filtered)
        {
            FilteredIssues.Add(item);
        }
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly MaintenanceViewModel _vm;

        public BusyScope(MaintenanceViewModel vm)
        {
            _vm = vm;
            _vm.IsBusy = true;
        }

        public void Dispose()
        {
            _vm.IsBusy = false;
        }
    }
}
