using System.IO;
using System.Text.Json;

namespace ClaudeQuotaTracker;

public enum IconMetric { HigherOfTwo, Session, Week }

public sealed class ShiftConfig
{
    public string Name { get; set; } = "Shift";
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public bool AutoTriggerEnabled { get; set; } = true;
}

public sealed class AppSettings
{
    public int RefreshIntervalSeconds { get; set; } = 300;
    public IconMetric Metric { get; set; } = IconMetric.HigherOfTwo;
    public bool AutoStartEnabled { get; set; } = true;

    public List<ShiftConfig> Shifts { get; set; } = new()
    {
        new ShiftConfig { Name = "Morning", Start = new TimeSpan(9, 0, 0), End = new TimeSpan(17, 30, 0) },
        new ShiftConfig { Name = "Night", Start = new TimeSpan(22, 0, 0), End = new TimeSpan(2, 0, 0) }
    };

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeQuotaTracker", "config.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // Corrupt or unreadable config — fall back to defaults rather
            // than crashing on startup. The user can re-save from Settings.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
