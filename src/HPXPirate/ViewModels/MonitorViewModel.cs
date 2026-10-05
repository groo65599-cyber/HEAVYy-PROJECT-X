using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class MonitorViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public MonitorViewModel(AppHost host)
    {
        _host = host;
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        StartBenchmarkCommand = new RelayCommand(StartBenchmark, () => !BenchmarkRunning);
        StopBenchmarkCommand = new RelayCommand(StopBenchmark, () => BenchmarkRunning);
        _host.Monitor.MetricsUpdated += OnMetrics;
        RefreshProcesses();
    }

    private TimeSpan _window = TimeSpan.FromSeconds(60);
    public string[] Windows { get; } = { "10 seconds", "30 seconds", "1 minute", "5 minutes" };
    private string _windowLabel = "1 minute";
    public string WindowLabel
    {
        get => _windowLabel;
        set
        {
            if (SetProperty(ref _windowLabel, value))
            {
                _window = value switch
                {
                    "10 seconds" => TimeSpan.FromSeconds(10),
                    "30 seconds" => TimeSpan.FromSeconds(30),
                    "5 minutes" => TimeSpan.FromMinutes(5),
                    _ => TimeSpan.FromMinutes(1)
                };
            }
        }
    }

    private List<double> _fps = new(), _cpu = new(), _gpu = new(), _ram = new(), _frameTime = new();
    public List<double> FpsSeries { get => _fps; private set => SetProperty(ref _fps, value); }
    public List<double> CpuSeries { get => _cpu; private set => SetProperty(ref _cpu, value); }
    public List<double> GpuSeries { get => _gpu; private set => SetProperty(ref _gpu, value); }
    public List<double> RamSeries { get => _ram; private set => SetProperty(ref _ram, value); }
    public List<double> FrameTimeSeries { get => _frameTime; private set => SetProperty(ref _frameTime, value); }

    private string _cpuText = "--", _gpuText = "--", _ramText = "--", _fpsText = "--", _ftText = "--";
    public string CpuText { get => _cpuText; private set => SetProperty(ref _cpuText, value); }
    public string GpuText { get => _gpuText; private set => SetProperty(ref _gpuText, value); }
    public string RamText { get => _ramText; private set => SetProperty(ref _ramText, value); }
    public string FpsText { get => _fpsText; private set => SetProperty(ref _fpsText, value); }
    public string FrameTimeText { get => _ftText; private set => SetProperty(ref _ftText, value); }

    public ObservableCollection<ProcessInfoModel> Processes { get; } = new();

    private bool _benchmarkRunning;
    public bool BenchmarkRunning { get => _benchmarkRunning; private set { SetProperty(ref _benchmarkRunning, value); ((RelayCommand)StartBenchmarkCommand).RaiseCanExecuteChanged(); ((RelayCommand)StopBenchmarkCommand).RaiseCanExecuteChanged(); } }

    private BenchmarkResult? _before, _after;
    public BenchmarkResult? BeforeResult { get => _before; private set => SetProperty(ref _before, value); }
    public BenchmarkResult? AfterResult { get => _after; private set => SetProperty(ref _after, value); }

    public ICommand RefreshProcessesCommand { get; }
    public ICommand StartBenchmarkCommand { get; }
    public ICommand StopBenchmarkCommand { get; }

    private void OnMetrics(PerformanceMetrics m)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CpuText = m.CpuUsage.ToString("0") + " %";
            GpuText = m.GpuUsage.ToString("0") + " %";
            RamText = $"{m.RamUsedMb / 1024:0.0} / {m.RamTotalMb / 1024:0.0} GB";
            FpsText = m.Fps > 0 ? m.Fps.ToString("0") : "--";
            FrameTimeText = m.FrameTimeMs > 0 ? m.FrameTimeMs.ToString("0.0") + " ms" : "--";

            FpsSeries = _host.Monitor.FpsHistory.Snapshot(_window).Select(x => x.v).ToList();
            CpuSeries = _host.Monitor.CpuHistory.Snapshot(_window).Select(x => x.v).ToList();
            GpuSeries = _host.Monitor.GpuHistory.Snapshot(_window).Select(x => x.v).ToList();
            RamSeries = _host.Monitor.RamHistory.Snapshot(_window).Select(x => x.v).ToList();
            FrameTimeSeries = _host.Monitor.FrameTimeHistory.Snapshot(_window).Select(x => x.v).ToList();
        });
    }

    private void RefreshProcesses()
    {
        var list = _host.Processes.GetTopProcesses(25);
        Processes.Clear();
        foreach (var p in list) Processes.Add(p);
    }

    private void StartBenchmark()
    {
        _host.Monitor.Benchmark.Start();
        BenchmarkRunning = true;
    }

    private void StopBenchmark()
    {
        var result = _host.Monitor.Benchmark.Stop("Benchmark");
        if (BeforeResult is null) BeforeResult = result;
        else AfterResult = result;
        BenchmarkRunning = false;
    }
}
