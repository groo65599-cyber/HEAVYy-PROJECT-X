using HPXPirate.Models;

namespace HPXPirate.Services;

/// <summary>
/// Lightweight composition root. Creates and owns every service the app uses.
/// </summary>
public sealed class AppHost : IDisposable
{
    public LoggingService Logger { get; private set; } = null!;
    public SettingsService Settings { get; private set; } = null!;
    public ProfileManager Profiles { get; private set; } = null!;
    public HardwareMonitorService Hardware { get; private set; } = null!;
    public FpsMonitorService Fps { get; private set; } = null!;
    public BottleneckAnalyzer Analyzer { get; private set; } = null!;
    public ProcessManagerService Processes { get; private set; } = null!;
    public RestorePointService Restore { get; private set; } = null!;
    public SystemOptimizerService Optimizer { get; private set; } = null!;
    public StartupManagerService Startup { get; private set; } = null!;
    public InputMapperService Input { get; private set; } = null!;
    public CrosshairOverlayService Crosshair { get; private set; } = null!;
    public EasyDragEngine EasyDrag { get; private set; } = null!;
    public DragAssistEngine DragAssist { get; private set; } = null!;
    public MonitorService Monitor { get; private set; } = null!;

    public AppSettings AppSettings => Settings.Current;

    private bool _initialized;

    public void Initialize()
    {
        if (_initialized) return;

        AppPaths.EnsureCreated();
        Logger = new LoggingService(AppPaths.Logs);

        Settings = new SettingsService(Logger);
        Profiles = new ProfileManager(Logger);
        Profiles.LoadAll();

        Hardware = new HardwareMonitorService(Logger);
        Hardware.Initialize();

        Fps = new FpsMonitorService(Logger);
        Analyzer = new BottleneckAnalyzer();
        Processes = new ProcessManagerService(Logger);
        Restore = new RestorePointService(Logger);
        Optimizer = new SystemOptimizerService(Logger, Restore, Processes);
        Optimizer.LoadChanges();
        Startup = new StartupManagerService(Logger);
        Input = new InputMapperService(Logger);
        Crosshair = new CrosshairOverlayService(Logger);
        EasyDrag = new EasyDragEngine();
        DragAssist = new DragAssistEngine();

        Monitor = new MonitorService(Logger, Hardware, Fps, Analyzer, Processes, Settings);

        _initialized = true;
        Logger.Info("AppHost initialised.");
    }

    public void Shutdown()
    {
        Logger?.Info("HPX PIRATE shutting down.");
        try { Monitor?.Dispose(); } catch { }
        try { Crosshair?.Dispose(); } catch { }
        try { Input?.Dispose(); } catch { }
        Settings?.Save();
    }

    public void Dispose() => Shutdown();
}
