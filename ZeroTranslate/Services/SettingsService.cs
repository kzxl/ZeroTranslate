using System.IO;
using System.Text.Json;
using ZeroTranslate.Core.Interfaces;
using ZeroTranslate.Models;

namespace ZeroTranslate.Services;

/// <summary>
/// Loads and saves application settings as JSON in %AppData%/ZeroTranslate.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZeroTranslate");

    private static readonly string SettingsFile =
        Path.Combine(SettingsFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently fail — settings are not critical
        }
    }
}
