using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.LogicTests;

/// <summary>
/// Minimal, dependency-free test runner for the platform-independent HPX PIRATE logic.
/// Run with:  dotnet run --project tests/HPXPirate.LogicTests
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("  HPX PIRATE — headless logic test suite");
        Console.WriteLine("=================================================");
        Console.WriteLine($"Data root: {AppPaths.Root}");
        Console.WriteLine();

        TestJsonRoundTrip();
        TestSettingsPersistence();
        TestProfileSaveLoad();
        TestProfileExportImport();
        TestProfileBackupRestore();
        TestDragEngineDeadzone();
        TestDragEngineMetrics();
        TestDragPresets();
        TestDragAssistEngine();
        TestFpsAnalyzer();
        TestBottleneckAnalyzer();
        TestHistoryBuffer();

        Console.WriteLine();
        Console.WriteLine("=================================================");
        Console.WriteLine($"  RESULT: {_passed} passed, {_failed} failed");
        Console.WriteLine("=================================================");
        return _failed == 0 ? 0 : 1;
    }

    // ---------------------------------------------------------------- helpers

    private static void Check(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  [PASS] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  [FAIL] {name}");
        }
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"-- {title} --");
    }

    private static ILogger NewLogger() => new LoggingService(AppPaths.Logs);

    // ---------------------------------------------------------------- tests

    private static void TestJsonRoundTrip()
    {
        Section("JSON round-trip (camelCase + enum as string)");
        var s = new AppSettings
        {
            Language = "id",
            Theme = "Neon",
            DefaultBoostMode = OptimizationMode.Extreme,
            MonitoringIntervalMs = 750
        };
        s.EasyDrag.Preset = EasyDragPreset.Precision;
        s.EasyDrag.MicroMovementStabilizer = StabilizerLevel.High;

        var json = JsonHelper.Serialize(s);
        Check("serialises without throwing", !string.IsNullOrWhiteSpace(json));
        Check("uses camelCase keys", json.Contains("\"language\""));
        Check("enums serialise as strings", json.Contains("\"Extreme\""));

        var back = JsonHelper.Deserialize<AppSettings>(json);
        Check("deserialises back", back is not null);
        Check("string value preserved", back!.Language == "id");
        Check("enum value preserved", back.DefaultBoostMode == OptimizationMode.Extreme);
        Check("nested enum preserved", back.EasyDrag.Preset == EasyDragPreset.Precision);
        Check("int value preserved", back.MonitoringIntervalMs == 750);
    }

    private static void TestSettingsPersistence()
    {
        Section("Settings persistence (save -> reload)");

        // Start clean
        if (File.Exists(AppPaths.SettingsFile)) File.Delete(AppPaths.SettingsFile);

        var svc1 = new SettingsService(NewLogger());
        svc1.Current.Theme = "Midnight";
        svc1.Current.MonitoringIntervalMs = 1234;
        svc1.Current.Crosshair.Color = "#2BE7FF";
        svc1.Save();

        Check("settings file written", File.Exists(AppPaths.SettingsFile));

        var svc2 = new SettingsService(NewLogger());
        Check("theme persisted", svc2.Current.Theme == "Midnight");
        Check("interval persisted", svc2.Current.MonitoringIntervalMs == 1234);
        Check("nested crosshair persisted", svc2.Current.Crosshair.Color == "#2BE7FF");

        svc2.ResetToDefaults();
        var svc3 = new SettingsService(NewLogger());
        Check("reset restores defaults", svc3.Current.Theme == "Dark");
    }

    private static void TestProfileSaveLoad()
    {
        Section("Game profile save / load");

        // Clean profile dir
        if (Directory.Exists(AppPaths.Profiles))
            foreach (var f in Directory.GetFiles(AppPaths.Profiles, "*.json")) File.Delete(f);

        var pm1 = new ProfileManager(NewLogger());
        pm1.LoadAll();
        Check("seeds default profiles when empty", pm1.Profiles.Count >= 4);

        var custom = GameProfile.CreateDefault("Test Game 9000");
        custom.ExecutablePath = @"C:\Games\testgame\TestGame.exe";
        custom.FpsTarget = 144;
        custom.KeyboardMapping.Add(new ControlMapping { Input = "R", Action = "Reload", X = 0.6, Y = 0.4 });
        pm1.Save(custom);

        var pm2 = new ProfileManager(NewLogger());
        pm2.LoadAll();
        var loaded = pm2.Profiles.FirstOrDefault(p => p.Game == "Test Game 9000");
        Check("custom profile reloaded", loaded is not null);
        Check("fps target preserved", loaded?.FpsTarget == 144);
        Check("mapping list preserved", loaded?.KeyboardMapping.Any(m => m.Input == "R") == true);

        var found = pm2.FindByExecutable(@"C:\Games\testgame\TestGame.exe");
        Check("FindByExecutable matches path", found?.Game == "Test Game 9000");

        pm2.Delete(loaded!);
        var pm3 = new ProfileManager(NewLogger());
        pm3.LoadAll();
        Check("deleted profile removed", pm3.Profiles.All(p => p.Game != "Test Game 9000"));
    }

    private static void TestProfileExportImport()
    {
        Section("Game profile export / import");

        var pm = new ProfileManager(NewLogger());
        pm.LoadAll();
        var src = GameProfile.CreateDefault("Export Me");
        src.FpsTarget = 240;
        pm.Save(src);

        var tmp = Path.Combine(Path.GetTempPath(), "hpx_export_test.json");
        pm.Export(src, tmp);
        Check("export file created", File.Exists(tmp));

        var imported = pm.Import(tmp);
        Check("imported game name kept", imported.Game == "Export Me");
        Check("imported gets new id", imported.Id != src.Id);
        Check("imported fps target kept", imported.FpsTarget == 240);

        File.Delete(tmp);
    }

    private static void TestProfileBackupRestore()
    {
        Section("Game profile backup / restore");

        var pm = new ProfileManager(NewLogger());
        pm.LoadAll();
        var p = GameProfile.CreateDefault("Backup Me");
        p.FpsTarget = 60;
        pm.Save(p);

        var backup = pm.Backup(p);
        Check("backup file created", File.Exists(backup));

        var restored = pm.Restore(backup);
        Check("restored game name", restored.Game == "Backup Me");
        Check("restored fps target", restored.FpsTarget == 60);
    }

    private static void TestDragEngineDeadzone()
    {
        Section("Easy Drag — deadzone suppresses micro jitter");

        var engine = new EasyDragEngine();
        var s = new EasyDragSettings { InputDeadzone = 5.0, DragSensitivity = 1, DragWeight = 1 };

        var small = engine.Process(1, 1, s); // magnitude ~1.41 < 5
        Check("sub-deadzone input produces zero output", small.OutMagnitude == 0);

        var big = engine.Process(20, 0, s); // magnitude 20 > 5
        Check("above-deadzone input produces output", big.OutMagnitude > 0);
    }

    private static void TestDragEngineMetrics()
    {
        Section("Easy Drag — measured metrics stay in range");

        var engine = new EasyDragEngine();
        var s = new EasyDragSettings
        {
            DragSensitivity = 1.0, DragWeight = 1.0, DragSmoothness = 0.3,
            InputSmoothing = 0.2, MaxDragSpeed = 2000, InputDeadzone = 0
        };

        bool inRange = true;
        bool finite = true;
        for (int i = 0; i < 200; i++)
        {
            double dx = Math.Sin(i / 7.0) * 25;
            double dy = Math.Cos(i / 9.0) * 25;
            var sample = engine.Process(dx, dy, s, 0.016);
            if (sample.StabilityScore is < 0 or > 100) inRange = false;
            if (sample.SmoothnessScore is < 0 or > 100) inRange = false;
            if (double.IsNaN(sample.OutMagnitude) || double.IsInfinity(sample.OutMagnitude)) finite = false;
        }
        Check("stability/smoothness within 0..100", inRange);
        Check("all outputs finite", finite);
        Check("distance accumulated", engine.Processor.TotalDistance > 0);

        engine.Reset();
        Check("reset clears distance", engine.Processor.TotalDistance == 0);
        Check("reset restores scores to 100", engine.Processor.StabilityScore == 100);
    }

    private static void TestDragPresets()
    {
        Section("Easy Drag — presets & aim feel mutate settings");

        var s = new EasyDragSettings();
        EasyDragEngine.ApplyPreset(s, EasyDragPreset.Precision);
        Check("preset name recorded", s.Preset == EasyDragPreset.Precision);
        Check("precision uses Precision curve", s.DragResponseCurve == ResponseCurve.Precision);
        Check("precision raises stabilizer", s.MicroMovementStabilizer == StabilizerLevel.High);

        EasyDragEngine.ApplyAimFeel(s, AimFeel.Light);
        Check("aim feel name recorded", s.AimFeel == AimFeel.Light);
        Check("light feel uses FastStart curve", s.DragResponseCurve == ResponseCurve.FastStart);

        // Curves must be monotonic-ish and bounded
        bool bounded = true;
        foreach (ResponseCurve c in Enum.GetValues<ResponseCurve>())
            for (int i = 0; i <= 10; i++)
            {
                var v = Curves.Evaluate(c, i / 10.0, new double[] { 0, 0.5, 1 });
                if (v is < 0 or > 1.0001) bounded = false;
            }
        Check("all response curves bounded 0..1", bounded);
    }

    private static void TestDragAssistEngine()
    {
        Section("Drag Assist engine");

        var engine = new DragAssistEngine();
        var s = new DragAssistSettings { DragSensitivity = 1.0, DragSmoothing = 0.25, DragSpeed = 1.0 };
        var sample = engine.Process(15, -10, s, 0.016);
        Check("drag assist produces output", sample.OutMagnitude > 0);
        Check("drag assist score in range", sample.SmoothnessScore is >= 0 and <= 100);
    }

    private static void TestFpsAnalyzer()
    {
        Section("FPS analyzer benchmark stats");

        var fx = new FpsAnalyzer();
        fx.Start();
        Check("analyzer running", fx.IsRunning);

        // 100 frames: mostly 120fps with a few slow frames
        for (int i = 0; i < 100; i++)
        {
            double ft = i % 25 == 0 ? 16.6 : 8.0; // occasional slow frame
            fx.Add(1000.0 / ft, ft);
        }
        var r = fx.Stop("unit");
        Check("analyzer stopped", !fx.IsRunning);
        Check("sample count captured", r.SampleCount == 100);
        Check("average fps computed", r.AverageFps > 0);
        Check("min <= average <= max", r.MinFps <= r.AverageFps && r.AverageFps <= r.MaxFps);
        Check("1% low computed", r.OnePercentLow > 0);
        Check("0.1% low computed", r.PointOnePercentLow > 0);
        Check("1% low <= average", r.OnePercentLow <= r.AverageFps + 0.001);
    }

    private static void TestBottleneckAnalyzer()
    {
        Section("Bottleneck analyzer verdicts");

        var a = new BottleneckAnalyzer();

        var mem = new PerformanceMetrics { RamUsedMb = 15500, RamTotalMb = 16000, CpuUsage = 40, GpuUsage = 40 };
        Check("detects memory pressure", a.Analyze(mem).Kind == SystemStatusKind.MemoryPressure);

        var gpu = new PerformanceMetrics { CpuUsage = 50, GpuUsage = 99, RamUsedMb = 4000, RamTotalMb = 16000 };
        Check("detects GPU bottleneck", a.Analyze(gpu).Kind == SystemStatusKind.GpuBottleneck);

        var cpu = new PerformanceMetrics { CpuUsage = 95, GpuUsage = 50, RamUsedMb = 4000, RamTotalMb = 16000 };
        Check("detects CPU bottleneck", a.Analyze(cpu).Kind == SystemStatusKind.CpuBottleneck);

        var stable = new PerformanceMetrics { CpuUsage = 40, GpuUsage = 45, Fps = 120, FrameTimeMs = 8.3, RamUsedMb = 4000, RamTotalMb = 16000 };
        Check("reports stable when healthy", a.Analyze(stable).Kind == SystemStatusKind.Stable);
    }

    private static void TestHistoryBuffer()
    {
        Section("History buffer windowing");

        var hb = new HistoryBuffer(TimeSpan.FromMinutes(10));
        var now = DateTime.Now;
        for (int i = 0; i < 100; i++)
            hb.Add(now.AddSeconds(-i), i);

        var last10 = hb.Snapshot(TimeSpan.FromSeconds(10));
        Check("snapshot respects window", last10.Count <= 11);
        Check("snapshot non-empty", last10.Count > 0);
        Check("last value correct", hb.Last == 99);
    }
}
