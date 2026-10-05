using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class CrosshairView : UserControl
{
    private readonly CrosshairViewModel _vm;

    public CrosshairView()
    {
        InitializeComponent();
        _vm = new CrosshairViewModel(App.Host);
        DataContext = _vm;

        _vm.PropertyChanged += (_, _) => RenderPreview();
        PreviewCanvas.SizeChanged += (_, _) => RenderPreview();
        Loaded += (_, _) => RenderPreview();
    }

    private void RenderPreview()
    {
        if (PreviewCanvas is null) return;
        PreviewCanvas.Children.Clear();

        double w = PreviewCanvas.ActualWidth, h = PreviewCanvas.ActualHeight;
        if (w < 2 || h < 2) return;

        double cx = w / 2 + _vm.OffsetX;
        double cy = h / 2 + _vm.OffsetY;

        var color = ParseColor(_vm.Color);
        var brush = new SolidColorBrush(color) { Opacity = Math.Clamp(_vm.Opacity, 0, 1) };
        var outlineBrush = new SolidColorBrush(Colors.Black) { Opacity = Math.Clamp(_vm.Opacity * 0.8, 0, 1) };

        double size = Math.Max(2, _vm.Size);
        double thick = Math.Max(1, _vm.Thickness);
        double gap = Math.Max(0, _vm.Gap);
        var shape = _vm.Shape ?? "Cross";

        void AddLine(double x, double y, double lw, double lh)
        {
            if (_vm.Outline)
                PreviewCanvas.Children.Add(new Rectangle
                {
                    Width = lw + 2, Height = lh + 2, Fill = outlineBrush, RadiusX = 1, RadiusY = 1,
                    RenderTransform = new TranslateTransform(x - 1, y - 1)
                });
            PreviewCanvas.Children.Add(new Rectangle
            {
                Width = lw, Height = lh, Fill = brush, RadiusX = 1, RadiusY = 1,
                RenderTransform = new TranslateTransform(x, y)
            });
        }

        if (shape is "Cross" or "CrossDot")
        {
            AddLine(cx - thick / 2, cy - gap - size, thick, size);
            AddLine(cx - thick / 2, cy + gap, thick, size);
            AddLine(cx - gap - size, cy - thick / 2, size, thick);
            AddLine(cx + gap, cy - thick / 2, size, thick);
        }

        if (shape is "Circle")
        {
            double d = (gap + size) * 2;
            if (_vm.Outline)
                PreviewCanvas.Children.Add(new Ellipse
                {
                    Width = d + 2, Height = d + 2, Stroke = outlineBrush, StrokeThickness = thick + 2,
                    RenderTransform = new TranslateTransform(cx - d / 2 - 1, cy - d / 2 - 1)
                });
            PreviewCanvas.Children.Add(new Ellipse
            {
                Width = d, Height = d, Stroke = brush, StrokeThickness = thick,
                RenderTransform = new TranslateTransform(cx - d / 2, cy - d / 2)
            });
        }

        if (shape is "Dot" or "CrossDot" || _vm.CenterDot)
        {
            double dot = Math.Max(2, thick + 1);
            if (_vm.Outline)
                PreviewCanvas.Children.Add(new Ellipse
                {
                    Width = dot + 2, Height = dot + 2, Fill = outlineBrush,
                    RenderTransform = new TranslateTransform(cx - dot / 2 - 1, cy - dot / 2 - 1)
                });
            PreviewCanvas.Children.Add(new Ellipse
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
