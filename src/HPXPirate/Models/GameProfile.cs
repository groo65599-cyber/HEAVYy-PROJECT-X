namespace HPXPirate.Models;

public enum MappingInputType { Keyboard, MouseButton, MouseWheel }

/// <summary>A single keyboard/mouse -> screen control point mapping.</summary>
public sealed class ControlMapping
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public MappingInputType InputType { get; set; } = MappingInputType.Keyboard;

    /// <summary>Key name ("W", "SPACE") or mouse button ("MouseLeft", "MouseRight", "MouseMiddle", "WheelUp").</summary>
    public string Input { get; set; } = "W";

    /// <summary>Human readable action, e.g. "Move Forward".</summary>
    public string Action { get; set; } = "Action";

    /// <summary>Normalised screen position (0..1) of the on-screen control point.</summary>
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;

    public double Size { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public string Color { get; set; } = "#FF2BD6";
}

/// <summary>Per-game configuration profile. Serialised to readable JSON.</summary>
public sealed class GameProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Game { get; set; } = "New Game";
    public string ExecutablePath { get; set; } = "";
    public string Resolution { get; set; } = "1920x1080";
    public int FpsTarget { get; set; } = 120;
    public OptimizationMode PerformanceMode { get; set; } = OptimizationMode.Balanced;

    public double MouseSensitivity { get; set; } = 1.0;
    public double DragSensitivity { get; set; } = 1.0;

    public MouseSettings Mouse { get; set; } = new();
    public DragAssistSettings DragAssist { get; set; } = new();
    public EasyDragSettings EasyDrag { get; set; } = new();
    public CrosshairSettings Crosshair { get; set; } = new();

    public List<ControlMapping> KeyboardMapping { get; set; } = new();
    public List<ControlMapping> MouseMapping { get; set; } = new();

    public DateTime Created { get; set; } = DateTime.Now;
    public DateTime Modified { get; set; } = DateTime.Now;

    public static GameProfile CreateDefault(string name) => new()
    {
        Game = name,
        KeyboardMapping = new List<ControlMapping>
        {
            new() { Input = "W", Action = "Move Forward", X = 0.30, Y = 0.35 },
            new() { Input = "A", Action = "Move Left",    X = 0.20, Y = 0.50 },
            new() { Input = "S", Action = "Move Back",    X = 0.30, Y = 0.65 },
            new() { Input = "D", Action = "Move Right",   X = 0.40, Y = 0.50 },
            new() { Input = "SPACE", Action = "Jump",     X = 0.80, Y = 0.70 },
        },
        MouseMapping = new List<ControlMapping>
        {
            new() { InputType = MappingInputType.MouseButton, Input = "MouseLeft",  Action = "Fire", X = 0.70, Y = 0.35 },
            new() { InputType = MappingInputType.MouseButton, Input = "MouseRight", Action = "Aim",  X = 0.80, Y = 0.35 },
        }
    };
}
