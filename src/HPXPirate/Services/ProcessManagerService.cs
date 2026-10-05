using System.Diagnostics;
using HPXPirate.Interop;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// Enumerates processes and applies safe priority changes. Windows system processes,
/// security software, drivers and critical services are NEVER touched.
/// </summary>
public sealed class ProcessManagerService
{
    private readonly ILogger _log;

    // Never optimise / terminate these.
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "memory compression", "secure system",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "dwm", "fontdrvhost", "sihost", "ctfmon", "taskhostw",
        "explorer", "shellexperiencehost", "startmenuexperiencehost",
        "searchhost", "runtimebroker", "audiodg", "conhost", "wmiprvse",
        "msmpeng", "nissrv", "securityhealthservice", "windefend",
        "avp", "avast", "avgsvc", "mcafee", "bdagent", "ekrn", "nortonsecurity",
        "nvcontainer", "nvdisplay.container", "amdow", "radeonsoftware",
        "igfxem", "igfxtray", "hpxpirate"
    };

    // Common game/launcher executables (used for auto-detection heuristics).
    private static readonly HashSet<string> GameHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "freefire", "garena", "pubg", "pubgm", "codm", "gameloop", "bluestacks",
        "ldplayer", "memu", "nox", "msiplayer", "steam", "steamwebhelper",
        "valorant", "cs2", "csgo", "fortnite", "dota2", "genshinimpact", "roblox",
        "leagueclient", "overwatch", "apex_legends", "r5apex", "rainbowsix"
    };

    public ProcessManagerService(ILogger log) => _log = log;

    public bool IsProtected(string name) => Protected.Contains(name);

    public bool LooksLikeGame(string name)
    {
        var n = Path.GetFileNameWithoutExtension(name);
        return GameHints.Contains(n) || GameHints.Any(g => n.Contains(g, StringComparison.OrdinalIgnoreCase));
    }

    public List<ProcessInfoModel> GetTopProcesses(int count = 25)
    {
        var list = new List<ProcessInfoModel>();
        var cpuSamples = new Dictionary<int, (TimeSpan cpu, DateTime time)>();

        Process[] procs;
        try { procs = Process.GetProcesses(); }
        catch (Exception ex) { _log.Error("GetProcesses failed.", ex); return list; }

        foreach (var p in procs)
        {
            try
            {
                double ramMb = p.WorkingSet64 / (1024.0 * 1024.0);
                double cpu = 0;
                try
                {
                    var t = p.TotalProcessorTime;
                    cpu = t.TotalMilliseconds; // raw; converted below if needed
                }
                catch { }

                list.Add(new ProcessInfoModel
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    Description = SafeDescription(p),
                    RamMb = ramMb,
                    CpuPercent = cpu,
                    IsGame = LooksLikeGame(p.ProcessName),
                    IsProtected = IsProtected(p.ProcessName)
                });
            }
            catch { /* process exited */ }
            finally { p.Dispose(); }
        }

        // Sort by RAM as a stable default (CPU delta needs two samples).
        return list.OrderByDescending(x => x.RamMb).Take(count).ToList();
    }

    private static string SafeDescription(Process p)
    {
        try { return p.MainModule?.FileVersionInfo.FileDescription ?? ""; }
        catch { return ""; }
    }

    public bool SetPriority(int pid, ProcessPriorityClass cls, out string message)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            if (IsProtected(p.ProcessName))
            {
                message = $"{p.ProcessName} is protected and was not modified.";
                return false;
            }
            p.PriorityClass = cls;
            message = $"{p.ProcessName} priority set to {cls}.";
            _log.Info(message);
            return true;
        }
        catch (Exception ex)
        {
            message = "Could not change priority: " + ex.Message;
            _log.Warn(message);
            return false;
        }
    }

    public ProcessPriorityClass? GetPriority(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.PriorityClass; }
        catch { return null; }
    }

    /// <summary>Best-effort detection of the currently running game process.</summary>
    public ProcessInfoModel? DetectRunningGame()
    {
        foreach (var proc in GetTopProcesses(60))
        {
            if (proc.IsGame && !proc.IsProtected)
                return proc;
        }
        return null;
    }
}
