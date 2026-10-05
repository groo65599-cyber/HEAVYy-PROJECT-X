using System.IO;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>Manages per-game profiles: CRUD, import/export, backup/restore and auto-detection.</summary>
public sealed class ProfileManager
{
    private readonly ILogger _log;
    private readonly List<GameProfile> _profiles = new();

    public event Action? ProfilesChanged;

    public ProfileManager(ILogger log)
    {
        _log = log;
    }

    public IReadOnlyList<GameProfile> Profiles => _profiles;

    public void LoadAll()
    {
        _profiles.Clear();
        try
        {
            AppPaths.EnsureCreated();
            foreach (var file in Directory.EnumerateFiles(AppPaths.Profiles, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var p = JsonHelper.Deserialize<GameProfile>(json);
                    if (p != null) _profiles.Add(p);
                }
                catch (Exception ex)
                {
                    _log.Warn($"Skipping unreadable profile '{Path.GetFileName(file)}': {ex.Message}");
                }
            }

            if (_profiles.Count == 0)
                SeedDefaults();

            _log.Info($"Loaded {_profiles.Count} game profile(s).");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load profiles.", ex);
        }
        ProfilesChanged?.Invoke();
    }

    private void SeedDefaults()
    {
        foreach (var name in new[] { "Free Fire", "PUBG Mobile", "COD Mobile", "Game Custom" })
        {
            var p = GameProfile.CreateDefault(name);
            _profiles.Add(p);
            Save(p);
        }
        _log.Info("Seeded default game profiles.");
    }

    public void Save(GameProfile profile)
    {
        try
        {
            AppPaths.EnsureCreated();
            profile.Modified = DateTime.Now;
            var file = Path.Combine(AppPaths.Profiles, SafeName(profile.Id) + ".json");
            File.WriteAllText(file, JsonHelper.Serialize(profile));
            if (!_profiles.Contains(profile)) _profiles.Add(profile);
            ProfilesChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save profile '{profile.Game}'.", ex);
        }
    }

    public void Delete(GameProfile profile)
    {
        try
        {
            var file = Path.Combine(AppPaths.Profiles, SafeName(profile.Id) + ".json");
            if (File.Exists(file)) File.Delete(file);
            _profiles.Remove(profile);
            ProfilesChanged?.Invoke();
            _log.Info($"Deleted profile '{profile.Game}'.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete profile '{profile.Game}'.", ex);
        }
    }

    public GameProfile Reset(GameProfile profile)
    {
        var fresh = GameProfile.CreateDefault(profile.Game);
        fresh.Id = profile.Id;
        fresh.ExecutablePath = profile.ExecutablePath;
        Save(fresh);
        return fresh;
    }

    // ---- Import / Export -------------------------------------------------

    public string Export(GameProfile profile, string targetPath)
    {
        File.WriteAllText(targetPath, JsonHelper.Serialize(profile));
        _log.Info($"Exported profile '{profile.Game}' to {targetPath}.");
        return targetPath;
    }

    public GameProfile Import(string sourcePath)
    {
        var json = File.ReadAllText(sourcePath);
        var p = JsonHelper.Deserialize<GameProfile>(json)
                ?? throw new InvalidDataException("Profile file is not valid JSON.");
        p.Id = Guid.NewGuid().ToString("N"); // avoid collisions
        Save(p);
        _log.Info($"Imported profile '{p.Game}'.");
        return p;
    }

    public string Backup(GameProfile profile)
    {
        AppPaths.EnsureCreated();
        var target = Path.Combine(AppPaths.Backups, $"{SafeName(profile.Game)}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(target, JsonHelper.Serialize(profile));
        _log.Info($"Backed up profile '{profile.Game}'.");
        return target;
    }

    public GameProfile Restore(string backupPath)
    {
        var json = File.ReadAllText(backupPath);
        var p = JsonHelper.Deserialize<GameProfile>(json)
                ?? throw new InvalidDataException("Backup file is not valid JSON.");
        Save(p);
        _log.Info($"Restored profile '{p.Game}' from backup.");
        return p;
    }

    /// <summary>Returns the profile whose executable matches a running process, if any.</summary>
    public GameProfile? FindByExecutable(string exePathOrName)
    {
        if (string.IsNullOrWhiteSpace(exePathOrName)) return null;
        var name = Path.GetFileNameWithoutExtension(exePathOrName).ToLowerInvariant();
        return _profiles.FirstOrDefault(p =>
        {
            if (string.IsNullOrWhiteSpace(p.ExecutablePath)) return false;
            var pn = Path.GetFileNameWithoutExtension(p.ExecutablePath).ToLowerInvariant();
            return pn == name || p.ExecutablePath.ToLowerInvariant() == exePathOrName.ToLowerInvariant();
        });
    }

    private static string SafeName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }
}
