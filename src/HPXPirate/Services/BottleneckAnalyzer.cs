using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// Heuristic bottleneck detector based on live utilisation data. It never claims a
/// precise FPS gain; it only interprets the measured numbers and gives advice.
/// </summary>
public sealed class BottleneckAnalyzer
{
    private readonly Queue<double> _frameTimes = new();
    private const int FrameCap = 120;

    public void Feed(double frameTimeMs)
    {
        if (frameTimeMs <= 0) return;
        _frameTimes.Enqueue(frameTimeMs);
        while (_frameTimes.Count > FrameCap) _frameTimes.Dequeue();
    }

    public SystemVerdict Analyze(PerformanceMetrics m)
    {
        // Frame-time instability (coefficient of variation of recent frame times)
        double instability = 0;
        if (_frameTimes.Count > 20)
        {
            var arr = _frameTimes.ToArray();
            var mean = arr.Average();
            if (mean > 0)
            {
                var variance = arr.Select(x => (x - mean) * (x - mean)).Average();
                instability = Math.Sqrt(variance) / mean; // CV
            }
        }

        // Memory pressure
        if (m.RamUsagePercent >= 90)
            return new SystemVerdict
            {
                Kind = SystemStatusKind.MemoryPressure,
                Title = "MEMORY PRESSURE DETECTED",
                Detail = $"RAM is {m.RamUsagePercent:0}% full ({m.RamUsedMb / 1024:0.0} / {m.RamTotalMb / 1024:0.0} GB).",
                Recommendation = "Close unused background apps or browser tabs. HPX PIRATE can free safe background load via BOOST NOW."
            };

        // CPU bottleneck: CPU pinned while GPU has headroom
        if (m.CpuUsage >= 90 && m.GpuUsage < 75 && m.GpuUsage > 0)
            return new SystemVerdict
            {
                Kind = SystemStatusKind.CpuBottleneck,
                Title = "CPU BOTTLENECK DETECTED",
                Detail = $"CPU {m.CpuUsage:0}% vs GPU {m.GpuUsage:0}%.",
                Recommendation = "Reduce background CPU load, raise the game process priority, and close CPU-heavy apps."
            };

        // GPU bottleneck: GPU pinned near 100%
        if (m.GpuUsage >= 95)
            return new SystemVerdict
            {
                Kind = SystemStatusKind.GpuBottleneck,
                Title = "GPU BOTTLENECK DETECTED",
                Detail = $"GPU is at {m.GpuUsage:0}% — the GPU is the limiting factor.",
                Recommendation = "Lower in-game graphics settings, resolution or render scale. This is expected at high settings."
            };

        // Frame-time instability
        if (instability > 0.35 && m.Fps > 0)
            return new SystemVerdict
            {
                Kind = SystemStatusKind.FrameTimeInstability,
                Title = "FRAME-TIME INSTABILITY DETECTED",
                Detail = $"Frame-time variation is {instability * 100:0}% (jittery).",
                Recommendation = "Enable a performance mode, reduce background load, and check for thermal throttling."
            };

        // Background pressure
        if (m.CpuUsage > 60 && m.GpuUsage > 0 && m.GpuUsage < 60)
            return new SystemVerdict
            {
                Kind = SystemStatusKind.BackgroundPressure,
                Title = "BACKGROUND PROCESS PRESSURE",
                Detail = $"CPU {m.CpuUsage:0}% with GPU headroom suggests background activity.",
                Recommendation = "Use BOOST NOW to trim safe background processes."
            };

        return new SystemVerdict
        {
            Kind = SystemStatusKind.Stable,
            Title = "Performance Stable",
            Detail = m.Fps > 0
                ? $"FPS {m.Fps:0} · Frame time {m.FrameTimeMs:0.0} ms · CPU {m.CpuUsage:0}% · GPU {m.GpuUsage:0}%"
                : "No active game detected. Monitoring system only.",
            Recommendation = "No action needed."
        };
    }

    public void Reset() => _frameTimes.Clear();
}
