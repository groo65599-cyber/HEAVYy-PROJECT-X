using System.Text.Json.Serialization;

namespace HPXPirate.Models;

public enum OptimizationMode { Balanced, Performance, Extreme, Custom }

public enum ResponseCurve { Linear, Smooth, FastStart, SlowStart, Precision, Custom }

public enum StabilizerLevel { Off, Low, Medium, High }

public enum EasyDragPreset { Light, Balanced, Stable, FastDrag, Precision, Custom }

public enum AimFeel { Light, Smooth, Stable, Responsive, Precision }

public sealed class MouseSettings
{
    public double HorizontalSensitivity { get; set; } = 1.0;
    public double VerticalSensitivity { get; set; } = 1.0;
    public double AdsSensitivity { get; set; } = 0.8;
    public double GeneralSensitivity { get; set; } = 1.0;
    public bool MouseAcceleration { get; set; } = false;
    public double MouseSmoothing { get; set; } = 0.0;
    public ResponseCurve ResponseCurve { get; set; } = ResponseCurve.Linear;
    public double DpiMultiplier { get; set; } = 1.0;
    public int PollingRateHz { get; set; } = 1000;
}

public sealed class DragAssistSettings
{
    public bool Enabled { get; set; } = false;
    public double DragSensitivity { get; set; } = 1.0;
    public double DragAcceleration { get; set; } = 0.0;
    public double DragSmoothing { get; set; } = 0.25;
    public ResponseCurve ResponseCurve { get; set; } = ResponseCurve.Smooth;
    public double StartZoneSensitivity { get; set; } = 1.0;
    public double VerticalMultiplier { get; set; } = 1.0;
    public double HorizontalMultiplier { get; set; } = 1.0;
    public double DragDistance { get; set; } = 100.0;
    public double DragSpeed { get; set; } = 1.0;
    public double[] CustomCurvePoints { get; set; } = new double[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
}

public sealed class EasyDragSettings
{
    public bool Enabled { get; set; } = false;
    public EasyDragPreset Preset { get; set; } = EasyDragPreset.Balanced;

    // Easy Drag Controls
    public double DragWeight { get; set; } = 1.0;
    public double DragSensitivity { get; set; } = 1.0;
    public double DragStability { get; set; } = 0.5;
    public double DragSmoothness { get; set; } = 0.35;
    public double DragAcceleration { get; set; } = 0.0;
    public double VerticalDragMultiplier { get; set; } = 1.0;
    public double HorizontalDragMultiplier { get; set; } = 1.0;
    public double InitialDragResponse { get; set; } = 1.0;
    public ResponseCurve DragResponseCurve { get; set; } = ResponseCurve.Smooth;
    public StabilizerLevel MicroMovementStabilizer { get; set; } = StabilizerLevel.Low;
    public double InputSmoothing { get; set; } = 0.3;
    public double InputDeadzone { get; set; } = 0.0;
    public double MaxDragSpeed { get; set; } = 2000.0;

    // Aim Feel (mouse feel / input consistency — NOT aim assist)
    public AimFeel AimFeel { get; set; } = AimFeel.Smooth;

    // Custom curve control points (normalised 0..1, y values)
    public double[] CustomCurvePoints { get; set; } = new double[] { 0.0, 0.2, 0.5, 0.8, 1.0 };
}

public sealed class CrosshairSettings
{
    public bool Enabled { get; set; } = false;
    public string Shape { get; set; } = "Cross"; // Cross, Dot, Circle, CrossDot
    public double Size { get; set; } = 12;
    public double Thickness { get; set; } = 2;
    public double Gap { get; set; } = 4;
    public double Opacity { get; set; } = 0.9;
    public string Color { get; set; } = "#FF2BD6";
    public bool CenterDot { get; set; } = true;
    public bool Outline { get; set; } = true;
    public string ToggleHotkey { get; set; } = "F8";
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;
}

public sealed class OverlaySettings
{
    public bool FpsCounter { get; set; } = true;
    public bool FrameTimeGraph { get; set; } = true;
    public bool PerformanceOverlay { get; set; } = false;
    public bool ScreenRegionMarker { get; set; } = false;
    public bool ColorAccessibilityFilter { get; set; } = false;
    public string AccessibilityMode { get; set; } = "None"; // None, Deuteranopia, Protanopia, Tritanopia
}

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Dark";
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool AutoLoadProfiles { get; set; } = true;
    public bool AutoOptimize { get; set; } = false;
    public OptimizationMode DefaultBoostMode { get; set; } = OptimizationMode.Balanced;
    public int MonitoringIntervalMs { get; set; } = 1000;
    public int IdleMonitoringIntervalMs { get; set; } = 3000;
    public bool ReduceMonitoringWhenIdle { get; set; } = true;
    public bool CreateRestorePoints { get; set; } = true;
    public bool BackupBeforeChanges { get; set; } = true;
    public bool DiagnosticMode { get; set; } = false;
    public bool SafeMode { get; set; } = false;

    public MouseSettings Mouse { get; set; } = new();
    public DragAssistSettings DragAssist { get; set; } = new();
    public EasyDragSettings EasyDrag { get; set; } = new();
    public CrosshairSettings Crosshair { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();
}
