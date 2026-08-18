using DriverManager.Core.Models;

namespace DriverManager.App.ViewModels;

public sealed class SystemInfoSectionViewModel
{
    public SystemInfoSectionViewModel(SystemInfoSection section)
    {
        Title = section.Title;
        Items = section.Items.ToArray();
    }

    public string Title { get; }

    public IReadOnlyList<SystemInfoItem> Items { get; }
}
