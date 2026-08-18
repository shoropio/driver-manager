using DriverManager.Core.Models;
using System.Text.Json;

namespace DriverManager.Services.Implementations;

public sealed class DriverStateStore
{
    public string StatePath { get; }

    public DriverStateStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverManager"))
    {
    }

    public DriverStateStore(string baseFolder)
    {
        Directory.CreateDirectory(baseFolder);
        StatePath = Path.Combine(baseFolder, "driver-state.json");
    }

    public async Task<DriverStateSnapshot?> LoadAsync()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(StatePath);
            return JsonSerializer.Deserialize<DriverStateSnapshot>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(DriverStateSnapshot snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(StatePath, json);
        }
        catch
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                File.Delete(StatePath);
            }
        }
        catch
        {
        }
    }
}
