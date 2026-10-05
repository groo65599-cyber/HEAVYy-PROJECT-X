using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>Collects real FPS samples during a benchmark window and computes statistics.</summary>
public sealed class FpsAnalyzer
{
    private readonly List<double> _fps = new();
    private readonly List<double> _frameTimes = new();
    private DateTime _start;
    private bool _running;

    public bool IsRunning => _running;
    public TimeSpan Elapsed => _running ? DateTime.Now - _start : TimeSpan.Zero;

    public void Start()
    {
        _fps.Clear();
        _frameTimes.Clear();
        _start = DateTime.Now;
        _running = true;
    }

    public void Add(double fps, double frameTimeMs)
    {
        if (!_running) return;
        if (fps > 0) _fps.Add(fps);
        if (frameTimeMs > 0) _frameTimes.Add(frameTimeMs);
    }

    public BenchmarkResult Stop(string label)
    {
        _running = false;
        return Build(label);
    }

    private BenchmarkResult Build(string label)
    {
        var result = new BenchmarkResult
        {
            Label = label,
            DurationSeconds = (DateTime.Now - _start).TotalSeconds,
            SampleCount = _fps.Count
        };

        if (_fps.Count == 0) return result;

        result.AverageFps = _fps.Average();
        result.MinFps = _fps.Min();
        result.MaxFps = _fps.Max();
        result.AverageFrameTimeMs = _frameTimes.Count > 0 ? _frameTimes.Average() : 0;

        // 1% / 0.1% low from frame times (slowest frames)
        var sorted = _frameTimes.OrderByDescending(x => x).ToArray();
        result.OnePercentLow = AvgFpsOfSlowest(sorted, 0.01);
        result.PointOnePercentLow = AvgFpsOfSlowest(sorted, 0.001);

        // Frame-time spikes: frames slower than 1.5x the average
        if (result.AverageFrameTimeMs > 0)
            result.FrameTimeSpikes = _frameTimes.Count(ft => ft > result.AverageFrameTimeMs * 1.5);

        return result;
    }

    private static double AvgFpsOfSlowest(double[] slowestFirst, double fraction)
    {
        if (slowestFirst.Length == 0) return 0;
        int take = Math.Max(1, (int)Math.Ceiling(slowestFirst.Length * fraction));
        var avgMs = slowestFirst.Take(take).Average();
        return avgMs > 0 ? 1000.0 / avgMs : 0;
    }
}
