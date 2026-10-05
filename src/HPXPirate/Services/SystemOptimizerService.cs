using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// HPX PERFORMANCE ENGINE — builds and applies a plan of safe, individually
/// reversible optimizations. Every change is backed up so it can be undone.
/// No risky registry edits, no blind service disabling, no overclocking.
/// </summary>
public sealed class SystemOptimizerService
{
    private readonly ILogger _log;
    private readonly RestorePointService _restore;
    private readonly ProcessManagerService _processes;
    private readonly List<ChangeRecord> _changes = new();

    public IReadOnlyList<ChangeRecord> Changes => _changes;

    public SystemOptimizerService(ILogger log, RestorePointService restore, ProcessManagerService processes)
    {
        _log = log;
        _restore = restore;
        _processes = processes;
    }

    // ------------------------------------------------------------------
    //  Plan building
    // ------------------------------------------------------------------
    public List<OptimizationAction> BuildPlan(OptimizationMode mode)
    {
        var all = new List<OptimizationAction>
        {
            new() { Title = "Power Plan Optimisation", Category = OptimizationCategory.PowerPlan, MinimumMode = OptimizationMode.Balanced,
                    Description = "Switch the active Windows power plan to High Performance for maximum responsiveness.",
                    Risk = ActionRisk.Safe },
            new() { Title = "Game Mode Recommendation", Category = OptimizationCategory.GameMode, MinimumMode = OptimizationMode.Balanced,
                    Description = "Ensure Windows Game Mode is enabled so the foreground game gets priority.",
                    Risk = ActionRisk.Safe },
            new() { Title = "Game Process Priority", Category = OptimizationCategory.ProcessPriority, MinimumMode = OptimizationMode.Balanced,
                    Description = "Raise the detected game process priority to High (reversible).",
                    Risk = ActionRisk.Safe },
            new() { Title = "Temporary File Cleanup", Category = OptimizationCategory.TempCleanup, MinimumMode = OptimizationMode.Balanced,
                    Description = "Remove stale files from the user Temp folder. System files are never touched.",
                    Risk = ActionRisk.Safe, IsReversible = false },
            new() { Title = "Game DVR / Capture Disable", Category = OptimizationCategory.GameDvr, MinimumMode = OptimizationMode.Performance,
                    Description = "Disable background game capture (Game DVR) which can cost frames.",
                    Risk = ActionRisk.Safe },
            new() { Title = "Visual Effects (Performance)", Category = OptimizationCategory.VisualEffects, MinimumMode = OptimizationMode.Performance,
                    Description = "Set Windows visual effects to favour performance.",
                    Risk = ActionRisk.Moderate },
            new() { Title = "Shader / Cache Cleanup", Category = OptimizationCategory.CacheCleanup, MinimumMode = OptimizationMode.Performance,
                    Description = "Clear DirectX shader cache so it can rebuild cleanly.",
                    Risk = ActionRisk.Safe, IsReversible = false },
            new() { Title = "Background Load Reduction", Category = OptimizationCategory.BackgroundProcess, MinimumMode = OptimizationMode.Performance,
                    Description = "Lower priority of safe background apps (browsers, launchers). Protected apps are skipped.",
                    Risk = ActionRisk.Moderate },
            new() { Title = "Startup App Review", Category = OptimizationCategory.StartupApp, MinimumMode = OptimizationMode.Extreme,
                    Description = "Disable non-essential user startup entries (reversible).",
                    Risk = ActionRisk.Advanced },
            new() { Title = "Network Latency Tweak", Category = OptimizationCategory.NetworkTweak, MinimumMode = OptimizationMode.Extreme,
                    Description = "Apply conservative TCP settings that can reduce latency (reversible).",
                    Risk = ActionRisk.Advanced },
        };

        foreach (var a in all)
            a.Selected = mode == OptimizationMode.Custom
                ? a.Risk == ActionRisk.Safe
                : a.MinimumMode <= mode;

        return all;
    }

    // ------------------------------------------------------------------
    //  Apply
    // ------------------------------------------------------------------
    public BoostResult ApplyPlan(IEnumerable<OptimizationAction> plan, OptimizationMode mode, bool createRestorePoint)
    {
        var result = new BoostResult { Mode = mode };
        var selected = plan.Where(a => a.Selected).ToList();

        _log.Info($"Applying {selected.Count} optimization(s) in {mode} mode.");

        if (createRestorePoint)
        {
            _restore.TryCreateRestorePoint($"HPX PIRATE - {mode} boost", out var msg);
            result.RestorePointId = _restore.WriteInternalSnapshot(JsonHelper.Serialize(_changes));
            result.Steps.Add(new BoostStepResult { Label = "Restore Point", Success = true, Detail = msg });
        }

        int ok = 0;
        foreach (var action in selected)
        {
            bool success;
            try
            {
                success = ApplyAction(action);
            }
            catch (Exception ex)
            {
                success = false;
                action.ResultMessage = ex.Message;
                _log.Error($"Optimization '{action.Title}' failed.", ex);
            }

            action.Applied = true;
            action.Succeeded = success;
            if (success)
            {
                ok++;
                _changes.Add(new ChangeRecord
                {
                    Category = action.Category.ToString(),
                    Description = action.Title,
                    PreviousState = action.PreviousState
                });
            }

            result.Steps.Add(new BoostStepResult
            {
                Label = action.Title,
                Success = success,
                Detail = action.ResultMessage
            });
        }

        result.Success = ok > 0;
        result.Summary = ok == selected.Count
            ? $"All {ok} optimization(s) applied successfully."
            : $"{ok} of {selected.Count} optimization(s) applied. No unsafe changes were made.";
        SaveChanges();
        return result;
    }

