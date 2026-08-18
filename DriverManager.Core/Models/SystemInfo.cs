namespace DriverManager.Core.Models;

/// <summary>
/// Información general del equipo (hardware, BIOS, placa base, sistema operativo).
/// </summary>
public sealed class SystemInfo
{
    public DateTime CapturedAt { get; set; }

    public List<SystemInfoSection> Sections { get; set; } = new();
}

public sealed class SystemInfoSection
{
    public string Title { get; set; } = string.Empty;

    public List<SystemInfoItem> Items { get; set; } = new();
}

public sealed class SystemInfoItem
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
