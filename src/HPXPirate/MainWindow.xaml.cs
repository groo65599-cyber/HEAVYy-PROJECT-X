using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HPXPirate.Interop;
using HPXPirate.Services;
using HPXPirate.Views;

namespace HPXPirate;

/// <summary>
/// Main application shell: custom chrome title bar, cyberpunk sidebar navigation and a
/// lazily-created content host. Also owns the tray icon and the global crosshair hotkey.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppHost _host = App.Host;
    private readonly Dictionary<string, UserControl> _views = new();
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _reallyClose;

    private static readonly Dictionary<string, string> SectionTitles = new()
    {
        ["dashboard"] = "Dashboard",
        ["performance"] = "Performance Engine",
        ["profiles"] = "Game Profiles",
        ["mapper"] = "Control Mapper",
        ["dragassist"] = "Drag Assist",
        ["easydrag"] = "Easy Drag Lab",
        ["crosshair"] = "Crosshair",
        ["monitor"] = "Monitor",
        ["optimizer"] = "System Optimizer",
        ["settings"] = "Settings",
    };

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
    }

    // ------------------------------------------------------------------
    //  Startup wiring
    // ------------------------------------------------------------------
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _host.Input.AttachToWindow(this);
            _host.Input.StartHooks();
            _host.Input.HotkeyPressed += OnGlobalHotkey;
            _host.Input.RegisterHotkey(_host.AppSettings.Crosshair.ToggleHotkey,
                CrosshairOverlayService.CrosshairToggleHotkeyId);

            _host.Monitor.Start();
            InitTray();
            ShowView("dashboard");
        }
        catch (Exception ex)
        {
            _host.Logger.Error("MainWindow initialisation error.", ex);
        }
    }

    private void OnGlobalHotkey(int id)
    {
        if (id != CrosshairOverlayService.CrosshairToggleHotkeyId) return;
        Dispatcher.Invoke(() =>
        {
            _host.Crosshair.ApplySettings(_host.AppSettings.Crosshair);
            _host.Crosshair.Toggle();
        });
    }

    // ------------------------------------------------------------------
    //  Navigation
    // ------------------------------------------------------------------
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string key })
            ShowView(key);
    }

    private void ShowView(string key)
    {
        if (!_views.TryGetValue(key, out var view))
        {
            view = key switch
            {
                "performance" => new PerformanceView(),
                "profiles" => new GameProfilesView(),
                "mapper" => new ControlMapperView(),
                "dragassist" => new DragAssistView(),
                "easydrag" => new EasyDragLabView(),
                "crosshair" => new CrosshairView(),
                "monitor" => new MonitorView(),
                "optimizer" => new SystemOptimizerView(),
                "settings" => new SettingsView(),
                _ => new DashboardView(),
            };
            _views[key] = view;
        }

        ContentHost.Content = view;
        BreadcrumbText.Text = SectionTitles.TryGetValue(key, out var t) ? t : "Dashboard";
    }

    // ------------------------------------------------------------------
    //  Window chrome
    // ------------------------------------------------------------------
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximise();
            return;
        }
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Minimise_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximise_Click(object sender, RoutedEventArgs e) => ToggleMaximise();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximise()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            try { NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int)); } catch { }
            try { NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int)); } catch { }

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }
        catch (Exception ex)
        {
            _host.Logger.Warn("Window chrome init failed: " + ex.Message);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_GETMINMAXINFO)
        {
            try
            {
                var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lParam);
                var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
                    if (NativeMethods.GetMonitorInfo(monitor, ref info))
                    {
                        mmi.ptMaxPosition.X = info.rcWork.Left - info.rcMonitor.Left;
                        mmi.ptMaxPosition.Y = info.rcWork.Top - info.rcMonitor.Top;
                        mmi.ptMaxSize.X = info.rcWork.Right - info.rcWork.Left;
                        mmi.ptMaxSize.Y = info.rcWork.Bottom - info.rcWork.Top;
                        mmi.ptMaxTrackSize.X = mmi.ptMaxSize.X;
                        mmi.ptMaxTrackSize.Y = mmi.ptMaxSize.Y;
                        System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
                        handled = true;
                    }
                }
            }
            catch { }
        }
        return IntPtr.Zero;
    }

    // ------------------------------------------------------------------
    //  Tray
    // ------------------------------------------------------------------
    private void InitTray()
    {
        try
        {
            System.Drawing.Icon? icon = null;
            try
            {
                var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/hpx.ico"))?.Stream;
                if (stream != null) icon = new System.Drawing.Icon(stream);
            }
            catch { }

            _tray = new System.Windows.Forms.NotifyIcon
            {
                Text = "HPX PIRATE",
                Visible = true,
                Icon = icon ?? System.Drawing.SystemIcons.Application
            };
            _tray.DoubleClick += (_, _) => RestoreFromTray();

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open HPX PIRATE", null, (_, _) => RestoreFromTray());
            menu.Items.Add("Toggle Crosshair", null, (_, _) =>
            {
                _host.Crosshair.ApplySettings(_host.AppSettings.Crosshair);
                _host.Crosshair.Toggle();
            });
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => { _reallyClose = true; Close(); });
            _tray.ContextMenuStrip = menu;
        }
        catch (Exception ex)
        {
            _host.Logger.Warn("Tray icon unavailable: " + ex.Message);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _host.AppSettings.MinimizeToTray)
        {
            try { Hide(); } catch { }
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyClose && _host.AppSettings.MinimizeToTray && !_host.AppSettings.SafeMode)
        {
            // Keep running in the tray unless the user chose Exit.
            e.Cancel = true;
            try { Hide(); } catch { }
            return;
        }

        try
        {
            _tray?.Dispose();
            _tray = null;
        }
        catch { }
    }
}
