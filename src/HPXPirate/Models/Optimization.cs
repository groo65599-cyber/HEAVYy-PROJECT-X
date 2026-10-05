namespace HPXPirate.Models;

/// <summary>Category of a safe optimization that the engine can apply.</summary>
public enum OptimizationCategory
{
    PowerPlan,
    GameMode,
    BackgroundProcess,
    StartupApp,
    TempCleanup,
    CacheCleanup,
    ProcessPriority,
    GameDvr,
    VisualEffects,
    NetworkTweak
}

public enum ActionRisk { Safe, Moderate, Advanced }

/// <summary>Describes a single, individually reversible optimization step.</summary>
public sealed class OptimizationAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public OptimizationCategory Category { get; set; }
    public ActionRisk Risk { get; set; } = ActionRisk.Safe;

    /// <summary>Which boost modes include this action.</summary>
    public OptimizationMode MinimumMode { get; set; } = OptimizationMode.Balanced;

    /// <summary>True if the action is currently selected in the plan.</summary>
    public bool Selected { get; set; } = true;

    /// <summary>Result of applying this action.</summary>
    public bool Applied { get; set; }
    public bool Succeeded { get; set; }
    public string ResultMessage { get; set; } = "";

    /// <summary>State captured before applying, used to roll back.</summary>
    public string? PreviousState { get; set; }

    public bool IsReversible { get; set; } = true;
}

public sealed class ProcessInfoModel
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double CpuPercent { get; set; }
    public double RamMb { get; set; }
    public bool IsGame { get; set; }
    public bool IsProtected { get; set; }
    public bool IsSafeToOptimize => !IsProtected && !IsGame;
    public string Priority { get; set; } = "Normal";
}

public sealed class BoostStepResult
{
    public string Label { get; set; } = "";
    public bool Success { get; set; }
    public string Detail { get; set; } = "";
}

public sealed class BoostResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public OptimizationMode Mode { get; set; }
    public bool Success { get; set; }
    public List<BoostStepResult> Steps { get; set; } = new();
    public string Summary { get; set; } = "";
    public string? RestorePointId { get; set; }
}

/// <summary>A logged change so the user can undo it later.</summary>
public sealed class ChangeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public string? PreviousState { get; set; }
    public bool Reverted { get; set; }
}
