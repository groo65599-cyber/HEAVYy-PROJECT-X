using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class DragAssistViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private DragAssistSettings S => _host.AppSettings.DragAssist;

    public DragAssistViewModel(AppHost host)
    {
        _host = host;
        SaveCommand = new RelayCommand(() => { _host.Settings.Save(); Status = "Drag Assist settings saved."; });
        LinearCommand = new RelayCommand(() => SetCurve(ResponseCurve.Linear));
        SmoothCommand = new RelayCommand(() => SetCurve(ResponseCurve.Smooth));
        FastCommand = new RelayCommand(() => SetCurve(ResponseCurve.FastStart));
        SlowCommand = new RelayCommand(() => SetCurve(ResponseCurve.SlowStart));
        PrecisionCommand = new RelayCommand(() => SetCurve(ResponseCurve.Precision));
        CustomCommand = new RelayCommand(() => SetCurve(ResponseCurve.Custom));
        UpdateCurve();
    }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool Enabled { get => S.Enabled; set { S.Enabled = value; OnPropertyChanged(); } }
    public double DragSensitivity { get => S.DragSensitivity; set { S.DragSensitivity = value; OnPropertyChanged(); UpdateCurve(); } }
    public double DragAcceleration { get => S.DragAcceleration; set { S.DragAcceleration = value; OnPropertyChanged(); } }
    public double DragSmoothing { get => S.DragSmoothing; set { S.DragSmoothing = value; OnPropertyChanged(); } }
    public double StartZoneSensitivity { get => S.StartZoneSensitivity; set { S.StartZoneSensitivity = value; OnPropertyChanged(); } }
    public double VerticalMultiplier { get => S.VerticalMultiplier; set { S.VerticalMultiplier = value; OnPropertyChanged(); } }
    public double HorizontalMultiplier { get => S.HorizontalMultiplier; set { S.HorizontalMultiplier = value; OnPropertyChanged(); } }
    public double DragDistance { get => S.DragDistance; set { S.DragDistance = value; OnPropertyChanged(); } }
    public double DragSpeed { get => S.DragSpeed; set { S.DragSpeed = value; OnPropertyChanged(); } }

    private List<double> _curve = new();
    public List<double> Curve { get => _curve; private set => SetProperty(ref _curve, value); }

    public string CurveName => S.ResponseCurve.ToString();

    public ICommand SaveCommand { get; }
    public ICommand LinearCommand { get; }
    public ICommand SmoothCommand { get; }
    public ICommand FastCommand { get; }
    public ICommand SlowCommand { get; }
    public ICommand PrecisionCommand { get; }
    public ICommand CustomCommand { get; }

    private void SetCurve(ResponseCurve curve)
    {
        S.ResponseCurve = curve;
        OnPropertyChanged(nameof(CurveName));
        UpdateCurve();
    }

    private void UpdateCurve()
    {
        var pts = new List<double>();
        for (int i = 0; i <= 50; i++)
        {
            double x = i / 50.0;
            pts.Add(Curves.Evaluate(S.ResponseCurve, x, S.CustomCurvePoints) * 100);
        }
        Curve = pts;
    }
}
