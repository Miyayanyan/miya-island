using System.Text.Json;
using MiyaIsland.Models;

namespace MiyaIsland.Services;

public sealed class AppearanceSettingsService
{
    private readonly string SettingsDirectory;
    private readonly string SettingsFile;

    public AppearanceSettingsService(string? dataDirectory = null)
    {
        SettingsDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiyaIsland");
        SettingsFile = Path.Combine(SettingsDirectory, "appearance.json");
    }

    public AppearanceSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppearanceSettings();
            var json = File.ReadAllText(SettingsFile);
            return AppearanceSettingsMigration.Deserialize(json);
        }
        catch
        {
            return new AppearanceSettings();
        }
    }

    public void Save(AppearanceSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFile, json);
    }
}
