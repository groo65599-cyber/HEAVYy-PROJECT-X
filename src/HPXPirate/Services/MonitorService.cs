using System.Timers;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// HPX MONITOR — orchestrates hardware + FPS sampling on a light background timer.
/// Sampling slows down automatically when no game is running to keep idle cost low.
/// </summary>
public sealed class MonitorService : IDisposable
{
    private readonly ILogger _log;
    private readonly HardwareMonitorService _hardware;
    private readonly FpsMonitorService _fps;
    private readonly BottleneckAnalyzer _analyzer;
    private readonly ProcessManagerService _processes;
    private readonly SettingsService _settings;
    private readonly FpsAnalyzer _benchmark = new();

    private System.Timers.Timer? _timer;
    private readonly object _gate = new();
    private DateTime _lastTick = DateTime.Now;
    private bool _gameRunning;

    public PerformanceMetrics Current { get; private set; } = new();
    public SystemVerdict Verdict { get; private set; } = new();
    public FpsAnalyzer Benchmark => _benchmark;

    public HistoryBuffer FpsHistory { get; } = new(TimeSpan.FromMinutes(5));
    public HistoryBuffer FrameTimeHistory { get; } = new(TimeSpan.FromMinutes(5));
    public HistoryBuffer CpuHistory { get; } = new(TimeSpan.FromMinutes(5));
    public HistoryBuffer GpuHistory { get; } = new(TimeSpan.FromMinutes(5));
    public HistoryBuffer RamHistory { get; } = new(TimeSpan.FromMinutes(5));

    public event Action<PerformanceMetrics>? MetricsUpdated;
    public event Action<SystemVerdict>? VerdictUpdated;

    public bool IsRunning => _timer?.Enabled == true;
    public bool GameRunning => _gameRunning;

    public MonitorService(ILogger log, HardwareMonitorService hardware, FpsMonitorService fps,
        BottleneckAnalyzer analyzer, ProcessManagerService processes, SettingsService settings)
    {
        _log = log;
        _hardware = hardware;
        _fps = fps;
        _analyzer = analyzer;
        _processes = processes;
        _settings = settings;
    }

    public void Start()
    {
        if (_timer != null) { _timer.Enabled = true; return; }
        _timer = new System.Timers.Timer(_settings.Current.MonitoringIntervalMs) { AutoReset = true };
        _timer.Elapsed += OnTick;
        _timer.Enabled = true;
        _fps.Start();
        _log.Info("Monitor started.");
    }

    public void Stop()
    {
        if (_timer != null) _timer.Enabled = false;
        _log.Info("Monitor stopped.");
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        lock (_gate)
        {
            try
            {
                var now = DateTime.Now;
                double interval = (now - _lastTick).TotalSeconds;
                if (interval <= 0) interval = 1;
                _lastTick = now;

                // FPS from ETW present events
                _fps.Tick(interval);

                var hw = _hardware.Sample();
                var game = _processes.DetectRunningGame();
                _gameRunning = game != null;

                var m = new PerformanceMetrics
                {
                    Timestamp = now,
                    Fps = _fps.CurrentFps,
                    FrameTimeMs = _fps.CurrentFrameTimeMs,
                    AverageFps = _fps.AverageFps,
                    OnePercentLow = _fps.OnePercentLow,
                    PointOnePercentLow = _fps.PointOnePercentLow,
                    MinFps = _fps.MinFps,
                    MaxFps = _fps.MaxFps,
                    CpuUsage = hw.CpuUsage,
                    CpuTemperature = hw.CpuTemperature,
                    CpuCoreCount = hw.CpuCores,
                    GpuUsage = hw.GpuUsage,
                    GpuTemperature = hw.GpuTemperature,
                    GpuMemoryUsedMb = hw.GpuMemUsedMb,
                    GpuMemoryTotalMb = hw.GpuMemTotalMb,
                    GpuName = hw.GpuName,
                    RamUsedMb = hw.RamUsedMb,
                    RamTotalMb = hw.RamTotalMb,
                    DiskUsagePercent = hw.DiskUsagePercent,
                    NetworkKbps = hw.NetworkKbps
                };
                Current = m;

                FpsHistory.Add(now, m.Fps);
                FrameTimeHistory.Add(now, m.FrameTimeMs);
                CpuHistory.Add(now, m.CpuUsage);
                GpuHistory.Add(now, m.GpuUsage);
                RamHistory.Add(now, m.RamUsagePercent);

                if (m.FrameTimeMs > 0) _analyzer.Feed(m.FrameTimeMs);
                if (_benchmark.IsRunning) _benchmark.Add(m.Fps, m.FrameTimeMs);

                var verdict = _analyzer.Analyze(m);
                Verdict = verdict;

                MetricsUpdated?.Invoke(m);
                VerdictUpdated?.Invoke(verdict);

                AdaptInterval();
            }
            catch (Exception ex)
            {
                _log.Error("Monitor tick failed.", ex);
            }
        }
    }

    private void AdaptInterval()
    {
        if (_timer is null) return;
        int desired = _gameRunning || !_settings.Current.ReduceMonitoringWhenIdle
            ? _settings.Current.MonitoringIntervalMs
            : _settings.Current.IdleMonitoringIntervalMs;
        if ((int)_timer.Interval != desired)
            _timer.Interval = desired;
    }

    public void ApplyIntervalSettings()
    {
        if (_timer != null)
            _timer.Interval = _settings.Current.MonitoringIntervalMs;
    }

    public void Dispose()
    {
        try { _timer?.Dispose(); } catch { }
        _fps.Dispose();
        _hardware.Dispose();
    }
}
