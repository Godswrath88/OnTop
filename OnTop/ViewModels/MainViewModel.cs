using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.Services;
using Forms = System.Windows.Forms;

namespace OnTop.ViewModels;

public class MainViewModel : ObservableObject
{
    // ── Services ────────────────────────────────────────────────────────
    public SettingsService Settings { get; }
    public StatsService Stats { get; private set; } = null!;   // created in ctor
    public LogService Log { get; }
    public HotkeyService Hotkeys { get; }
    /// <summary>S28 L: Exit + Kill live on their own thread so they work while the UI is frozen.</summary>
    public EmergencyStopService EStop { get; }
    /// <summary>The macro module (S27). Created in the ctor right after Stats;
    /// disposed first. HOTFIX v0.36.1: this declaration was missing since v0.33.0
    /// (the ctor assigned it without a property) — the first confirmed build
    /// surfaced it.</summary>
    public MacroService MacroSvc { get; private set; } = null!;   // created in ctor

    // ── State ───────────────────────────────────────────────────────────
    private AppSettings _cfg = new();
    private bool _detectionPaused;
    private Zone? _selectedZone;
    private string _statusText = "OnTop running";

    // ── Hotkey IDs ──────────────────────────────────────────────────────
    private int _hkToggleId = -1, _hkExitId = -1, _hkConfigId = -1, _hkKillId = -1, _hkPauseId = -1, _hkRehookId = -1, _hkPinsId = -1, _hkPeekId = -1, _hkRulesId = -1, _hkProfilesId = -1;
    private int _hkQuickRecordId = -1, _hkViewLogsId = -1, _hkRestartId = -1;
    private int _hkStealthId = -1;
    private readonly List<int> _macroHkIds = new();
    private int _hkMacrosId = -1;
    private int _hkMacroRecordId = -1;
    /// <summary>Registered optional toggle hotkeys (one per main-GUI toggle).</summary>
    private readonly List<int> _toggleHotkeyIds = new();

    // ── Collections ─────────────────────────────────────────────────────
    public ObservableCollection<Zone> Zones { get; } = new();
    /// <summary>Auto-pin rules (Window Rules). Edited by WindowRulesWindow,
    /// scanned by WindowRuleService, persisted via SaveAll/SaveRules.</summary>
    public ObservableCollection<WindowRule> WindowRules { get; } = new();
    /// <summary>S28 Stage E: placement rules (live collection, synced to Cfg.Placements).</summary>
    public ObservableCollection<PlacementRule> Placements { get; } = new();
    public PlacementService PlacementSvc { get; private set; } = null!;   // created in ctor
    /// <summary>S28 Stage H: hold-to-peek / hold-to-pin (keyboard hook only while armed).</summary>
    public HoldChordService HoldChords { get; private set; } = null!;
    private int _hkPlacementId = -1;
    private readonly List<int> _libraryHkIds = new();
    private int _hkPinCursorId = -1, _hkOpUpId = -1, _hkOpDownId = -1, _hkClickThroughId = -1, _hkCornerId = -1, _hkCheatId = -1, _hkPaletteId = -1;
    private int _hkPinTimedId = -1, _hkSendMonId = -1, _hkMacroPauseId = -1, _hkMacroStatusId = -1;
    private bool _hotkeysSuspended;
    /// <summary>S28 K: TRUE while the Macros window is open (drives the status strip).</summary>
    public bool MacrosWindowOpen { get => _macrosWindowOpen; set { _macrosWindowOpen = value; MacroStatusSyncRequested?.Invoke(); } }
    private bool _macrosWindowOpen;
    /// <summary>S28 K: App re-evaluates whether the status strip should be shown.</summary>
    public event Action? MacroStatusSyncRequested;
    /// <summary>S28 K: the status-strip hotkey / × button toggles this.</summary>
    public event Action? MacroStatusToggleRequested;
    private readonly Dictionary<IntPtr, Views.ClickThroughBorderWindow> _ctBorders = new();

    /// <summary>S28 K: while a hotkey-capture UI is open, NO global hotkey may fire —
    /// the keys must be captured, not executed (from testing: pressing a bound key in the
    /// recorder ran the original action). BindHotkeys refuses to register while
    /// suspended; hold chords disarm too.</summary>
    public void SuspendGlobalHotkeys()
    {
        _hotkeysSuspended = true;
        BindHotkeys();   // unregisters everything and returns early
        HoldChords.Sync();
        Log.Debug("Global hotkeys SUSPENDED (capture UI open)", "Hotkey");
    }
    public void ResumeGlobalHotkeys()
    {
        _hotkeysSuspended = false;
        BindHotkeys();
        HoldChords.Sync();
        Log.Debug("Global hotkeys RESUMED", "Hotkey");
    }
    public bool HotkeysSuspended => _hotkeysSuspended;
    /// <summary>S28 Stage I: the window most recently pinned by OnTop (tray quick-opacity).</summary>
    public IntPtr LastPinned { get; private set; } = IntPtr.Zero;
    /// <summary>S28 Stage G: windows we made click-through (so unpin can revert).</summary>
    private readonly HashSet<IntPtr> _clickThrough = new();
    public ObservableCollection<ProcessException> Exceptions { get; } = new();
    public ObservableCollection<string> AutoCloseTriggers { get; } = new();
    public ObservableCollection<string> GroupNames { get; } = new() { "All" };

    // ── Undo/Redo ───────────────────────────────────────────────────────
    private readonly UndoStack _undo = new();

    // ── Auto-draft (crash recovery) ─────────────────────────────────────
    private Timer? _draftTimer;

    // ── Double-click detection ──────────────────────────────────────────

    // ── Properties ──────────────────────────────────────────────────────
    public AppSettings Cfg { get => _cfg; set { _cfg = value; OnPropertyChanged(); } }
    public bool DetectionPaused
    {
        get => _detectionPaused;
        set
        {
            // Single event source: EVERY pause path (GUI toggle, tray, command,
            // hotkey) lands here, so tray text + badge always stay in sync.
            if (Set(ref _detectionPaused, value)) PauseStateChanged?.Invoke();
        }
    }

    private bool _testMode;
    /// <summary>Test Mode (session-only, never persisted): zone gestures give
    /// HIT/MISS feedback — toast, log, overlay flash — but NEVER execute their
    /// action. For safely verifying zone placement/size after edits.</summary>
    public bool TestMode
    {
        get => _testMode;
        set
        {
            if (!Set(ref _testMode, value)) return;
            Log.Action($"Test Mode {(value ? "ON" : "OFF")}", "Detection");
            ToastService.Show(value
                ? "Test Mode ON — Zones Report Hits, Actions Suppressed"
                : "Test Mode OFF", value ? ToastType.Warning : ToastType.Info);
        }
    }
    public Zone? SelectedZone { get => _selectedZone; set { Set(ref _selectedZone, value); OnPropertyChanged(nameof(HasSelection)); } }
    public bool HasSelection => _selectedZone != null;
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    // ── Zone Groups ─────────────────────────────────────────────────────
    private string _groupFilter = "All";
    /// <summary>Filters the zones DataGrid through its default collection view.
    /// The zone map binds the raw collection, so it always shows every zone.</summary>
    public string GroupFilter
    {
        get => _groupFilter;
        set { if (Set(ref _groupFilter, string.IsNullOrEmpty(value) ? "All" : value)) ApplyGroupFilter(); }
    }

    // ── Slider proxies (bound to UI, push to zones) ─────────────────────
    private int _sliderTol, _sliderCap, _sliderVar;
    public int SliderTol
    {
        get => _sliderTol;
        set { if (Set(ref _sliderTol, value)) OnSliderChanged(); }
    }
    public int SliderCap
    {
        get => _sliderCap;
        set { if (Set(ref _sliderCap, value)) OnSliderChanged(); }
    }
    public int SliderVar
    {
        get => _sliderVar;
        set { if (Set(ref _sliderVar, value)) OnSliderChanged(); }
    }

    // ── Commands ────────────────────────────────────────────────────────
    public ICommand RemoveZoneCmd { get; }
    public ICommand DuplicateZoneCmd { get; }
    public ICommand UndoCmd { get; }
    public ICommand RedoCmd { get; }
    public ICommand SaveCmd { get; }
    public ICommand TogglePauseCmd { get; }
    public ICommand EnableGroupCmd { get; }
    public ICommand DisableGroupCmd { get; }
    public ICommand OpenConfigFolderCmd { get; }
    public ICommand OpenLogsFolderCmd { get; }
    public ICommand BackupCmd { get; }

    // ── Constructor ─────────────────────────────────────────────────────
    public MainViewModel()
    {
        Settings = new SettingsService();
        Log = new LogService(Settings.LogDir);
        Settings.AttachLogger(Log);   // logging audit: config I/O failures surface
        Stats = new StatsService(Settings.ConfigDir, Log);   // Enabled synced after LoadSettings (see OnCfgPropertyChanged)
        MacroSvc = new Services.MacroService(this);
        PlacementSvc = new PlacementService(this);
        HoldChords = new HoldChordService(this);
        Hotkeys = new HotkeyService();
        EStop = new EmergencyStopService(Log) { OnExitRequested = OnHotkeyExit };

        RemoveZoneCmd = new RelayCommand(RemoveSelectedZone);
        DuplicateZoneCmd = new RelayCommand(DuplicateSelectedZone);
        UndoCmd = new RelayCommand(Undo);
        RedoCmd = new RelayCommand(Redo);
        SaveCmd = new RelayCommand(SaveAll);
        TogglePauseCmd = new RelayCommand(() => DetectionPaused = !DetectionPaused);
        EnableGroupCmd = new RelayCommand(() => SetGroupEnabled(true));
        DisableGroupCmd = new RelayCommand(() => SetGroupEnabled(false));
        OpenConfigFolderCmd = new RelayCommand(() => OpenFolder(Settings.ConfigDir));
        OpenLogsFolderCmd = new RelayCommand(() => OpenFolder(Log.LogDirectory));
        BackupCmd = new RelayCommand(BackupNow);

        LoadSettings();
        ThemeOptions.MicaEnabled = _cfg.MicaEnabled;   // before any window is created
        Log.Verbose = _cfg.VerboseLogging;
        Log.IsCategoryEnabled = cat => !_cfg.DisabledLogCategories.Contains(cat, StringComparer.OrdinalIgnoreCase);
        Log.PurgeOld(_cfg.LogRetentionDays);
        Log.Info($"Loaded {Zones.Count} zones, {Exceptions.Count} exceptions");

        // Group names + per-zone health flags track the zone list.
        Zones.CollectionChanged += OnZonesCollectionChanged;
        foreach (var z in Zones) z.PropertyChanged += Zone_HealthWatch;
        RefreshGroupNames();
        RecomputeZoneHealth();
        _cfg.PropertyChanged += OnCfgPropertyChanged;
        Stats.Enabled = _cfg.StatsEnabled;   // S28: honor the switch from the first bump
        // Roadmap (self-repair): Apply(true) REWRITES the .lnk with the CURRENT
        // exe path every launch — a moved/renamed install heals automatically.
        try
        {
            StartupShortcut.Apply(_cfg.StartWithWindows);
            if (_cfg.StartWithWindows) Log.Debug("Startup shortcut verified/re-pointed to the current exe", "System");
        }
        catch (Exception ex) { Log.Error($"Startup shortcut sync: {ex.Message}"); }

        if (Settings.RecoveryNote is { } recovered)
        {
            Log.Error(recovered == "defaults"
                ? "settings.json and all backups were unreadable — started with defaults"
                : $"settings.json was unreadable — recovered from {recovered}");
            ToastService.Show(recovered == "defaults"
                ? "Settings Were Corrupt And No Backup Was Readable — Started Fresh"
                : $"Settings Were Corrupt — Recovered From {recovered}", ToastType.Error, 7000);
        }

        // S28: things Validate neutralized on load (blanked updater URL,
        // powerful-field stamp mismatch). Logged + toasted once; never silent.
        foreach (var w in Settings.LoadWarnings)
        {
            Log.Warn($"Config load: {w}", "Config");
            ToastService.Show(w.Length > 140 ? w[..140] + "…" : w, ToastType.Warning, 9000);
        }

        if (Settings.HasDraft())
        {
            Log.Warn("Draft settings found from previous session");
            StatusText = "Recovered draft from previous session";
            ToastService.Show("Recovered Draft Settings From Previous Session", ToastType.Warning, 4500);
        }
    }

    // ── Settings I/O ────────────────────────────────────────────────────
    private void LoadSettings()
    {
        _cfg = Settings.Load();
        Zones.Clear();
        foreach (var z in _cfg.Zones) Zones.Add(z);
        foreach (var r in _cfg.WindowRules) WindowRules.Add(r);
        Placements.Clear();
        foreach (var p in _cfg.Placements) Placements.Add(p);
        Exceptions.Clear();
        foreach (var e in _cfg.Exceptions) Exceptions.Add(e);
        AutoCloseTriggers.Clear();
        foreach (var t in _cfg.AutoCloseTriggers.Where(t => !string.IsNullOrWhiteSpace(t)))
            AutoCloseTriggers.Add(t.Trim());

        _sliderTol = _cfg.DefTolerance;
        _sliderCap = _cfg.DefCapacity;
        _sliderVar = _cfg.DefVariation;

        // Profiles: first run after v11 — wrap the existing workspace as "Default".
        if (_cfg.Profiles.Count == 0)
        {
            _cfg.Profiles.Add(CaptureLiveWorkspace(_cfg.ActiveProfile));
            Log.Info($"Created initial profile \"{_cfg.ActiveProfile}\" from the current workspace", "Profiles");
        }
        OnPropertyChanged(nameof(SliderTol));
        OnPropertyChanged(nameof(SliderCap));
        OnPropertyChanged(nameof(SliderVar));
        OnPropertyChanged(nameof(Cfg));
    }

    public void SaveAll()
    {
        _cfg.Zones = Zones.ToList();
        _cfg.Exceptions = Exceptions.ToList();
        _cfg.AutoCloseTriggers = AutoCloseTriggers.ToList();
        _cfg.WindowRules = WindowRules.ToList();
        _cfg.Placements = Placements.ToList();
        SyncActiveProfile();
        _cfg.DefTolerance = _sliderTol;
        _cfg.DefCapacity = _sliderCap;
        _cfg.DefVariation = _sliderVar;
        Settings.Save(_cfg);
        Settings.DeleteDraft();
        Log.Action("Settings saved");
        // v9.5 feedback: the saved confirmation lives in the status bubble, not a toast.
        StatusText = $"Saved At {DateTime.Now:h:mm:ss tt}";
    }

    // ── Hotkey Management ───────────────────────────────────────────────
    public void BindHotkeys() => BindHotkeys(isRetry: false);

