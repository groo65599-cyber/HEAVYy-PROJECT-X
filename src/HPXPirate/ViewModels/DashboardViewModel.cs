using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public DashboardViewModel(AppHost host)
    {
        _host = host;
        BoostCommand = new RelayCommand(async () => await RunBoostAsync(), () => !IsBoosting);
        _host.Monitor.MetricsUpdated += OnMetrics;
        _host.Monitor.VerdictUpdated += OnVerdict;
    }

    // ---- live metrics ----
    private double _fps, _avgFps, _oneLow, _pointOneLow, _frameTime;
    private double _cpu, _gpu, _ramPercent, _vramPercent, _cpuTemp, _gpuTemp;
    private string _ramText = "-- / -- GB";
    private string _gpuName = "Detecting GPU...";

    public double Fps { get => _fps; private set => SetProperty(ref _fps, value); }
    public double AverageFps { get => _avgFps; private set => SetProperty(ref _avgFps, value); }
    public double OnePercentLow { get => _oneLow; private set => SetProperty(ref _oneLow, value); }
    public double PointOnePercentLow { get => _pointOneLow; private set => SetProperty(ref _pointOneLow, value); }
    public double FrameTime { get => _frameTime; private set => SetProperty(ref _frameTime, value); }
    public double Cpu { get => _cpu; private set => SetProperty(ref _cpu, value); }
    public double Gpu { get => _gpu; private set => SetProperty(ref _gpu, value); }
    public double RamPercent { get => _ramPercent; private set => SetProperty(ref _ramPercent, value); }
    public double VramPercent { get => _vramPercent; private set => SetProperty(ref _vramPercent, value); }
    public double CpuTemp { get => _cpuTemp; private set => SetProperty(ref _cpuTemp, value); }
    public double GpuTemp { get => _gpuTemp; private set => SetProperty(ref _gpuTemp, value); }
    public string RamText { get => _ramText; private set => SetProperty(ref _ramText, value); }
    public string GpuName { get => _gpuName; private set => SetProperty(ref _gpuName, value); }

    public string FpsDisplay => Fps > 0 ? Fps.ToString("0") : "--";
    public string FrameTimeDisplay => FrameTime > 0 ? FrameTime.ToString("0.0") + " ms" : "-- ms";

    // ---- status ----
    private string _statusTitle = "Performance Stable";
    private string _statusDetail = "Waiting for data...";
    private string _statusRecommendation = "";
    private bool _statusHealthy = true;
    public string StatusTitle { get => _statusTitle; private set => SetProperty(ref _statusTitle, value); }
    public string StatusDetail { get => _statusDetail; private set => SetProperty(ref _statusDetail, value); }
    public string StatusRecommendation { get => _statusRecommendation; private set => SetProperty(ref _statusRecommendation, value); }
    public bool StatusHealthy { get => _statusHealthy; private set => SetProperty(ref _statusHealthy, value); }

    // ---- graphs ----
    private List<double> _fpsSeries = new();
    private List<double> _frameTimeSeries = new();
    private List<double> _cpuSeries = new();
    private List<double> _gpuSeries = new();
    public List<double> FpsSeries { get => _fpsSeries; private set => SetProperty(ref _fpsSeries, value); }
    public List<double> FrameTimeSeries { get => _frameTimeSeries; private set => SetProperty(ref _frameTimeSeries, value); }
    public List<double> CpuSeries { get => _cpuSeries; private set => SetProperty(ref _cpuSeries, value); }
    public List<double> GpuSeries { get => _gpuSeries; private set => SetProperty(ref _gpuSeries, value); }

    // ---- boost ----
    private bool _isBoosting;
    private string _boostSummary = "Press BOOST NOW to scan and optimise.";
    public bool IsBoosting { get => _isBoosting; private set { SetProperty(ref _isBoosting, value); ((RelayCommand)BoostCommand).RaiseCanExecuteChanged(); } }
    public string BoostSummary { get => _boostSummary; private set => SetProperty(ref _boostSummary, value); }
    public ObservableCollection<BoostStepResult> BoostSteps { get; } = new();

    public ICommand BoostCommand { get; }

    private void OnMetrics(PerformanceMetrics m)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Fps = m.Fps;
            AverageFps = m.AverageFps;
            OnePercentLow = m.OnePercentLow;
            PointOnePercentLow = m.PointOnePercentLow;
            FrameTime = m.FrameTimeMs;
            Cpu = m.CpuUsage;
            Gpu = m.GpuUsage;
            RamPercent = m.RamUsagePercent;
            VramPercent = m.VramUsagePercent;
            CpuTemp = m.CpuTemperature;
            GpuTemp = m.GpuTemperature;
            RamText = $"{m.RamUsedMb / 1024:0.0} / {m.RamTotalMb / 1024:0.0} GB";
            if (!string.IsNullOrWhiteSpace(m.GpuName)) GpuName = m.GpuName;

            OnPropertyChanged(nameof(FpsDisplay));
            OnPropertyChanged(nameof(FrameTimeDisplay));

            FpsSeries = _host.Monitor.FpsHistory.Snapshot(TimeSpan.FromSeconds(60)).Select(x => x.v).ToList();
            FrameTimeSeries = _host.Monitor.FrameTimeHistory.Snapshot(TimeSpan.FromSeconds(60)).Select(x => x.v).ToList();
            CpuSeries = _host.Monitor.CpuHistory.Snapshot(TimeSpan.FromSeconds(60)).Select(x => x.v).ToList();
            GpuSeries = _host.Monitor.GpuHistory.Snapshot(TimeSpan.FromSeconds(60)).Select(x => x.v).ToList();
        });
    }

    private void OnVerdict(SystemVerdict v)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusTitle = v.Title;
            StatusDetail = v.Detail;
            StatusRecommendation = v.Recommendation;
            StatusHealthy = v.IsHealthy;
        });
    }

    private async Task RunBoostAsync()
    {
        IsBoosting = true;
        BoostSteps.Clear();
        BoostSummary = "Scanning system...";

        var mode = _host.AppSettings.DefaultBoostMode;
        var plan = _host.Optimizer.BuildPlan(mode);

        var result = await Task.Run(() => _host.Optimizer.ApplyPlan(plan, mode, _host.AppSettings.CreateRestorePoints));

        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var step in result.Steps) BoostSteps.Add(step);
            BoostSummary = result.Summary;
            IsBoosting = false;
        });
    }

    public void Detach()
    {
        _host.Monitor.MetricsUpdated -= OnMetrics;
        _host.Monitor.VerdictUpdated -= OnVerdict;
    }
}