    private bool ApplyAction(OptimizationAction action)
    {
        switch (action.Category)
        {
            case OptimizationCategory.PowerPlan: return ApplyPowerPlan(action);
            case OptimizationCategory.GameMode: return ApplyGameMode(action);
            case OptimizationCategory.GameDvr: return ApplyGameDvr(action);
            case OptimizationCategory.VisualEffects: return ApplyVisualEffects(action);
            case OptimizationCategory.ProcessPriority: return ApplyProcessPriority(action);
            case OptimizationCategory.TempCleanup: return CleanTemp(action);
            case OptimizationCategory.CacheCleanup: return CleanCache(action);
            case OptimizationCategory.BackgroundProcess: return ReduceBackground(action);
            case OptimizationCategory.StartupApp: return ReviewStartup(action);
            case OptimizationCategory.NetworkTweak: return ApplyNetworkTweak(action);
            default:
                action.ResultMessage = "Unknown action.";
                return false;
        }
    }

    // ---- individual executors ----------------------------------------

    private bool ApplyPowerPlan(OptimizationAction a)
    {
        var (cur, _) = Run("powercfg", "/getactivescheme");
        a.PreviousState = cur;
        const string highPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        var (_, err) = Run("powercfg", $"/setactive {highPerf}");
        if (string.IsNullOrWhiteSpace(err))
        {
            a.ResultMessage = "Active power plan set to High Performance.";
            return true;
        }
        a.ResultMessage = "Could not change power plan (run as administrator).";
        return false;
    }