    private void BindHotkeys(bool isRetry)
    {
        // Unbind old
        Hotkeys.Unregister(_hkToggleId);
        // (Exit/Kill: e-stop thread — see below)
        Hotkeys.Unregister(_hkConfigId);
        Hotkeys.Unregister(_hkPauseId);
        Hotkeys.Unregister(_hkRehookId);
        Hotkeys.Unregister(_hkPinsId);
        Hotkeys.Unregister(_hkPeekId);
        Hotkeys.Unregister(_hkRulesId);
        Hotkeys.Unregister(_hkProfilesId);
        Hotkeys.Unregister(_hkQuickRecordId);
        Hotkeys.Unregister(_hkViewLogsId);
        Hotkeys.Unregister(_hkRestartId);
        Hotkeys.Unregister(_hkStealthId);
        Hotkeys.Unregister(_hkMacrosId);
        Hotkeys.Unregister(_hkMacroRecordId);
        Hotkeys.Unregister(_hkPlacementId);
        foreach (var id in new[] { _hkPinCursorId, _hkOpUpId, _hkOpDownId, _hkClickThroughId, _hkCornerId, _hkCheatId, _hkPaletteId, _hkPinTimedId, _hkSendMonId, _hkMacroPauseId, _hkMacroStatusId }) Hotkeys.Unregister(id);
        foreach (var id in _macroHkIds) Hotkeys.Unregister(id);
        _macroHkIds.Clear();
        foreach (var id in _libraryHkIds) Hotkeys.Unregister(id);
        _libraryHkIds.Clear();
        foreach (var id in _toggleHotkeyIds) Hotkeys.Unregister(id);
        _toggleHotkeyIds.Clear();

        // S28 L: Exit + Kill are registered on the E-STOP THREAD, never on this
        // window — a hung UI thread must not take the escape hatch down with it.
        // While a capture UI is open Exit is released (so it can be re-recorded)
        // but Kill stays armed: pressing the Kill combo ANYWHERE stops OnTop.
        EStop.Rebind(_hotkeysSuspended ? "" : _cfg.HotkeyExit, string.IsNullOrWhiteSpace(_cfg.HotkeyKill) ? "Ctrl+Alt+Q" : _cfg.HotkeyKill);
        _hkExitId = _hotkeysSuspended || EStop.ExitBound || string.IsNullOrWhiteSpace(_cfg.HotkeyExit) ? 0 : -1;
        _hkKillId = EStop.KillBound ? 0 : -1;
        if (_hotkeysSuspended) return;   // S28 K: capture UI open — nothing else binds
        _hkToggleId = RegisterIfSet(_cfg.HotkeyToggle, OnHotkeyToggle);
        _hkConfigId = RegisterIfSet(_cfg.HotkeyConfig, OnHotkeyConfig);
        _hkPauseId = Hotkeys.Register("Ctrl+Alt+P", OnHotkeyPause);
        _hkRehookId = RegisterIfSet(_cfg.HotkeyRehook, OnHotkeyRehook);
        _hkPinsId = RegisterIfSet(_cfg.HotkeyPins, OnHotkeyPins);
        _hkPeekId = RegisterIfSet(_cfg.HotkeyPeek, OnHotkeyPeek);
        _hkRulesId = RegisterIfSet(_cfg.HotkeyRules, OnHotkeyRules);
        _hkProfilesId = RegisterIfSet(_cfg.HotkeyProfiles, OnHotkeyProfiles);
        _hkQuickRecordId = RegisterIfSet(_cfg.HotkeyQuickRecord, OnHotkeyQuickRecord);
        _hkViewLogsId = RegisterIfSet(_cfg.HotkeyViewLogs, OnHotkeyViewLogs);
        _hkRestartId = RegisterIfSet(_cfg.HotkeyRestart, OnHotkeyRestart);
        _hkStealthId = RegisterIfSet(_cfg.HotkeyStealth, OnHotkeyStealth);
        _hkMacrosId = RegisterIfSet(_cfg.HotkeyMacros, OnHotkeyMacros);
        _hkPlacementId = _cfg.PlacementEnabled ? RegisterIfSet(_cfg.HotkeyPlacement, OnHotkeyPlacement) : -1;   // inert while off
        // S28 Stage G: pin extras (core pinning — always available) + discoverability.
        _hkPinCursorId = RegisterIfSet(_cfg.HotkeyPinUnderCursor, () => Application.Current.Dispatcher.Invoke(TogglePinUnderCursor));
        _hkOpUpId = RegisterIfSet(_cfg.HotkeyPinOpacityUp, () => Application.Current.Dispatcher.Invoke(() => NudgePinOpacity(Win32.GetForegroundWindow(), +10)));
        _hkOpDownId = RegisterIfSet(_cfg.HotkeyPinOpacityDown, () => Application.Current.Dispatcher.Invoke(() => NudgePinOpacity(Win32.GetForegroundWindow(), -10)));
        _hkClickThroughId = RegisterIfSet(_cfg.HotkeyPinClickThrough, () => Application.Current.Dispatcher.Invoke(ToggleClickThroughUnderCursor));
        _hkCornerId = RegisterIfSet(_cfg.HotkeyPinCycleCorner, () => Application.Current.Dispatcher.Invoke(() => CycleCorner(Win32.GetForegroundWindow())));
        _hkCheatId = RegisterIfSet(_cfg.HotkeyCheatSheet, () => Application.Current.Dispatcher.Invoke(() => CheatSheetRequested?.Invoke()));
        _hkPaletteId = RegisterIfSet(_cfg.HotkeyPalette, () => Application.Current.Dispatcher.Invoke(() => PaletteRequested?.Invoke()));
        _hkPinTimedId = RegisterIfSet(_cfg.HotkeyPinTimed, () => Application.Current.Dispatcher.Invoke(() => PinTimed(Win32.GetForegroundWindow())));
        _hkSendMonId = RegisterIfSet(_cfg.HotkeySendToMonitor, () => Application.Current.Dispatcher.Invoke(() => SendToNextMonitor(Win32.GetForegroundWindow())));
        _hkMacroPauseId = _cfg.MacrosEnabled ? RegisterIfSet(_cfg.HotkeyMacroPause, () => Application.Current.Dispatcher.Invoke(MacroSvc.TogglePause)) : -1;
        _hkMacroStatusId = _cfg.MacrosEnabled ? RegisterIfSet(_cfg.HotkeyMacroStatus, () => Application.Current.Dispatcher.Invoke(() => MacroStatusToggleRequested?.Invoke())) : -1;
        // S28: the record/stop hotkey binds ONLY while macros are enabled and the
        // profile allows them — inert otherwise (Standing Design Law).
        _hkMacroRecordId = _cfg.MacrosEnabled && !MacroSvc.ActiveProfileDenies()
            ? RegisterIfSet(_cfg.HotkeyMacroRecord, OnHotkeyMacroRecord) : -1;

        // Per-macro hotkeys (S27): press = play, press again = stop. Bound only
        // while the master toggle is on; assignment-time conflict checks keep
        // these unique (see HotkeyDialog.FindConflict + MacrosWindow).
        // S28: a DENY profile policy means no macro hotkey binds at all.
        if (_cfg.MacrosEnabled && !MacroSvc.ActiveProfileDenies())
            foreach (var mac in _cfg.Macros)
            {
                if (!mac.Enabled || string.IsNullOrWhiteSpace(mac.Hotkey)) continue;
                var m = mac;
                int id = Hotkeys.Register(m.Hotkey, () =>
                {
                    if (MacroSvc.PlayingId == m.Id) MacroSvc.Stop();
                    else MacroSvc.Play(m);
                });
                if (id >= 0) _macroHkIds.Add(id);
                else Log.Warn($"Macro hotkey failed to register: {m.Name} ({m.Hotkey})", "Hotkey");
            }

        // S28 Stage F: library action hotkeys — only while the feature is on.
        if (_cfg.ActionLibraryEnabled)
            foreach (var act in _cfg.LibraryActions)
            {
                if (!act.Enabled || string.IsNullOrWhiteSpace(act.Hotkey)) continue;
                var a = act;
                int id = Hotkeys.Register(a.Hotkey, () => Application.Current.Dispatcher.Invoke(() => RunLibraryAction(a, "hotkey")));
                if (id >= 0) _libraryHkIds.Add(id);
                else Log.Warn($"Library hotkey failed to register: {a.Name} ({a.Hotkey})", "Hotkey");
            }

        var failed = new List<(string What, string Key)>();
        if (_hkToggleId == -1) failed.Add(("toggle", _cfg.HotkeyToggle));
        if (_hkExitId == -1) failed.Add(("exit", _cfg.HotkeyExit));
        if (_hkConfigId == -1) failed.Add(("config", _cfg.HotkeyConfig));
        if (_hkKillId == -1) failed.Add(("kill", _cfg.HotkeyKill));
        if (_hkQuickRecordId == -1) failed.Add(("quick record", _cfg.HotkeyQuickRecord));
        if (_hkViewLogsId == -1) failed.Add(("view logs", _cfg.HotkeyViewLogs));
        if (_hkRestartId == -1) failed.Add(("soft restart", _cfg.HotkeyRestart));

        // Optional per-toggle hotkeys (all default unbound). WM_HOTKEY arrives
        // on the window's message loop — the UI thread — so flipping Cfg here
        // is safe, and the settings tracker logs each flip automatically.
        RegisterToggleHotkey(_cfg.HotkeyDesktopOnly, "Desktop Only", failed,
            () => _cfg.DesktopOnly, v => _cfg.DesktopOnly = v);
        RegisterToggleHotkey(_cfg.HotkeyDebugOverlay, "Debug Overlay", failed,
            () => _cfg.DebugOverlay, v => _cfg.DebugOverlay = v);
        RegisterToggleHotkey(_cfg.HotkeyApplyAll, "Apply To All Zones", failed,
            () => _cfg.ApplyAllZones, v => _cfg.ApplyAllZones = v);
        RegisterToggleHotkey(_cfg.HotkeyOverrideExceptions, "Override Exceptions", failed,
            () => _cfg.OverrideExceptions, v => _cfg.OverrideExceptions = v);
        RegisterToggleHotkey(_cfg.HotkeyConfirmExit, "Confirm Before Exit", failed,
            () => _cfg.ConfirmBeforeExit, v => _cfg.ConfirmBeforeExit = v);
        RegisterToggleHotkey(_cfg.HotkeyLockOverlays, "Lock Overlays", failed,
            () => _cfg.OverlaysLocked, v => _cfg.OverlaysLocked = v);
        RegisterToggleHotkey(_cfg.HotkeyGuisOnTop, "Keep GUIs On Top", failed,
            () => _cfg.GuiAlwaysOnTop, v => _cfg.GuiAlwaysOnTop = v);
        RegisterToggleHotkey(_cfg.HotkeyStealth, "Stealth Mode", failed,
            () => _cfg.StealthMode, v => _cfg.StealthMode = v);
        RegisterToggleHotkey(_cfg.HotkeyZonesStealth, "Zones Trigger Stealth", failed,
            () => _cfg.ZonesTriggerStealth, v => _cfg.ZonesTriggerStealth = v);

        if (failed.Count > 0)
        {
            foreach (var f in failed) Log.Error($"Failed to bind {f.What}: {f.Key}", "Hotkey");
            if (!isRetry)   // one warning, not one per retry tick
                ToastService.Show(
                    $"Hotkeys In Use By Another App: {string.Join(", ", failed.Select(f => f.Key))} — Retrying Every 30s Until They're Free",
                    ToastType.Warning, 6500);
            EnsureHotkeyRetry();
        }
        else if (_hotkeyRetryPending)
        {
            StopHotkeyRetry();
            Log.Info("Hotkeys recovered — the conflicting program released them", "Hotkey");
            ToastService.Show("Hotkeys Bound — The Conflict Cleared", ToastType.Success);
        }

        Log.Info($"Hotkeys: toggle={_cfg.HotkeyToggle} exit={_cfg.HotkeyExit} config={_cfg.HotkeyConfig} kill={_cfg.HotkeyKill} (exit/kill on the e-stop thread{(EStop.KillBound ? "" : $" — KILL NOT BOUND, error {EStop.KillError}")})", "Hotkey");
    }

    // ── Hotkey Retry (grabs F1/F10 the moment the other program closes) ──
    private DispatcherTimer? _hotkeyRetryTimer;
    private bool _hotkeyRetryPending;

    private void EnsureHotkeyRetry()
    {
        _hotkeyRetryPending = true;
        _hotkeyRetryTimer ??= CreateHotkeyRetryTimer();
        if (!_hotkeyRetryTimer.IsEnabled) _hotkeyRetryTimer.Start();
    }

