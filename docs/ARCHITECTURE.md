# HPX PIRATE — Architecture

This document describes how HPX PIRATE is put together so a new contributor can be
productive quickly.

---

## 1. High-level shape

```
┌─────────────────────────────────────────────────────────────┐
│                         App (WPF)                           │
│   OnStartup → AppHost.Initialize() → MainWindow.Show()      │
│   Crash guards: Dispatcher / AppDomain / TaskScheduler      │
└───────────────▲─────────────────────────────────────────────┘
                │ DataContext / App.Host
┌───────────────┴─────────────────────────────────────────────┐
│                        MainWindow (shell)                   │
│   Custom chrome · left navigation · tray icon · content     │
│   Nav tag → View (Dashboard, Performance, …)                │
└───────────────▲─────────────────────────────────────────────┘
                │ bindings
┌───────────────┴─────────────────────────────────────────────┐
│                        ViewModels (MVVM)                    │
│   ViewModelBase (INotifyPropertyChanged) · RelayCommand     │
└───────────────▲─────────────────────────────────────────────┘
                │ method calls / events
┌───────────────┴─────────────────────────────────────────────┐
│                     Services (AppHost)                      │
│   logging · settings · profiles · hardware · fps(ETW) ·     │
│   analyzer · optimizer · restore · processes · startup ·    │
│   input mapper · crosshair · drag engines · monitor         │
└───────────────▲─────────────────────────────────────────────┘
                │ P/Invoke / WMI / ETW / registry
┌───────────────┴─────────────────────────────────────────────┐
│                     Windows OS (Win32)                      │
└─────────────────────────────────────────────────────────────┘
```

**Design rules**

1. **MVVM.** Views bind to ViewModels; ViewModels talk to Services; Services talk to the OS.
2. **One composition root.** `AppHost` owns every service (manual DI, no container library).
3. **No blocking UI.** Long work runs on background threads; results marshal back via
   `Dispatcher`.
4. **Fail soft.** Any hardware/OS feature that is unavailable degrades to a no-op or "n/a"
   instead of throwing.
5. **Reversible by construction.** Anything that mutates the system records an
   `OptimizationChange` that can be undone.

---

## 2. Composition root — `AppHost`

`AppHost.Initialize()` creates services in dependency order:

| Service | Responsibility |
| --- | --- |
| `LoggingService` | Rolling file log under `%APPDATA%\HPX Pirate\logs`. |
| `SettingsService` | Loads/saves `settings.json`; exposes `Current` (`AppSettings`). |
| `ProfileManager` | CRUD + import/export/backup for game profiles. |
| `HardwareMonitorService` | CPU/GPU/RAM/disk/network via LibreHardwareMonitor + PerformanceCounter + WMI + `GlobalMemoryStatusEx`. |
| `FpsMonitorService` | ETW capture of `DxgKrnl` Present events → FPS / frame time. |
| `BottleneckAnalyzer` | CPU-vs-GPU verdict from load + frame time. |
| `ProcessManagerService` | Enumerate processes, set priority, remember original priority. |
| `RestorePointService` | Create System Restore points via WMI `SystemRestore`. |
| `SystemOptimizerService` | Tweak catalog, apply/undo, change log. |
| `StartupManagerService` | Read/observe startup entries. |
| `InputMapperService` | Low-level keyboard/mouse hooks + remap table. |
| `CrosshairOverlayService` | Owns the click-through overlay window + global hotkey. |
| `EasyDragEngine` / `DragAssistEngine` | Response curves, deadzone, EMA smoothing, measured metrics. |
| `MonitorService` | Aggregates FPS + hardware into a live sample stream for the UI. |

`AppHost.Shutdown()` disposes the monitor, crosshair, input hooks and saves settings.

---

## 3. Threading model

| Concern | Thread |
| --- | --- |
| UI | WPF dispatcher thread. |
| Input hooks | A **dedicated STA thread** with its own message pump (`InputHook`). |
| Hardware polling | Background timer/task; results posted to the dispatcher. |
| ETW FPS capture | Background ETW session thread. |
| Overlay rendering | WPF dispatcher (overlay is a WPF `Window`). |

