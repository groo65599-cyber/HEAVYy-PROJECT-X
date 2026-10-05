using System.Windows.Input;
using HPXPirate.Models;
using HPXPirate.Services;

namespace HPXPirate.ViewModels;

public sealed class CrosshairViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private CrosshairSettings C => _host.AppSettings.Crosshair;

    public const int ToggleHotkeyId = CrosshairOverlayService.CrosshairToggleHotkeyId;

    public CrosshairViewModel(AppHost host)
    {
        _host = host;
        ShowCommand = new RelayCommand(() => { _host.Crosshair.ApplySettings(C); _host.Crosshair.Show(); OnPropertyChanged(nameof(IsOverlayVisible)); });
        HideCommand = new RelayCommand(() => { _host.Crosshair.Hide(); OnPropertyChanged(nameof(IsOverlayVisible)); });
        ToggleCommand = new RelayCommand(() => { _host.Crosshair.ApplySettings(C); _host.Crosshair.Toggle(); OnPropertyChanged(nameof(IsOverlayVisible)); });
        SaveCommand = new RelayCommand(() => { _host.Settings.Save(); _host.Crosshair.ApplySettings(C); Status = "Crosshair saved."; });
        ApplyHotkeyCommand = new RelayCommand(ApplyHotkey);
    }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool IsOverlayVisible => _host.Crosshair.IsVisible;

    public bool Enabled { get => C.Enabled; set { C.Enabled = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public string Shape { get => C.Shape; set { C.Shape = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public double Size { get => C.Size; set { C.Size = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public double Thickness { get => C.Thickness; set { C.Thickness = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public double Gap { get => C.Gap; set { C.Gap = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public double Opacity { get => C.Opacity; set { C.Opacity = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public string Color { get => C.Color; set { C.Color = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public bool CenterDot { get => C.CenterDot; set { C.CenterDot = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public bool Outline { get => C.Outline; set { C.Outline = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public string ToggleHotkey { get => C.ToggleHotkey; set { C.ToggleHotkey = value; OnPropertyChanged(); } }
    public double OffsetX { get => C.OffsetX; set { C.OffsetX = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }
    public double OffsetY { get => C.OffsetY; set { C.OffsetY = value; OnPropertyChanged(); _host.Crosshair.ApplySettings(C); } }

    public string[] Shapes { get; } = { "Cross", "Dot", "Circle", "CrossDot" };
    public string[] Colors { get; } = { "#FF2BD6", "#2BE7FF", "#35E08A", "#FFB020", "#FF4D6D", "#FFFFFF", "#000000" };

    public ICommand ShowCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ApplyHotkeyCommand { get; }

    private void ApplyHotkey()
    {
        _host.Input.UnregisterHotkey(ToggleHotkeyId);
        if (_host.Input.RegisterHotkey(ToggleHotkey, ToggleHotkeyId))
        {
            Status = $"Toggle hotkey '{ToggleHotkey}' registered.";
        }
        else Status = $"Could not register '{ToggleHotkey}' (in use?).";
    }

    /// <summary>Called by the shell when the global hotkey fires, to refresh the UI.</summary>
    public void OnHotkey(int id)
    {
        if (id != ToggleHotkeyId) return;
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            _host.Crosshair.ApplySettings(C);
            _host.Crosshair.Toggle();
            OnPropertyChanged(nameof(IsOverlayVisible));
        });
    }
}
