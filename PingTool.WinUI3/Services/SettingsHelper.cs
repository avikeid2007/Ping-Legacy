using System.Text.Json;

namespace PingTool.Services;

/// <summary>
/// Simple settings storage using local app data folder.
/// </summary>
public static class SettingsHelper
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PingLegacy");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    // Accessed from the UI thread, scheduled-ping timer threads, and background scan tasks at once -
    // a plain Dictionary is not thread-safe and concurrent writes were crashing the app in production.
    private static readonly object _lock = new();
    private static Dictionary<string, string>? _settings;

    private static Dictionary<string, string> Settings
    {
        get
        {
            if (_settings == null)
            {
                Load();
            }
            return _settings!;
        }
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                _settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                _settings = new();
            }
        }
        catch
        {
            _settings = new();
        }
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(Settings);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public static void Save<T>(string key, T value)
    {
        lock (_lock)
        {
            try
            {
                Settings[key] = JsonSerializer.Serialize(value);
                Persist();
            }
            catch
            {
                // Ignore serialization errors - don't let a bad value crash the app
            }
        }
    }

    public static T? Read<T>(string key)
    {
        lock (_lock)
        {
            if (Settings.TryGetValue(key, out var json))
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(json);
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }
    }
}