Hooks and hotkeys must live on a thread with a running message loop — that is why
`InputMapperService`/`CrosshairOverlayService` spin up their own STA threads rather than
using the UI thread.

---

## 4. The FPS monitor (non-injection)

HPX PIRATE uses the same class of technique as PresentMon:

1. Start an ETW session with the **Microsoft-Windows-DxgKrnl** provider.
2. Listen for `Present` / `PresentHistory` events emitted by the GPU scheduler.
3. Convert present timestamps into instantaneous FPS and frame time.
4. Feed a `HistoryBuffer` for 1% / 0.1% low calculation and the live `LineGraph`.

Because it reads **OS-level telemetry** and never touches the game process, there is no
injection and nothing for an anti-cheat to flag. Some protected titles simply do not emit
these events; in that case the monitor reports "no capture" and the rest of the app works.

`FpsAnalyzer` turns a finished capture into: average / min / max FPS, frame-time
consistency, stutter count, and a human-readable verdict.

---

## 5. The optimizer (safe & reversible)

`SystemOptimizerService` holds a catalog of `Optimization` items. Each item declares:

- a human-readable description and category,
- the **current** and **target** state,
- an `Apply()` and `Undo()` action,
- whether it needs admin / a restore point.

Flow:

```
Preview  →  Create Restore Point  →  Apply  →  Record OptimizationChange  →  Log
                                                      │
                                        UNDO / UNDO ALL  ←┘
```

Only **HKCU** keys are modified. No system files are deleted. No Windows services are
blindly disabled. The full change log is persisted so rollback survives restarts.

---

## 6. Input mapping & drag engines

- **Hooks**: `SetWindowsHookEx(WH_KEYBOARD_LL / WH_MOUSE_LL)` on the dedicated STA thread.
- **Remap table**: source → target key/button, with per-profile overrides.
- **Drag engines**: apply a response curve to the *user's own* mouse deltas:

```
raw delta → deadzone → response curve → EMA smoothing → micro-movement stabiliser → output
```

Metrics (stability, smoothness) are **measured** from the actual input stream
(variance of deltas, jitter, etc.) — never fabricated. The engines have no knowledge of
the screen, the game, or any target; they only reshape the user's own input.

---

## 7. Crosshair overlay

A borderless WPF `Window` with extended styles:

```
WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
```

- **Transparent** → clicks pass through to the game.
- **ToolWindow** → no taskbar entry.
- **NoActivate** → never steals focus.
- Toggled by a global hotkey registered with `RegisterHotKey`.

Rendering (shape, colour, size, thickness, gap, opacity, offset, centre dot, outline) is
done in `CrosshairOverlayWindow.Render()` and mirrored in the designer preview.

---

## 8. Persistence

- Format: **JSON** (`System.Text.Json`, camelCase, `JsonStringEnumConverter`).
- Location: `%APPDATA%\HPX Pirate`.
- `JsonHelper` centralises (de)serialisation + atomic writes.
- Profiles are backed up to `profiles\backups\` before being overwritten.

---

## 9. UI / theming

- `Themes/Colors.xaml` — palette (dark base, neon-magenta `#FF2BD6` accent, metallic
  silver). `Themes/Controls.xaml` — control templates (buttons, cards, sliders, toggles,
  nav buttons).
- Custom chrome via `WindowChrome` + `DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE)`;
  `WM_GETMINMAXINFO` is handled so maximise respects the custom frame.
- `Controls/LineGraph.cs` — a dependency-free real-time chart `FrameworkElement`.

---

## 10. Testing strategy

- **Headless logic tests** (`tests/HPXPirate.LogicTests`) compile only the
  platform-independent files and exercise settings, profiles, drag engines, analyzers and
  the history buffer. They run anywhere .NET runs.
- **Manual smoke tests** on Windows cover startup, persistence, profile IO, keymapping,
  monitoring and rollback (see README → Testing).

---

## 11. Extending

- **New tweak**: add an `Optimization` to the catalog with `Apply`/`Undo`; it automatically
  appears in the UI, the change log and rollback.
- **New module**: add a ViewModel + View, register the nav button in `MainWindow.xaml`,
  and (if it needs a service) wire it into `AppHost`.
- **New curve**: extend the response-curve enum and the `DragEngine` evaluation switch.
