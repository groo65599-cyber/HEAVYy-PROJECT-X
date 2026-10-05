using System.Windows;
using HPXPirate.Models;
using HPXPirate.Views;

namespace HPXPirate.Services;

/// <summary>Owns the crosshair overlay window and its toggle hotkey.</summary>
public sealed class CrosshairOverlayService : IDisposable
{
    private readonly ILogger _log;
    private CrosshairOverlayWindow? _window;
    private CrosshairSettings _settings = new();

    public const int CrosshairToggleHotkeyId = 0x4001;
    public bool IsVisible => _window?.IsVisible == true;

    public event Action<bool>? VisibilityChanged;

    public CrosshairOverlayService(ILogger log) => _log = log;

    public void ApplySettings(CrosshairSettings settings)
    {
        _settings = settings;
        if (_window != null)
            _window.Dispatcher.Invoke(() => _window.Apply(settings));
    }

    public void Show()
    {
        try
        {
            if (_window is null)
            {
                _window = new CrosshairOverlayWindow();
                _window.Apply(_settings);
                _window.Closed += (_, _) => { _window = null; VisibilityChanged?.Invoke(false); };
            }
            _window.Show();
            _window.Apply(_settings);
            VisibilityChanged?.Invoke(true);
            _log.Info("Crosshair overlay shown.");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to show crosshair overlay.", ex);
        }
    }

    public void Hide()
    {
        try
        {
            _window?.Hide();
            VisibilityChanged?.Invoke(false);
            _log.Info("Crosshair overlay hidden.");
        }
        catch (Exception ex)
        {
            _log.Error("Failed to hide crosshair overlay.", ex);
        }
    }

    public void Toggle()
    {
        if (IsVisible) Hide();
        else Show();
    }

    public void Dispose()
    {
        try { _window?.Close(); } catch { }
        _window = null;
    }
}
