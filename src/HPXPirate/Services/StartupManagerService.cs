using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace HPXPirate.Services;

/// <summary>Adds/removes HPX PIRATE from the Windows startup (HKCU Run) and lists startup entries.</summary>
public sealed class StartupManagerService
{
    private readonly ILogger _log;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "HPXPIRATE";

    public StartupManagerService(ILogger log) => _log = log;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) != null;
        }
        catch { return false; }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (key is null) return;
            if (enabled)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName
                          ?? Path.Combine(AppContext.BaseDirectory, "HPXPirate.exe");
                key.SetValue(AppName, $"\"{exe}\" --tray");
                _log.Info("Added to Windows startup.");
            }
            else
            {
                key.DeleteValue(AppName, false);
                _log.Info("Removed from Windows startup.");
            }
        }
        catch (Exception ex)
        {
            _log.Warn("Could not change startup setting: " + ex.Message);
        }
    }

    public List<(string Name, string Command, string Source)> ListStartupEntries()
    {
        var list = new List<(string, string, string)>();
        ReadRunKey(Registry.CurrentUser, RunKey, "HKCU", list);
        ReadRunKey(Registry.LocalMachine, RunKey, "HKLM", list);
        return list;
    }

    private static void ReadRunKey(RegistryKey root, string path, string source, List<(string, string, string)> list)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                var cmd = key.GetValue(name)?.ToString() ?? "";
                list.Add((name, cmd, source));
            }
        }
        catch { }
    }
}
