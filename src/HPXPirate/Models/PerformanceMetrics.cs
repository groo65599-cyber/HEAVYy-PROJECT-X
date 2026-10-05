namespace HPXPirate.Models;

/// <summary>A single instantaneous snapshot of system + game performance.</summary>
public sealed class PerformanceMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // FPS
    public double Fps { get; set; }
    public double AverageFps { get; set; }
    public double OnePercentLow { get; set; }
    public double PointOnePercentLow { get; set; }
    public double FrameTimeMs { get; set; }
    public double MinFps { get; set; }
    public double MaxFps { get; set; }

    // CPU
    public double CpuUsage { get; set; }
    public double CpuTemperature { get; set; }
    public int CpuCoreCount { get; set; }

    // GPU
    public double GpuUsage { get; set; }
    public double GpuTemperature { get; set; }
    public double GpuMemoryUsedMb { get; set; }
    public double GpuMemoryTotalMb { get; set; }
    public string GpuName { get; set; } = "";

    // Memory
    public double RamUsedMb { get; set; }
    public double RamTotalMb { get; set; }
    public double RamUsagePercent => RamTotalMb > 0 ? RamUsedMb / RamTotalMb * 100.0 : 0;

    // Storage / Network
    public double DiskUsagePercent { get; set; }
    public double NetworkKbps { get; set; }

    // Derived
    public double VramUsagePercent => GpuMemoryTotalMb > 0 ? GpuMemoryUsedMb / GpuMemoryTotalMb * 100.0 : 0;
}

/// <summary>Overall health verdict produced by the bottleneck analyzer.</summary>
public enum SystemStatusKind
{
    Stable,
    CpuBottleneck,
    GpuBottleneck,
    MemoryPressure,
    BackgroundPressure,
    FrameTimeInstability
}

public sealed class SystemVerdict
{
    public SystemStatusKind Kind { get; set; } = SystemStatusKind.Stable;
    public string Title { get; set; } = "Performance Stable";
    public string Detail { get; set; } = "";
    public string Recommendation { get; set; } = "";
    public bool IsHealthy => Kind == SystemStatusKind.Stable;
}

/// <summary>Aggregated FPS benchmark result (BEFORE / AFTER comparison).</summary>
public sealed class BenchmarkResult
{
    public string Label { get; set; } = "";
    public double AverageFps { get; set; }
    public double MinFps { get; set; }
    public double MaxFps { get; set; }
    public double OnePercentLow { get; set; }
    public double PointOnePercentLow { get; set; }
    public double AverageFrameTimeMs { get; set; }
    public int FrameTimeSpikes { get; set; }
    public double DurationSeconds { get; set; }
    public int SampleCount { get; set; }
}
