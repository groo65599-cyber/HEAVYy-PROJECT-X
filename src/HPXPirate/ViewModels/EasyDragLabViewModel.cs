using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class EasyDragLabViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private EasyDragSettings S => _host.AppSettings.EasyDrag;

    private readonly DragProcessor _rawProcessor = new();   // baseline (no processing)
    private readonly DragProcessor _engineProcessor = new(); // processed

    public EasyDragLabViewModel(AppHost host)
    {
        _host = host;

        ApplyCommand = new RelayCommand(() =>
        {
            _host.Settings.Save();
            _host.EasyDrag.Reset();
            Status = "Easy Drag settings applied.";
        });
        ResetLabCommand = new RelayCommand(ResetLab);

        LightCommand = new RelayCommand(() => Preset(EasyDragPreset.Light));
        BalancedCommand = new RelayCommand(() => Preset(EasyDragPreset.Balanced));
        StableCommand = new RelayCommand(() => Preset(EasyDragPreset.Stable));
        FastDragCommand = new RelayCommand(() => Preset(EasyDragPreset.FastDrag));
        PrecisionCommand = new RelayCommand(() => Preset(EasyDragPreset.Precision));
        CustomCommand = new RelayCommand(() => Preset(EasyDragPreset.Custom));

        AimLightCommand = new RelayCommand(() => Aim(AimFeel.Light));
        AimSmoothCommand = new RelayCommand(() => Aim(AimFeel.Smooth));
        AimStableCommand = new RelayCommand(() => Aim(AimFeel.Stable));
        AimResponsiveCommand = new RelayCommand(() => Aim(AimFeel.Responsive));
        AimPrecisionCommand = new RelayCommand(() => Aim(AimFeel.Precision));

        UpdateCurve();
    }

    private string _status = "Drag inside the test area to measure your input.";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    // ---- controls ----
    public bool Enabled { get => S.Enabled; set { S.Enabled = value; OnPropertyChanged(); } }
    public double DragWeight { get => S.DragWeight; set { S.DragWeight = value; OnPropertyChanged(); } }
    public double DragSensitivity { get => S.DragSensitivity; set { S.DragSensitivity = value; OnPropertyChanged(); UpdateCurve(); } }
    public double DragStability { get => S.DragStability; set { S.DragStability = value; OnPropertyChanged(); } }
    public double DragSmoothness { get => S.DragSmoothness; set { S.DragSmoothness = value; OnPropertyChanged(); } }
    public double DragAcceleration { get => S.DragAcceleration; set { S.DragAcceleration = value; OnPropertyChanged(); } }
    public double VerticalDragMultiplier { get => S.VerticalDragMultiplier; set { S.VerticalDragMultiplier = value; OnPropertyChanged(); } }
    public double HorizontalDragMultiplier { get => S.HorizontalDragMultiplier; set { S.HorizontalDragMultiplier = value; OnPropertyChanged(); } }
    public double InitialDragResponse { get => S.InitialDragResponse; set { S.InitialDragResponse = value; OnPropertyChanged(); } }
    public double InputSmoothing { get => S.InputSmoothing; set { S.InputSmoothing = value; OnPropertyChanged(); } }
    public double InputDeadzone { get => S.InputDeadzone; set { S.InputDeadzone = value; OnPropertyChanged(); } }
    public double MaxDragSpeed { get => S.MaxDragSpeed; set { S.MaxDragSpeed = value; OnPropertyChanged(); } }

    public StabilizerLevel MicroMovementStabilizer
    {
        get => S.MicroMovementStabilizer;
        set { S.MicroMovementStabilizer = value; OnPropertyChanged(); }
    }

    public string PresetName => S.Preset.ToString();
    public string AimFeelName => S.AimFeel.ToString();
    public string CurveName => S.DragResponseCurve.ToString();

    private List<double> _curve = new();
    public List<double> Curve { get => _curve; private set => SetProperty(ref _curve, value); }

    // ---- lab metrics ----
    private double _rawMagnitude, _processedMagnitude, _distance, _speed;
    private double _stability = 100, _smoothness = 100;
    private double _beforeStability = 100, _beforeSmoothness = 100;
    private string _responseLabel = "—";
    private List<double> _rawSeries = new();
    private List<double> _processedSeries = new();

    public double RawMagnitude { get => _rawMagnitude; private set => SetProperty(ref _rawMagnitude, value); }
    public double ProcessedMagnitude { get => _processedMagnitude; private set => SetProperty(ref _processedMagnitude, value); }
    public double Distance { get => _distance; private set => SetProperty(ref _distance, value); }
    public double Speed { get => _speed; private set => SetProperty(ref _speed, value); }
    public double Stability { get => _stability; private set => SetProperty(ref _stability, value); }
    public double Smoothness { get => _smoothness; private set => SetProperty(ref _smoothness, value); }
    public double BeforeStability { get => _beforeStability; private set => SetProperty(ref _beforeStability, value); }
    public double BeforeSmoothness { get => _beforeSmoothness; private set => SetProperty(ref _beforeSmoothness, value); }
    public string ResponseLabel { get => _responseLabel; private set => SetProperty(ref _responseLabel, value); }
    public List<double> RawSeries { get => _rawSeries; private set => SetProperty(ref _rawSeries, value); }
    public List<double> ProcessedSeries { get => _processedSeries; private set => SetProperty(ref _processedSeries, value); }

    public ICommand ApplyCommand { get; }
    public ICommand ResetLabCommand { get; }
    public ICommand LightCommand { get; }
    public ICommand BalancedCommand { get; }
    public ICommand StableCommand { get; }
    public ICommand FastDragCommand { get; }
    public ICommand PrecisionCommand { get; }
    public ICommand CustomCommand { get; }
    public ICommand AimLightCommand { get; }
    public ICommand AimSmoothCommand { get; }
    public ICommand AimStableCommand { get; }
    public ICommand AimResponsiveCommand { get; }
    public ICommand AimPrecisionCommand { get; }

    /// <summary>Called by the Easy Drag Lab view for every mouse move in the test area.</summary>
    public void ProcessDrag(double dx, double dy, double dt = 0.016)
    {
        // Baseline (raw, identity processing) for BEFORE numbers
        var rawSettings = new EasyDragSettings
        {
            DragSensitivity = 1, DragWeight = 1, DragSmoothness = 0, InputSmoothing = 0,
            DragAcceleration = 0, InitialDragResponse = 1, MicroMovementStabilizer = StabilizerLevel.Off,
            InputDeadzone = 0, DragResponseCurve = ResponseCurve.Linear, MaxDragSpeed = 1e9
        };
        var raw = _rawProcessor.Process(dx, dy, rawSettings, dt);
        var proc = _engineProcessor.Process(dx, dy, S, dt);

        RawMagnitude = raw.RawMagnitude;
        ProcessedMagnitude = proc.OutMagnitude;
        Distance = _engineProcessor.TotalDistance;
        Speed = proc.Speed;
        Stability = proc.StabilityScore;
        Smoothness = proc.SmoothnessScore;
        BeforeStability = raw.StabilityScore;
        BeforeSmoothness = raw.SmoothnessScore;

        ResponseLabel = proc.OutMagnitude > proc.RawMagnitude * 1.05 ? "FAST"
            : proc.OutMagnitude < proc.RawMagnitude * 0.95 ? "PRECISE" : "BALANCED";

        var rawList = new List<double>(RawSeries) { Math.Min(100, raw.RawMagnitude) };
        var procList = new List<double>(ProcessedSeries) { Math.Min(100, proc.OutMagnitude) };
        if (rawList.Count > 120) rawList.RemoveAt(0);
        if (procList.Count > 120) procList.RemoveAt(0);
        RawSeries = rawList;
        ProcessedSeries = procList;
    }

    private void ResetLab()
    {
        _rawProcessor.Reset();
        _engineProcessor.Reset();
        RawSeries = new();
        ProcessedSeries = new();
        RawMagnitude = ProcessedMagnitude = Distance = Speed = 0;
        Stability = Smoothness = BeforeStability = BeforeSmoothness = 100;
        ResponseLabel = "—";
        Status = "Lab reset.";
    }

    private void Preset(EasyDragPreset preset)
    {
        EasyDragEngine.ApplyPreset(S, preset);
        RefreshAll();
        Status = $"Preset '{preset}' applied.";
    }

    private void Aim(AimFeel feel)
    {
        EasyDragEngine.ApplyAimFeel(S, feel);
        RefreshAll();
        Status = $"Aim Feel '{feel}' applied (mouse feel / input consistency).";
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(PresetName));
        OnPropertyChanged(nameof(AimFeelName));
        OnPropertyChanged(nameof(CurveName));
        foreach (var p in GetType().GetProperties())
        {
            if (p.Name is "DragWeight" or "DragSensitivity" or "DragStability" or "DragSmoothness"
                or "DragAcceleration" or "VerticalDragMultiplier" or "HorizontalDragMultiplier"
                or "InitialDragResponse" or "InputSmoothing" or "InputDeadzone" or "MaxDragSpeed"
                or "MicroMovementStabilizer")
                OnPropertyChanged(p.Name);
        }
        UpdateCurve();
    }

    private void UpdateCurve()
    {
        var pts = new List<double>();
        for (int i = 0; i <= 50; i++)
            pts.Add(Curves.Evaluate(S.DragResponseCurve, i / 50.0, S.CustomCurvePoints) * 100);
        Curve = pts;
    }
}
