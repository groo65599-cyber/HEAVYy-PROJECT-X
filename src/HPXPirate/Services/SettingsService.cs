using System.IO;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>Loads and persists <see cref="AppSettings"/> to disk (JSON).</summary>
public sealed class SettingsService
{
    private readonly ILogger _log;
    public AppSettings Current { get; private set; } = new();

    public SettingsService(ILogger log)
    {
        _log = log;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                Current = JsonHelper.Deserialize<AppSettings>(json) ?? new AppSettings();
                _log.Info("Settings loaded.");
            }
            else
            {
                Current = new AppSettings();
                Save();
                _log.Info("Default settings created.");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load settings, using defaults.", ex);
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(AppPaths.SettingsFile, JsonHelper.Serialize(Current));
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save settings.", ex);
        }
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        Save();
        _log.Info("Settings reset to defaults.");
    }
}
