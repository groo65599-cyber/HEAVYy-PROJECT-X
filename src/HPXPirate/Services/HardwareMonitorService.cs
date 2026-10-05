using System.Diagnostics;
using System.Management;
using HPXPirate.Interop;

namespace HPXPirate.Services;

public readonly record struct HardwareSample(
    double CpuUsage,
    double CpuTemperature,
    int CpuCores,
    double GpuUsage,
    double GpuTemperature,
    double GpuMemUsedMb,
    double GpuMemTotalMb,
    string GpuName,
    double RamUsedMb,
    double RamTotalMb,
    double DiskUsagePercent,
    double NetworkKbps);

/// <summary>
/// Samples real hardware counters. Uses PerformanceCounter + GlobalMemoryStatusEx and,
/// when available, LibreHardwareMonitor for temperatures and GPU load. Every source is
/// optional and fails gracefully so the app never crashes when a counter is missing.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly ILogger _log;

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _diskCounter;
    private PerformanceCounter? _netCounter;

    private LibreHardwareMonitor.Hardware.Computer? _computer;
    private bool _lhmReady;
    private string _gpuName = "";
    private double _gpuMemTotalMb;
    private int _cpuCores = Environment.ProcessorCount;

    public HardwareMonitorService(ILogger log) => _log = log;

    public void Initialize()
    {
        // --- CPU usage counter ---
        _cpuCounter = TryCreateCounter("Processor Information", "% Processor Time", "_Total")
                   ?? TryCreateCounter("Processor", "% Processor Time", "_Total");

        // --- Disk / Network (optional) ---
        _diskCounter = TryCreateCounter("PhysicalDisk", "% Disk Time", "_Total");
        _netCounter = TryCreateCounter("Network Interface", "Bytes Total/sec", null);

        // --- GPU name + VRAM via WMI ---
        TryReadGpuInfo();

        // --- LibreHardwareMonitor for temps / GPU load ---
        try
        {
            _computer = new LibreHardwareMonitor.Hardware.Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };
            _computer.Open();
            _lhmReady = true;
            _log.Info("LibreHardwareMonitor initialised.");
        }
        catch (Exception ex)
        {
            _lhmReady = false;
            _log.Warn("LibreHardwareMonitor unavailable (temps/GPU load limited): " + ex.Message);
        }
    }

    private PerformanceCounter? TryCreateCounter(string category, string counter, string? instance)
    {
        try
        {
            var c = instance is null
                ? new PerformanceCounter(category, counter, true)
                : new PerformanceCounter(category, counter, instance, true);
            c.NextValue(); // prime
            return c;
        }
        catch (Exception ex)
        {
            _log.Debug($"Counter '{category}\\{counter}' unavailable: {ex.Message}");
            return null;
        }
    }

    private void TryReadGpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject mo in searcher.Get())
            {
                var name = mo["Name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                _gpuName = name;
                var ram = mo["AdapterRAM"];
                if (ram != null)
                {
                    try { _gpuMemTotalMb = Convert.ToDouble(ram) / (1024.0 * 1024.0); } catch { }
                }
                break; // primary adapter
            }
        }
        catch (Exception ex)
        {
            _log.Debug("WMI GPU query failed: " + ex.Message);
        }
    }

    public HardwareSample Sample()
    {
        double cpu = SafeNext(_cpuCounter);
        if (cpu < 0) cpu = 0;

        // RAM via GlobalMemoryStatusEx
        double ramTotalMb = 0, ramUsedMb = 0;
        try
        {
            var ms = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
            if (NativeMethods.GlobalMemoryStatusEx(ref ms))
            {
                ramTotalMb = ms.ullTotalPhys / (1024.0 * 1024.0);
                ramUsedMb = (ms.ullTotalPhys - ms.ullAvailPhys) / (1024.0 * 1024.0);
            }
        }
        catch { }

        double gpuUsage = 0, gpuTemp = 0, gpuMemUsed = 0, gpuMemTotal = _gpuMemTotalMb, cpuTemp = 0;
        if (_lhmReady && _computer != null)
        {
            try
            {
                foreach (var hw in _computer.Hardware)
                {
                    hw.Update();
                    foreach (var sub in hw.SubHardware) sub.Update();

                    var hwType = hw.HardwareType.ToString();
                    foreach (var s in hw.Sensors)
                    {
                        if (s.Value is null) continue;
                        var v = s.Value.Value;
                        var st = s.SensorType.ToString();
                        var sn = s.Name;

                        if (hwType.Contains("Cpu"))
                        {
                            if (st == "Temperature" && (sn.Contains("Package") || sn.Contains("Total") || sn.Contains("Core")))
                                cpuTemp = Math.Max(cpuTemp, v);
                            if (st == "Load" && sn.Contains("Total")) cpu = v;
                        }
                        else if (hwType.Contains("Gpu"))
                        {
                            if (st == "Load" && (sn.Contains("Core") || sn.Contains("Total") || sn.Contains("D3D")))
                                gpuUsage = Math.Max(gpuUsage, v);
                            if (st == "Temperature" && (sn.Contains("Core") || sn.Contains("GPU")))
                                gpuTemp = Math.Max(gpuTemp, v);
                            if (st == "SmallData" && sn.Contains("Memory Used"))
                                gpuMemUsed = v;
                            if (st == "SmallData" && sn.Contains("Memory Total") && v > 0)
                                gpuMemTotal = v;
                            if (st == "Load" && sn.Contains("Memory"))
                                gpuMemUsed = gpuMemTotal * v / 100.0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Debug("LHM sample failed: " + ex.Message);
            }
        }

        double disk = SafeNext(_diskCounter);
        if (disk < 0) disk = 0;

        double netKbps = SampleNetwork();

        return new HardwareSample(
            CpuUsage: Math.Clamp(cpu, 0, 100),
            CpuTemperature: cpuTemp,
            CpuCores: _cpuCores,
            GpuUsage: Math.Clamp(gpuUsage, 0, 100),
            GpuTemperature: gpuTemp,
            GpuMemUsedMb: gpuMemUsed,
            GpuMemTotalMb: gpuMemTotal,
            GpuName: _gpuName,
            RamUsedMb: ramUsedMb,
            RamTotalMb: ramTotalMb,
            DiskUsagePercent: Math.Clamp(disk, 0, 100),
            NetworkKbps: netKbps);
    }

    private double SampleNetwork()
    {
        try
        {
            if (_netCounter is null) return 0;
            var bytesPerSec = _netCounter.NextValue();
            return bytesPerSec * 8.0 / 1000.0; // kbps
        }
        catch { return 0; }
    }

    private static double SafeNext(PerformanceCounter? c)
    {
        try { return c?.NextValue() ?? 0; }
        catch { return 0; }
    }

    public void Dispose()
    {
        try { _cpuCounter?.Dispose(); } catch { }
        try { _diskCounter?.Dispose(); } catch { }
        try { _netCounter?.Dispose(); } catch { }
        try { _computer?.Close(); } catch { }
    }
}
