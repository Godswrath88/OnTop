using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Linq;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.Services;
using OnTop.ViewModels;
using OnTop.Views;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace OnTop;

public partial class App : Application
{
    private MainViewModel _vm = null!;
    private MainWindow _mainWin = null!;
    private Forms.NotifyIcon _trayIcon = null!;
    private IntPtr _mouseHook;
    private OverlayService? _overlays;
    private AutoCloseService? _autoClose;
    private WindowRuleService? _windowRules;

    // Timed pause ("Pause For…") — auto-resumes when the timer elapses.
    private System.Windows.Threading.DispatcherTimer? _pauseTimer;
    private DateTime? _pauseUntil;

    // ── Single instance ─────────────────────────────────────────────────
    private const string MutexName = @"Local\OnTop_SingleInstance_7F3D";
    private const string ShowEventName = @"Local\OnTop_ShowSettings_7F3D";
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showSignal;
    private bool _ownsMutex;

    // ── Crash handling ──────────────────────────────────────────────────
    private int _dispatcherErrorCount;

    // ── Startup grace (a stray double-click can't insta-exit at launch) ──
    private readonly int _startedTick = Environment.TickCount;
    private const int StartupGraceMs = 2000;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Safety net FIRST — everything below it can fail.
        RegisterExceptionHandlers();

