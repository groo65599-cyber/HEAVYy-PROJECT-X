using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HPXPirate.Interop;

namespace HPXPirate.Services;

/// <summary>
/// HPX CONTROL MAPPER runtime: global hotkeys plus low-level keyboard/mouse capture
/// used by the mapping editor, the Easy Drag Lab and the crosshair toggle.
/// The hooks only observe the user's own input; nothing is injected into games.
/// </summary>
public sealed class InputMapperService : IDisposable
{
    private readonly ILogger _log;

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private InputHook.LowLevelProc? _keyProc;   // keep delegates alive
    private InputHook.LowLevelProc? _mouseProc;
    private Thread? _hookThread;
    private uint _hookThreadId;
    private volatile bool _running;

    private IntPtr _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private readonly Dictionary<int, string> _hotkeys = new();

    // ---- events (raised on the hook thread) ----
    public event Action<string>? KeyDown;
    public event Action<string>? KeyUp;
    public event Action<string>? MouseButtonDown;
    public event Action<int, int>? MouseMoved;
    public event Action<int>? HotkeyPressed;

    public bool CaptureEnabled { get; set; }
    public bool HooksActive => _keyboardHook != IntPtr.Zero || _mouseHook != IntPtr.Zero;

    public InputMapperService(ILogger log) => _log = log;

    // ------------------------------------------------------------------
    //  Window / hotkeys
    // ------------------------------------------------------------------
    public void AttachToWindow(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);
            _log.Info("Input mapper attached to main window.");
        }
        catch (Exception ex)
        {
            _log.Warn("Could not attach input mapper to window: " + ex.Message);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            HotkeyPressed?.Invoke(id);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public bool RegisterHotkey(string hotkey, int id)
    {
        try
        {
            if (_hwnd == IntPtr.Zero) return false;
            var (mods, vk) = ParseHotkey(hotkey);
            NativeMethods.UnregisterHotKey(_hwnd, id);
            if (NativeMethods.RegisterHotKey(_hwnd, id, mods | NativeMethods.MOD_NOREPEAT, vk))
            {
                _hotkeys[id] = hotkey;
                _log.Info($"Registered hotkey '{hotkey}' (id {id}).");
                return true;
            }
            _log.Warn($"Failed to register hotkey '{hotkey}' (already in use?).");
            return false;
        }
        catch (Exception ex)
        {
            _log.Warn("RegisterHotkey error: " + ex.Message);
            return false;
        }
    }

    public void UnregisterHotkey(int id)
    {
        try { if (_hwnd != IntPtr.Zero) NativeMethods.UnregisterHotKey(_hwnd, id); }
        catch { }
    }

    public static (uint mods, uint vk) ParseHotkey(string hotkey)
    {
        uint mods = 0;
        uint vk = 0;
        foreach (var raw in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = raw.Trim();
            switch (part.ToUpperInvariant())
            {
                case "CTRL": case "CONTROL": mods |= NativeMethods.MOD_CONTROL; break;
                case "ALT": mods |= NativeMethods.MOD_ALT; break;
                case "SHIFT": mods |= NativeMethods.MOD_SHIFT; break;
                default:
                    if (Enum.TryParse<Key>(part, true, out var key))
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
        return (mods, vk);
    }

    // ------------------------------------------------------------------
    //  Low-level hooks
    // ------------------------------------------------------------------
    public void StartHooks()
    {
        if (_running) return;
        _running = true;
        _hookThread = new Thread(HookThreadProc)
        {
            IsBackground = true,
            Name = "HPX-InputHook",
            Priority = ThreadPriority.AboveNormal
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    private void HookThreadProc()
    {
        try
        {
            _hookThreadId = (uint)GetCurrentThreadId();
            _keyProc = KeyboardProc;
            _mouseProc = MouseProc;

            var module = InputHook.GetModuleHandle(null);
            _keyboardHook = InputHook.SetWindowsHookEx(InputHook.WH_KEYBOARD_LL, _keyProc, module, 0);
            _mouseHook = InputHook.SetWindowsHookEx(InputHook.WH_MOUSE_LL, _mouseProc, module, 0);

            if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
                _log.Warn("One or more input hooks failed to install (try running as administrator).");
            else
                _log.Info("Input hooks installed.");

            // message pump required for the hooks
            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Input hook thread error.", ex);
        }
        finally
        {
            if (_keyboardHook != IntPtr.Zero) InputHook.UnhookWindowsHookEx(_keyboardHook);
            if (_mouseHook != IntPtr.Zero) InputHook.UnhookWindowsHookEx(_mouseHook);
            _keyboardHook = _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<InputHook.KBDLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            bool down = msg is InputHook.WM_KEYDOWN or InputHook.WM_SYSKEYDOWN;
            bool up = msg is InputHook.WM_KEYUP or InputHook.WM_SYSKEYUP;
            if (down || up)
            {
                var name = KeyName(info.vkCode);
                try
                {
                    if (down) KeyDown?.Invoke(name);
                    else KeyUp?.Invoke(name);
                }
                catch { }
            }
        }
        return InputHook.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<InputHook.MSLLHOOKSTRUCT>(lParam);
            int msg = wParam.ToInt32();
            try
            {
                switch (msg)
                {
                    case InputHook.WM_MOUSEMOVE:
                        MouseMoved?.Invoke(info.pt.x, info.pt.y);
                        break;
                    case InputHook.WM_LBUTTONDOWN: MouseButtonDown?.Invoke("MouseLeft"); break;
                    case InputHook.WM_RBUTTONDOWN: MouseButtonDown?.Invoke("MouseRight"); break;
                    case InputHook.WM_MBUTTONDOWN: MouseButtonDown?.Invoke("MouseMiddle"); break;
                    case InputHook.WM_MOUSEWHEEL:
                        int delta = (short)((info.mouseData >> 16) & 0xffff);
                        MouseButtonDown?.Invoke(delta > 0 ? "WheelUp" : "WheelDown");
                        break;
                }
            }
            catch { }
        }
        return InputHook.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public static string KeyName(uint vk)
    {
        try
        {
            var key = KeyInterop.KeyFromVirtualKey((int)vk);
            return key == Key.None ? $"VK{vk:X2}" : key.ToString();
        }
        catch { return $"VK{vk:X2}"; }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public InputHook.POINT pt;
    }

    public void Dispose()
    {
        _running = false;
        try
        {
            foreach (var id in _hotkeys.Keys.ToList())
                UnregisterHotkey(id);
            if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero);
            _hookThread?.Join(500);
        }
        catch { }
        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
}