    private DispatcherTimer CreateHotkeyRetryTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(_cfg.HotkeyRetrySeconds, 5, 600)) };   // S28 J: user-set
        timer.Tick += (_, _) => BindHotkeys(isRetry: true);
        return timer;
    }

    private void StopHotkeyRetry()
    {
        _hotkeyRetryPending = false;
        _hotkeyRetryTimer?.Stop();
    }

    /// <summary>Hotkeys may be cleared (unbound): -2 = intentionally unbound, -1 = bind failure.</summary>
    private int RegisterIfSet(string hotkey, Action callback)
        => string.IsNullOrWhiteSpace(hotkey) ? -2 : Hotkeys.Register(hotkey, callback);

    /// <summary>Register one optional toggle hotkey: pressing it flips the
    /// setting, toasts the new state (the GUI is usually hidden when a global
    /// hotkey fires), and updates the status bubble. Empty = stay unbound;
    /// failures join the shared 30s retry list.</summary>
    private void RegisterToggleHotkey(string hotkey, string label,
        List<(string What, string Key)> failed, Func<bool> get, Action<bool> set)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return;
        int id = Hotkeys.Register(hotkey, () =>
        {
            bool now = !get();
            set(now);
            StatusText = $"{label}: {(now ? "On" : "Off")} (Hotkey)";
            ToastService.Show($"{label} {(now ? "On" : "Off")}", ToastType.Info);
        });
        if (id == -1) failed.Add((label, hotkey));
        else _toggleHotkeyIds.Add(id);
    }

    // ── Hotkey Callbacks ────────────────────────────────────────────────
    /// <summary>Windows pinned BY OnTop this run (the viewer badges these; pins
    /// made before OnTop started or by other tools show without the badge).</summary>
    public readonly HashSet<IntPtr> PinnedByUs = new();

    private void OnHotkeyToggle() => TogglePin(Win32.GetForegroundWindow());

    /// <summary>Toggle always-on-top on a window (focused, or under the cursor —
    /// S28). Never our own GUI; exceptions honored; logged + toasted; stats.</summary>
    public void TogglePin(IntPtr fg)
    {
        if (fg == IntPtr.Zero) return;
        if (Win32.IsOwnWindow(fg)) return;         // never pin OnTop's own GUI
        if (IsExceptionProcess(fg, "toggle")) return;
        Win32.ToggleTopmost(fg);
        bool isNow = Win32.IsTopmost(fg);
        if (!isNow) RevertClickThrough(fg);   // S28
        if (isNow) { LastPinned = fg; ApplyPinMemory(fg); } else RememberPin(fg);   // S28 Stage I
        if (isNow) PinnedByUs.Add(fg); else PinnedByUs.Remove(fg);
        if (isNow) Stats.BumpPin(Win32.GetProcessName(fg) ?? ""); else Stats.BumpUnpin();
        string name = Win32.GetProcessName(fg) ?? "window";
        Log.Action($"Always on top {(isNow ? "ON" : "OFF")} — {name}");
        ToastService.Show($"Always On Top {(isNow ? "ON" : "OFF")} — {name}",
            isNow ? ToastType.Success : ToastType.Info);
    }

    /// <summary>Unpin one window (from the Pinned Windows viewer). Never our own.</summary>
    public void Unpin(IntPtr hwnd)
    {
        if (Win32.IsOwnWindow(hwnd)) return;
        if (!Win32.IsWindow(hwnd)) { PinnedByUs.Remove(hwnd); _clickThrough.Remove(hwnd); return; }
        Win32.SetTopmost(hwnd, false);
        RevertClickThrough(hwnd);   // S28: never leave a click-through window behind
        PinnedByUs.Remove(hwnd);
        Stats.BumpUnpin();
        string name = Win32.GetProcessName(hwnd) ?? "window";
        Log.Action($"Unpinned — {name}", "Pins");
        ToastService.Show($"Unpinned — {name}", ToastType.Info);
    }

    /// <summary>Unpin every window OnTop pinned this run. Returns the count.
    /// Skips OnTop's own windows so the pins/settings GUI can't bury itself.</summary>
    public int UnpinAllOurs()
    {
        int n = 0;
        foreach (var h in PinnedByUs.ToList())
        {
            if (Win32.IsOwnWindow(h)) { PinnedByUs.Remove(h); continue; }
            if (Win32.IsWindow(h)) { Win32.SetTopmost(h, false); n++; }
            PinnedByUs.Remove(h);
        }
        if (n > 0) { Log.Action($"Unpinned all OnTop pins ({n})", "Pins"); Stats.BumpUnpin(n); }
        ToastService.Show(n > 0 ? $"Unpinned {n} OnTop Pin{(n == 1 ? "" : "s")}" : "No OnTop Pins To Remove",
            ToastType.Info);
        return n;
    }

    private void OnHotkeyExit()
    {
        var fg = Win32.GetForegroundWindow();
        if (IsExceptionProcess(fg, "exit")) return;
        Log.Action("Exit hotkey pressed");
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    private void OnHotkeyConfig()
    {
        Application.Current.Dispatcher.Invoke(ToggleSettingsWindow);
    }

    /// <summary>S28 L: the same hard stop the Kill hotkey performs (tray "Kill" item).</summary>
    public void KillNow(string why) => EStop.KillNow(why);

    private void OnHotkeyStealth()
    {
        _cfg.StealthMode = !_cfg.StealthMode;   // App's handler unhooks/re-hooks + toasts
        Log.Action($"Stealth {(_cfg.StealthMode ? "ENABLED" : "DISABLED")} via hotkey", "Hotkey");
    }

    private void OnHotkeyPause()
    {
        DetectionPaused = !DetectionPaused;   // setter raises PauseStateChanged
        Log.Action($"Detection {(DetectionPaused ? "PAUSED" : "RESUMED")}");
    }

    /// <summary>Ctrl+Alt+R — leave Stealth Mode and re-install the hook (for
    /// when you're out of a game that disallowed it). No-op if already hooked.</summary>
    private void OnHotkeyRehook()
    {
        if (_cfg.StealthMode)
        {
            _cfg.StealthMode = false;   // App's handler re-installs the hook + toasts
            Log.Action("Re-hook hotkey — leaving stealth mode");
        }
        else
        {
            ToastService.Show("Mouse Hook Is Already Active", ToastType.Info);
        }
    }

    /// <summary>Pinned Windows hotkey fired — the View opens the viewer.</summary>
    /// <summary>S28: show the main window (welcome tour buttons, etc.).</summary>
    public event Action? MainWindowRequested;
    /// <summary>S28: Low Impact Mode toggled — App sets process priority and
    /// slows its scanners; WindowRuleService slows its scan.</summary>
    public event Action<bool>? LowImpactChanged;
    public void RequestMainWindow() => Application.Current.Dispatcher.Invoke(() => MainWindowRequested?.Invoke());

    /// <summary>S28 H: hold-to-peek (true = held, false = released). App routes to OverlayService.PeekHold.</summary>
    public event Action<bool>? PeekHoldRequested;
    public void RequestPeekHold(bool held) => PeekHoldRequested?.Invoke(held);
    public event Action? CheatSheetRequested;
    public event Action? PaletteRequested;

    // ── S28 Stage G: pin extras (alpha + click-through + corners). Foreign windows
    // get layered-alpha / ex-style / position changes only — still no input, no memory. ──
    private static IntPtr RootOf(IntPtr h) { var r = Win32.GetAncestor(h, Win32.GA_ROOT); return r == IntPtr.Zero ? h : r; }

    /// <summary>Change a window's opacity by delta percent (clamped 20–100).
    /// Applies to any top-level window (pinned or not); 100% removes layering.</summary>
    public void NudgePinOpacity(IntPtr hwnd, int deltaPct)
    {
        hwnd = RootOf(hwnd);
        if (hwnd == IntPtr.Zero || Win32.IsOwnWindow(hwnd) || !Win32.IsWindow(hwnd)) return;
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        int cur = 100;
        if ((ex & Win32.WS_EX_LAYERED) != 0 && Win32.GetLayeredWindowAttributes(hwnd, out _, out byte a, out uint fl) && (fl & Win32.LWA_ALPHA) != 0) cur = a * 100 / 255;
        int next = Math.Clamp(cur + deltaPct, 20, 100);
        SetPinOpacity(hwnd, next);
    }

    public void SetPinOpacity(IntPtr hwnd, int pct)
    {
        hwnd = RootOf(hwnd);
        if (hwnd == IntPtr.Zero || Win32.IsOwnWindow(hwnd) || !Win32.IsWindow(hwnd)) return;
        pct = Math.Clamp(pct, 20, 100);
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        if (pct >= 100)
        {
            if ((ex & Win32.WS_EX_LAYERED) != 0) { Win32.SetLayeredWindowAttributes(hwnd, 0, 255, Win32.LWA_ALPHA); if (!_clickThrough.Contains(hwnd)) Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex & ~Win32.WS_EX_LAYERED); }
        }
        else
        {
            if ((ex & Win32.WS_EX_LAYERED) == 0) Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_LAYERED);
            Win32.SetLayeredWindowAttributes(hwnd, 0, (byte)(pct * 255 / 100), Win32.LWA_ALPHA);
        }
        string name = Win32.GetProcessName(hwnd) ?? "window";
        Log.Action($"Window opacity {pct}% — {name}", "Pins");
        ToastService.Show($"Opacity {pct}% — {name}", ToastType.Info, 1500);
    }

    /// <summary>Click-through: clicks pass to whatever is beneath. The same key
    /// reverts it; unpinning reverts it; never our own windows.</summary>
    public void ToggleClickThrough(IntPtr hwnd)
    {
        hwnd = RootOf(hwnd);
        if (hwnd == IntPtr.Zero || Win32.IsOwnWindow(hwnd) || !Win32.IsWindow(hwnd)) return;
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        bool on = (ex & Win32.WS_EX_TRANSPARENT) != 0;
        string name = Win32.GetProcessName(hwnd) ?? "window";
        if (on)
        {
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex & ~Win32.WS_EX_TRANSPARENT);
            _clickThrough.Remove(hwnd);
            SyncClickThroughBorder(hwnd, false);
            Log.Action($"Click-through OFF — {name}", "Pins");
            ToastService.Show($"Click-Through OFF — {name}", ToastType.Info);
        }
        else
        {
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_LAYERED);
            _clickThrough.Add(hwnd);
            SyncClickThroughBorder(hwnd, true);
            Log.Action($"Click-through ON — {name} (press the hotkey again over it, or unpin, to revert)", "Pins");
            ToastService.Show($"Click-Through ON — {name}. Same Hotkey Over It (Or Unpin) Reverts.", ToastType.Warning, 5000);
        }
    }
    /// <summary>S28 K: current opacity % of a window (100 if not layered).</summary>
    public int GetOpacityPct(IntPtr hwnd)
    {
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        if ((ex & Win32.WS_EX_LAYERED) != 0 && Win32.GetLayeredWindowAttributes(hwnd, out _, out byte a, out uint fl) && (fl & Win32.LWA_ALPHA) != 0) return a * 100 / 255;
        return 100;
    }
    public bool IsClickThrough(IntPtr hwnd) => _clickThrough.Contains(hwnd);
    private void SyncClickThroughBorder(IntPtr hwnd, bool on)
    {
        if (on && _cfg.ClickThroughBorder && !_ctBorders.ContainsKey(hwnd))
        {
            var b = new Views.ClickThroughBorderWindow(hwnd);
            b.Closed += (_, _) => _ctBorders.Remove(hwnd);
            _ctBorders[hwnd] = b; b.Show();
        }
        else if (!on && _ctBorders.TryGetValue(hwnd, out var existing)) { _ctBorders.Remove(hwnd); existing.Close(); }
    }
    /// <summary>Re-evaluate every border after the setting flips.</summary>
    public void RefreshClickThroughBorders()
    {
        foreach (var h in _clickThrough.ToList()) SyncClickThroughBorder(h, _cfg.ClickThroughBorder);
    }

    public void ToggleClickThroughUnderCursor()
    {
        Win32.GetCursorPos(out var pt);
        var h = Win32.WindowFromPoint(pt);   // WindowFromPoint skips WS_EX_TRANSPARENT windows — so check our tracked set first
        foreach (var t in _clickThrough.ToList())
        {
            if (!Win32.IsWindow(t)) { _clickThrough.Remove(t); continue; }
            if (Win32.GetWindowRect(t, out var rc) && pt.X >= rc.Left && pt.X < rc.Right && pt.Y >= rc.Top && pt.Y < rc.Bottom) { ToggleClickThrough(t); return; }
        }
        if (h != IntPtr.Zero) ToggleClickThrough(h);
    }
    /// <summary>Revert click-through we applied (called from every unpin path).</summary>
    private void RevertClickThrough(IntPtr hwnd)
    {
        if (!_clickThrough.Contains(hwnd)) return;
        SyncClickThroughBorder(hwnd, false);
        if (Win32.IsWindow(hwnd)) { int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE); Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex & ~Win32.WS_EX_TRANSPARENT); }
        _clickThrough.Remove(hwnd);
    }

    /// <summary>Hop the window to the next corner of its current monitor (TL → TR → BR → BL), keeping its size.</summary>
    public void CycleCorner(IntPtr hwnd)
    {
        hwnd = RootOf(hwnd);
        if (hwnd == IntPtr.Zero || Win32.IsOwnWindow(hwnd) || !Win32.IsWindow(hwnd) || !Win32.GetWindowRect(hwnd, out var rc)) return;
        var scr = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(rc.Left + 10, rc.Top + 10)).WorkingArea;
        int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
        bool left = rc.Left - scr.Left < scr.Right - rc.Right, top = rc.Top - scr.Top < scr.Bottom - rc.Bottom;
        int corner = (top, left) switch { (true, true) => 1, (true, false) => 2, (false, false) => 3, _ => 0 };   // next
        int nx = corner is 0 or 1 ? scr.Left : scr.Right - w, ny = corner is 0 or 1 ? scr.Top : scr.Bottom - h;
        if (corner == 1) nx = scr.Right - w; if (corner == 3) nx = scr.Left;
        Win32.SetWindowPos(hwnd, IntPtr.Zero, nx, ny, 0, 0, Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        Log.Action($"Window moved to corner {new[] { "TL", "TR", "BR", "BL" }[corner]} — {Win32.GetProcessName(hwnd) ?? "window"}", "Pins");
    }

    // ── S28 Stage G: process → profile rules (ticked by App's 5 s watcher) ──
    private string? _profileBeforeRule;
    private string? _ruleProfileActive;
    public void TickProfileRules()
    {
        if (!_cfg.ProfileRulesEnabled || _cfg.ProfileRules.Count == 0) return;
        try
        {
            ProfileRule? hit = null;
            foreach (var r in _cfg.ProfileRules)
            {
                if (!r.Enabled || r.Process.Trim().Length == 0 || r.Profile.Trim().Length == 0) continue;
                string n = r.Process.Trim(); if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
                var ps = Process.GetProcessesByName(n); bool any = ps.Length > 0; foreach (var p in ps) p.Dispose();
                if (any) { hit = r; break; }
            }
            if (hit is not null)
            {
                if (_ruleProfileActive is null && !string.Equals(_cfg.ActiveProfile, hit.Profile, StringComparison.OrdinalIgnoreCase))
                {
                    _profileBeforeRule = _cfg.ActiveProfile;
                    if (SwitchProfile(hit.Profile)) { _ruleProfileActive = hit.Profile; Log.Action($"Profile rule: '{hit.Process}' running → profile '{hit.Profile}'", "Profiles"); }
                }
            }
            else if (_ruleProfileActive is not null)
            {
                var rule = _cfg.ProfileRules.FirstOrDefault(r => string.Equals(r.Profile, _ruleProfileActive, StringComparison.OrdinalIgnoreCase));
                if (rule is { RevertWhenGone: true } && _profileBeforeRule is not null && SwitchProfile(_profileBeforeRule))
                    Log.Action($"Profile rule: process gone → back to '{_profileBeforeRule}'", "Profiles");
                _ruleProfileActive = null; _profileBeforeRule = null;
            }
        }
        catch (Exception ex) { Log.Debug($"Profile rules tick: {ex.Message}", "Profiles"); }
    }

    /// <summary>S28 Stage G: bind the active profile to the current monitor layout.</summary>
    public void BindActiveProfileToDisplays()
    {
        string sig = DisplaySignature.Current();
        _cfg.DisplayProfiles[sig] = _cfg.ActiveProfile;
        Log.Action($"Display layout bound → profile '{_cfg.ActiveProfile}' ({DisplaySignature.Friendly()})", "Profiles");
        ToastService.Show($"This Layout → Profile '{_cfg.ActiveProfile}'", ToastType.Success);
        Settings.SaveNoBackup(_cfg);
    }

    /// <summary>S28 Stage G: DND predicate — routine toasts quiet while guarded.</summary>
    public bool ShouldSuppressRoutineToasts() => _cfg.DoNotDisturb && MacroSvc.FindRunningWatched() is not null;

    /// <summary>S28 Stage G: every current binding, for the cheat sheet.</summary>
    public List<(string Label, string Key)> AllBindings()
    {
        var c = _cfg;
        var list = new List<(string, string)>
        {
            ("Always On Top (focused)", c.HotkeyToggle), ("Pin Window Under Cursor", c.HotkeyPinUnderCursor), ("Opacity +", c.HotkeyPinOpacityUp), ("Opacity −", c.HotkeyPinOpacityDown),
            ("Click-Through Under Cursor", c.HotkeyPinClickThrough), ("Cycle Corner", c.HotkeyPinCycleCorner), ("Exit OnTop", c.HotkeyExit), ("Show/Hide Window", c.HotkeyConfig),
            ("Kill (no cleanup)", c.HotkeyKill), ("Pause Detection", "Ctrl+Alt+P"), ("Re-Enable Mouse Hook", c.HotkeyRehook), ("Quick Record Zone", c.HotkeyQuickRecord),
            ("View Logs", c.HotkeyViewLogs), ("Soft Restart", c.HotkeyRestart), ("Pinned Windows", c.HotkeyPins), ("Peek", c.HotkeyPeek), ("Auto-Pin Manager", c.HotkeyRules),
            ("Cycle Profiles", c.HotkeyProfiles), ("Toggle Stealth", c.HotkeyStealth), ("Macros Window", c.HotkeyMacros), ("Record / Stop Macro", c.HotkeyMacroRecord),
            ("Macro Panic Stop", "Ctrl+Alt+X"), ("Apply All Placements", c.HotkeyPlacement), ("Cheat Sheet", c.HotkeyCheatSheet), ("Command Palette", c.HotkeyPalette),
            ("Hold To Peek", c.HotkeyHoldPeek), ("Hold To Pin", c.HotkeyHoldPin), ("Timed Pin", c.HotkeyPinTimed), ("Send To Next Monitor", c.HotkeySendToMonitor), ("Pause/Resume Macro", c.HotkeyMacroPause), ("Macro Status Strip", c.HotkeyMacroStatus),
            ("Desktop Only", c.HotkeyDesktopOnly), ("Debug Overlay", c.HotkeyDebugOverlay), ("Apply To All Zones", c.HotkeyApplyAll), ("Override Exceptions", c.HotkeyOverrideExceptions),
            ("Confirm Before Exit", c.HotkeyConfirmExit), ("Lock Overlays", c.HotkeyLockOverlays), ("Keep GUIs On Top", c.HotkeyGuisOnTop), ("Zones Trigger Stealth", c.HotkeyZonesStealth),
        };
        if (c.MacrosEnabled) foreach (var m in c.Macros.Where(m => m.Enabled && m.Hotkey.Length > 0)) list.Add(($"Macro: {m.Name}", m.Hotkey));
        if (c.ActionLibraryEnabled) foreach (var a in c.LibraryActions.Where(a => a.Enabled && a.Hotkey.Length > 0)) list.Add(($"Action: {a.Name}", a.Hotkey));
        return list;
    }

    // ── S28 Stage I: pin memory, scenes, timed pin, send-to-monitor ──────
    private void RememberPin(IntPtr hwnd)
    {
        if (!_cfg.PinMemoryEnabled) return;
        string proc = Win32.GetProcessName(hwnd) ?? ""; if (proc.Length == 0) return;
        var e = new PinMemoryEntry { ClickThrough = _clickThrough.Contains(hwnd) };
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        if ((ex & Win32.WS_EX_LAYERED) != 0 && Win32.GetLayeredWindowAttributes(hwnd, out _, out byte a, out uint fl) && (fl & Win32.LWA_ALPHA) != 0) e.Opacity = a * 100 / 255;
        if (Win32.GetWindowRect(hwnd, out var rc)) { e.Left = rc.Left; e.Top = rc.Top; e.W = rc.Right - rc.Left; e.H = rc.Bottom - rc.Top; }
        _cfg.PinMemory[proc.ToLowerInvariant()] = e;
    }
    private void ApplyPinMemory(IntPtr hwnd)
    {
        if (!_cfg.PinMemoryEnabled) return;
        string proc = (Win32.GetProcessName(hwnd) ?? "").ToLowerInvariant();
        if (!_cfg.PinMemory.TryGetValue(proc, out var e)) return;
        if (e.W > 0 && e.H > 0) Win32.SetWindowPos(hwnd, IntPtr.Zero, e.Left, e.Top, e.W, e.H, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        if (e.Opacity < 100) SetPinOpacity(hwnd, e.Opacity);
        if (e.ClickThrough && !_clickThrough.Contains(hwnd)) ToggleClickThrough(hwnd);
        Log.Info($"Pin memory applied — {proc}", "Pins");
    }
    /// <summary>Snapshot every OnTop pin (geometry/opacity/click-through) as a named scene.</summary>
    public PinScene SaveScene(string name)
    {
        var sc = new PinScene { Name = name };
        foreach (var h in PinnedByUs.ToList())
        {
            if (!Win32.IsWindow(h) || !Win32.GetWindowRect(h, out var rc)) continue;
            var it = new PinSceneItem { Process = Win32.GetProcessName(h) ?? "", TitleContains = Win32.GetWindowTitle(h), Left = rc.Left, Top = rc.Top, W = rc.Right - rc.Left, H = rc.Bottom - rc.Top, ClickThrough = _clickThrough.Contains(h) };
            int ex = Win32.GetWindowLong(h, Win32.GWL_EXSTYLE);
            if ((ex & Win32.WS_EX_LAYERED) != 0 && Win32.GetLayeredWindowAttributes(h, out _, out byte a, out uint fl) && (fl & Win32.LWA_ALPHA) != 0) it.Opacity = a * 100 / 255;
            if (it.TitleContains.Length > 40) it.TitleContains = it.TitleContains[..40];
            sc.Items.Add(it);
        }
        _cfg.PinScenes.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        _cfg.PinScenes.Add(sc);
        Settings.SaveNoBackup(_cfg);
        Log.Action($"Pin scene saved: {name} ({sc.Items.Count} windows)", "Pins");
        ToastService.Show($"Scene '{name}' Saved ({sc.Items.Count} Windows)", ToastType.Success);
        return sc;
    }
    public void ApplyScene(PinScene sc, string trigger)
    {
        int n = 0;
        var wins = Win32.GetVisibleWindows();
        foreach (var it in sc.Items)
        {
            var hit = wins.FirstOrDefault(w => string.Equals(w.Process, it.Process, StringComparison.OrdinalIgnoreCase) && (it.TitleContains.Length == 0 || w.Title.IndexOf(it.TitleContains, StringComparison.OrdinalIgnoreCase) >= 0));
            if (hit.Hwnd == IntPtr.Zero || Win32.IsOwnWindow(hit.Hwnd)) { hit = wins.FirstOrDefault(w => string.Equals(w.Process, it.Process, StringComparison.OrdinalIgnoreCase)); if (hit.Hwnd == IntPtr.Zero) continue; }
            var h = hit.Hwnd;
            Win32.SetWindowPos(h, IntPtr.Zero, it.Left, it.Top, Math.Max(50, it.W), Math.Max(30, it.H), Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            if (!Win32.IsTopmost(h)) { Win32.SetTopmost(h, true); PinnedByUs.Add(h); Stats.BumpPin(it.Process); }
            if (it.Opacity < 100) SetPinOpacity(h, it.Opacity);
            if (it.ClickThrough && !_clickThrough.Contains(h)) ToggleClickThrough(h);
            n++;
        }
        Log.Action($"Pin scene applied ({trigger}): {sc.Name} — {n}/{sc.Items.Count} windows", "Pins");
        ToastService.Show($"Scene '{sc.Name}': {n} Of {sc.Items.Count} Windows", n > 0 ? ToastType.Success : ToastType.Warning);
    }
    private readonly Dictionary<IntPtr, DispatcherTimer> _timedPins = new();
    public void PinTimed(IntPtr fg)
    {
        if (fg == IntPtr.Zero || Win32.IsOwnWindow(fg) || IsExceptionProcess(fg, "toggle")) return;
        int secs = Math.Clamp(_cfg.PinTimedSeconds, 3, 3600);
        if (_timedPins.TryGetValue(fg, out var old)) old.Stop();
        if (!Win32.IsTopmost(fg)) { Win32.SetTopmost(fg, true); PinnedByUs.Add(fg); Stats.BumpPin(Win32.GetProcessName(fg) ?? ""); }
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(secs) };
        t.Tick += (_, _) => { t.Stop(); _timedPins.Remove(fg); if (Win32.IsWindow(fg)) Unpin(fg); Log.Action("Timed pin expired", "Pins"); ToastService.Show("Timed Pin Released", ToastType.Info); };
        _timedPins[fg] = t; t.Start();
        Log.Action($"Timed pin {secs}s — {Win32.GetProcessName(fg) ?? "window"}", "Pins");
        ToastService.Show($"Pinned For {secs}s — {Win32.GetProcessName(fg) ?? "window"}", ToastType.Success);
    }
    public void SendToNextMonitor(IntPtr fg)
    {
        fg = RootOf(fg);
        if (fg == IntPtr.Zero || Win32.IsOwnWindow(fg) || !Win32.GetWindowRect(fg, out var rc)) return;
        var screens = System.Windows.Forms.Screen.AllScreens; if (screens.Length < 2) { ToastService.Show("Only One Monitor", ToastType.Info); return; }
        int cur = 0; for (int i = 0; i < screens.Length; i++) if (screens[i].Bounds.Contains(rc.Left + 10, rc.Top + 10)) { cur = i; break; }
        var a = screens[cur].WorkingArea; var b = screens[(cur + 1) % screens.Length].WorkingArea;
        double fx = (rc.Left - a.Left) / (double)Math.Max(1, a.Width), fy = (rc.Top - a.Top) / (double)Math.Max(1, a.Height);
        double fw = (rc.Right - rc.Left) / (double)Math.Max(1, a.Width), fh = (rc.Bottom - rc.Top) / (double)Math.Max(1, a.Height);
        Win32.ShowWindowAsync(fg, Win32.SW_RESTORE);
        Win32.SetWindowPos(fg, IntPtr.Zero, b.Left + (int)(fx * b.Width), b.Top + (int)(fy * b.Height), Math.Max(100, (int)(fw * b.Width)), Math.Max(60, (int)(fh * b.Height)), Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        Log.Action($"Sent to monitor {(cur + 1) % screens.Length + 1} — {Win32.GetProcessName(fg) ?? "window"}", "Pins");
    }

    // ── S28 Stage I: idle rule + profile schedules (watcher tick) ────────
    private bool _idleFired; private string? _profileBeforeIdle, _profileBeforeSched, _schedActive;
    public void TickIdleAndSchedules()
    {
        try
        {
            if (_cfg.IdleRuleEnabled && _cfg.IdleAction != 0)
            {
                bool idle = Win32.IdleMilliseconds() >= (uint)_cfg.IdleMinutes * 60_000u;
                if (idle && !_idleFired)
                {
                    _idleFired = true;
                    switch (_cfg.IdleAction)
                    {
                        case 1: if (_cfg.IdleProfile.Length > 0 && !string.Equals(_cfg.ActiveProfile, _cfg.IdleProfile, StringComparison.OrdinalIgnoreCase)) { _profileBeforeIdle = _cfg.ActiveProfile; SwitchProfile(_cfg.IdleProfile); } break;
                        case 2: UnpinAllOurs(); break;
                        case 3: _cfg.StealthMode = true; break;
                    }
                    Log.Action($"Idle rule fired after {_cfg.IdleMinutes} min (action {_cfg.IdleAction})", "System");
                }
                else if (!idle && _idleFired)
                {
                    _idleFired = false;
                    if (_profileBeforeIdle is not null) { SwitchProfile(_profileBeforeIdle); _profileBeforeIdle = null; Log.Action("Activity resumed — idle profile reverted", "System"); }
                }
            }
            if (_cfg.ProfileSchedulesEnabled && _cfg.ProfileSchedules.Count > 0 && _ruleProfileActive is null)
            {
                var now = DateTime.Now;
                var hit = _cfg.ProfileSchedules.FirstOrDefault(s => s.IsActive(now) && s.Profile.Length > 0);
                if (hit is not null && _schedActive is null && !string.Equals(_cfg.ActiveProfile, hit.Profile, StringComparison.OrdinalIgnoreCase))
                { _profileBeforeSched = _cfg.ActiveProfile; if (SwitchProfile(hit.Profile)) { _schedActive = hit.Profile; Log.Action($"Schedule: {hit.DaysText} {hit.StartText}-{hit.EndText} → profile '{hit.Profile}'", "Profiles"); } }
                else if (hit is null && _schedActive is not null)
                { if (_profileBeforeSched is not null) SwitchProfile(_profileBeforeSched); Log.Action("Schedule ended — profile reverted", "Profiles"); _schedActive = null; _profileBeforeSched = null; }
            }
        }
        catch (Exception ex) { Log.Debug($"Idle/schedule tick: {ex.Message}", "System"); }
    }

    /// <summary>S28: pin/unpin the top-level window under the mouse cursor.</summary>
    public void TogglePinUnderCursor()
    {
        Win32.GetCursorPos(out var pt);
        var h = Win32.WindowFromPoint(pt);
        if (h == IntPtr.Zero) return;
        var root = Win32.GetAncestor(h, Win32.GA_ROOT);
        TogglePin(root == IntPtr.Zero ? h : root);
    }

    /// <summary>S28 Stage F — THE library dispatcher. Every kind routes to an
    /// existing guarded verb; the library adds reachability, never power.</summary>
    public void RunLibraryAction(LibraryAction a, string trigger)
    {
        if (!_cfg.ActionLibraryEnabled) { ToastService.Show("Action Library Is Off", ToastType.Warning); return; }
        if (!a.Enabled) { ToastService.Show($"Action '{a.Name}' Is Disabled", ToastType.Info); return; }
        Log.Action($"Library action '{a.Name}' ({trigger}): {a.Summary}", "Zones");
        switch (a.Kind)
        {
            case LibraryKind.RunCommand: LaunchViaShell(a.Command, $"action '{a.Name}'"); break;
            case LibraryKind.KillProcess: KillZoneProcess(new Zone { Name = a.Name, ActionProcess = a.Process }); break;
            case LibraryKind.ToggleWindow: ToggleZoneWindow(new Zone { Name = a.Name, ActionProcess = a.Process }); break;
            case LibraryKind.RunMacro:
                var mac = _cfg.Macros.Find(m => m.Id == a.MacroId);
                if (mac is null) ToastService.Show($"Action '{a.Name}': Macro Not Found", ToastType.Warning); else MacroSvc.Play(mac);
                break;
            case LibraryKind.ApplyPlacement:
                var rule = Placements.FirstOrDefault(p => p.Id == a.PlacementId);
                if (rule is null) ToastService.Show($"Action '{a.Name}': Placement Not Found", ToastType.Warning); else _ = PlacementSvc.ApplyAsync(rule, $"action '{a.Name}'");
                break;
            case LibraryKind.SwitchProfile: if (!SwitchProfile(a.ProfileName)) ToastService.Show($"Action '{a.Name}': Profile '{a.ProfileName}' Not Found Or Already Active", ToastType.Info); break;
            case LibraryKind.PauseToggle: DetectionPaused = !DetectionPaused; Log.Action($"Detection {(DetectionPaused ? "PAUSED" : "RESUMED")} via action"); break;
            case LibraryKind.Peek: RequestPeek(); break;
            case LibraryKind.StealthToggle: _cfg.StealthMode = !_cfg.StealthMode; Log.Action($"Stealth {(_cfg.StealthMode ? "ENABLED" : "DISABLED")} via action", "Hotkey"); break;
            case LibraryKind.PinUnderCursor: TogglePinUnderCursor(); break;
            case LibraryKind.ApplyScene:
                var sc = _cfg.PinScenes.Find(x => x.Id == a.PlacementId);
                if (sc is null) ToastService.Show($"Action '{a.Name}': Scene Not Found", ToastType.Warning); else ApplyScene(sc, $"action '{a.Name}'");
                break;
        }
    }

    /// <summary>S28 Stage F: ONE conflict check for every assignment surface
    /// (macros window, library window). Normalized compare across all app
    /// hotkeys, built-ins, every macro, every library action.</summary>
    public string? FindHotkeyConflict(string proposed, string? exceptMacroId = null, string? exceptLibraryId = null)
    {
        static string Norm(string k) => (k ?? "").Trim().Replace(" ", "").ToUpperInvariant();
        string key = Norm(proposed);
        var c = _cfg;
        var all = new (string Label, string Key)[]
        {
            ("Pause (Built-In)", "Ctrl+Alt+P"), ("Macro Panic Stop (Built-In)", "Ctrl+Alt+X"),
            ("Always On Top Toggle", c.HotkeyToggle), ("Exit", c.HotkeyExit), ("Open Config", c.HotkeyConfig),
            ("Kill Switch", c.HotkeyKill), ("Quick Record", c.HotkeyQuickRecord), ("View Logs", c.HotkeyViewLogs),
            ("Restart", c.HotkeyRestart), ("Re-Hook", c.HotkeyRehook), ("Pinned Windows", c.HotkeyPins),
            ("Peek", c.HotkeyPeek), ("Auto-Pin Manager", c.HotkeyRules), ("Cycle Profiles", c.HotkeyProfiles),
            ("Toggle Stealth", c.HotkeyStealth), ("Macros Window", c.HotkeyMacros), ("Record / Stop Macro", c.HotkeyMacroRecord),
            ("Apply All Placements", c.HotkeyPlacement), ("Pin Under Cursor", c.HotkeyPinUnderCursor), ("Opacity +", c.HotkeyPinOpacityUp), ("Opacity −", c.HotkeyPinOpacityDown),
            ("Click-Through", c.HotkeyPinClickThrough), ("Cycle Corner", c.HotkeyPinCycleCorner), ("Cheat Sheet", c.HotkeyCheatSheet), ("Command Palette", c.HotkeyPalette),
            ("Hold To Peek", c.HotkeyHoldPeek), ("Hold To Pin", c.HotkeyHoldPin), ("Timed Pin", c.HotkeyPinTimed), ("Send To Next Monitor", c.HotkeySendToMonitor), ("Pause/Resume Macro", c.HotkeyMacroPause), ("Macro Status Strip", c.HotkeyMacroStatus), ("Desktop Only", c.HotkeyDesktopOnly),
            ("Debug Overlay", c.HotkeyDebugOverlay), ("Apply To All Zones", c.HotkeyApplyAll),
            ("Override Exceptions", c.HotkeyOverrideExceptions), ("Confirm Before Exit", c.HotkeyConfirmExit),
            ("Lock Overlays", c.HotkeyLockOverlays), ("Keep GUIs On Top", c.HotkeyGuisOnTop), ("Zones Trigger Stealth", c.HotkeyZonesStealth),
        };
        foreach (var (label, k) in all)
            if (Norm(k) == key) return $"Hotkey Conflict: \"{proposed}\" Is Already {label} — Each Combo Can Only Do One Thing";
        foreach (var m in c.Macros)
            if (m.Id != exceptMacroId && Norm(m.Hotkey) == key) return $"Hotkey Conflict: \"{proposed}\" Is Already Macro \"{m.Name}\"";
        foreach (var a in c.LibraryActions)
            if (a.Id != exceptLibraryId && Norm(a.Hotkey) == key) return $"Hotkey Conflict: \"{proposed}\" Is Already Action \"{a.Name}\"";
        return null;
    }

    private void OnHotkeyPlacement()
    {
        Log.Action("Apply placements hotkey", "Hotkey");
        Application.Current.Dispatcher.InvokeAsync(async () => await PlacementSvc.ApplyAllAsync("hotkey"));
    }

    private void OnHotkeyMacroRecord()
    {
        Log.Action("Macro record/stop hotkey", "Hotkey");
        Application.Current.Dispatcher.Invoke(() => MacroSvc.ToggleRecording());
    }

    /// <summary>S28: open the Macros window (optional hotkey / tray).</summary>
    public event Action? MacrosRequested;
    private void OnHotkeyMacros()
    {
        Log.Action("Macros window hotkey", "Hotkey");
        Application.Current.Dispatcher.Invoke(() => MacrosRequested?.Invoke());
    }

    public event Action? PinsRequested;
    private void OnHotkeyPins()
    {
        Log.Action("Pinned Windows hotkey pressed");
        Application.Current.Dispatcher.Invoke(() => PinsRequested?.Invoke());
    }

    /// <summary>Peek hotkey fired — the App flashes all zones on the overlay.</summary>
    public event Action? PeekRequested;
    private void OnHotkeyPeek()
    {
        Log.Action("Peek hotkey pressed");
        Stats.BumpPeek();
        Application.Current.Dispatcher.Invoke(() => PeekRequested?.Invoke());
    }

    /// <summary>GUI path to the same peek (APP section button).</summary>
    public void RequestPeek()
    {
        Log.Action("Peek requested (GUI)");
        Stats.BumpPeek();
        PeekRequested?.Invoke();
    }

    /// <summary>GUI path to reopen the overlay legend (tray also has one).</summary>
    public event Action? LegendRequested;
    public void RequestLegend()
    {
        Log.Action("Overlay legend requested (GUI)");
        LegendRequested?.Invoke();
    }

    /// <summary>Window Rules hotkey fired — the View opens the rules manager.</summary>
    public event Action? RulesRequested;
    private void OnHotkeyRules()
    {
        Log.Action("Window Rules hotkey pressed");
        Application.Current.Dispatcher.Invoke(() => RulesRequested?.Invoke());
    }

    /// <summary>Welcome-tour offer: four corner zones on the primary monitor
    /// (inset so they're visible), grouped "Starter", one undo step.</summary>
    public void AddStarterZones()
    {
        var b = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        const int inset = 60;
        PushUndo();
        (int x, int y, string name)[] corners =
        {
            (b.Left + inset,  b.Top + inset,    "Starter TL"),
            (b.Right - inset, b.Top + inset,    "Starter TR"),
            (b.Left + inset,  b.Bottom - inset, "Starter BL"),
            (b.Right - inset, b.Bottom - inset, "Starter BR"),
        };
        foreach (var c in corners)
        {
            var z = AddZoneAt(c.x, c.y, pushUndo: false);   // one undo for all four
            z.Name = c.name;
            z.Group = "Starter";
        }
        RefreshGroupNames();
        Log.Action("Starter zones added (welcome tour)", "Zones");
        ToastService.Show("Added 4 Starter Zones (Group: Starter)", ToastType.Success);
    }

    // ── Zone drag preview (map → overlay) ───────────────────────────────
    /// <summary>While a zone is dragged on a map, the overlay shows it live at
    /// full opacity; on release it lingers ~3 s then restores.
    /// OverlayService subscribes.</summary>
    public event Action<Zone>? ZonePreviewStarted;
    public event Action? ZonePreviewEnded;
    public void BeginZonePreviewFor(Zone z) => ZonePreviewStarted?.Invoke(z);
    public void EndZonePreview() => ZonePreviewEnded?.Invoke();

    // ── Display-configuration awareness ─────────────────────────────────
    /// <summary>Raised (on the UI thread) after a display-config change was
    /// processed, so maps can repaint their monitor outlines.</summary>
    public event Action? DisplaysChanged;

    /// <summary>True if the zone's anchor sits on any current monitor.</summary>
    private static bool IsOnAnyMonitor(Zone z)
    {
        foreach (var scr in System.Windows.Forms.Screen.AllScreens)
            if (scr.Bounds.Contains(z.X, z.Y)) return true;
        return false;
    }

    /// <summary>Called by App on WM_DISPLAYCHANGE (marshalled to UI thread):
    /// re-evaluates zone health, optionally auto-disables newly-off-screen
    /// zones (Cfg.AutoDisableOffscreenZones), refreshes maps, and reports.</summary>
    public void OnDisplaysChanged()
    {
        int monitors = System.Windows.Forms.Screen.AllScreens.Length;
        Log.Info($"Display configuration changed — {monitors} monitor(s) now", "System");
        // S28 Stage G: layout → profile.
        if (_cfg.DisplayProfilesEnabled && _cfg.DisplayProfiles.TryGetValue(DisplaySignature.Current(), out var prof) && SwitchProfile(prof))
            Log.Action($"Display layout matched → profile '{prof}'", "Profiles");

        var disabled = new List<string>();
        if (_cfg.AutoDisableOffscreenZones)
        {
            foreach (var z in Zones.Where(z => z.Enabled && !IsOnAnyMonitor(z)))
            {
                z.Enabled = false;
                disabled.Add(z.Name);
            }
        }

        RecomputeZoneHealth();
        DisplaysChanged?.Invoke();

        if (disabled.Count > 0)
        {
            Log.Action($"Auto-disabled {disabled.Count} off-screen zone(s): {string.Join(", ", disabled)}", "Zones");
            ToastService.Show($"Disabled {disabled.Count} Off-Screen Zone{(disabled.Count == 1 ? "" : "s")}: {string.Join(", ", disabled.Take(3))}{(disabled.Count > 3 ? "…" : "")}", ToastType.Warning);
            SaveAll();
        }
    }

    // ── Profiles (named zone workspaces) ────────────────────────────────
    /// <summary>Raised after any profile change (switch/create/rename/delete)
    /// so the combo, tray, and manager can refresh.</summary>
    public event Action? ProfilesChanged;

    public string ActiveProfileName => _cfg.ActiveProfile;
    public IReadOnlyList<string> ProfileNames => _cfg.Profiles.Select(p => p.Name).ToList();

    /// <summary>Snapshot the live workspace into a Profile record (deep clone).</summary>
    private Profile CaptureLiveWorkspace(string name) => new()
    {
        Name = name,
        Zones = Zones.Select(z => z.Clone()).ToList(),
        Exceptions = Exceptions.Select(e => e.Clone()).ToList(),
        AutoCloseTriggers = AutoCloseTriggers.ToList(),
        DefTolerance = _sliderTol,
        DefCapacity = _sliderCap,
        DefVariation = _sliderVar,
    };

    /// <summary>Write the live workspace into the ACTIVE profile record.</summary>
    private void SyncActiveProfile()
    {
        int i = _cfg.Profiles.FindIndex(p =>
            string.Equals(p.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
        var snap = CaptureLiveWorkspace(_cfg.ActiveProfile);
        // S28: the record is REPLACED wholesale, so profile-level settings that
        // aren't part of the live workspace (MacroPolicy) must be carried over.
        if (i >= 0) { snap.MacroPolicy = _cfg.Profiles[i].MacroPolicy; _cfg.Profiles[i] = snap; }
        else _cfg.Profiles.Add(snap);
    }

    /// <summary>Switch to another profile: save the current workspace into its
    /// record, load the target's (deep clones), refresh everything, persist.</summary>
    public bool SwitchProfile(string name)
    {
        if (string.Equals(name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase)) return false;
        var target = _cfg.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            Log.Warn($"Profile switch failed — \"{name}\" not found", "Profiles");
            return false;
        }

        SyncActiveProfile();                       // save where we are
        _cfg.ActiveProfile = target.Name;

        var load = target.Clone();                 // deep clones into the live side
        // S28 Stage I: a plain-language diff so automation switches are legible.
        try
        {
            var oldNames = new HashSet<string>(Zones.Select(z => z.Name)); var newNames = new HashSet<string>(load.Zones.Select(z => z.Name));
            int added = newNames.Except(oldNames).Count(), removed = oldNames.Except(newNames).Count();
            var prev = _cfg.Profiles.FirstOrDefault(p => string.Equals(p.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
            string macroNote = (prev?.MacroPolicy ?? 0) != target.MacroPolicy ? (target.MacroPolicy == Profile.PolicyDeny ? ", macros now BLOCKED" : ", macros now allowed") : "";
            Log.Info($"Profile diff {_cfg.ActiveProfile} → {target.Name}: zones {load.Zones.Count} (+{added}/−{removed}), exceptions {load.Exceptions.Count}{macroNote}", "Profiles");
            LastProfileDiff = $"+{added} / −{removed} zones{macroNote}";
        }
        catch (Exception ex) { Log.Debug($"Profile diff: {ex.Message}", "Profiles"); }
        Zones.Clear();
        foreach (var z in load.Zones) Zones.Add(z);
        Exceptions.Clear();
        foreach (var e in load.Exceptions) Exceptions.Add(e);
        AutoCloseTriggers.Clear();
        foreach (var t in load.AutoCloseTriggers) AutoCloseTriggers.Add(t);
        SliderTol = load.DefTolerance;
        SliderCap = load.DefCapacity;
        SliderVar = load.DefVariation;

        RefreshGroupNames();
        RecomputeZoneHealth();
        // S28: per-profile macro policy — a DENY profile stops any playback and
        // BindHotkeys (below, via SaveAll's caller path) skips macro binds.
        if (target.MacroPolicy == Profile.PolicyDeny && MacroSvc.PlayingId is not null)
        {
            MacroSvc.Stop();
            Log.Action($"Profile \"{target.Name}\" denies macros — playback stopped", "Recording");
        }
        BindHotkeys();
        SaveAll();
        Stats.BumpProfileSwitch(target.Name);
        Log.Action($"Switched to profile \"{target.Name}\" ({load.Zones.Count} zones)", "Profiles");
        ToastService.Show($"Profile: {target.Name} ({LastProfileDiff})", ToastType.Success);
        ProfilesChanged?.Invoke();
        return true;
    }

    /// <summary>S28 MINIMAL MODE (Standing Design Law): one click turns every
    /// non-core feature off — only always-on-top pinning + zone detection remain.
    /// Logged as an ACTION with the full list; reversible per switch.</summary>
    public void ApplyMinimalMode()
    {
        MacroSvc.Stop();
        _cfg.MacrosEnabled = false;
        _cfg.WindowRulesEnabled = false;
        _cfg.AutoCloseEnabled = false;
        _cfg.UpdateCheckOnStartup = false;
        _cfg.StatsEnabled = false;
        _cfg.ZoneActionsEnabled = false;
        _cfg.StealthWatcherEnabled = false;
        _cfg.AutoDisableOffscreenZones = false;
        _cfg.PlacementEnabled = false;
        _cfg.ActionLibraryEnabled = false;
        _cfg.ProfileRulesEnabled = false;
        _cfg.DisplayProfilesEnabled = false;
        _cfg.HoldChordsEnabled = false;
        _cfg.IdleRuleEnabled = false; _cfg.ProfileSchedulesEnabled = false; _cfg.PinMemoryEnabled = false;
        BindHotkeys();
        SaveAll();
        Log.Action("MINIMAL MODE applied — macros, auto-pin, auto-close, update check, statistics, zone actions, stealth watcher, display auto-disable, window placement all OFF", "Config");
        ToastService.Show("Minimal Mode — Only Pinning + Zone Detection Are Active", ToastType.Success, 6000);
    }

    public string LastProfileDiff { get; private set; } = "";

    /// <summary>S28: the active profile's macro policy (0 Allow / 1 Deny); setting
    /// it writes the record, rebinds hotkeys, stops playback if denying, saves.</summary>
    public int ActiveProfileMacroPolicy
    {
        get
        {
            var p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
            return p?.MacroPolicy ?? Profile.PolicyAllow;
        }
        set
        {
            var p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
            if (p is null) { SyncActiveProfile(); p = _cfg.Profiles.FirstOrDefault(x => string.Equals(x.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase)); }
            if (p is null) return;
            p.MacroPolicy = value == Profile.PolicyDeny ? Profile.PolicyDeny : Profile.PolicyAllow;
            if (p.MacroPolicy == Profile.PolicyDeny) MacroSvc.Stop();
            BindHotkeys();
            Settings.SaveNoBackup(_cfg);
            Log.Action($"Profile \"{p.Name}\" macro policy → {(p.MacroPolicy == Profile.PolicyDeny ? "DENY" : "ALLOW")}", "Recording");
            OnPropertyChanged(nameof(ActiveProfileMacroPolicy));
        }
    }

    /// <summary>Create a new profile (copy of the current workspace) and switch to it.</summary>
    public bool CreateProfile(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 60) return false;
        if (_cfg.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            ToastService.Show($"A Profile Named \"{name}\" Already Exists", ToastType.Warning);
            return false;
        }
        SyncActiveProfile();
        _cfg.Profiles.Add(CaptureLiveWorkspace(name));
        _cfg.ActiveProfile = name;
        SaveAll();
        Log.Action($"Profile created: \"{name}\" (copy of current workspace)", "Profiles");
        ToastService.Show($"Profile Created: {name}", ToastType.Success);
        ProfilesChanged?.Invoke();
        return true;
    }

    public bool RenameProfile(string oldName, string newName)
    {
        newName = (newName ?? "").Trim();
        if (newName.Length is 0 or > 60) return false;
        var p = _cfg.Profiles.FirstOrDefault(x =>
            string.Equals(x.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (p == null) return false;
        if (_cfg.Profiles.Any(x => x != p && string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            ToastService.Show($"A Profile Named \"{newName}\" Already Exists", ToastType.Warning);
            return false;
        }
        bool wasActive = string.Equals(_cfg.ActiveProfile, p.Name, StringComparison.OrdinalIgnoreCase);
        p.Name = newName;
        if (wasActive) _cfg.ActiveProfile = newName;
        SaveAll();
        Log.Action($"Profile renamed: \"{oldName}\" → \"{newName}\"", "Profiles");
        ProfilesChanged?.Invoke();
        return true;
    }

    /// <summary>Delete a profile. The last profile can't be deleted; deleting the
    /// active one switches to the first remaining first.</summary>
    public bool DeleteProfile(string name)
    {
        if (_cfg.Profiles.Count <= 1)
        {
            ToastService.Show("The Last Profile Can't Be Deleted", ToastType.Warning);
            return false;
        }
        var p = _cfg.Profiles.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        if (p == null) return false;
        if (string.Equals(_cfg.ActiveProfile, p.Name, StringComparison.OrdinalIgnoreCase))
        {
            var next = _cfg.Profiles.First(x => x != p).Name;
            SwitchProfile(next);
            // BUG FIX (found in testing): SwitchProfile → SyncActiveProfile REPLACES
            // the doomed profile's record with a fresh snapshot object, so the
            // old reference `p` is no longer in the list and Remove(p) silently
            // failed — "said it deleted, but the profile stayed."
        }
        _cfg.Profiles.RemoveAll(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        SaveAll();
        Log.Action($"Profile deleted: \"{name}\"", "Profiles");
        ToastService.Show($"Profile Deleted: {name}", ToastType.Info);
        ProfilesChanged?.Invoke();
        return true;
    }

    /// <summary>Cycle to the next profile (the optional hotkey).</summary>
    private void OnHotkeyProfiles()
    {
        if (_cfg.Profiles.Count < 2) return;
        int i = _cfg.Profiles.FindIndex(p =>
            string.Equals(p.Name, _cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
        var next = _cfg.Profiles[(i + 1 + _cfg.Profiles.Count) % _cfg.Profiles.Count].Name;
        Log.Action($"Profile cycle hotkey → \"{next}\"", "Profiles");
        Application.Current.Dispatcher.Invoke(() => SwitchProfile(next));
    }

    /// <summary>Optional Quick Record hotkey — record a zone in the current
    /// mode without opening the main window. The View auto-names it.</summary>
    private void OnHotkeyQuickRecord()
    {
        Log.Action("Quick Record hotkey");
        Application.Current.Dispatcher.Invoke(() => RecordRequested?.Invoke());
    }

    /// <summary>Optional View Logs hotkey — open the live Log Viewer.</summary>
    private void OnHotkeyViewLogs()
        => Application.Current.Dispatcher.Invoke(() => LogsRequested?.Invoke());

    /// <summary>Optional Soft Restart hotkey — save and relaunch (App handles
    /// the flush + relaunch + shutdown, since it owns the single-instance mutex).</summary>
    /// <summary>Soft restart from a GUI button — same path as the hotkey.</summary>
    public void RequestRestart()
    {
        Log.Action("Soft restart requested (button)");
        RestartRequested?.Invoke();
    }

    private void OnHotkeyRestart()
    {
        Log.Action("Soft restart hotkey");
        Application.Current.Dispatcher.Invoke(() => RestartRequested?.Invoke());
    }

    // ── Events for the View to wire up ──────────────────────────────────
    public event Action? ToggleSettingsRequested;
    public event Action? PauseStateChanged;
    /// <summary>Quick Record hotkey fired — the View starts a recording.</summary>
    public event Action? RecordRequested;
    /// <summary>View Logs hotkey fired — the View opens the Log Viewer.</summary>
    public event Action? LogsRequested;
    /// <summary>Soft Restart hotkey fired — App saves and relaunches.</summary>
    public event Action? RestartRequested;
    /// <summary>Locate: briefly flash a zone on every open map. Raised when a list
    /// row is double-clicked; map canvases subscribe and pulse the zone.</summary>
    public event Action<Zone>? FlashZoneRequested;
    public void RequestFlashZone(Zone zone) => FlashZoneRequested?.Invoke(zone);
    public void ToggleSettingsWindow() => ToggleSettingsRequested?.Invoke();

    // ── Process Exceptions ──────────────────────────────────────────────
    private bool IsExceptionProcess(IntPtr hwnd, string which)
    {
        if (_cfg.OverrideExceptions) return false;
        string? proc = Win32.GetProcessName(hwnd);
        if (proc == null) return false;
        return Exceptions.Any(e =>
            string.Equals(e.Name, proc, StringComparison.OrdinalIgnoreCase) &&
            (which == "toggle" ? e.BlockToggle : e.BlockExit));
    }

    /// <summary>Called from the low-level mouse hook when LButton is pressed.
    /// IMPORTANT: runs on the hook thread — must return fast and marshal UI work.</summary>
    // ── S28 Stage G: gesture pipeline. The hook forwards left/middle/right
    // button-downs; we count clicks per button (500 ms, 10 px) and dispatch the
    // matching gesture on the UI thread: 0 double-left · 1 middle · 2 triple-left ·
    // 3 Ctrl+double-left · 4 double-right. OnLeftClick is kept as a thin alias.
    private readonly Dictionary<string, DateTime> _zoneLastFire = new();
    private readonly DateTime[] _lastBtnTime = new DateTime[3];
    private readonly Win32.POINT[] _lastBtnPos = new Win32.POINT[3];
    private readonly int[] _btnCount = new int[3];
    public void OnLeftClick(int x, int y) => OnMouseButton(0, x, y);
    public void OnMouseButton(int button, int x, int y)   // 0 left, 1 middle, 2 right
    {
        if (DetectionPaused) return;
        if (button < 0 || button > 2) return;
        var now = DateTime.Now;
        double elapsed = (now - _lastBtnTime[button]).TotalMilliseconds;
        bool near = Math.Abs(x - _lastBtnPos[button].X) < 10 && Math.Abs(y - _lastBtnPos[button].Y) < 10;
        _btnCount[button] = elapsed < 500 && near ? _btnCount[button] + 1 : 1;
        _lastBtnTime[button] = now;
        _lastBtnPos[button] = new Win32.POINT { X = x, Y = y };
        bool ctrl = (Win32.GetAsyncKeyState(0x11) & 0x8000) != 0;   // VK_CONTROL
        bool shift = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0, alt = (Win32.GetAsyncKeyState(0x12) & 0x8000) != 0;
        int count = _btnCount[button];
        int gesture = -1;
        if (button == 1 && count == 1) gesture = 1;
        else if (button == 0 && count == 2) gesture = ctrl ? 3 : shift ? 5 : alt ? 6 : 0;
        else if (button == 0 && count == 3) gesture = 2;
        else if (button == 2 && count == 2) gesture = 4;
        if (gesture < 0) return;
        if (count >= 3) _btnCount[button] = 0;
        // Marshal ALL handling to the UI thread so the hook callback returns immediately.
        Application.Current?.Dispatcher.BeginInvoke(() => HandleGesture(x, y, gesture));
    }

    /// <summary>S28 K: set by App — true for OnTop's own zone-overlay HWNDs.</summary>
    public Func<IntPtr, bool>? IsOverlayWindow { get; set; }

    private void HandleGesture(int x, int y, int gesture)
    {
        if (_cfg.VerboseLogging) Log.Debug($"Gesture {Zone.GestureNames[Math.Clamp(gesture, 0, Zone.GestureNames.Length - 1)]} at ({x},{y})", "Detection");
        if (_cfg.DesktopOnly)
        {
            // S28 K BUG FIX (from testing: "double clicks aren't detecting"): WindowFromPoint
            // returned OUR OWN ZONE OVERLAY (it covers the whole monitor and is only
            // click-through while Overlays Locked is on), so every gesture was
            // silently gated as "not the desktop". Look THROUGH the overlays to the
            // real top-level window beneath, and say why when a gesture is gated.
            var pt = new Win32.POINT { X = x, Y = y };
            var top = Win32.TopWindowAtPoint(pt, h => IsOverlayWindow?.Invoke(h) == true);
            bool onDesktop = top == IntPtr.Zero || Win32.IsDesktopWindow(top) || Win32.IsDesktopWindow(Win32.WindowFromPoint(pt)) && !(IsOverlayWindow?.Invoke(Win32.WindowFromPoint(pt)) ?? false);
            if (!onDesktop)
            {
                string who = top == IntPtr.Zero ? "?" : $"'{Win32.GetWindowTitle(top)}' [{Win32.GetProcessName(top) ?? "?"}]";
                if (TestMode) { Log.Action($"TEST MISS at ({x},{y}) — Desktop Only: click landed on {who}", "Detection"); ToastService.Show($"Test Miss — Not On Desktop ({Win32.GetProcessName(top) ?? "window"})", ToastType.Info); }
                else if (_cfg.VerboseLogging) Log.Debug($"Gesture at ({x},{y}) gated by Desktop Only — under cursor: {who}", "Detection");
                return;
            }
        }

        var fg = Win32.GetForegroundWindow();
        if (IsExceptionProcess(fg, "exit")) return;

        foreach (var zone in Zones.Where(z => z.Enabled && z.Gesture == gesture))
        {
            if (zone.Contains(x, y))
            {
                // S28 Stage I: schedule + cooldown gates.
                if (!zone.IsScheduledNow(DateTime.Now)) { Log.Info($"Zone {zone.Name}: outside its schedule — ignored", "Detection"); continue; }
                if (zone.CooldownSec > 0 && _zoneLastFire.TryGetValue(zone.Id, out var last) && (DateTime.Now - last).TotalSeconds < zone.CooldownSec)
                { Log.Info($"Zone {zone.Name}: in cooldown ({zone.CooldownSec}s) — ignored", "Detection"); return; }
                _zoneLastFire[zone.Id] = DateTime.Now;
                if (_cfg.ZoneHitFlash && !TestMode) RequestFlashZone(zone);
                Log.Action($"Zone match: {zone.Name} at ({x},{y})", "Detection");

                // Resolve the action: the global override wins, then the zone's own.
                // S28 K BUG FIX (from testing: "exit/stealth should be toggleable if you JUST want
                // an action"): the global override replaces EXIT with Stealth — it no
                // longer swallows Run/Kill/Macro/Placement/Library actions. What happens
                // AFTER an advanced action is the zone's own AfterAction (Nothing /
                // Stealth / Exit) — see RunAfterAction.
                var action = zone.TriggerAction == ZoneAction.Exit && _cfg.ZonesTriggerStealth ? ZoneAction.Stealth : zone.TriggerAction;

                // TEST MODE: report the hit, flash the zone, execute NOTHING —
                // and skip stats so test clicks never pollute the counters.
                if (TestMode)
                {
                    Log.Action($"TEST HIT — {zone.Name} at ({x},{y}) (suppressed action: {action})", "Detection");
                    ToastService.Show($"TEST HIT — {zone.Name}", ToastType.Success);
                    RequestFlashZone(zone);
                    return;
                }

                Stats.BumpZoneTrigger(zone.Id, zone.Name, action == ZoneAction.Stealth);
                if (action == ZoneAction.Stealth)
                {
                    // Use the app normally until a zone registers — then unhook
                    // instead of exiting, so nothing remains for anti-cheats to
                    // flag. Reversible from the tray, the GUI, or Ctrl+Alt+R.
                    Log.Action($"Zone triggered STEALTH: {zone.Name} — unhooking instead of exiting", "Detection");
                    StatusText = $"Zone {zone.Name} Triggered Stealth Mode";
                    _cfg.StealthMode = true;   // App's handler removes the hook + toasts
                    return;
                }

                // Per-zone actions (S26). SECURITY.md "Per-Zone Actions": these
                // run ONLY user-authored config, are shown verbatim in the UI,
                // and every execution is logged AND toasted — never silent.
                // S28 feature switch: with Zone Actions off, an advanced action
                // is reported and refused — the zone does NOT fall through to Exit.
                if ((action is ZoneAction.RunCommand or ZoneAction.KillProcess or ZoneAction.ToggleWindow or ZoneAction.RunMacro or ZoneAction.ApplyPlacement or ZoneAction.Library)
                    && !_cfg.ZoneActionsEnabled)
                {
                    Log.Warn($"Zone {zone.Name} action {action} skipped — Zone Actions are disabled (Settings → Features)", "Zones");
                    ToastService.Show($"Zone {zone.Name}: Actions Are Disabled (Settings → Features)", ToastType.Warning);
                    return;
                }
                if (action == ZoneAction.RunCommand) { RunZoneCommand(zone); RunAfterAction(zone); return; }
                if (action == ZoneAction.KillProcess) { KillZoneProcess(zone); RunAfterAction(zone); return; }
                if (action == ZoneAction.ToggleWindow) { ToggleZoneWindow(zone); RunAfterAction(zone); return; }
                if (action == ZoneAction.Library)
                {
                    var la = _cfg.LibraryActions.Find(x => x.Id == zone.ActionLibraryId);
                    if (la is null) ToastService.Show($"Zone {zone.Name}: Library Action Not Found (Right-Click → Set Action)", ToastType.Warning);
                    else { RunLibraryAction(la, $"zone {zone.Name}"); RunAfterAction(zone); }
                    return;
                }
                if (action == ZoneAction.ApplyPlacement)
                {
                    var rule = Placements.FirstOrDefault(p => p.Id == zone.ActionPlacementId);
                    if (rule is null) ToastService.Show($"Zone {zone.Name}: Placement Rule Not Found (Right-Click → Set Action)", ToastType.Warning);
                    else { _ = PlacementSvc.ApplyAsync(rule, $"zone {zone.Name}"); RunAfterAction(zone); }
                    return;
                }
                if (action == ZoneAction.RunMacro)
                {
                    var mac = _cfg.Macros.Find(mm => mm.Id == zone.ActionMacroId);
                    if (mac is null) ToastService.Show($"Zone {zone.Name}: Macro Not Found (Right-Click → Set Action)", ToastType.Warning);
                    else { MacroSvc.Play(mac); RunAfterAction(zone); }   // all playback guards apply
                    return;
                }

                if (_cfg.ConfirmBeforeExit)
                {
                    if (!Views.ConfirmDialog.Confirm(null,
                            "OnTop — Confirm Exit",
                            $"Exit OnTop?\nZone: {zone.Name}",
                            "Exit", "Stay"))
                        return;
                }
                Application.Current.Shutdown();
                return;
            }
        }

        // Fell through every zone → a miss. Only worth reporting in Test Mode.
        if (_cfg.VerboseLogging && !TestMode) Log.Debug($"Gesture at ({x},{y}): no enabled zone with this gesture contains it ({Zones.Count(z => z.Enabled && z.Gesture == gesture)} candidate zone(s))", "Detection");
        if (TestMode)
        {
            Log.Action($"TEST MISS at ({x},{y})", "Detection");
            ToastService.Show($"Test Miss At ({x},{y})", ToastType.Info);
        }
    }

    // ── Per-zone action execution (SECURITY.md: "Per-Zone Actions") ─────
    /// <summary>Processes that must never be killable via a zone — killing
    /// these can destabilize or lock the whole session.</summary>
    private static readonly string[] ProtectedProcesses =
        { "explorer", "winlogon", "csrss", "svchost", "lsass", "services", "system", "smss", "wininit", "dwm" };

    /// <summary>S28 K: what a zone does AFTER its advanced action — Nothing (default),
    /// Stealth, or Exit. Exit honors Confirm Before Exit. Never applies to plain
    /// Exit/Stealth zones (they ARE the after-step).</summary>
    private void RunAfterAction(Zone zone)
    {
        switch (zone.AfterAction)
        {
            case 1:
                Log.Action($"Zone {zone.Name}: after action → STEALTH", "Detection");
                _cfg.StealthMode = true;
                break;
            case 2:
                Log.Action($"Zone {zone.Name}: after action → EXIT", "Detection");
                if (_cfg.ConfirmBeforeExit && !Views.ConfirmDialog.Confirm(null, "OnTop — Confirm Exit", $"Exit OnTop?\nZone: {zone.Name}", "Exit", "Stay")) return;
                Application.Current.Shutdown();
                break;
        }
    }

    /// <summary>S28 inert-load guard: the standing invariant is that NOTHING
    /// executes while a config is being read. If any choke-point is ever reached
    /// during a load, refuse loudly — that is a bug, not a feature.</summary>
    private bool RefuseIfLoading(string what)
    {
        if (!Services.SettingsService.IsLoading) return false;
        Log.Error($"INERT-LOAD GUARD: {what} was invoked during a config load — refused (report this)", "Config");
        ToastService.Show("Blocked: An Action Tried To Run During Config Load (See Log)", ToastType.Error, 7000);
        return true;
    }

    private void RunZoneCommand(Zone zone)
    {
        if (RefuseIfLoading("RunZoneCommand")) return;
        string cmd = zone.ActionCommand.Trim();
        if (cmd.Length == 0)
        {
            ToastService.Show($"Zone {zone.Name}: No Command Set (Right-Click → Set Action)", ToastType.Warning);
            return;
        }
        LaunchViaShell(cmd, $"zone {zone.Name}");
    }

    /// <summary>THE single launcher choke-point (SECURITY.md "Internal Capability
    /// Isolation", S28 Stage E). Every feature that starts a program — zone Run
    /// Command, placement launch, and anything future — comes through here.
    /// ShellExecute as the current (non-elevated) user, exactly what typing it
    /// into Win+R would do; nothing is parsed or expanded by us. Always logged
    /// as an ACTION and toasted; refused during config load and in Test Mode.
    /// Config-in, not call-in: callers pass a string that came from vetted
    /// config the user authored — never runtime-composed input.</summary>
    public bool LaunchViaShell(string commandOrPath, string context)
    {
        if (RefuseIfLoading($"LaunchViaShell({context})")) return false;
        if (TestMode) { Log.Action($"TEST MODE — launch suppressed ({context}): {commandOrPath}", "Zones"); ToastService.Show($"Test Mode — Would Launch: {commandOrPath}", ToastType.Info); return false; }
        string cmd = (commandOrPath ?? "").Trim();
        if (cmd.Length == 0) return false;
        Log.Action($"LAUNCH ({context}): {cmd}", "Zones");
        ToastService.Show($"Running ({context}): {cmd}", ToastType.Info);
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cmd) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Launch failed ({context}): {ex.Message}", "Zones");
            ToastService.Show("Launch Failed — See Log", ToastType.Error);
            return false;
        }
    }

    private void KillZoneProcess(Zone zone)
    {
        if (RefuseIfLoading("KillZoneProcess")) return;
        string name = zone.ActionProcess.Trim().TrimEnd();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0)
        {
            ToastService.Show($"Zone {zone.Name}: No Process Set (Right-Click → Set Action)", ToastType.Warning);
            return;
        }
        if (Array.Exists(ProtectedProcesses, p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
        {
            Log.Warn($"Zone {zone.Name} refused to kill protected process '{name}'", "Zones");
            ToastService.Show($"Refused — '{name}' Is A Protected System Process", ToastType.Warning);
            return;
        }
        int killed = 0;
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName(name))
            {
                try { proc.Kill(); killed++; }
                catch (Exception ex) { Log.Debug($"Kill {name} pid {proc.Id} failed: {ex.Message}", "Zones"); }
                finally { proc.Dispose(); }
            }
        }
        catch (Exception ex) { Log.Error($"Kill lookup failed ({name}): {ex.Message}", "Zones"); }
        Log.Action($"Zone {zone.Name} KILL PROCESS '{name}': {killed} terminated", "Zones");
        ToastService.Show(killed > 0
            ? $"Zone {zone.Name} — Killed {killed} × {name}"
            : $"Zone {zone.Name} — No '{name}' Process Found", killed > 0 ? ToastType.Success : ToastType.Info);
    }

    private void ToggleZoneWindow(Zone zone)
    {
        if (RefuseIfLoading("ToggleZoneWindow")) return;
        string name = zone.ActionProcess.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        if (name.Length == 0)
        {
            ToastService.Show($"Zone {zone.Name}: No Process Set (Right-Click → Set Action)", ToastType.Warning);
            return;
        }
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName(name))
            {
                using (proc)
                {
                    var h = proc.MainWindowHandle;
                    if (h == IntPtr.Zero) continue;
                    if (Win32.IsIconic(h))
                    {
                        Win32.ShowWindowAsync(h, Win32.SW_RESTORE);
                        Win32.SetForegroundWindow(h);
                        Log.Action($"Zone {zone.Name} TOGGLE WINDOW '{name}': restored", "Zones");
                    }
                    else
                    {
                        Win32.ShowWindowAsync(h, Win32.SW_MINIMIZE);
                        Log.Action($"Zone {zone.Name} TOGGLE WINDOW '{name}': minimized", "Zones");
                    }
                    return;
                }
            }
            ToastService.Show($"Zone {zone.Name} — No '{name}' Window Found", ToastType.Info);
            Log.Info($"Zone {zone.Name} toggle: no '{name}' window found", "Zones");
        }
        catch (Exception ex)
        {
            Log.Error($"Zone toggle failed ({name}): {ex.Message}", "Zones");
            ToastService.Show("Toggle Failed — See Log", ToastType.Error);
        }
    }

    // ── Zone CRUD ───────────────────────────────────────────────────────
    // Recording is view-driven now (v9.5 click-to-place flow): MainWindow hides
    // itself, shows ZoneCaptureOverlay, and calls AddZoneAt with the clicked point.

    /// <summary>Rename a zone (record-time prompt, map context menu).</summary>
    public void RenameZone(Zone zone, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || zone.Name == name) return;
        zone.Name = name;
        Log.Action($"Zone named: {name}", "Recording");
        StatusText = $"Zone Named {name}";
    }

    /// <summary>Create a new zone at the given physical screen coordinates.
    /// Used by the Record button (cursor position) and the zone map (double-click).</summary>
    public Zone AddZoneAt(int x, int y, bool pushUndo = true)
    {
        if (pushUndo) PushUndo();
        var zone = new Zone
        {
            Name = $"Zone {Zones.Count + 1}",
            X = x, Y = y,
            Tolerance = _cfg.DefTolerance,
            CapX = _cfg.DefCapacity, CapY = _cfg.DefCapacity,
            Variation = _cfg.DefVariation,
            ColorIndex = Zones.Count,
            Monitor = MonitorIndexOf(x, y),
            // New zones join the active group so they appear in the filtered list.
            Group = _groupFilter == "All" ? "Default" : _groupFilter
        };
        Zones.Add(zone);
        SelectedZone = zone;
        Log.Action($"Zone recorded: {zone.Name} at ({zone.X},{zone.Y})", "Recording");
        StatusText = $"Recorded {zone.Name} at ({zone.X}, {zone.Y})";
        return zone;
    }

    /// <summary>Create a 1:1 zone from a dragged rectangle (Drag Box recording):
    /// the dragged size IS the detection area — Tolerance and Variation start
    /// at 0 so nothing pads it (v9.5 behavior).</summary>
    public Zone AddBoxZone(int centerX, int centerY, int width, int height)
    {
        PushUndo();
        var zone = new Zone
        {
            Name = $"Zone {Zones.Count + 1}",
            Kind = ZoneKind.Box,
            X = centerX, Y = centerY,
            Tolerance = 0, Variation = 0,
            CapX = Math.Max(1, width), CapY = Math.Max(1, height),
            ColorIndex = Zones.Count,
            Monitor = MonitorIndexOf(centerX, centerY),
            Group = _groupFilter == "All" ? "Default" : _groupFilter
        };
        Zones.Add(zone);
        SelectedZone = zone;
        Log.Action($"Zone recorded (drag box): {zone.Name} {width}×{height} at ({centerX},{centerY})", "Recording");
        StatusText = $"Recorded {zone.Name} — {width}×{height}px Box";
        return zone;
    }

    /// <summary>Create a freeform Draw zone from absolute physical-pixel
    /// vertices. Detection is ray-cast point-in-polygon against the real
    /// outline; the sliders do not apply to Draw zones.</summary>
    public Zone AddDrawZone(List<ZonePoint> physicalPoints)
    {
        PushUndo();
        var zone = new Zone
        {
            Name = $"Zone {Zones.Count + 1}",
            Tolerance = 0, Variation = 0, CapX = 0, CapY = 0,
            ColorIndex = Zones.Count,
            Group = _groupFilter == "All" ? "Default" : _groupFilter
        };
        zone.SetPolygonFromPhysical(physicalPoints);
        zone.Monitor = MonitorIndexOf(zone.X, zone.Y);
        Zones.Add(zone);
        SelectedZone = zone;
        Log.Action($"Zone recorded (draw): {zone.Name} — {zone.RelPoints.Count} points, " +
                   $"{zone.HalfX * 2}×{zone.HalfY * 2}px bounds", "Recording");
        StatusText = $"Recorded {zone.Name} — Freeform ({zone.RelPoints.Count} Points)";
        return zone;
    }

    /// <summary>Redraw Zone (map context menu): replace a Draw zone's polygon in
    /// place, undoably — name, group, color, action, and enabled state survive.</summary>
    public void ReplaceDrawPoints(Zone zone, List<ZonePoint> physicalPoints)
    {
        if (physicalPoints.Count < 3) return;
        PushUndo();
        zone.SetPolygonFromPhysical(physicalPoints);
        zone.Monitor = MonitorIndexOf(zone.X, zone.Y);
        Log.Action($"Zone redrawn: {zone.Name} — {zone.RelPoints.Count} points, " +
                   $"{zone.HalfX * 2}×{zone.HalfY * 2}px bounds", "Recording");
        StatusText = $"Redrew {zone.Name}";
    }

    /// <summary>Zone map drag is starting — snapshot once so the whole drag is one undo step.</summary>
    public void BeginZoneDrag() => PushUndo();

    /// <summary>Reorder a zone in the list. Detection is first-match by list
    /// order, so earlier = higher priority when zones overlap. Moves are relative
    /// to the full list (what drives detection), not the current group filter.</summary>
    public void MoveZoneUp(Zone zone) => MoveZone(zone, -1);
    public void MoveZoneDown(Zone zone) => MoveZone(zone, +1);

    private void MoveZone(Zone zone, int delta)
    {
        int i = Zones.IndexOf(zone);
        int j = i + delta;
        if (i < 0 || j < 0 || j >= Zones.Count) return;
        PushUndo();
        Zones.Move(i, j);
        _cfg.Zones = Zones.ToList();
        SelectedZone = zone;
        Log.Action($"Moved zone '{zone.Name}' {(delta < 0 ? "up" : "down")} (priority)", "Zones");
    }

    /// <summary>Nudge the selected zone's anchor by a pixel delta (Ctrl+Arrow in the
    /// list; Shift = 10px). One undo step per press.</summary>
    public void NudgeSelectedZone(int dx, int dy)
    {
        if (SelectedZone is not Zone z) return;
        PushUndo();
        z.X += dx;
        z.Y += dy;
        _cfg.Zones = Zones.ToList();
    }

    /// <summary>Copy a zone onto another monitor at the same *relative* position
    /// within that monitor. No-op for a bad index.</summary>
    public void DuplicateZoneToMonitor(Zone zone, int monitorIndex)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (monitorIndex < 0 || monitorIndex >= screens.Length) return;

        var src = System.Array.Find(screens, s => s.Bounds.Contains(zone.X, zone.Y)) ?? screens[0];
        var sb = src.Bounds;
        var db = screens[monitorIndex].Bounds;
        double relX = sb.Width > 0 ? (zone.X - sb.X) / (double)sb.Width : 0.5;
        double relY = sb.Height > 0 ? (zone.Y - sb.Y) / (double)sb.Height : 0.5;

        PushUndo();
        var copy = zone.Clone();
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        copy.Name += $" (M{monitorIndex + 1})";
        copy.ColorIndex = Zones.Count;
        copy.X = db.X + (int)(relX * db.Width);
        copy.Y = db.Y + (int)(relY * db.Height);
        copy.Monitor = monitorIndex;
        Zones.Add(copy);
        _cfg.Zones = Zones.ToList();
        SelectedZone = copy;
        Log.Action($"Duplicated zone '{zone.Name}' to monitor {monitorIndex + 1}", "Zones");
    }

    /// <summary>Zone map drag finished — refresh the monitor index and log the move.</summary>
    public void EndZoneDrag(Zone zone)
    {
        zone.Monitor = MonitorIndexOf(zone.X, zone.Y);
        Log.Action($"Zone moved: {zone.Name} to ({zone.X},{zone.Y})");
        StatusText = $"Moved {zone.Name} to ({zone.X}, {zone.Y})";
    }

    /// <summary>Overlay corner-drag finished — log the waterfall result and, if this
    /// zone is selected, reload the sliders silently so they show its new values.</summary>
    public void EndZoneResize(Zone zone)
    {
        Log.Action($"Zone resized: {zone.Name} to {zone.HalfX * 2}×{zone.HalfY * 2}px " +
                   $"(cap {zone.CapX}/{zone.CapY}, tol {zone.Tolerance}, var {zone.Variation})");
        StatusText = $"Resized {zone.Name} to {zone.HalfX * 2}×{zone.HalfY * 2}px";
        if (ReferenceEquals(zone, SelectedZone))
        {
            _sliderTol = zone.Tolerance;
            _sliderCap = zone.CapX;
            _sliderVar = zone.Variation;
            OnPropertyChanged(nameof(SliderTol));
            OnPropertyChanged(nameof(SliderCap));
            OnPropertyChanged(nameof(SliderVar));
        }
    }

    private static int MonitorIndexOf(int x, int y)
    {
        var screens = Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
            if (screens[i].Bounds.Contains(x, y)) return i;
        return 0;
    }

    // ── Zone Groups ─────────────────────────────────────────────────────
    /// <summary>Rebuild the group dropdown from current zone groups ("All" first).</summary>
    public void RefreshGroupNames()
    {
        var current = Zones.Select(z => string.IsNullOrWhiteSpace(z.Group) ? "Default" : z.Group.Trim())
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                           .ToList();
        current.Insert(0, "All");

        for (int i = GroupNames.Count - 1; i >= 0; i--)
            if (!current.Contains(GroupNames[i], StringComparer.OrdinalIgnoreCase))
                GroupNames.RemoveAt(i);
        foreach (var g in current)
            if (!GroupNames.Contains(g, StringComparer.OrdinalIgnoreCase))
                GroupNames.Add(g);
        if (GroupNames.IndexOf("All") > 0) { GroupNames.Remove("All"); GroupNames.Insert(0, "All"); }

        if (!GroupNames.Contains(GroupFilter)) GroupFilter = "All";
    }

    // ── Zone health flags (off-screen / no hit area / duplicate name) ───
    private void OnZonesCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (Zone z in e.OldItems) z.PropertyChanged -= Zone_HealthWatch;
        if (e.NewItems != null) foreach (Zone z in e.NewItems) z.PropertyChanged += Zone_HealthWatch;
        RefreshGroupNames();
        RecomputeZoneHealth();
    }

    private void Zone_HealthWatch(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Zone.Name) or nameof(Zone.X) or nameof(Zone.Y)
            or nameof(Zone.CapX) or nameof(Zone.CapY) or nameof(Zone.Tolerance)
            or nameof(Zone.Variation) or nameof(Zone.Kind))
            RecomputeZoneHealth();
    }

    /// <summary>Flag zones that are off-screen, have no hit area, or share a name
    /// with another zone. Off-screen is tested against the virtual-screen bounds
    /// (catches truly stranded zones; gaps between monitors aren't flagged).</summary>
    private void RecomputeZoneHealth()
    {
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in Zones)
            nameCounts[z.Name] = nameCounts.TryGetValue(z.Name, out var c) ? c + 1 : 1;

        // Off-screen = the anchor isn't inside ANY monitor's rectangle. Testing
        // each monitor (not the virtual-screen bounding box) also catches zones
        // stranded in the gap between mismatched monitors. Note: dragging on the
        // map clamps to the desktop bounds, so this mainly flags coordinate edits,
        // gaps, and zones left behind when a monitor is disconnected.
        var screens = System.Windows.Forms.Screen.AllScreens;

        foreach (var z in Zones)
        {
            var issues = new List<string>();
            bool onMonitor = false;
            foreach (var scr in screens)
                if (scr.Bounds.Contains(z.X, z.Y)) { onMonitor = true; break; }
            if (!onMonitor) issues.Add("Off-screen (not on any monitor)");
            if (z.HalfX == 0 && z.HalfY == 0)
                issues.Add("No hit area (size, tolerance, and variation are all 0)");
            if (nameCounts.TryGetValue(z.Name, out var cnt) && cnt > 1)
                issues.Add("Duplicate name");
            z.HealthWarning = issues.Count > 0 ? string.Join("\n", issues) : "";
        }
    }

    private void ApplyGroupFilter()
    {
        var view = CollectionViewSource.GetDefaultView(Zones);
        if (view == null) return;
        string search = _zoneSearch.Trim();
        bool noGroup = _groupFilter == "All";
        bool noSearch = search.Length == 0;
        view.Filter = noGroup && noSearch
            ? null
            : o => o is Zone z
                && (noGroup || string.Equals(z.Group, _groupFilter, StringComparison.OrdinalIgnoreCase))
                && (noSearch || z.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || z.Group.Contains(search, StringComparison.OrdinalIgnoreCase));
        StatusText = !noSearch
            ? $"Filtering Zones: \"{search}\""
            : noGroup ? "Showing All Zones" : $"Showing Group: {_groupFilter}";
    }

    private string _zoneSearch = "";
    /// <summary>Live text filter over the zone list (name or group); composes
    /// with the group dropdown. The map is unaffected — it draws all zones.</summary>
    public string ZoneSearch
    {
        get => _zoneSearch;
        set { if (Set(ref _zoneSearch, value ?? "")) ApplyGroupFilter(); }
    }

    /// <summary>Enable/disable every zone in the filtered group ("All" = every zone).</summary>
    private void SetGroupEnabled(bool enabled)
    {
        var targets = (_groupFilter == "All"
                ? Zones.AsEnumerable()
                : Zones.Where(z => string.Equals(z.Group, _groupFilter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (targets.Count == 0) return;
        PushUndo();
        foreach (var z in targets) z.Enabled = enabled;
        Log.Action($"Group {(enabled ? "enabled" : "disabled")}: {_groupFilter} ({targets.Count} zones)");
        StatusText = $"{(enabled ? "Enabled" : "Disabled")} {targets.Count} Zone(s) In {_groupFilter}";
    }

    /// <summary>Assign the selected zone to a group (creates the group implicitly).</summary>
    public void SetSelectedZoneGroup(string? name)
    {
        if (SelectedZone == null) { Views.ConfirmDialog.Notice("Select A Zone First", "Click a zone in the list (or on the map), then press Set Group again."); return; }
        name = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
        PushUndo();
        SelectedZone.Group = name;
        RefreshGroupNames();
        Log.Action($"Zone group set: {SelectedZone.Name} → {name}");
        StatusText = $"Set {SelectedZone.Name} To Group {name}";
    }

    /// <summary>Swatch click in the grid — advance the zone to the next palette color.</summary>
    public void CycleZoneColor(Zone zone)
    {
        PushUndo();
        zone.ColorIndex = (zone.ColorIndex + 1) % ZonePalette.Palette.Length;
        StatusText = $"Color Changed: {zone.Name}";
    }

    /// <summary>Action chip click — flip a zone between Exit and Stealth triggers.</summary>
    public void CycleZoneAction(Zone zone)
    {
        PushUndo();
        zone.TriggerAction = zone.TriggerAction == ZoneAction.Exit ? ZoneAction.Stealth : ZoneAction.Exit;
        Log.Action($"Zone trigger set: {zone.Name} → {zone.TriggerAction}", "Recording");
        StatusText = $"{zone.Name} Now Triggers {zone.TriggerAction}";
    }

    // ── Exceptions Management (Exceptions sub-GUI) ──────────────────────
    public List<string> GetRunningProcessNames()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes
                .Select(p => { try { return p.ProcessName; } catch { return null; } })
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    public void AddException(string name)
    {
        name = SettingsService.CleanProcessName(name);
        if (name.Length == 0) return;
        if (Exceptions.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        Exceptions.Add(new ProcessException { Name = name, BlockToggle = true, BlockExit = true });
        Log.Action($"Exception added: {name}");
        StatusText = $"Exception Added: {name}";
    }

    public void RemoveException(ProcessException exception)
    {
        if (Exceptions.Remove(exception))
        {
            Log.Action($"Exception removed: {exception.Name}");
            StatusText = $"Exception Removed: {exception.Name}";
        }
    }

    public void ImportExceptions(string path)
    {
        try
        {
            var incoming = Settings.ImportExceptionsFile(path);
            int added = 0;
            foreach (var ex in incoming.Where(i =>
                         !Exceptions.Any(e => e.Name.Equals(i.Name, StringComparison.OrdinalIgnoreCase))))
            {
                Exceptions.Add(ex);
                added++;
            }
            Log.Action($"Exceptions imported: {added} added, {incoming.Count - added} skipped ({path})");
            ToastService.Show($"Imported {added} Exception(s), Skipped {incoming.Count - added}", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Exception import failed: {ex.Message}");
            ToastService.Show("Exception Import Failed — See Log", ToastType.Error);
        }
    }

    public void ExportExceptions(string path)
    {
        try
        {
            Settings.ExportExceptionsFile(path, Exceptions);
            Log.Action($"Exceptions exported: {Exceptions.Count} ({path})");
            ToastService.Show($"Exported {Exceptions.Count} Exception(s)", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Exception export failed: {ex.Message}");
            ToastService.Show("Exception Export Failed — See Log", ToastType.Error);
        }
    }

    // ── Auto-Close Management (Auto-Close sub-GUI) ──────────────────────
    public void AddAutoCloseTrigger(string name)
    {
        name = SettingsService.CleanProcessName(name);
        if (name.Length == 0) return;
        if (AutoCloseTriggers.Any(t => t.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
        AutoCloseTriggers.Add(name);
        Log.Action($"Auto-close trigger added: {name}");
        StatusText = $"Auto-Close Trigger Added: {name}";
    }

    public void RemoveAutoCloseTrigger(string name)
    {
        if (AutoCloseTriggers.Remove(name))
        {
            Log.Action($"Auto-close trigger removed: {name}");
            StatusText = $"Auto-Close Trigger Removed: {name}";
        }
    }

    public void ImportAutoClose(string path)
    {
        try
        {
            var incoming = Settings.ImportAutoCloseFile(path);
            int added = 0;
            foreach (var name in incoming.Where(n =>
                         !AutoCloseTriggers.Any(t => t.Equals(n, StringComparison.OrdinalIgnoreCase))))
            {
                AutoCloseTriggers.Add(name);
                added++;
            }
            Log.Action($"Auto-close triggers imported: {added} added, {incoming.Count - added} skipped ({path})");
            ToastService.Show($"Imported {added} Trigger(s), Skipped {incoming.Count - added}", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Auto-close import failed: {ex.Message}");
            ToastService.Show("Auto-Close Import Failed — See Log", ToastType.Error);
        }
    }

    public void ExportAutoClose(string path)
    {
        try
        {
            Settings.ExportAutoCloseFile(path, AutoCloseTriggers);
            Log.Action($"Auto-close triggers exported: {AutoCloseTriggers.Count} ({path})");
            ToastService.Show($"Exported {AutoCloseTriggers.Count} Trigger(s)", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Auto-close export failed: {ex.Message}");
            ToastService.Show("Auto-Close Export Failed — See Log", ToastType.Error);
        }
    }

    // ── Zone Import / Export ────────────────────────────────────────────
    public void ImportZones(string path)
    {
        try
        {
            // Roadmap: auto-backup BEFORE any import touches the workspace —
            // a bad file can then always be walked back.
            _cfg.Zones = Zones.ToList();
            string bak = Settings.WriteBackup(_cfg);
            Log.Action($"Pre-import backup written: {bak}", "Config");

            // S28 QUARANTINE: powerful actions in the file arrive INERT (reset to
            // Exit) and are listed verbatim; the user opts each back in.
            var quarantine = new List<QuarantineItem>();
            var incoming = Settings.ImportZonesFile(path, quarantine);
            var toAdd = incoming
                .Where(z => !Zones.Any(e => e.Name == z.Name && e.X == z.X && e.Y == z.Y))
                .ToList();
            if (toAdd.Count > 0)
            {
                PushUndo();
                foreach (var zone in toAdd)
                {
                    zone.Id = Guid.NewGuid().ToString("N")[..8];   // avoid ID collisions
                    zone.ColorIndex = Zones.Count;
                    Zones.Add(zone);
                }
            }
            RefreshGroupNames();
            Log.Action($"Zones imported: {toAdd.Count} added, {incoming.Count - toAdd.Count} duplicates skipped ({path})");
            ToastService.Show($"Imported {toAdd.Count} Zone(s), Skipped {incoming.Count - toAdd.Count} Duplicate(s)", ToastType.Success);
            ReviewQuarantine(quarantine, "zone import", path);
        }
        catch (Exception ex)
        {
            Log.Error($"Zone import failed: {ex.Message}");
            ToastService.Show("Zone Import Failed — See Log", ToastType.Error);
        }
    }

    public void ExportZones(string path)
    {
        try
        {
            Settings.ExportZonesFile(path, Zones.ToList());
            Log.Action($"Zones exported: {Zones.Count} ({path})");
            ToastService.Show($"Exported {Zones.Count} Zone(s)", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Zone export failed: {ex.Message}");
            ToastService.Show("Zone Export Failed — See Log", ToastType.Error);
        }
    }

    // ── Multi-Monitor / Bounds Validation ───────────────────────────────
    /// <summary>Startup check: warn about zones referencing missing monitors or
    /// sitting outside the current virtual screen.</summary>
    public void ValidateZonesAgainstDisplays()
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0 || Zones.Count == 0) return;
        var union = screens[0].Bounds;
        for (int i = 1; i < screens.Length; i++)
            union = System.Drawing.Rectangle.Union(union, screens[i].Bounds);

        var missing = Zones.Where(z => z.Monitor >= screens.Length).Select(z => z.Name).ToList();
        var offscreen = Zones.Where(z => !union.Contains(z.X, z.Y)).Select(z => z.Name).ToList();

        if (missing.Count > 0) Log.Warn($"Zones on missing monitors: {string.Join(", ", missing)}");
        if (offscreen.Count > 0) Log.Warn($"Zones outside the virtual screen: {string.Join(", ", offscreen)}");
        int total = missing.Count + offscreen.Count;
        if (total > 0)
            ToastService.Show($"{total} Zone(s) Reference Missing Monitors Or Sit Off-Screen — Check The Zone Map",
                ToastType.Warning, 6000);
    }

    // ── Settings Change Tracking ────────────────────────────────────────
    /// <summary>v9.5 behavior: every settings change writes an ACTION log line
    /// (so the Log Viewer shows toggles, hotkeys, retention, etc. as they flip).
    /// Also applies the side effects for the settings that have them.</summary>
    private void OnCfgPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        string prop = e.PropertyName ?? "";
        if (prop == nameof(AppSettings.StatsEnabled)) Stats.Enabled = _cfg.StatsEnabled;   // S28 feature switch
        if (prop == nameof(AppSettings.LowImpactMode)) LowImpactChanged?.Invoke(_cfg.LowImpactMode);   // App applies priority + intervals
        if (prop is nameof(AppSettings.PlacementEnabled) or nameof(AppSettings.ActionLibraryEnabled)) BindHotkeys();   // inert while off
        if (prop == nameof(AppSettings.UiTheme)) ThemeOptions.ApplyTheme(_cfg.UiTheme);   // S28 I: live
        if (prop == nameof(AppSettings.ClickThroughBorder)) RefreshClickThroughBorders();
        if (prop is nameof(AppSettings.MacrosEnabled) or nameof(AppSettings.ShowMacroStatusBar)) MacroStatusSyncRequested?.Invoke();
        if (prop == nameof(AppSettings.HotkeyRetrySeconds) && _hotkeyRetryTimer is not null) _hotkeyRetryTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_cfg.HotkeyRetrySeconds, 5, 600));
        if (prop is nameof(AppSettings.HoldChordsEnabled) or nameof(AppSettings.HotkeyHoldPeek) or nameof(AppSettings.HotkeyHoldPin) or nameof(AppSettings.StealthMode)) HoldChords.Sync();

        // OverlayOpacity fires continuously during slider drags — skip the spam.
        // Bulk-sync list properties (reassigned wholesale by SaveAll/SaveDraftNow)
        // are excluded too: their logged value is just "List`1[...]" noise, and
        // the meaningful changes are already logged individually as actions
        // (zone added/removed, trigger added/removed, etc.).
        if (prop is not (nameof(AppSettings.OverlayOpacity))
                 and not (nameof(AppSettings.SchemaVersion))
                 and not (nameof(AppSettings.Zones))
                 and not (nameof(AppSettings.Exceptions))
                 and not (nameof(AppSettings.AutoCloseTriggers))
                 and not (nameof(AppSettings.WindowPlacements))
                 and not "")
        {
            object? value = typeof(AppSettings).GetProperty(prop)?.GetValue(_cfg);
            // Lists print as "List`1[...]" by default — show contents instead.
            if (value is System.Collections.IEnumerable en and not string)
                value = "[" + string.Join(", ", en.Cast<object>().Take(12)) + "]";
            Log.Action($"Setting changed: {prop} = {value}", "Settings");
        }

        switch (prop)
        {
            case nameof(AppSettings.StartWithWindows):
                try
                {
                    StartupShortcut.Apply(_cfg.StartWithWindows);
                    StatusText = _cfg.StartWithWindows ? "Startup Shortcut Created" : "Startup Shortcut Removed";
                }
                catch (Exception ex)
                {
                    Log.Error($"Startup shortcut: {ex.Message}");
                    ToastService.Show("Could Not Update The Startup Shortcut — See Log", ToastType.Error);
                }
                break;

            case nameof(AppSettings.MicaEnabled):
                ThemeOptions.MicaEnabled = _cfg.MicaEnabled;
                foreach (Window window in Application.Current.Windows)
                {
                    var hwnd = new WindowInteropHelper(window).Handle;
                    if (hwnd != IntPtr.Zero) Win32.ApplyBackdrop(hwnd);
                }
                break;

            case nameof(AppSettings.VerboseLogging):
                Log.Verbose = _cfg.VerboseLogging;
                break;
        }
    }

    // ── Backup / Restore / Folders ──────────────────────────────────────
    private void OpenFolder(string dir)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error($"Open folder failed: {ex.Message}");
        }
    }

    /// <summary>Write a timestamped snapshot of all settings + zones (v9.5 Backup button).</summary>
    public void BackupNow()
    {
        try
        {
            _cfg.Zones = Zones.ToList();
            _cfg.Exceptions = Exceptions.ToList();
            _cfg.AutoCloseTriggers = AutoCloseTriggers.ToList();
            string path = Settings.WriteBackup(_cfg);
            Log.Action($"Backup written: {path}");
            StatusText = $"Backup Saved At {DateTime.Now:h:mm:ss tt}";
            ToastService.Show("Backup Saved To The Config Folder", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Backup failed: {ex.Message}");
            ToastService.Show("Backup Failed — See Log", ToastType.Error);
        }
    }

    /// <summary>Replace all settings/zones from a backup file (undoable).</summary>
    public void RestoreFrom(string path)
    {
        try
        {
            var loaded = Settings.ReadSettingsFile(path);

            // S28 PROVENANCE: a file under OnTop's own config dir (its backups)
            // is TRUSTED — recovery is seamless, never re-approved. Anything
            // picked from elsewhere is an OUTSIDE file: powerful content lands
            // inert and is reviewed BEFORE it becomes the live config.
            var quarantine = new List<QuarantineItem>();
            bool outside = !Settings.IsOwnPath(path);
            if (outside)
            {
                Services.SettingsService.QuarantineSettings(loaded, quarantine);
                Log.Warn($"Restore from OUTSIDE path — {quarantine.Count} powerful item(s) quarantined: {path}", "Config");
                ReviewQuarantine(quarantine, "restore", path);
            }

            // Safety net: snapshot the current state before replacing it.
            _cfg.Zones = Zones.ToList();
            _cfg.Exceptions = Exceptions.ToList();
            _cfg.AutoCloseTriggers = AutoCloseTriggers.ToList();
            string safety = Settings.WriteBackup(_cfg);
            Log.Action($"Safety backup before restore: {safety}");

            PushUndo();
            ApplyLoadedSettings(loaded);
            BindHotkeys();
            Log.Action($"Settings restored from backup: {path}");
            StatusText = "Restored From Backup";
            ToastService.Show("Settings Restored From Backup", ToastType.Success);
        }
        catch (Exception ex)
        {
            Log.Error($"Restore failed: {ex.Message}");
            ToastService.Show("Restore Failed — See Log", ToastType.Error);
        }
    }

    /// <summary>S28: show the Import Review for quarantined items (no-op when
    /// empty), then log the outcome per item — never silent either way.</summary>
    private void ReviewQuarantine(List<QuarantineItem> items, string what, string path)
    {
        if (items.Count == 0) return;
        int kept = Views.ImportReviewWindow.Review(
            Application.Current?.MainWindow,
            "OnTop — Import Review",
            $"This {what} came from outside OnTop ({System.IO.Path.GetFileName(path)}). Every command, process, macro, and URL below was neutralized on the way in. Tick only what you recognize and trust — unticked items stay inert. A pre-import backup was written first, so this is reversible.",
            items);
        foreach (var it in items)
            Log.Action($"Import review — {(it.Keep ? "ENABLED" : "kept inert")}: {it.Category} / {it.Subject} — {it.Detail}", "Config");
        ToastService.Show(kept == 0
            ? $"All {items.Count} Imported Action(s) Left Inert"
            : $"Enabled {kept} Of {items.Count} Imported Action(s)", kept == 0 ? ToastType.Info : ToastType.Warning, 6000);
    }

    /// <summary>Copy a loaded AppSettings into the live one in place (the live
    /// instance is what every binding and subscription targets, so it must not
    /// be swapped). Scalars via reflection, collections reloaded, sliders and
    /// groups refreshed.</summary>
    private void ApplyLoadedSettings(AppSettings source)
    {
        var collections = new HashSet<string>
        {
            nameof(AppSettings.Zones), nameof(AppSettings.Exceptions), nameof(AppSettings.AutoCloseTriggers)
        };
        foreach (var prop in typeof(AppSettings).GetProperties())
        {
            if (!prop.CanRead || !prop.CanWrite || collections.Contains(prop.Name)) continue;
            prop.SetValue(_cfg, prop.GetValue(source));
        }

        Zones.Clear();
        foreach (var zone in source.Zones) Zones.Add(zone);
        Exceptions.Clear();
        foreach (var exception in source.Exceptions) Exceptions.Add(exception);
        AutoCloseTriggers.Clear();
        foreach (var trigger in source.AutoCloseTriggers.Where(t => !string.IsNullOrWhiteSpace(t)))
            AutoCloseTriggers.Add(trigger.Trim());

        _sliderTol = _cfg.DefTolerance;
        _sliderCap = _cfg.DefCapacity;
        _sliderVar = _cfg.DefVariation;

        // Profiles: first run after v11 — wrap the existing workspace as "Default".
        if (_cfg.Profiles.Count == 0)
        {
            _cfg.Profiles.Add(CaptureLiveWorkspace(_cfg.ActiveProfile));
            Log.Info($"Created initial profile \"{_cfg.ActiveProfile}\" from the current workspace", "Profiles");
        }
        OnPropertyChanged(nameof(SliderTol));
        OnPropertyChanged(nameof(SliderCap));
        OnPropertyChanged(nameof(SliderVar));
        SelectedZone = null;
        RefreshGroupNames();
    }

    private void RemoveSelectedZone()
    {
        if (SelectedZone == null) { Views.ConfirmDialog.Notice("Select A Zone First", "Click the zone you want to remove in the list (or on the map), then press Remove again."); return; }
        PushUndo();
        Log.Action($"Zone removed: {SelectedZone.Name}");
        Zones.Remove(SelectedZone);
        SelectedZone = null;
    }

    // ── Bulk zone operations (multi-select in the lists) ────────────────
    /// <summary>Remove several zones as ONE undoable action.</summary>
    public void RemoveZones(IList<Zone> zones)
    {
        var list = zones?.Where(z => z != null).Distinct().ToList();
        if (list is not { Count: > 0 }) return;
        PushUndo();
        foreach (var z in list) Zones.Remove(z);
        SelectedZone = null;
        RefreshGroupNames();
        RecomputeZoneHealth();
        Log.Action($"Zones removed (bulk): {list.Count} — {string.Join(", ", list.Take(5).Select(z => z.Name))}{(list.Count > 5 ? "…" : "")}", "Zones");
        ToastService.Show($"Removed {list.Count} Zone{(list.Count == 1 ? "" : "s")}", ToastType.Info);
    }

    /// <summary>Set the group for several zones as ONE undoable action.</summary>
    public void SetGroupFor(IList<Zone> zones, string group)
    {
        var list = zones?.Where(z => z != null).Distinct().ToList();
        if (list is not { Count: > 0 }) return;
        group = (group ?? "").Trim();
        if (group.Length > 100) group = group[..100];
        PushUndo();
        foreach (var z in list) z.Group = group;
        RefreshGroupNames();
        Log.Action($"Group set (bulk): \"{group}\" on {list.Count} zone(s)", "Zones");
        ToastService.Show($"Group \"{group}\" Set On {list.Count} Zone{(list.Count == 1 ? "" : "s")}", ToastType.Success);
    }

    /// <summary>Enable/disable several zones as ONE undoable action.</summary>
    public void SetEnabledFor(IList<Zone> zones, bool enabled)
    {
        var list = zones?.Where(z => z != null && z.Enabled != enabled).Distinct().ToList();
        if (list is not { Count: > 0 }) return;
        PushUndo();
        foreach (var z in list) z.Enabled = enabled;
        RecomputeZoneHealth();
        Log.Action($"Zones {(enabled ? "enabled" : "disabled")} (bulk): {list.Count}", "Zones");
        ToastService.Show($"{(enabled ? "Enabled" : "Disabled")} {list.Count} Zone{(list.Count == 1 ? "" : "s")}", ToastType.Info);
    }

    private void DuplicateSelectedZone()
    {
        if (SelectedZone == null) { Views.ConfirmDialog.Notice("Select A Zone First", "Click the zone you want to copy in the list (or on the map), then press Duplicate again."); return; }
        PushUndo();
        var copy = SelectedZone.Clone();
        copy.Id = Guid.NewGuid().ToString("N")[..8];
        copy.Name += " (Copy)";
        copy.ColorIndex = Zones.Count;
        copy.X += 30; copy.Y += 30;
        Zones.Add(copy);
        Log.Action($"Zone duplicated: {copy.Name}");
    }

    // ── Slider → Zone sync ──────────────────────────────────────────────
    private DateTime _lastSliderPush = DateTime.MinValue;
    private void OnSliderChanged()
    {
        _cfg.DefTolerance = _sliderTol;
        _cfg.DefCapacity = _sliderCap;
        _cfg.DefVariation = _sliderVar;

        if ((DateTime.Now - _lastSliderPush).TotalMilliseconds > 400)
        {
            PushUndo();
            _lastSliderPush = DateTime.Now;
        }

        if (_cfg.ApplyAllZones)
        {
            foreach (var z in Zones)
            {
                if (z.Kind == ZoneKind.Draw) continue;   // sliders don't apply to freeform zones
                z.Tolerance = _sliderTol;
                z.CapX = _sliderCap; z.CapY = _sliderCap;
                z.Variation = _sliderVar;
            }
        }
        else if (SelectedZone != null && SelectedZone.Kind != ZoneKind.Draw)
        {
            SelectedZone.Tolerance = _sliderTol;
            SelectedZone.CapX = _sliderCap; SelectedZone.CapY = _sliderCap;
            SelectedZone.Variation = _sliderVar;
        }
    }

    public void OnZoneSelected(Zone? zone)
    {
        if (zone == null || zone == SelectedZone) return;
        SelectedZone = zone;
        // Load zone values into sliders without triggering push
        _sliderTol = zone.Tolerance;
        _sliderCap = zone.CapX;
        _sliderVar = zone.Variation;
        OnPropertyChanged(nameof(SliderTol));
        OnPropertyChanged(nameof(SliderCap));
        OnPropertyChanged(nameof(SliderVar));
    }

    // ── Undo/Redo ───────────────────────────────────────────────────────
    private void PushUndo() => _undo.Push(Zones.Select(z => z.Clone()).ToList(), _cfg);
    private void Undo()
    {
        if (!_undo.CanUndo) return;
        _undo.PushRedo(Zones.Select(z => z.Clone()).ToList(), _cfg);
        var (zones, cfg) = _undo.Pop();
        RestoreState(zones, cfg);
    }
    private void Redo()
    {
        if (!_undo.CanRedo) return;
        _undo.PushUndoOnly(Zones.Select(z => z.Clone()).ToList(), _cfg);
        var (zones, cfg) = _undo.PopRedo();
        RestoreState(zones, cfg);
    }
    private void RestoreState(System.Collections.Generic.List<Zone> zones, AppSettings cfg)
    {
        Zones.Clear();
        foreach (var z in zones) Zones.Add(z);
        _cfg.DefTolerance = cfg.DefTolerance;
        _cfg.DefCapacity = cfg.DefCapacity;
        _cfg.DefVariation = cfg.DefVariation;
        _sliderTol = cfg.DefTolerance; _sliderCap = cfg.DefCapacity; _sliderVar = cfg.DefVariation;
        OnPropertyChanged(nameof(SliderTol)); OnPropertyChanged(nameof(SliderCap)); OnPropertyChanged(nameof(SliderVar));
        OnPropertyChanged(nameof(Cfg));
    }

    /// <summary>Start the 30s auto-draft timer (call when settings window opens).</summary>
    public void StartAutoDraft()
    {
        _draftTimer ??= new Timer(_ => SaveDraftNow(), null, 30000, 30000);
    }

    /// <summary>Write the crash-recovery draft immediately. Called by the draft
    /// timer and by the global exception handlers on the way down.</summary>
    public void SaveDraftNow()
    {
        try
        {
            _cfg.Zones = Zones.ToList();
            _cfg.Exceptions = Exceptions.ToList();
            // Only reassign when contents actually changed: a fresh List every 30s
            // tick is a new reference, so Set() fires PropertyChanged and the
            // settings logger records a phantom "Setting changed" every tick.
            if (!_cfg.AutoCloseTriggers.SequenceEqual(AutoCloseTriggers))
                _cfg.AutoCloseTriggers = AutoCloseTriggers.ToList();
            Settings.SaveDraft(_cfg);
        }
        catch (Exception ex) { Log.Debug($"Auto-draft failed: {ex.Message}", "Config"); }
    }

    /// <summary>Stop the draft timer and remove the draft (call on save/close).</summary>
    public void StopAutoDraft()
    {
        _draftTimer?.Dispose();
        _draftTimer = null;
        Settings.DeleteDraft();
    }

    public void Dispose()
    {
        MacroSvc.Dispose();   // recording hooks off, playback stopped
        HoldChords.Dispose(); // hold-chord keyboard hook off
        _draftTimer?.Dispose();
        Hotkeys.Dispose();
        EStop.Dispose();
    }
}

// ── Undo Stack ──────────────────────────────────────────────────────────
// Uses LinkedList so the oldest entry can be trimmed when the cap is hit.
public class UndoStack
{
    private const int MaxEntries = 50;
    private readonly System.Collections.Generic.LinkedList<(System.Collections.Generic.List<Zone>, AppSettings)> _undo = new();
    private readonly System.Collections.Generic.LinkedList<(System.Collections.Generic.List<Zone>, AppSettings)> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Push(System.Collections.Generic.List<Zone> zones, AppSettings cfg)
    {
        _undo.AddLast((zones, CloneCfg(cfg)));
        _redo.Clear();
        while (_undo.Count > MaxEntries)
            _undo.RemoveFirst();   // trim oldest
    }
    public void PushRedo(System.Collections.Generic.List<Zone> zones, AppSettings cfg)
    {
        _redo.AddLast((zones, CloneCfg(cfg)));
        while (_redo.Count > MaxEntries)
            _redo.RemoveFirst();
    }
    public void PushUndoOnly(System.Collections.Generic.List<Zone> zones, AppSettings cfg)
    {
        _undo.AddLast((zones, CloneCfg(cfg)));
        while (_undo.Count > MaxEntries)
            _undo.RemoveFirst();
    }
    public (System.Collections.Generic.List<Zone>, AppSettings) Pop()
    {
        var v = _undo.Last!.Value; _undo.RemoveLast(); return v;
    }
    public (System.Collections.Generic.List<Zone>, AppSettings) PopRedo()
    {
        var v = _redo.Last!.Value; _redo.RemoveLast(); return v;
    }
    public void Clear() { _undo.Clear(); _redo.Clear(); }

    private static AppSettings CloneCfg(AppSettings c) => new()
    {
        DefTolerance = c.DefTolerance, DefCapacity = c.DefCapacity, DefVariation = c.DefVariation,
        DesktopOnly = c.DesktopOnly, DebugOverlay = c.DebugOverlay, ApplyAllZones = c.ApplyAllZones,
        OverrideExceptions = c.OverrideExceptions, StartWithWindows = c.StartWithWindows, ConfirmBeforeExit = c.ConfirmBeforeExit
    };
}
