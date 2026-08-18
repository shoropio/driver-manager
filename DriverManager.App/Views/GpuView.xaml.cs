using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DriverManager.App.ViewModels;

namespace DriverManager.App.Views;

public partial class GpuView : UserControl
{
    private readonly DispatcherTimer _telemetryTimer;

    public GpuView()
    {
        InitializeComponent();
        _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _telemetryTimer.Tick += OnTelemetryTick;
        _telemetryTimer.Start();
    }

    private async void OnTelemetryTick(object? sender, EventArgs e)
    {
        if (!IsVisible || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        await viewModel.RefreshTelemetryAsync();
    }
}
