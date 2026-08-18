using System.Windows;
using System.Windows.Controls;
using DriverManager.App.ViewModels;
using DriverManager.App.Views;

namespace DriverManager.App.Converters;

public class ViewTemplateSelector : DataTemplateSelector
{
    public DataTemplate DashboardTemplate { get; set; } = null!;
    public DataTemplate DriversTemplate { get; set; } = null!;
    public DataTemplate GpuTemplate { get; set; } = null!;
    public DataTemplate SystemTemplate { get; set; } = null!;
    public DataTemplate UpdatesTemplate { get; set; } = null!;
    public DataTemplate DownloadsTemplate { get; set; } = null!;
    public DataTemplate BackupsTemplate { get; set; } = null!;
    public DataTemplate RestoreTemplate { get; set; } = null!;
    public DataTemplate HistoryTemplate { get; set; } = null!;
    public DataTemplate SettingsTemplate { get; set; } = null!;

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            AppPage.Dashboard => DashboardTemplate,
            AppPage.Drivers => DriversTemplate,
            AppPage.Gpu => GpuTemplate,
            AppPage.System => SystemTemplate,
            AppPage.Updates => UpdatesTemplate,
            AppPage.Downloads => DownloadsTemplate,
            AppPage.Backups => BackupsTemplate,
            AppPage.Restore => RestoreTemplate,
            AppPage.History => HistoryTemplate,
            AppPage.Settings => SettingsTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}