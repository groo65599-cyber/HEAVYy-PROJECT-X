using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HPXPirate.Models;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

public partial class ControlMapperView : UserControl
{
    private readonly ControlMapperViewModel _vm;
    private ControlMapping? _dragging;
    private Point _dragOffset;

    public ControlMapperView()
    {
        InitializeComponent();
        _vm = new ControlMapperViewModel(App.Host);
        DataContext = _vm;

        _vm.Mappings.CollectionChanged += (_, _) => RebuildCanvas();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlMapperViewModel.SelectedProfile)) RebuildCanvas();
        };
        MapCanvas.SizeChanged += (_, _) => RebuildCanvas();
        Loaded += (_, _) => RebuildCanvas();
    }

    private void RebuildCanvas()
    {
        if (MapCanvas is null) return;
        MapCanvas.Children.Clear();

        double w = MapCanvas.ActualWidth, h = MapCanvas.ActualHeight;
        if (w < 2 || h < 2) return;

        foreach (var m in _vm.Mappings)
        {
            var el = CreatePoint(m);
            Canvas.SetLeft(el, m.X * w - el.Width / 2);
            Canvas.SetTop(el, m.Y * h - el.Height / 2);
            MapCanvas.Children.Add(el);
        }
    }

    private FrameworkElement CreatePoint(ControlMapping m)
    {
        var accent = (Brush)FindResource("Brush.Accent.Magenta");
        var border = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = new SolidColorBrush(Color.FromArgb(70, 255, 43, 214)),
            BorderBrush = accent,
            BorderThickness = new Thickness(2),
            Cursor = Cursors.SizeAll,
            Tag = m,
            ToolTip = m.Action,
            Child = new TextBlock
            {
                Text = m.Input,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2)
            }
        };
        border.MouseLeftButtonDown += Point_MouseDown;
        border.MouseMove += Point_MouseMove;
        border.MouseLeftButtonUp += Point_MouseUp;
        return border;
    }

    private void Point_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not ControlMapping m) return;
        _dragging = m;
        var pos = e.GetPosition(MapCanvas);
        _dragOffset = new Point(pos.X - Canvas.GetLeft(el), pos.Y - Canvas.GetTop(el));
        el.CaptureMouse();
        e.Handled = true;
    }

    private void Point_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null || sender is not FrameworkElement el) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(MapCanvas);
        double w = MapCanvas.ActualWidth, h = MapCanvas.ActualHeight;
        if (w < 2 || h < 2) return;

        double left = Math.Clamp(pos.X - _dragOffset.X, 0, w - el.Width);
        double top = Math.Clamp(pos.Y - _dragOffset.Y, 0, h - el.Height);
        Canvas.SetLeft(el, left);
        Canvas.SetTop(el, top);

        _dragging.X = Math.Clamp((left + el.Width / 2) / w, 0, 1);
        _dragging.Y = Math.Clamp((top + el.Height / 2) / h, 0, 1);
        e.Handled = true;
    }

    private void Point_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el) el.ReleaseMouseCapture();
        _dragging = null;
        e.Handled = true;
    }
}
