using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HPXPirate.ViewModels;

namespace HPXPirate.Views;

/// <summary>
/// Easy Drag Lab (Section 27). Measures the user's OWN mouse drag input and shows
/// how the Easy Drag processing changes stability / smoothness in real time.
/// No targeting, no auto-aim, no enemy detection — only the user's own pointer data.
/// </summary>
public partial class EasyDragLabView : UserControl
{
    private readonly EasyDragLabViewModel _vm;
    private bool _dragging;
    private Point _lastPoint;
    private DateTime _lastTime;

    public EasyDragLabView()
    {
        InitializeComponent();
        _vm = new EasyDragLabViewModel(App.Host);
        DataContext = _vm;

        DragPad.MouseLeftButtonDown += OnPadDown;
        DragPad.MouseMove += OnPadMove;
        DragPad.MouseLeftButtonUp += OnPadUp;
        DragPad.MouseLeave += OnPadLeave;
    }

    private void OnPadDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastPoint = e.GetPosition(PadCanvas);
        _lastTime = DateTime.UtcNow;
        MoveDot(_lastPoint);
        DragPad.CaptureMouse();
        e.Handled = true;
    }

    private void OnPadMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        var p = e.GetPosition(PadCanvas);
        var now = DateTime.UtcNow;
        double dt = Math.Max(0.001, (now - _lastTime).TotalSeconds);
        if (dt > 0.2) dt = 0.016; // ignore stalls / first sample

        double dx = p.X - _lastPoint.X;
        double dy = p.Y - _lastPoint.Y;

        _vm.ProcessDrag(dx, dy, dt);
        MoveDot(p);

        _lastPoint = p;
        _lastTime = now;
        e.Handled = true;
    }

    private void OnPadUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        DragPad.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnPadLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragging = false;
            DragPad.ReleaseMouseCapture();
        }
    }

    private void MoveDot(Point p)
    {
        double w = PadCanvas.ActualWidth, h = PadCanvas.ActualHeight;
        double x = Math.Clamp(p.X, 0, Math.Max(0, w));
        double y = Math.Clamp(p.Y, 0, Math.Max(0, h));
        Canvas.SetLeft(PadDot, x - PadDot.Width / 2);
        Canvas.SetTop(PadDot, y - PadDot.Height / 2);
    }
}
