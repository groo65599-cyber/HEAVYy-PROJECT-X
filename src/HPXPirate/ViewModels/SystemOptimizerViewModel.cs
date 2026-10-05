using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed record StartupEntry(string Name, string Command, string Source);

public sealed class SystemOptimizerViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public SystemOptimizerViewModel(AppHost host)
    {
        _host = host;
        RefreshCommand = new RelayCommand(Refresh);
        OptimizeProcessCommand = new RelayCommand(OptimizeSelectedProcess, () => SelectedProcess != null);
        CleanTempCommand = new RelayCommand(() => RunCleanup(OptimizationCategory.TempCleanup));
        CleanCacheCommand = new RelayCommand(() => RunCleanup(OptimizationCategory.CacheCleanup));
        RestorePointCommand = new RelayCommand(CreateRestorePoint);
        BackupSettingsCommand = new RelayCommand(BackupSettings);
        RestoreSettingsCommand = new RelayCommand(RestoreSettings);
        RollbackCommand = new RelayCommand(Rollback, () => _host.Optimizer.Changes.Any(c => !c.Reverted));
        Refresh();
    }

    public ObservableCollection<ProcessInfoModel> Processes { get; } = new();
    public ObservableCollection<StartupEntry> StartupEntries { get; } = new();
    public ObservableCollection<ChangeRecord> Changes { get; } = new();

    private ProcessInfoModel? _selectedProcess;
    public ProcessInfoModel? SelectedProcess
    {
        get => _selectedProcess;
        set { SetProperty(ref _selectedProcess, value); ((RelayCommand)OptimizeProcessCommand).RaiseCanExecuteChanged(); }
    }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public ICommand RefreshCommand { get; }
    public ICommand OptimizeProcessCommand { get; }
    public ICommand CleanTempCommand { get; }
    public ICommand CleanCacheCommand { get; }
    public ICommand RestorePointCommand { get; }
    public ICommand BackupSettingsCommand { get; }
    public ICommand RestoreSettingsCommand { get; }
    public ICommand RollbackCommand { get; }

    private void Refresh()
    {
        var procs = _host.Processes.GetTopProcesses(30);
        Processes.Clear();
        foreach (var p in procs) Processes.Add(p);

        StartupEntries.Clear();
        foreach (var (name, cmd, src) in _host.Startup.ListStartupEntries())
            StartupEntries.Add(new StartupEntry(name, cmd, src));

        Changes.Clear();
        foreach (var c in _host.Optimizer.Changes.OrderByDescending(c => c.Timestamp))
            Changes.Add(c);
        ((RelayCommand)RollbackCommand).RaiseCanExecuteChanged();
    }

    private void OptimizeSelectedProcess()
    {
        if (SelectedProcess is null) return;
        if (!SelectedProcess.IsSafeToOptimize)
        {
            Status = $"{SelectedProcess.Name} is protected (system/security/game) and was not modified.";
            return;
        }
        _host.Processes.SetPriority(SelectedProcess.Pid, ProcessPriorityClass.BelowNormal, out var msg);
        Status = msg;
        Refresh();
    }

    private void RunCleanup(OptimizationCategory category)
    {
        var plan = _host.Optimizer.BuildPlan(OptimizationMode.Custom);
        var action = plan.FirstOrDefault(a => a.Category == category);
        if (action is null) { Status = "Action not available."; return; }
        action.Selected = true;
        var result = _host.Optimizer.ApplyPlan(new[] { action }, OptimizationMode.Custom, false);
        Status = result.Steps.FirstOrDefault()?.Detail ?? result.Summary;
        Refresh();
    }

    private void CreateRestorePoint()
    {
        bool ok = _host.Restore.TryCreateRestorePoint("HPX PIRATE manual restore point", out var msg);
        Status = msg;
    }

    private void BackupSettings()
    {
        try
        {
            AppPaths.EnsureCreated();
            var target = Path.Combine(AppPaths.Backups, $"settings_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            File.Copy(AppPaths.SettingsFile, target, true);
            Status = "Settings backed up: " + target;
        }
        catch (Exception ex) { Status = "Backup failed: " + ex.Message; }
    }

    private void RestoreSettings()
    {
        try
        {
            var latest = new DirectoryInfo(AppPaths.Backups)
                .GetFiles("settings_*.json")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (latest is null) { Status = "No settings backup found."; return; }
            File.Copy(latest.FullName, AppPaths.SettingsFile, true);
            _host.Settings.Load();
            Status = "Settings restored from " + latest.Name;
        }
        catch (Exception ex) { Status = "Restore failed: " + ex.Message; }
    }

    private void Rollback()
    {
        int n = _host.Optimizer.RollbackAll();
        Status = n > 0 ? $"Reverted {n} change(s)." : "Nothing to revert.";
        Refresh();
    }
}
