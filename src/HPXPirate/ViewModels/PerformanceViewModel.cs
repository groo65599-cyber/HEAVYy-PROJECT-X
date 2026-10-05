using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class PerformanceViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public PerformanceViewModel(AppHost host)
    {
        _host = host;
        ApplyCommand = new RelayCommand(async () => await ApplyAsync(), () => !IsBusy);
        RollbackCommand = new RelayCommand(Rollback, () => _host.Optimizer.Changes.Any(c => !c.Reverted));
        RefreshPlan();
        RefreshChanges();
    }

    private OptimizationMode _mode = OptimizationMode.Balanced;
    public OptimizationMode Mode
    {
        get => _mode;
        set { if (SetProperty(ref _mode, value)) RefreshPlan(); }
    }

    public ObservableCollection<OptimizationAction> Plan { get; } = new();
    public ObservableCollection<ChangeRecord> Changes { get; } = new();

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { SetProperty(ref _isBusy, value); ((RelayCommand)ApplyCommand).RaiseCanExecuteChanged(); } }

    private string _resultText = "Select a mode and press APPLY OPTIMISATION.";
    public string ResultText { get => _resultText; private set => SetProperty(ref _resultText, value); }

    public bool CreateRestorePoint
    {
        get => _host.AppSettings.CreateRestorePoints;
        set { _host.AppSettings.CreateRestorePoints = value; OnPropertyChanged(); _host.Settings.Save(); }
    }

    public ICommand ApplyCommand { get; }
    public ICommand RollbackCommand { get; }

    private void RefreshPlan()
    {
        Plan.Clear();
        foreach (var a in _host.Optimizer.BuildPlan(Mode))
            Plan.Add(a);
    }

    private void RefreshChanges()
    {
        Changes.Clear();
        foreach (var c in _host.Optimizer.Changes.OrderByDescending(c => c.Timestamp))
            Changes.Add(c);
        ((RelayCommand)RollbackCommand).RaiseCanExecuteChanged();
    }

    private async Task ApplyAsync()
    {
        IsBusy = true;
        ResultText = "Applying optimisations...";
        var plan = Plan.ToList();
        var mode = Mode;
        var restore = CreateRestorePoint;
        var result = await Task.Run(() => _host.Optimizer.ApplyPlan(plan, mode, restore));
        Application.Current?.Dispatcher.Invoke(() =>
        {
            ResultText = result.Summary;
            RefreshPlan();
            RefreshChanges();
            IsBusy = false;
        });
    }

    private void Rollback()
    {
        int n = _host.Optimizer.RollbackAll();
        ResultText = n > 0 ? $"Reverted {n} change(s)." : "Nothing to revert.";
        RefreshChanges();
    }
}
