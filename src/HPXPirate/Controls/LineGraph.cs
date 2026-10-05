using System.Windows;
using System.Windows.Media;

namespace HPXPirate.Controls;

/// <summary>
/// Lightweight real-time line graph. Draws a series of doubles scaled to the control
/// bounds. No external charting dependency, so it stays cheap to render.
/// </summary>
public sealed class LineGraph : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<double>), typeof(LineGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(LineGraph),
        new FrameworkPropertyMetadata(Brushes.Magenta, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(LineGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AutoScaleProperty = DependencyProperty.Register(
        nameof(AutoScale), typeof(bool), typeof(LineGraph),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue), typeof(double), typeof(LineGraph),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(LineGraph),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool AutoScale
    {
        get => (bool)GetValue(AutoScaleProperty);
        set => SetValue(AutoScaleProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public LineGraph()
    {
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1) return;

        // Background
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), null, new Rect(0, 0, w, h));

        // Grid
        if (ShowGrid)
        {
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), 1);
            for (int i = 1; i < 4; i++)
            {
                double y = h * i / 4.0;
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }
            for (int i = 1; i < 6; i++)
            {
                double x = w * i / 6.0;
                dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            }
        }

        var data = Values?.ToArray();
        if (data is null || data.Length < 2) return;

        double max = AutoScale ? Math.Max(1, data.Max() * 1.15) : Math.Max(1, MaxValue);
        double stepX = w / (data.Length - 1);

        var points = new Point[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            double v = Math.Clamp(data[i], 0, max);
            double y = h - (v / max) * h;
            points[i] = new Point(i * stepX, y);
        }

        // Fill area
        if (Fill is not null)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(0, h), true, true);
                ctx.PolyLineTo(points, true, false);
                ctx.LineTo(new Point(w, h), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(Fill, null, geo);
        }

        // Line
        var pen = new Pen(Stroke, 2) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        for (int i = 1; i < points.Length; i++)
            dc.DrawLine(pen, points[i - 1], points[i]);
    }
}