        // ── Single instance: a second copy signals the first, then exits ──
        // A soft restart relaunches with --restart; the old instance is mid-
        // shutdown and about to release the mutex, so we briefly retry acquiring
        // it instead of bouncing straight off (which would just re-show the
        // dying instance and leave nothing running).
        bool isRestart = HasArg(e.Args, "--restart");
        _singleInstanceMutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex && isRestart)
        {
            for (int i = 0; i < 30 && !_ownsMutex; i++)   // up to ~3s
            {
                Thread.Sleep(100);
                try { _ownsMutex = _singleInstanceMutex.WaitOne(0); } catch { /* justified-silent: mutex race pre-logger (SECURITY.md instance rules) */ }
            }
        }
        if (!_ownsMutex)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch { /* justified-silent: handoff to first instance, pre-logger */ }
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        StartShowSignalListener();

        _vm = new MainViewModel();
        ThemeOptions.ApplyAccent(_vm.Cfg.AccentIndex);   // before ANY window exists
        ThemeOptions.ApplyTheme(_vm.Cfg.UiTheme);        // S28 Stage I
        _vm.Stats.BumpLaunch();

        // SECURITY (roadmap: warn when launched elevated): a global mouse hook
        // doesn't need admin reach, and running elevated needlessly widens what
        // a compromise could touch. Warn, but don't block — the user may have a
        // reason.
        using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
        {
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                _vm.Log.Warn("OnTop is running elevated (as administrator) — it doesn't need to be, and least-privilege is safer", "System");
                ToastService.Show("OnTop Is Running As Administrator — It Doesn't Need Elevation", ToastType.Warning);
            }
        }

        // Config conflict (corrupt file, or one from a newer build loaded
        // best-effort): ask the user in plain language before continuing, rather
        // than silently running on defaults. Cancel = close so they can restore a
        // backup / update. (SECURITY.md: fail safe, not silent.)
        if (_vm.Settings.Conflict is { } conflict)
        {
            bool cont = Views.ConfirmDialog.Confirm(null,
                conflict.FriendlyTitle, conflict.FriendlyMessage,
                "Continue", "Cancel (Close OnTop)");
            if (!cont)
            {
                _vm.Log.Info($"Startup cancelled by user after config conflict ({conflict.Kind})", "System");
                Shutdown();
                return;
            }
            _vm.Log.Info($"Config conflict acknowledged, continuing ({conflict.Kind})", "System");
        }

        // Command-line flags (for shortcuts / tooling):
        //   --restart    internal soft-restart handoff (handled above)
        //   --paused     start with zone detection paused
        //   --minimized  start in the tray without showing the window (already the
        //                default; honored + logged so a shortcut can request it)
        if (HasArg(e.Args, "--paused"))
        {
            _vm.DetectionPaused = true;
            _vm.Log.Info("Startup: detection paused (--paused)", "System");
        }
        if (HasArg(e.Args, "--minimized"))
            _vm.Log.Info("Startup: minimized to tray (--minimized)", "System");

        // ── Main window (created hidden — EnsureHandle gives us the HWND
        //    for hotkeys without the old show-then-hide startup flash, which
        //    also tripped IsVisibleChanged and deleted the recovery draft) ──
        _mainWin = new MainWindow { DataContext = _vm };
        _vm.ToggleSettingsRequested += ToggleMainWindow;
        _vm.PauseStateChanged += () => Dispatcher.Invoke(() => { UpdateTrayText(); UpdateTrayIcon(); });
        _vm.RecordRequested += () => Dispatcher.Invoke(_mainWin.QuickRecord);
        _vm.LogsRequested += () => Dispatcher.Invoke(_mainWin.ShowLogs);
        _vm.PinsRequested += () => Dispatcher.Invoke(_mainWin.ShowPinnedWindows);
        _vm.MacrosRequested += () => Dispatcher.Invoke(_mainWin.ShowMacros);
        _vm.MainWindowRequested += () => Dispatcher.Invoke(ShowMainWindow);
        // S28 Stage G: DND predicate, cheat sheet, palette.
        ToastService.SuppressRoutine = _vm.ShouldSuppressRoutineToasts;
        _vm.CheatSheetRequested += () => Dispatcher.Invoke(ToggleCheatSheet);
        _vm.PaletteRequested += () => Dispatcher.Invoke(ShowPalette);
        // S28: Low Impact Mode — BelowNormal priority + slower scanners. Applied
        // at startup and live on toggle. The mouse hook is NOT touched.
        _vm.LowImpactChanged += low => Dispatcher.Invoke(() => ApplyLowImpact(low));
        // S28: persistent recording status bar follows MacrosEnabled/ShowMacroStatusBar.
        // (S28 K: MacrosEnabled / ShowMacroStatusBar changes now arrive via VM.MacroStatusSyncRequested.)
        // S28: always-visible macro indicator — tray badge + tooltip + status bar
        // follow the recorder/player state (roadmap End Goal F).
        _vm.MacroStatusSyncRequested += () => Dispatcher.Invoke(SyncMacroStatusWindow);
        _vm.MacroStatusToggleRequested += () => Dispatcher.Invoke(ToggleMacroStatusDismissed);
        _vm.MacroSvc.StateChanged += () => Dispatcher.Invoke(() =>
        {
            UpdateTrayIcon(); UpdateTrayText();
            bool nowActive = _vm.MacroSvc.IsRecording || _vm.MacroSvc.PlayingId is not null || _vm.MacroSvc.CountdownRemaining > 0;
            if (!nowActive) { _statusActivityUntil = DateTime.Now.AddSeconds(4); _statusLinger ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(4500) }; _statusLinger.Tick -= LingerTick; _statusLinger.Tick += LingerTick; _statusLinger.Stop(); _statusLinger.Start(); }
            SyncMacroStatusWindow();
            if (_vm.MacroSvc.IsRecording) _vm.StatusText = "● RECORDING MACRO — Stop From The Macros Window";
            else if (_vm.MacroSvc.PlayingId is not null) _vm.StatusText = "▶ PLAYING MACRO — Ctrl+Alt+X Stops";
            else if (_vm.StatusText.StartsWith("● RECORDING") || _vm.StatusText.StartsWith("▶ PLAYING")) _vm.StatusText = "Ready";
        });
        _vm.PeekRequested += () => Dispatcher.Invoke(() => _overlays?.Peek());
        _vm.LegendRequested += () => Dispatcher.Invoke(() => _overlays?.ShowLegend());
        _vm.RulesRequested += () => Dispatcher.Invoke(_mainWin.ShowWindowRules);
        _vm.RestartRequested += () => Dispatcher.Invoke(SoftRestart);
        // If detection resumes by any means (manual toggle, hotkey, timer expiry),
        // drop any pending auto-resume timer so it can't fire later.
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.DetectionPaused) && !_vm.DetectionPaused)
                ClearPauseTimer();
        };

        _vm.Hotkeys.Initialize(_mainWin);   // EnsureHandle() inside
        _vm.EStop.Start();                  // S28 L: Exit/Kill thread + UI watchdog — BEFORE the first bind
        _vm.BindHotkeys();

        // ── System tray ─────────────────────────────────────────────────
        SetupTrayIcon();

        // ── Low-level mouse hook for zone detection + watchdog ──────────
        // Stealth Mode ships without the hook entirely — nothing installed
        // for anti-cheat software to flag. The toggle re-installs it live.
        if (_vm.Cfg.StealthMode)
            _vm.Log.Warn("Stealth mode active — mouse hook NOT installed; zone detection off", "System");
        else
            InstallMouseHook();
        StartHookWatchdog();
        _vm.Cfg.PropertyChanged += OnStealthChanged;

        // ── Zone overlays (dots, detection boxes, legend) ───────────────
        _overlays = new OverlayService(_vm);
        _vm.PeekHoldRequested += held => Dispatcher.Invoke(() => _overlays.PeekHold(held));   // S28 H
        _vm.IsOverlayWindow = _overlays.IsOverlayHwnd;   // S28 K: desktop-only gate looks through overlays
        _overlays.Refresh();   // restore overlays for zones saved with ShowDot

        // ── Auto-close engine + multi-monitor sanity check ──────────────
        _autoClose = new AutoCloseService(_vm);
        _windowRules = new WindowRuleService(_vm);
        // S28 Stage E: "On Start" placements, a few seconds after launch (never
        // during load; PlacementService itself refuses while the feature is off).
        if (_vm.Cfg.PlacementEnabled && _vm.Placements.Any(p => p.Enabled && p.ApplyOnStartup))
            _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(4000); await _vm.PlacementSvc.ApplyAllAsync("startup", startupOnly: true); });
        ApplyLowImpact(_vm.Cfg.LowImpactMode);
        SyncMacroStatusWindow();
        _vm.HoldChords.Sync();   // S28 H: installs the keyboard hook only if hold chords are armed
        SubscribeSystemEvents();
        StartStealthWatcher();

        // First-run welcome tour (fresh installs only — migration marks existing
        // configs as completed).
        if (!_vm.Cfg.FirstRunCompleted)
            Dispatcher.BeginInvoke(() => new Views.WelcomeWindow(_vm).Show());

        // Stage-one update check (SECURITY.md amended exception): dormant unless
        // the user configured a manifest URL; delayed so startup is never
        // blocked; all failure modes are log-only.
        if (_vm.Cfg.UpdateCheckOnStartup &&
            !string.IsNullOrWhiteSpace(_vm.Cfg.UpdateManifestUrl))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                try { await UpdateService.CheckAsync(_vm, manual: false); }
                catch (Exception ex)
                {
                    try { _vm.Log.Warn($"Startup update check crashed: {ex.Message}", "Updates"); } catch { /* justified-silent: cannot log a logging failure */ }
                }
            });
        }
        _vm.ValidateZonesAgainstDisplays();

        _vm.Log.Info("OnTop started — tray icon active, hotkeys bound");

        // Startup status toast: zones loaded + current mode, so a fresh session
        // confirms it's running and shows state at a glance.
        int total = _vm.Zones.Count;
        int enabled = _vm.Zones.Count(z => z.Enabled);
        var status = new System.Text.StringBuilder();
        status.Append(total == 0
            ? "OnTop started — no zones yet"
            : $"OnTop started — {enabled}/{total} zone{(total == 1 ? "" : "s")} enabled");
        if (_vm.DetectionPaused) status.Append(" · detection paused");
        if (_vm.Cfg.StealthMode) status.Append(" · stealth on (mouse hook off)");
        ToastService.Show(status.ToString(),
            _vm.Cfg.StealthMode || _vm.DetectionPaused ? ToastType.Warning : ToastType.Info, 4000);
    }

    // ── Global Exception Handlers ───────────────────────────────────────
    private void RegisterExceptionHandlers()
    {
        // UI-thread exceptions: log, recover up to 3 times per session so an
        // isolated binding/render error does not take down the whole tray app.
        DispatcherUnhandledException += (_, ex) =>
        {
            LogCrash("Dispatcher", ex.Exception);
            _dispatcherErrorCount++;
            if (_dispatcherErrorCount <= 3)
            {
                ex.Handled = true;
                ToastService.Show("Unexpected Error — Details Were Logged", ToastType.Error, 5000);
                OfferCrashReport();   // roadmap: diagnostics + comment
            }
            else
            {
                // Repeated failures: stop suppressing, save what we can on the way down.
                try { _vm?.SaveDraftNow(); } catch { /* justified-silent: crash path — best-effort save */ }
            }
        };

        // Anything fatal on any thread: save a recovery draft, log, auto-bundle
        // diagnostics (no prompt — the process is dying), let it die.
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            try { _vm?.SaveDraftNow(); } catch { /* justified-silent: crash path — best-effort save */ }
            LogCrash("Fatal", ex.ExceptionObject);
            try
            {
                if (_vm is not null)
                    DiagnosticsService.WriteCrashBundle(_vm, "(fatal crash — bundle auto-saved, no prompt possible)");
            }
            catch { /* justified-silent: crash path — best-effort bundle */ }
        };

        // Faulted Tasks nobody awaited: log and observe so they stay non-fatal.
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            LogCrash("Task", ex.Exception);
            ex.SetObserved();
        };
    }

    private bool _crashPromptShown;

    /// <summary>After a survivable unhandled error, offer ONCE per session to
    /// save a diagnostics bundle with a "what were you doing?" comment (roadmap
    /// item). Deferred via BeginInvoke so it never runs inside the
    /// exception handler's frame. Purely local — nothing is transmitted.</summary>
    private void OfferCrashReport()
    {
        if (_crashPromptShown || _vm is null) return;
        _crashPromptShown = true;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var comment = Views.InputDialog.Prompt(null,
                    "Save A Crash Report?",
                    "OnTop Hit An Unexpected Error (Logged). Describe What You Were Doing To Save A Diagnostics Bundle — Or Cancel To Skip:",
                    "");
                if (comment is null) { _vm.Log.Info("Crash report declined by user", "System"); return; }
                string path = DiagnosticsService.WriteCrashBundle(_vm, comment);
                _vm.Log.Action($"Crash diagnostics bundle saved: {path}", "System");
                ToastService.Show("Crash Bundle Saved To Config\\Crashes", ToastType.Success, 5000);
            }
            catch (Exception e2)
            {
                try { _vm.Log.Error($"Crash-report prompt failed: {e2.Message}", "System"); } catch { /* justified-silent: cannot log a logging failure */ }
            }
        });
    }

    // ── Auto-stealth while watched processes run (S26, roadmap) ─────────
    // The "silent layer": while any user-listed process (a game, an anti-cheat)
    // is running, Stealth engages automatically — and releases when they're all
    // gone, IF it was this watcher that engaged it (a manual stealth is never
    // auto-released). 5 s cadence; idle-cheap when the list is empty.
    private System.Windows.Threading.DispatcherTimer? _stealthWatch;
    private bool _autoStealthEngaged;

    private void StartStealthWatcher()
    {
        _stealthWatch = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_vm.Cfg.LowImpactMode ? 15 : 5),   // S28 Low Impact
        };
        _stealthWatch.Tick += (_, _) =>
        {
            _vm.TickProfileRules();   // S28 Stage G: process → profile (inert while off)
            _vm.TickIdleAndSchedules();   // S28 Stage I: idle rule + time schedules (inert while off)
            try
            {
                string list = _vm.Cfg.StealthWatchedProcesses;
                if (string.IsNullOrWhiteSpace(list) || !_vm.Cfg.StealthWatcherEnabled)   // S28 feature switch
                {
                    _autoStealthEngaged = false;
                    return;
                }
                string? running = null;
                foreach (var raw in list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = raw.Trim();
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                    if (name.Length == 0) continue;
                    var procs = System.Diagnostics.Process.GetProcessesByName(name);
                    bool any = procs.Length > 0;
                    foreach (var pr in procs) pr.Dispose();
                    if (any) { running = name; break; }
                }
                if (running is not null && !_vm.Cfg.StealthMode)
                {
                    _autoStealthEngaged = true;
                    _vm.Log.Action($"Auto-Stealth ENGAGED — watched process running: {running}", "System");
                    _vm.Cfg.StealthMode = true;      // App's handler unhooks + toasts
                }
                else if (running is null && _vm.Cfg.StealthMode && _autoStealthEngaged)
                {
                    _autoStealthEngaged = false;
                    _vm.Log.Action("Auto-Stealth RELEASED — watched processes gone", "System");
                    _vm.Cfg.StealthMode = false;     // re-hooks + toasts
                }
                else if (running is null)
                {
                    _autoStealthEngaged = false;     // manual stealth stays manual
                }
            }
            catch (Exception ex)
            {
                _vm.Log.Debug($"Stealth watcher tick failed: {ex.Message}", "System");
            }
        };
        _stealthWatch.Start();
    }

    private void LogCrash(string source, object? error)
    {
        string text = error?.ToString() ?? "(unknown error)";
        try { _vm?.Log.Error($"[{source}] Unhandled exception: {text}"); } catch { /* justified-silent: cannot log a logging failure */ }
        try
        {
            // Fallback that works even if the log service never came up.
            File.AppendAllText(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OnTop.crash.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  [{source}]  {text}\r\n");
        }
        catch { /* justified-silent: the last-resort crash writer has no fallback left */ }
    }

    // ── Single-Instance Show Signal ─────────────────────────────────────
    private void StartShowSignalListener()
    {
        var signal = _showSignal!;
        Task.Run(() =>
        {
            while (true)
            {
                try { signal.WaitOne(); }
                catch (ObjectDisposedException) { return; }

                Dispatcher.BeginInvoke(() =>
                {
                    ShowMainWindow();
                    ToastService.Show("OnTop Is Already Running — Settings Opened", ToastType.Info);
                });
            }
        });
    }

    // ── Tray Icon ───────────────────────────────────────────────────────
    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Text = $"OnTop — {_vm.Zones.Count} zones | Active",
            Visible = true
        };

        // Try to load custom icon, fall back to app icon
        try
        {
            if (System.IO.File.Exists(_vm.Settings.IconPath))
                _trayIcon.Icon = new Drawing.Icon(_vm.Settings.IconPath);
            else
                _trayIcon.Icon = Drawing.SystemIcons.Application;
        }
        catch { _trayIcon.Icon = Drawing.SystemIcons.Application; }

        _trayIcon.DoubleClick += (_, _) => ToggleMainWindow();
        _trayIcon.MouseUp += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Right) ShowTrayMenu();
        };
    }

    // ── Themed tray menu (WPF ContextMenu — matches the app, no stock strip) ──
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static bool HasArg(string[] args, string flag)
        => Array.Exists(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private void ShowTrayMenu()
    {
        // Win32 quirk (KB135788): a popup/context menu won't dismiss on an outside
        // click unless its owning window is the foreground window. The tray icon is
        // WinForms, so nothing WPF is foreground on right-click — force our (possibly
        // hidden) main window to the foreground first, or the menu stays stuck open.
        var hwnd = new WindowInteropHelper(_mainWin).EnsureHandle();
        if (hwnd != IntPtr.Zero) SetForegroundWindow(hwnd);

        var menu = BuildTrayMenu();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
        if (_vm.Cfg.TrayMenuStaysOpen) ApplyTrayPersistence(menu);
        menu.IsOpen = true;
    }

    /// <summary>Tray behavior: items don't dismiss the menu on
    /// click; instead the menu closes ~0.6 s after the cursor leaves it (a
    /// forgiving buffer, so grazing the edge doesn't slam it shut). Routed
    /// events traverse submenu popups, so the same timestamp covers those too.
    /// Outside-click and Esc still close it instantly, as they should.</summary>
    private void ApplyTrayPersistence(ContextMenu menu)
    {
        void MarkAll(ItemsControl host)
        {
            foreach (var obj in host.Items)
                if (obj is MenuItem mi)
                {
                    mi.StaysOpenOnClick = true;
                    MarkAll(mi);
                }
        }
        MarkAll(menu);

        int lastOver = Environment.TickCount;
        menu.PreviewMouseMove += (_, _) => lastOver = Environment.TickCount;
        var idle = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        idle.Tick += (_, _) =>
        {
            if (!menu.IsMouseOver && unchecked(Environment.TickCount - lastOver) > 600)
                menu.IsOpen = false;
        };
        menu.Opened += (_, _) => { lastOver = Environment.TickCount; idle.Start(); };
        menu.Closed += (_, _) => idle.Stop();
    }

    private void RebuildTrayMenu() { /* menu is rebuilt fresh on each open */ }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();   // picks up the implicit themed ContextMenu style

        menu.Items.Add(TrayItem($"Toggle OnTop ({Hk(_vm.Cfg.HotkeyConfig)})", ToggleMainWindow));
        menu.Items.Add(TrayItem($"Quick Record ({Hk(_vm.Cfg.HotkeyQuickRecord)})", () => _mainWin.QuickRecord()));
        menu.Items.Add(TrayItem($"Pinned Windows ({Hk(_vm.Cfg.HotkeyPins)})", () => _mainWin.ShowPinnedWindows()));
        menu.Items.Add(TrayItem($"Macros ({Hk(_vm.Cfg.HotkeyMacros)}){(_vm.Cfg.MacrosEnabled ? "" : " — Off")}", () => _mainWin.ShowMacros()));
        // S28 Stage I: quick opacity for the last window OnTop pinned.
        if (_vm.LastPinned != IntPtr.Zero && Win32.IsWindow(_vm.LastPinned))
        {
            var op = new MenuItem { Header = $"Last Pinned Opacity ({Win32.GetProcessName(_vm.LastPinned) ?? "window"})" };
            foreach (int pct in new[] { 100, 80, 60, 40, 20 }) { int p = pct; op.Items.Add(TrayItem($"{p}%", () => _vm.SetPinOpacity(_vm.LastPinned, p))); }
            menu.Items.Add(op);
        }
        menu.Items.Add(TrayItem($"Auto-Pin ({Hk(_vm.Cfg.HotkeyRules)})", () => _mainWin.ShowWindowRules()));

        // Profiles quick-switch submenu (active one checked).
        var profiles = new MenuItem { Header = $"Profiles ({Hk(_vm.Cfg.HotkeyProfiles)})" };
        foreach (var name in _vm.ProfileNames)
        {
            string n = name;
            var item = TrayItem(n, () =>
            {
                _vm.SwitchProfile(n);
                // Persist mode keeps the menu open — keep the ● honest live.
                foreach (var o in profiles!.Items)
                    if (o is MenuItem sib)
                        sib.IsChecked = string.Equals(sib.Header as string, _vm.ActiveProfileName, StringComparison.OrdinalIgnoreCase);
            });
            item.IsChecked = string.Equals(n, _vm.ActiveProfileName, StringComparison.OrdinalIgnoreCase);
            profiles.Items.Add(item);
        }
        menu.Items.Add(profiles);
        menu.Items.Add(new Separator());
        bool timed = _pauseTimer is not null && _pauseUntil is not null;
        string pauseHeader = _vm.DetectionPaused
            ? (timed
                ? $"Resume Detection (Auto In {Math.Max(0, (int)Math.Ceiling((_pauseUntil!.Value - DateTime.Now).TotalMinutes))} Min)"
                : "Resume Detection")
            : "Pause Detection";
        menu.Items.Add(TrayItem(pauseHeader,
            () => { _vm.DetectionPaused = !_vm.DetectionPaused; UpdateTrayText(); UpdateTrayIcon(); }));

        var pauseFor = new MenuItem { Header = "Pause For" };
        pauseFor.Items.Add(TrayItem("15 Minutes", () => PauseFor(TimeSpan.FromMinutes(15))));
        pauseFor.Items.Add(TrayItem("30 Minutes", () => PauseFor(TimeSpan.FromMinutes(30))));
        pauseFor.Items.Add(TrayItem("1 Hour", () => PauseFor(TimeSpan.FromHours(1))));
        pauseFor.Items.Add(TrayItem("2 Hours", () => PauseFor(TimeSpan.FromHours(2))));
        menu.Items.Add(pauseFor);
        menu.Items.Add(TrayItem(_vm.Cfg.StealthMode ? "Disable Stealth (Re-Hook Mouse)" : "Enable Stealth (No Hook)",
            () => _vm.Cfg.StealthMode = !_vm.Cfg.StealthMode));
        menu.Items.Add(TrayItem("Re-Enable Mouse Hook", () =>
        {
            if (_vm.Cfg.StealthMode) _vm.Cfg.StealthMode = false;   // clears stealth → App re-installs the hook
            else ToastService.Show("Mouse Hook Is Already Active", ToastType.Info);
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(TrayItem("Enable All Zones",
            () => { foreach (var z in _vm.Zones) z.Enabled = true; UpdateTrayText(); }));
        menu.Items.Add(TrayItem("Disable All Zones",
            () => { foreach (var z in _vm.Zones) z.Enabled = false; UpdateTrayText(); }));

        // Per-zone toggle submenu — a leading ✔ marks enabled zones. Rebuilt on
        // each open, so the marks are always current.
        var zonesMenu = new MenuItem { Header = $"Zones ({_vm.Zones.Count})" };
        if (_vm.Zones.Count == 0)
            zonesMenu.Items.Add(new MenuItem { Header = "(No Zones Yet)", IsEnabled = false });
        else
            foreach (var z in _vm.Zones)
            {
                var zone = z;   // capture per iteration
                zonesMenu.Items.Add(TrayItem(
                    $"{(zone.Enabled ? "\u2714  " : "\u2003\u2002")}{zone.Name}",
                    () => { zone.Enabled = !zone.Enabled; UpdateTrayText(); }));
            }
        menu.Items.Add(zonesMenu);
        menu.Items.Add(TrayItem("Show Overlay Legend", () => _overlays?.ShowLegend()));
        menu.Items.Add(new Separator());
        menu.Items.Add(TrayItem("Hotkeys...", () =>
        {
            _vm.SuspendGlobalHotkeys();   // S28 K: keys pressed in the recorder are captured, never executed
            var dlg = new HotkeyDialog(_vm.Cfg);
            dlg.Closed += (_, _) => _vm.ResumeGlobalHotkeys();
            if (dlg.ShowDialog() == true) { _vm.BindHotkeys(); _vm.SaveAll(); }
        }));
        menu.Items.Add(new Separator());
        menu.Items.Add(TrayItem($"Exit ({Hk(_vm.Cfg.HotkeyExit)})", () => Shutdown()));
        // S28 L: the same hard stop as the Kill hotkey, reachable by mouse — the
        // tray menu is on the UI thread, so this only helps while the UI still
        // answers; the hotkey is the one that works when it doesn't.
        menu.Items.Add(TrayItem($"Kill Now — No Cleanup ({Hk(_vm.Cfg.HotkeyKill)})", () => _vm.KillNow("tray menu")));
        return menu;
    }

    private static MenuItem TrayItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static string Hk(string key) => string.IsNullOrWhiteSpace(key) ? "Unbound" : key;

    // ── Timed pause ─────────────────────────────────────────────────────
    /// <summary>Pause detection and auto-resume after <paramref name="d"/>.
    /// Resuming early by any means cancels the timer (see the DetectionPaused
    /// PropertyChanged handler in OnStartup).</summary>
    private void PauseFor(TimeSpan d)
    {
        ClearPauseTimer();
        _pauseUntil = DateTime.Now + d;
        _pauseTimer = new System.Windows.Threading.DispatcherTimer { Interval = d };
        _pauseTimer.Tick += (_, _) =>
        {
            _vm.DetectionPaused = false;   // fires PropertyChanged → ClearPauseTimer
            ToastService.Show("Detection Resumed", ToastType.Info);
            UpdateTrayText();
        };
        _vm.DetectionPaused = true;
        _pauseTimer.Start();
        ToastService.Show($"Detection Paused For {DescribeDuration(d)}", ToastType.Info);
        UpdateTrayText();
    }

    private void ClearPauseTimer()
    {
        _pauseTimer?.Stop();
        _pauseTimer = null;
        _pauseUntil = null;
    }

    private static string DescribeDuration(TimeSpan d)
        => d.TotalMinutes < 60
            ? $"{(int)d.TotalMinutes} Min"
            : d.TotalHours == 1 ? "1 Hour" : $"{d.TotalHours:0.#} Hours";

    /// <summary>Base icon cache + a variant with an amber "paused" dot drawn in
    /// the corner (roadmap: tray status tint/badge while paused).</summary>
    private Drawing.Icon? _iconNormal, _iconPaused, _iconRecording, _iconPlaying;

    private Drawing.Icon PausedVariant(Drawing.Icon baseIcon)
        => BadgeVariant(baseIcon, Drawing.Color.FromArgb(255, 255, 180, 40));

    /// <summary>S28: the same corner badge in any color — amber paused, red
    /// recording, blue playing (always-visible macro indicator).</summary>
    private Drawing.Icon BadgeVariant(Drawing.Icon baseIcon, Drawing.Color color)
    {
        using var bmp = baseIcon.ToBitmap();
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            int d = Math.Max(6, bmp.Width * 7 / 16);              // badge diameter
            var rect = new Drawing.Rectangle(bmp.Width - d, bmp.Height - d, d - 1, d - 1);
            using var fill = new Drawing.SolidBrush(color);
            using var edge = new Drawing.Pen(Drawing.Color.FromArgb(255, 40, 30, 8), 1f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(fill, rect);
            g.DrawEllipse(edge, rect);
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon is null) return;
        try
        {
            _iconNormal ??= _trayIcon.Icon;
            if (_iconNormal is null) return;
            _iconPaused ??= PausedVariant(_iconNormal);
            _iconRecording ??= BadgeVariant(_iconNormal, Drawing.Color.FromArgb(255, 230, 60, 60));
            _iconPlaying ??= BadgeVariant(_iconNormal, Drawing.Color.FromArgb(255, 58, 139, 255));
            // Priority: recording > playing > paused > normal.
            _trayIcon.Icon = _vm.MacroSvc.IsRecording ? _iconRecording
                : _vm.MacroSvc.PlayingId is not null ? _iconPlaying
                : _vm.DetectionPaused ? _iconPaused : _iconNormal;
        }
        catch (Exception ex)
        {
            _vm?.Log.Debug($"Tray badge update failed: {ex.Message}", "System");
        }
    }

    // ── S28: Low Impact Mode + macro status bar ────────────────────────
    private Views.MacroStatusWindow? _macroStatus;
    private Views.CheatSheetWindow? _cheat;
    private void ToggleCheatSheet()
    {
        if (_cheat is { IsLoaded: true }) { _cheat.Close(); _cheat = null; return; }
        _cheat = new Views.CheatSheetWindow(_vm);
        _cheat.Closed += (_, _) => _cheat = null;
        _cheat.Show();
    }
    private void ShowPalette()
    {
        var p = new Views.CommandPaletteWindow(_vm, _mainWin);
        p.Show();
        p.Activate();
    }

    // S28 K (from testing: the strip shouldn't sit there forever just because macros are
    // on): shown while the Macros window is open OR a macro is recording / counting
    // down / playing, and for 4 s after activity ends. × or the strip hotkey hides
    // it until the next recording starts.
    private bool _statusDismissed;
    private DateTime _statusActivityUntil = DateTime.MinValue;
    private DispatcherTimer? _statusLinger;
    private void LingerTick(object? s, EventArgs e) { _statusLinger?.Stop(); SyncMacroStatusWindow(); }
    private void ToggleMacroStatusDismissed()
    {
        _statusDismissed = !_statusDismissed;
        if (!_statusDismissed) _statusActivityUntil = DateTime.Now.AddSeconds(6);
        SyncMacroStatusWindow();
    }
    private void SyncMacroStatusWindow()
    {
        var svc = _vm.MacroSvc;
        bool active = svc.IsRecording || svc.PlayingId is not null || svc.CountdownRemaining > 0;
        if (active) _statusDismissed = false;
        bool want = _vm.Cfg.MacrosEnabled && _vm.Cfg.ShowMacroStatusBar && !_statusDismissed
                    && (_vm.MacrosWindowOpen || active || DateTime.Now < _statusActivityUntil);
        if (want && _macroStatus is null)
        {
            _macroStatus = new Views.MacroStatusWindow(_vm, () => { _statusDismissed = true; SyncMacroStatusWindow(); });
            _macroStatus.Closed += (_, _) => _macroStatus = null;
            _macroStatus.Show();
        }
        else if (!want && _macroStatus is not null)
        {
            _macroStatus.Close();
            _macroStatus = null;
        }
    }

    private void ApplyLowImpact(bool low)
    {
        try
        {
            using var me = System.Diagnostics.Process.GetCurrentProcess();
            me.PriorityClass = low ? System.Diagnostics.ProcessPriorityClass.BelowNormal
                                   : System.Diagnostics.ProcessPriorityClass.Normal;
        }
        catch (Exception ex) { _vm.Log.Warn($"Process priority change failed: {ex.Message}", "System"); }
        if (_stealthWatch is not null) _stealthWatch.Interval = TimeSpan.FromSeconds(low ? 15 : 5);
        _windowRules?.ApplyLowImpact(low);
        _vm.Log.Info(low ? "Low Impact Mode ON — BelowNormal priority, auto-pin scan 10 s, stealth watch 15 s"
                         : "Low Impact Mode OFF — Normal priority, auto-pin scan 3 s, stealth watch 5 s", "System");
    }

    private void UpdateTrayText()
    {
        int enabled = 0;
        foreach (var z in _vm.Zones) if (z.Enabled) enabled++;
        string status = _vm.Cfg.StealthMode ? "STEALTH (Hook Off)"
            : _vm.DetectionPaused ? "PAUSED" : "Active";
        if (_vm.MacroSvc.IsRecording) status += " | RECORDING MACRO";
        else if (_vm.MacroSvc.PlayingId is not null) status += " | PLAYING MACRO";
        _trayIcon.Text = $"OnTop — {enabled}/{_vm.Zones.Count} zones | {status}";
    }

    // ── Toggle / Show Settings Window ───────────────────────────────────
    private void ShowMainWindow()
    {
        _mainWin.WindowState = WindowState.Normal;   // restore if it was minimized
        _mainWin.Show();
        _mainWin.Activate();
    }

    private void ToggleMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWin.IsVisible && _mainWin.WindowState != WindowState.Minimized)
                _mainWin.Hide();
            else
                ShowMainWindow();
        });
    }

    // ── Low-Level Mouse Hook ────────────────────────────────────────────
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelMouseProc? _mouseProc; // prevent GC

    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public Win32.POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

    // ── System events: resume / session switch / display change ─────────
    // SystemEvents are STATIC — they must be unsubscribed on exit or the app
    // leaks. Handlers arrive on a system thread; everything marshals to the UI
    // dispatcher. Roadmap items: "hook + topmost reapply on sleep/resume and
    // session switch" and the profiles remainder "monitor-configuration
    // awareness".
    private void SubscribeSystemEvents()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void UnsubscribeSystemEvents()
    {
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnPowerModeChanged(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
            Dispatcher.BeginInvoke(() => ReassertAfterSystemEvent("resume from sleep"));
    }

    private void OnSessionSwitch(object? sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
            Dispatcher.BeginInvoke(() => ReassertAfterSystemEvent("session unlock"));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => Dispatcher.BeginInvoke(() => _vm?.OnDisplaysChanged());

    /// <summary>Sleep/resume and lock/unlock can silently kill the low-level
    /// hook, drop hotkey registrations, and shuffle Z-order. Re-assert all of it.</summary>
    private void ReassertAfterSystemEvent(string why)
    {
        if (_vm is null) return;
        _vm.Log.Info($"System event: {why} — re-asserting hook, hotkeys, and topmost", "System");

        // 1. The mouse hook (unless stealth deliberately removed it).
        if (!_vm.Cfg.StealthMode && _mouseHook == IntPtr.Zero)
            InstallMouseHook();

        // 2. Hotkeys (RegisterHotKey bindings can be lost across sessions).
        _vm.BindHotkeys();

        // 3. Topmost on OnTop's pins (Z-order shuffles on resume are common).
        int reasserted = 0;
        foreach (var h in _vm.PinnedByUs.ToList())
        {
            if (!Win32.IsWindow(h)) { _vm.PinnedByUs.Remove(h); continue; }
            Win32.SetTopmost(h, true);
            reasserted++;
        }

        // 4. Our own GUI windows, if the user keeps them on top.
        if (_vm.Cfg.GuiAlwaysOnTop)
            foreach (Window w in Windows)
                if (w.IsVisible && w.Topmost) { w.Topmost = false; w.Topmost = true; }

        if (reasserted > 0)
            _vm.Log.Action($"Re-asserted always-on-top for {reasserted} pinned window(s)", "Pins");
    }

    private void InstallMouseHook()
    {
        _mouseProc ??= MouseHookCallback;
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        using var mod = proc.MainModule!;
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(mod.ModuleName!), 0);
        _lastHookPulse = Environment.TickCount;
        if (_mouseHook == IntPtr.Zero)
            _vm?.Log.Error($"Mouse hook install failed (Win32 error {Marshal.GetLastWin32Error()})");
    }

    // Roadmap: hook latency instrumentation — Windows silently evicts hooks that
    // exceed its timeout; an EMA over our click-handling cost warns us FIRST.
    private double _hookEmaMs;
    private bool _hookLatencyWarned;

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Liveness pulse — this callback fires for EVERY mouse message
        // (moves included), which is what the watchdog below relies on.
        _lastHookPulse = Environment.TickCount;

        // SECURITY/robustness: this runs inside the system input chain. An escaped
        // exception here can take the whole app down mid-hook and degrade input
        // for every process — so zone work is guarded, and CallNextHookEx ALWAYS
        // runs no matter what our handler does.
        try
        {
            // S28 Stage G: forward left / middle / right button-downs (gestures).
            int btn = wParam == WM_LBUTTONDOWN ? 0 : wParam == WM_MBUTTONDOWN ? 1 : wParam == WM_RBUTTONDOWN ? 2 : -1;
            if (nCode >= 0 && btn >= 0 &&
                unchecked(Environment.TickCount - _startedTick) >= StartupGraceMs)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                // lParam is a valid MSLLHOOKSTRUCT* whenever nCode >= 0.
                var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                _vm.OnMouseButton(btn, info.pt.X, info.pt.Y);
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0
                            / System.Diagnostics.Stopwatch.Frequency;
                _hookEmaMs = _hookEmaMs <= 0 ? ms : _hookEmaMs * 0.8 + ms * 0.2;
                if (!_hookLatencyWarned && _hookEmaMs > 3.0)
                {
                    _hookLatencyWarned = true;
                    _vm.Log.Warn($"Hook click-handling averaging {_hookEmaMs:0.0} ms — approaching hook-eviction territory; investigate zone count/log volume", "System");
                }
            }
        }
        catch (Exception ex)
        {
            try { _vm?.Log.Error($"Hook callback error (suppressed): {ex.Message}"); } catch { /* justified-silent: cannot log a logging failure */ }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ── Mouse Hook Watchdog ─────────────────────────────────────────────
    // Windows silently removes low-level hooks whose callbacks stall past
    // LowLevelHooksTimeout (debugger pauses, system load, sleep/resume).
    // Detection: if the cursor moved during an interval but the hook produced
    // no pulse for well over an interval, the hook is dead — re-install it.
    private DispatcherTimer? _hookWatchdog;
    private int _lastHookPulse;            // Environment.TickCount of last callback
    private Win32.POINT _watchdogCursor;
    private const int WatchdogIntervalMs = 5000;

    private void StartHookWatchdog()
    {
        Win32.GetCursorPos(out _watchdogCursor);
        _hookWatchdog = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(WatchdogIntervalMs) };
        _hookWatchdog.Tick += (_, _) =>
        {
            if (_vm.Cfg.StealthMode) return;   // no hook to watch — and never resurrect it
            Win32.GetCursorPos(out var pos);
            bool cursorMoved = pos.X != _watchdogCursor.X || pos.Y != _watchdogCursor.Y;
            _watchdogCursor = pos;
            if (!cursorMoved) return;   // idle system — no signal either way

            int silentMs = unchecked(Environment.TickCount - _lastHookPulse);
            if (silentMs <= WatchdogIntervalMs + 1500) return;

            _vm.Log.Warn($"Mouse hook silent for {silentMs} ms despite cursor movement — re-installing");
            if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
            InstallMouseHook();

            if (_mouseHook != IntPtr.Zero)
                ToastService.Show("Mouse Hook Was Re-Installed (Watchdog)", ToastType.Warning);
            else
                ToastService.Show("Mouse Hook Re-Install Failed — Zone Detection Inactive", ToastType.Error, 6000);
        };
        _hookWatchdog.Start();
    }

    // ── Stealth Mode (anti-cheat safe: no hook installed at all) ────────
    // Unlike Pause (hook stays, clicks ignored), Stealth removes the hook —
    // there is nothing left in the process for anti-cheat software to flag.
    private void OnStealthChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppSettings.StealthMode)) return;
        Dispatcher.Invoke(() =>
        {
            if (_vm.Cfg.StealthMode)
            {
                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
                _vm.Log.Warn("Stealth mode ON — mouse hook removed; zone detection inactive", "System");
                ToastService.Show("Stealth Mode On — Mouse Hook Removed, Zones Inactive", ToastType.Warning, 4500);
            }
            else
            {
                InstallMouseHook();
                _vm.Log.Info("Stealth mode OFF — mouse hook re-installed", "System");
                ToastService.Show("Stealth Mode Off — Zone Detection Active", ToastType.Success);
            }
            UpdateTrayText();
        });
    }

    // ── Soft restart ────────────────────────────────────────────────────
    /// <summary>Save current settings, relaunch this exe with --restart, then
    /// shut down. The new instance waits for the mutex (see OnStartup) so it
    /// takes over cleanly once this one exits and releases it.</summary>
    private void SoftRestart()
    {
        try
        {
            _vm.SaveAll();   // flush settings so the new instance loads them

            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                ToastService.Show("Couldn't Locate The App To Restart", ToastType.Error);
                _vm.Log.Warn("Soft restart aborted — ProcessPath unavailable", "System");
                return;
            }

            _vm.Log.Action("Soft restart — relaunching", "System");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--restart",
                UseShellExecute = true
            });
            Shutdown();
        }
        catch (Exception ex)
        {
            ToastService.Show("Restart Failed — See The Log", ToastType.Error);
            _vm.Log.Error($"Soft restart failed: {ex.Message}", "System");
        }
    }

    // ── Shutdown ────────────────────────────────────────────────────────
    protected override void OnExit(ExitEventArgs e)
    {
        // Guarded — the second-instance path shuts down before these exist.
        _hookWatchdog?.Stop();
        ClearPauseTimer();
        if (_vm is not null) _vm.Cfg.PropertyChanged -= OnStealthChanged;
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _overlays?.Dispose();
        _autoClose?.Dispose();
        _windowRules?.Dispose();
        if (_vm is not null)
        {
            // Capture the main window's final placement (it may still be visible
            // at exit) and flush settings once — pop-out placements were already
            // recorded in memory on their close, and one real save here avoids
            // rotating the backup chain on every window close during the session.
            try
            {
                if (_mainWin is not null)
                {
                    WindowPlacement.Save(_mainWin, "main", _vm.Cfg);
                    _mainWin.SaveGridLayout();   // column widths persist too
                }
                _vm.Settings.Save(_vm.Cfg);
            }
            catch (Exception ex)
            {
                // Final flush failed — say so while the logger still breathes.
                try { _vm.Log.Error($"Exit save failed: {ex.Message}", "Config"); } catch { /* logger down too */ }
            }
            UnsubscribeSystemEvents();
            _vm.Stats.AccumulateRuntimeAndSave();
            _vm.Stats.Dispose();
            _vm.Hotkeys.Dispose();
            _vm.EStop.Dispose();   // S28 L: background thread; process exit would end it anyway
            _vm.Log.Info("OnTop exited");
        }
        try { _macroStatus?.Close(); } catch (Exception ex) { _vm?.Log.Debug($"Status bar close: {ex.Message}", "System"); }
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        if (_ownsMutex)
        {
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { /* justified-silent: shutdown teardown */ }
        }
        _singleInstanceMutex?.Dispose();
        _showSignal?.Dispose();
        base.OnExit(e);
    }
}
