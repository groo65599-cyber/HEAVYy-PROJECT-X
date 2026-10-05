using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using HPXPirate.Interop;
using HPXPirate.Models;

namespace HPXPirate.Views;

/// <summary>
/// A transparent, click-through, always-on-top window that draws the user's crosshair.
/// It is a pure visual overlay: it never reads game data and never intercepts clicks.
/// </summary>
public partial class CrosshairOverlayWindow : Window
{
    private CrosshairSettings _settings = new();

    public CrosshairOverlayWindow()
    {
        InitializeComponent();
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        SourceInitialized += (_, _) => MakeClickThrough();
        SizeChanged += (_, _) => Render();
    }

    private void MakeClickThrough()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            ex |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT
                | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex);
        }
        catch { }
    }

    public void Apply(CrosshairSettings settings)
    {
        _settings = settings;
        Render();
    }

    private void Render()
    {
        Root.Children.Clear();
        if (!_settings.Enabled) return;

        double cx = Width / 2 + _settings.OffsetX;
        double cy = Height / 2 + _settings.OffsetY;

        var color = ParseColor(_settings.Color);
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(_settings.Opacity, 0, 1) };
        var outlineBrush = new SolidColorBrush(Colors.Black) { Opacity = Math.Clamp(_settings.Opacity * 0.8, 0, 1) };

        double size = Math.Max(2, _settings.Size);
        double thick = Math.Max(1, _settings.Thickness);
        double gap = Math.Max(0, _settings.Gap);
        var shape = _settings.Shape ?? "Cross";

        void AddLine(double x, double y, double w, double h)
        {
            if (_settings.Outline)
                Root.Children.Add(new Rectangle
                {
                    Width = w + 2, Height = h + 2, Fill = outlineBrush,
                    RadiusX = 1, RadiusY = 1,
                    RenderTransform = new TranslateTransform(x - 1, y - 1)
                });
            Root.Children.Add(new Rectangle
            {
                Width = w, Height = h, Fill = brush, RadiusX = 1, RadiusY = 1,
                RenderTransform = new TranslateTransform(x, y)
            });
        }

        if (shape is "Cross" or "CrossDot")
        {
            AddLine(cx - thick / 2, cy - gap - size, thick, size);       // up
            AddLine(cx - thick / 2, cy + gap, thick, size);              // down
            AddLine(cx - gap - size, cy - thick / 2, size, thick);       // left
            AddLine(cx + gap, cy - thick / 2, size, thick);              // right
        }

        if (shape is "Circle")
        {
            var d = (gap + size) * 2;
            if (_settings.Outline)
                Root.Children.Add(new Ellipse
                {
                    Width = d + 2, Height = d + 2, Stroke = outlineBrush, StrokeThickness = thick + 2,
                    RenderTransform = new TranslateTransform(cx - d / 2 - 1, cy - d / 2 - 1)
                });
            Root.Children.Add(new Ellipse
            {
                Width = d, Height = d, Stroke = brush, StrokeThickness = thick,
                RenderTransform = new TranslateTransform(cx - d / 2, cy - d / 2)
            });
        }

        if (shape is "Dot" or "CrossDot" || _settings.CenterDot)
        {
            double dot = Math.Max(2, thick + 1);
            if (_settings.Outline)
                Root.Children.Add(new Ellipse
                {
                    Width = dot + 2, Height = dot + 2, Fill = outlineBrush,
                    RenderTransform = new TranslateTransform(cx - dot / 2 - 1, cy - dot / 2 - 1)
                });
            Root.Children.Add(new Ellipse
            {
                Width = dot, Height = dot, Fill = brush,
                RenderTransform = new TranslateTransform(cx - dot / 2, cy - dot / 2)
            });
        }
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Color.FromRgb(0xFF, 0x2B, 0xD6); }
    }
}
