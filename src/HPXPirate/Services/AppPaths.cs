using System.IO;

namespace HPXPirate.Services;

/// <summary>Central place for all on-disk locations used by HPX PIRATE.</summary>
public static class AppPaths
{
    public const string ProductName = "HPX PIRATE";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HPXPirate");

    public static string Logs { get; } = Path.Combine(Root, "logs");
    public static string Profiles { get; } = Path.Combine(Root, "profiles");
    public static string Backups { get; } = Path.Combine(Root, "backups");
    public static string RestorePoints { get; } = Path.Combine(Root, "restorepoints");
    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
    public static string ChangesFile { get; } = Path.Combine(Root, "changes.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Profiles);
        Directory.CreateDirectory(Backups);
        Directory.CreateDirectory(RestorePoints);
    }
}
