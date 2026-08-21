using DriverManager.App.Localization;
using DriverManager.Core.Models;

namespace DriverManager.App.ViewModels;

public sealed class MaintenanceItemViewModel : ObservableObject
{
    private readonly MaintenanceIssue _issue;

    public MaintenanceItemViewModel(MaintenanceIssue issue)
    {
        _issue = issue;
    }

    public MaintenanceIssue Issue => _issue;

    public MaintenanceIssueType IssueType => _issue.IssueType;

    public MaintenanceSeverity Severity => _issue.Severity;

    public string DeviceName => DriverText.DeviceNameText(_issue.DeviceName);

    public string Category => DriverText.CategoryText(_issue.Category);

    public string Manufacturer => DriverText.ManufacturerText(_issue.Manufacturer);

    public string IssueTypeText => DriverText.MaintenanceIssueTypeText(_issue.IssueType);

    public string SeverityText => DriverText.MaintenanceSeverityText(_issue.Severity);

    public string Description => _issue.Description;

    public string SuggestedAction => _issue.SuggestedAction;

    public string InstalledVersion => DriverText.VersionText(_issue.InstalledVersion);

    public string AvailableVersion => DriverText.VersionText(_issue.AvailableVersion);

    public string ErrorCodeText => _issue.ErrorCode > 0 ? $"Código {_issue.ErrorCode}" : string.Empty;

    public string SeverityColor => _issue.Severity switch
    {
        MaintenanceSeverity.Critical => "#FFFF6B6B",
        MaintenanceSeverity.Warning => "#FFFFB454",
        _ => "#FF8B8FA6"
    };

    public string IssueTypeIcon => _issue.IssueType switch
    {
        MaintenanceIssueType.Broken => "M12 2 C6.48 2 2 6.48 2 12 s4.48 10 10 10 10-4.48 10-10 S17.52 2 12 2 Z M13 17 h-2 v-2 h2 v2 Z M13 13 h-2 V7 h2 v6 Z",
        MaintenanceIssueType.Missing => "M12 2 C6.48 2 2 6.48 2 12 s4.48 10 10 10 10-4.48 10-10 S17.52 2 12 2 Z M17 13 h-4 v4 h-2 v-4 H7 v-2 h4 V7 h2 v4 h4 v2 Z",
        MaintenanceIssueType.Obsolete => "M12 2 C6.48 2 2 6.48 2 12 s4.48 10 10 10 10-4.48 10-10 S17.52 2 12 2 Z M12 16 l4-4 h-3 V8 h-2 v4 H8 l4 4 Z",
        MaintenanceIssueType.Unsigned => "M12 1 L3 5 v6 c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12 V5 L12 1 Z M11 7 h2 v6 h-2 V7 Z M11 15 h2 v2 h-2 v-2 Z",
        _ => ""
    };
}
