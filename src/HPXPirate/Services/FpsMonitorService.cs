using System.Collections.Concurrent;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// Captures real game FPS without touching game memory or injecting DLLs.
/// Uses an ETW real-time session on the Microsoft-Windows-DxgKrnl provider and
/// counts "Present" events per process (the same principle used by PresentMon).
/// If ETW is unavailable the source reports "unavailable" rather than fake values.
/// </summary>
public sealed class FpsMonitorService : IDisposable
{
    private static readonly Guid DxgKrnlProvider = new("802ec45a-1e99-4b83-9920-87c98277ba9d");

    private readonly ILogger _log;
    private TraceEventSession? _session;
    private Thread? _thread;
    private readonly ConcurrentDictionary<int, long> _counts = new();

    private readonly Queue<double> _frameTimes = new(); // ms
    private const int HistoryCap = 1800; // ~30s @60fps .. enough for 0.1% low

    private double _lastFps;
    private double _lastFrameTimeMs;
    private int _lastPid = -1;

    public bool IsAvailable { get; private set; }
    public int TargetPid { get; private set; } = -1;
    public string SourceName { get; private set; } = "Unavailable";

    public double CurrentFps => _lastFps;
    public double CurrentFrameTimeMs => _lastFrameTimeMs;

    public FpsMonitorService(ILogger log) => _log = log;

    public bool Start()
    {
        if (IsAvailable) return true;
        try
        {
            _session = new TraceEventSession($"HPXPirate-FPS-{Environment.ProcessId}")
            {
                StopOnDispose = true
            };
            _session.EnableProvider(DxgKrnlProvider, TraceEventLevel.Informational);
            _session.Source.AllEvents += OnEvent;

            _thread = new Thread(() =>
            {
                try { _session.Source.Process(); }
                catch (Exception ex) { _log.Warn("ETW loop ended: " + ex.Message); }
            })
            { IsBackground = true, Name = "HPX-ETW", Priority = ThreadPriority.BelowNormal };
            _thread.Start();

            IsAvailable = true;
            SourceName = "ETW (DxgKrnl Present)";
            _log.Info("ETW FPS capture started.");
            return true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            SourceName = "Unavailable";
            _log.Warn("ETW FPS capture unavailable (run as administrator for game FPS): " + ex.Message);
            return false;
        }
    }

    private void OnEvent(TraceEvent data)
    {
        try
        {
            var name = data.EventName;
            if (name is not null && name.Contains("Present", StringComparison.OrdinalIgnoreCase))
                _counts.AddOrUpdate(data.ProcessID, 1, (_, v) => v + 1);
        }
        catch { /* never let the ETW callback throw */ }
    }

    /// <summary>Called once per second by the monitor loop.</summary>
    public void Tick(double intervalSeconds)
    {
        int bestPid = -1;
        long best = 0;
        foreach (var kv in _counts)
        {
            if (kv.Value > best) { best = kv.Value; bestPid = kv.Key; }
        }
        _counts.Clear();

        if (bestPid > 0 && best > 0)
        {
            _lastPid = bestPid;
            TargetPid = bestPid;
            _lastFps = intervalSeconds > 0 ? best / intervalSeconds : best;
            _lastFrameTimeMs = _lastFps > 0 ? 1000.0 / _lastFps : 0;
            PushFrameTime(_lastFrameTimeMs);
        }
        else
        {
            // No presents seen (no game running) -> report 0, never invent a number.
            _lastFps = 0;
            _lastFrameTimeMs = 0;
        }
    }

    private void PushFrameTime(double ms)
    {
        if (ms <= 0) return;
        _frameTimes.Enqueue(ms);
        while (_frameTimes.Count > HistoryCap) _frameTimes.Dequeue();
    }

    public double AverageFps
    {
        get
        {
            if (_frameTimes.Count == 0) return 0;
            var avgMs = _frameTimes.Average();
            return avgMs > 0 ? 1000.0 / avgMs : 0;
        }
    }

    public double MinFps
    {
        get
        {
            if (_frameTimes.Count == 0) return 0;
            var maxMs = _frameTimes.Max();
            return maxMs > 0 ? 1000.0 / maxMs : 0;
        }
    }

    public double MaxFps
    {
        get
        {
            if (_frameTimes.Count == 0) return 0;
            var minMs = _frameTimes.Min();
            return minMs > 0 ? 1000.0 / minMs : 0;
        }
    }

    public double OnePercentLow => PercentileLowFps(0.01);
    public double PointOnePercentLow => PercentileLowFps(0.001);

    private double PercentileLowFps(double fraction)
    {
        if (_frameTimes.Count < 10) return 0;
        // 1% low = average of the slowest `fraction` of frames.
        var sorted = _frameTimes.OrderByDescending(x => x).ToArray(); // slowest first
        int take = Math.Max(1, (int)Math.Ceiling(sorted.Length * fraction));
        var slowest = sorted.Take(take);
        var avgMs = slowest.Average();
        return avgMs > 0 ? 1000.0 / avgMs : 0;
    }

    public void ResetHistory()
    {
        _frameTimes.Clear();
        _lastFps = 0;
        _lastFrameTimeMs = 0;
    }

    public void Dispose()
    {
        try { _session?.Dispose(); } catch { }
        try { _thread?.Join(500); } catch { }
        _session = null;
        IsAvailable = false;
    }
}