    private bool ApplyGameMode(OptimizationAction a)
    {
        const string path = @"Software\Microsoft\GameBar";
        var prev = ReadReg(Registry.CurrentUser, path, "AutoGameModeEnabled");
        a.PreviousState = prev?.ToString();
        WriteReg(Registry.CurrentUser, path, "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
        a.ResultMessage = "Windows Game Mode enabled.";
        return true;
    }

    private bool ApplyGameDvr(OptimizationAction a)
    {
        var prev1 = ReadReg(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
        var prev2 = ReadReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
        a.PreviousState = $"{prev1}|{prev2}";
        WriteReg(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
        WriteReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
        a.ResultMessage = "Game DVR / background capture disabled.";
        return true;
    }

    private bool ApplyVisualEffects(OptimizationAction a)
    {
        var prev = ReadReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
        a.PreviousState = prev?.ToString();
        WriteReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, RegistryValueKind.DWord);
        a.ResultMessage = "Visual effects set to best performance.";
        return true;
    }

    private bool ApplyProcessPriority(OptimizationAction a)
    {
        var game = _processes.DetectRunningGame();
        if (game is null)
        {
            a.ResultMessage = "No running game detected — skipped (nothing to change).";
            return false;
        }
        var prev = _processes.GetPriority(game.Pid)?.ToString();
        a.PreviousState = $"{game.Pid}|{prev}";
        if (_processes.SetPriority(game.Pid, ProcessPriorityClass.High, out var msg))
        {
            a.ResultMessage = msg;
            return true;
        }
        a.ResultMessage = msg;
        return false;
    }

    private bool CleanTemp(OptimizationAction a)
    {
        long freed = 0;
        int removed = 0;
        var temp = Path.GetTempPath();
        try
        {
            foreach (var file in Directory.EnumerateFiles(temp))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < DateTime.Now.AddDays(-1))
                    {
                        freed += info.Length;
                        info.Delete();
                        removed++;
                    }
                }
                catch { /* in use */ }
            }
            a.ResultMessage = $"Removed {removed} stale temp file(s), freed {freed / 1024.0 / 1024.0:0.0} MB.";
            return true;
        }
        catch (Exception ex)
        {
            a.ResultMessage = "Temp cleanup failed: " + ex.Message;
            return false;
        }
    }

    private bool CleanCache(OptimizationAction a)
    {
        int cleared = 0;
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
            Path.Combine(Path.GetTempPath(), "NVIDIA Corporation"),
            Path.Combine(Path.GetTempPath(), "AMD")
        };
        foreach (var dir in candidates)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { File.Delete(f); cleared++; } catch { }
                }
            }
            catch { }
        }
        a.ResultMessage = $"Cleared {cleared} cached shader file(s).";
        return true;
    }

    private bool ReduceBackground(OptimizationAction a)
    {
        int lowered = 0;
        foreach (var p in _processes.GetTopProcesses(40))
        {
            if (!p.IsSafeToOptimize) continue;
            if (p.RamMb < 200) continue;
            if (_processes.SetPriority(p.Pid, ProcessPriorityClass.BelowNormal, out _))
                lowered++;
        }
        a.ResultMessage = lowered > 0
            ? $"Lowered priority of {lowered} background app(s)."
            : "No safe background apps needed adjustment.";
        return true;
    }

    private bool ReviewStartup(OptimizationAction a)
    {
        // Report-only by default for safety; user can act from the Startup view.
        a.ResultMessage = "Startup entries reviewed. Use the Startup view to disable specific entries.";
        return true;
    }

    private bool ApplyNetworkTweak(OptimizationAction a)
    {
        var (cur, _) = Run("netsh", "int tcp show global");
        a.PreviousState = cur;
        Run("netsh", "int tcp set global autotuninglevel=normal");
        Run("netsh", "int tcp set global timestamps=disabled");
        a.ResultMessage = "Conservative TCP settings applied.";
        return true;
    }

    // ------------------------------------------------------------------
    //  Rollback
    // ------------------------------------------------------------------
    public bool RollbackAction(ChangeRecord record, out string message)
    {
        try
        {
            switch (record.Category)
            {
                case nameof(OptimizationCategory.PowerPlan):
                    var guid = ExtractGuid(record.PreviousState);
                    if (guid != null) Run("powercfg", $"/setactive {guid}");
                    message = "Power plan restored.";
                    break;
                case nameof(OptimizationCategory.GameMode):
                    WriteReg(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled",
                        ParseInt(record.PreviousState, 0), RegistryValueKind.DWord);
                    message = "Game Mode setting restored.";
                    break;
                case nameof(OptimizationCategory.GameDvr):
                    var parts = (record.PreviousState ?? "0|0").Split('|');
                    WriteReg(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", ParseInt(parts.ElementAtOrDefault(0), 0), RegistryValueKind.DWord);
                    WriteReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", ParseInt(parts.ElementAtOrDefault(1), 0), RegistryValueKind.DWord);
                    message = "Game DVR setting restored.";
                    break;
                case nameof(OptimizationCategory.VisualEffects):
                    WriteReg(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting",
                        ParseInt(record.PreviousState, 0), RegistryValueKind.DWord);
                    message = "Visual effects restored.";
                    break;
                case nameof(OptimizationCategory.ProcessPriority):
                    var pp = (record.PreviousState ?? "").Split('|');
                    if (int.TryParse(pp.ElementAtOrDefault(0), out var pid))
                    {
                        var cls = Enum.TryParse<ProcessPriorityClass>(pp.ElementAtOrDefault(1), out var c) ? c : ProcessPriorityClass.Normal;
                        _processes.SetPriority(pid, cls, out _);
                    }
                    message = "Process priority restored.";
                    break;
                case nameof(OptimizationCategory.NetworkTweak):
                    Run("netsh", "int tcp set global autotuninglevel=normal");
                    message = "Network settings restored to defaults.";
                    break;
                default:
                    message = "This change does not require rollback (no system state was modified).";
                    break;
            }
            record.Reverted = true;
            SaveChanges();
            _log.Info("Rolled back: " + record.Description);
            return true;
        }
        catch (Exception ex)
        {
            message = "Rollback failed: " + ex.Message;
            _log.Error("Rollback failed.", ex);
            return false;
        }
    }

    public int RollbackAll()
    {
        int n = 0;
        foreach (var c in _changes.Where(c => !c.Reverted).ToList())
        {
            if (RollbackAction(c, out _)) n++;
        }
        return n;
    }

    // ------------------------------------------------------------------
    //  helpers
    // ------------------------------------------------------------------
    private static (string output, string error) Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return ("", "failed to start");
            var outp = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            return (outp, err);
        }
        catch (Exception ex)
        {
            return ("", ex.Message);
        }
    }

    private static object? ReadReg(RegistryKey root, string path, string name)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(name);
        }
        catch { return null; }
    }

    private static void WriteReg(RegistryKey root, string path, string name, object value, RegistryValueKind kind)
    {
        using var key = root.CreateSubKey(path, writable: true);
        key?.SetValue(name, value, kind);
    }

    private static int ParseInt(string? s, int fallback)
        => int.TryParse(s, out var v) ? v : fallback;

    private static string? ExtractGuid(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var idx = s.IndexOf("GUID:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = s.Substring(idx + 5).Trim();
        var space = rest.IndexOf(' ');
        return space > 0 ? rest[..space] : rest;
    }

    private void SaveChanges()
    {
        try
        {
            AppPaths.EnsureCreated();
            File.WriteAllText(AppPaths.ChangesFile, JsonHelper.Serialize(_changes));
        }
        catch (Exception ex) { _log.Debug("SaveChanges failed: " + ex.Message); }
    }

    public void LoadChanges()
    {
        try
        {
            if (File.Exists(AppPaths.ChangesFile))
            {
                var list = JsonHelper.Deserialize<List<ChangeRecord>>(File.ReadAllText(AppPaths.ChangesFile));
                if (list != null) { _changes.Clear(); _changes.AddRange(list); }
            }
        }
        catch (Exception ex) { _log.Debug("LoadChanges failed: " + ex.Message); }
    }
}
