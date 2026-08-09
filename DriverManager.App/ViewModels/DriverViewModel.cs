using DriverManager.App.Localization;
using DriverManager.Core.Models;

namespace DriverManager.App.ViewModels;

public sealed class DriverViewModel : ObservableObject
{
    private DriverInfo _driverInfo;
    private bool _isSelected;

    public DriverViewModel(DriverInfo driverInfo)
    {
        _driverInfo = driverInfo;
    }

    public DriverInfo DriverInfo
    {
        get => _driverInfo;
        set
        {
            if (SetProperty(ref _driverInfo, value))
            {
                Refresh();
            }
        }
    }

    public string DeviceName => DriverText.DeviceNameText(_driverInfo.DeviceName);
    public string Category => DriverText.CategoryText(_driverInfo.Category);
    public string Manufacturer => DriverText.ManufacturerText(_driverInfo.Manufacturer);
    public string InstalledVersion => DriverText.VersionText(_driverInfo.InstalledVersion);
    public string AvailableVersion => DriverText.VersionText(_driverInfo.AvailableVersion);
    public string Status => _driverInfo.Status.ToString();
    public string StatusText => DriverText.StatusText(_driverInfo.Status);
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(DeviceName));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Manufacturer));
        OnPropertyChanged(nameof(InstalledVersion));
        OnPropertyChanged(nameof(AvailableVersion));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
    }
}
