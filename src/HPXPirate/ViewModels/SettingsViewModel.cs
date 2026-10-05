using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private AppSettings S => _host.AppSettings;

    public SettingsViewModel(AppHost host)
    {
        _host = host;
        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(ResetAll);
        RestorePointCommand = new RelayCommand(() =>
        {
            _host.Restore.TryCreateRestorePoint("HPX PIRATE manual restore point", out var msg);
            Status = msg;
        });
    }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set { S.StartWithWindows = value; _host.Startup.SetEnabled(value); OnPropertyChanged(); }
    }
    public bool MinimizeToTray { get => S.MinimizeToTray; set { S.MinimizeToTray = value; OnPropertyChanged(); } }
    public string Language { get => S.Language; set { S.Language = value; OnPropertyChanged(); } }
    public string Theme { get => S.Theme; set { S.Theme = value; OnPropertyChanged(); } }

    public OptimizationMode DefaultBoostMode { get => S.DefaultBoostMode; set { S.DefaultBoostMode = value; OnPropertyChanged(); } }
    public bool AutoOptimize { get => S.AutoOptimize; set { S.AutoOptimize = value; OnPropertyChanged(); } }
    public int MonitoringIntervalMs
    {
        get => S.MonitoringIntervalMs;
        set { S.MonitoringIntervalMs = value; _host.Monitor.ApplyIntervalSettings(); OnPropertyChanged(); }
    }
    public int IdleMonitoringIntervalMs { get => S.IdleMonitoringIntervalMs; set { S.IdleMonitoringIntervalMs = value; OnPropertyChanged(); } }
    public bool ReduceMonitoringWhenIdle { get => S.ReduceMonitoringWhenIdle; set { S.ReduceMonitoringWhenIdle = value; OnPropertyChanged(); } }

    public bool AutoLoadProfiles { get => S.AutoLoadProfiles; set { S.AutoLoadProfiles = value; OnPropertyChanged(); } }
    public bool CreateRestorePoints { get => S.CreateRestorePoints; set { S.CreateRestorePoints = value; OnPropertyChanged(); } }
    public bool BackupBeforeChanges { get => S.BackupBeforeChanges; set { S.BackupBeforeChanges = value; OnPropertyChanged(); } }
    public bool SafeMode { get => S.SafeMode; set { S.SafeMode = value; OnPropertyChanged(); } }
    public bool DiagnosticMode { get => S.DiagnosticMode; set { S.DiagnosticMode = value; OnPropertyChanged(); } }

    public string[] Languages { get; } = { "en", "id" };
    public string[] Themes { get; } = { "Dark", "Midnight", "Neon" };
    public OptimizationMode[] Modes { get; } = { OptimizationMode.Balanced, OptimizationMode.Performance, OptimizationMode.Extreme, OptimizationMode.Custom };

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand RestorePointCommand { get; }

    private void Save()
    {
        _host.Settings.Save();
        Status = "Settings saved.";
    }

    private void ResetAll()
    {
        var result = MessageBox.Show(
            "Reset ALL HPX PIRATE settings to defaults?\nThis does not touch Windows settings or your profiles.",
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        _host.Settings.ResetToDefaults();
        OnPropertyChanged(string.Empty);
        Status = "Settings reset to defaults.";
    }
}
