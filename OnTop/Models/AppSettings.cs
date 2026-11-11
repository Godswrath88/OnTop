using System.Collections.Generic;

namespace OnTop.Models;

public class ProcessException : ObservableObject
{
    private string _name = "";
    private bool _blockToggle = true;
    private bool _blockExit = true;

    public string Name { get => _name; set => Set(ref _name, value); }
    public bool BlockToggle { get => _blockToggle; set => Set(ref _blockToggle, value); }
    public bool BlockExit { get => _blockExit; set => Set(ref _blockExit, value); }

    public ProcessException Clone() => new() { Name = Name, BlockToggle = BlockToggle, BlockExit = BlockExit };
}

public class AppSettings : ObservableObject
{
    // Defaults
    private int _defTolerance = 5;
    private int _defCapacity = 20;
    private int _defVariation = 5;

    // Toggles
    private bool _desktopOnly = true;
    private bool _debugOverlay;
    private bool _applyAllZones = true;
    private bool _overrideExceptions;
    private bool _startWithWindows;
    private bool _confirmBeforeExit;
    private bool _overlaysLocked;
    private double _overlayOpacity = 1.0;
    private bool _slidersLocked;
    private bool _micaEnabled = true;
    private bool _guiAlwaysOnTop = true;
    private int _zoneViewMode = 2;      // 0 Map, 1 List, 2 Split
    private bool _verboseLogging;
    private bool _stealthMode;
    private bool _zonesTriggerStealth;

    // Schema (forward-compatible migrations — see SettingsService.Migrate)
    private int _schemaVersion;

    /// <summary>Saved size/position per window, keyed by a stable name ("main",
    /// "map", "list", "logs"). Restored on open, clamped to a visible monitor.</summary>
    public Dictionary<string, WindowRect> WindowPlacements { get; set; } = new();
    /// <summary>Persisted DataGrid column widths, keyed "gridKey/columnKey".
    /// See Helpers/GridColumns. Survives restarts and lives in the user config.</summary>
    public Dictionary<string, double> ColumnWidths { get; set; } = new();

    // ── Profiles (named zone workspaces) ────────────────────────────────
    /// <summary>Stored profile snapshots. The LIVE workspace stays in the
    /// top-level fields (Zones/Exceptions/...); SaveAll syncs live → active.</summary>
    public List<Profile> Profiles { get; set; } = new();
    private string _activeProfile = "Default";
    public string ActiveProfile { get => _activeProfile; set => Set(ref _activeProfile, value ?? "Default"); }

    // ── Window Rules (auto-pin) ─────────────────────────────────────────
    /// <summary>Auto-pin rules (see Models/WindowRule). SECURITY: the feature is
    /// OFF by default; rules only cause Z-order changes on matches.</summary>
    public List<WindowRule> WindowRules { get; set; } = new();
    private bool _windowRulesEnabled;
    public bool WindowRulesEnabled { get => _windowRulesEnabled; set => Set(ref _windowRulesEnabled, value); }

    // Recording
    private int _recordMode;   // 0 Point Click, 1 Drag Box, 2 Freeform Draw

    // Hotkeys
    private string _hotkeyToggle = "F1";
    private string _hotkeyExit = "F10";
    private string _hotkeyConfig = "Ctrl+Alt+R";
    private string _hotkeyKill = "Ctrl+Alt+Q";

    // Action hotkeys — optional (default-unbound) v9.5-parity utilities.
    private string _hotkeyQuickRecord = "";
    private string _hotkeyViewLogs = "";
    private string _hotkeyRestart = "";
    private string _hotkeyRehook = "Ctrl+Alt+R";
    private string _hotkeyPins = "";
    private bool _showInTaskbar = true;
    private bool _autoDisableOffscreenZones;
    private string _hotkeyPeek = "";
    private string _hotkeyRules = "";
    private string _hotkeyProfiles = "";
    private string _updateManifestUrl = "";
    private bool _updateCheckOnStartup = true;
    private string _ignoredUpdateVersion = "";

    // Toggle hotkeys — one optional (default-unbound) bind per main-GUI toggle.
    private string _hotkeyDesktopOnly = "";
    private string _hotkeyDebugOverlay = "";
    private string _hotkeyApplyAll = "";
    private string _hotkeyOverrideExceptions = "";
    private string _hotkeyConfirmExit = "";
    private string _hotkeyLockOverlays = "";
    private string _hotkeyGuisOnTop = "";
    private string _hotkeyStealth = "";
    private string _hotkeyZonesStealth = "";

    // Auto-close
    private bool _autoCloseEnabled;
    private bool _autoCloseOnStartOnly;
    private int _autoCloseInterval = 10;
    private List<string> _autoCloseTriggers = new();

    // Logging
    private int _logRetentionDays = 7;

    // UI
    private string _uiMode = "Cozy";

    public int DefTolerance { get => _defTolerance; set => Set(ref _defTolerance, value); }
    public int DefCapacity { get => _defCapacity; set => Set(ref _defCapacity, value); }
    public int DefVariation { get => _defVariation; set => Set(ref _defVariation, value); }
    public bool DesktopOnly { get => _desktopOnly; set => Set(ref _desktopOnly, value); }
    public bool DebugOverlay { get => _debugOverlay; set => Set(ref _debugOverlay, value); }
    public bool ApplyAllZones { get => _applyAllZones; set => Set(ref _applyAllZones, value); }
    public bool OverrideExceptions { get => _overrideExceptions; set => Set(ref _overrideExceptions, value); }
    public bool StartWithWindows { get => _startWithWindows; set => Set(ref _startWithWindows, value); }
    public bool ConfirmBeforeExit { get => _confirmBeforeExit; set => Set(ref _confirmBeforeExit, value); }
    public bool OverlaysLocked { get => _overlaysLocked; set => Set(ref _overlaysLocked, value); }
    public double OverlayOpacity { get => _overlayOpacity; set => Set(ref _overlayOpacity, value); }
    public bool SlidersLocked { get => _slidersLocked; set => Set(ref _slidersLocked, value); }
    public bool MicaEnabled { get => _micaEnabled; set => Set(ref _micaEnabled, value); }
    public bool GuiAlwaysOnTop { get => _guiAlwaysOnTop; set => Set(ref _guiAlwaysOnTop, value); }
    public int ZoneViewMode { get => _zoneViewMode; set => Set(ref _zoneViewMode, value); }
    public bool VerboseLogging { get => _verboseLogging; set => Set(ref _verboseLogging, value); }
    /// <summary>Anti-cheat safe mode: the global mouse hook is fully removed
    /// (not just ignored) while this is on. Zone detection is inactive.</summary>
    public bool StealthMode { get => _stealthMode; set => Set(ref _stealthMode, value); }
    /// <summary>Global override: every zone triggers Stealth instead of Exit,
    /// regardless of its per-zone TriggerAction.</summary>
    public bool ZonesTriggerStealth { get => _zonesTriggerStealth; set => Set(ref _zonesTriggerStealth, value); }

    /// <summary>Log categories the user unticked in the Log Viewer (not tracked).</summary>
    public List<string> DisabledLogCategories { get; set; } = new();

    /// <summary>Monitor indices hidden from the zone map (view-only; detection unaffected).</summary>
    public List<int> HiddenMonitors { get; set; } = new();
    public int SchemaVersion { get => _schemaVersion; set => Set(ref _schemaVersion, value); }
    /// <summary>How Record captures a zone: 0 Point Click, 1 Drag Box, 2 Freeform Draw.
    /// Live-switching modes during a capture updates this (the choice sticks).</summary>
    public int RecordMode { get => _recordMode; set => Set(ref _recordMode, value); }
    public string HotkeyToggle { get => _hotkeyToggle; set => Set(ref _hotkeyToggle, value); }
    public string HotkeyExit { get => _hotkeyExit; set => Set(ref _hotkeyExit, value); }
    public string HotkeyConfig { get => _hotkeyConfig; set => Set(ref _hotkeyConfig, value); }
    public string HotkeyKill { get => _hotkeyKill; set => Set(ref _hotkeyKill, value); }
    /// <summary>Optional — trigger a recording in the current RecordMode without
    /// opening the main window (auto-names the zone; no naming prompt).</summary>
    public string HotkeyQuickRecord { get => _hotkeyQuickRecord; set => Set(ref _hotkeyQuickRecord, value); }
    /// <summary>Optional — open the live Log Viewer.</summary>
    public string HotkeyViewLogs { get => _hotkeyViewLogs; set => Set(ref _hotkeyViewLogs, value); }
    /// <summary>Optional — save and relaunch OnTop (soft restart).</summary>
    public string HotkeyRestart { get => _hotkeyRestart; set => Set(ref _hotkeyRestart, value); }
    public string HotkeyRehook { get => _hotkeyRehook; set => Set(ref _hotkeyRehook, value); }
    public string HotkeyPins { get => _hotkeyPins; set => Set(ref _hotkeyPins, value); }
    /// <summary>DEPRECATED (v13) — kept only so older configs can be migrated
    /// into MinimizeToTray. The main window now ALWAYS shows in the taskbar
    /// while open (S22).</summary>
    public bool ShowInTaskbar { get => _showInTaskbar; set => Set(ref _showInTaskbar, value); }
    private bool _minimizeToTray;
    private bool _trayMenuStaysOpen;
    private bool _firstRunCompleted;
    private int _accentIndex;
    private string _stealthWatchedProcesses = "";
    private bool _hideOverlaysInStealth = true;
    private bool _macrosEnabled;
    /// <summary>Minimize hides to the tray instead of the taskbar (off = a
    /// normal taskbar minimize). Replaces ShowInTaskbar's old double duty.</summary>
    public bool MinimizeToTray { get => _minimizeToTray; set => Set(ref _minimizeToTray, value); }
    /// <summary>Tray menu stays open after clicking an item (toggle several
    /// things in one visit); it closes ~0.6s after the cursor leaves it.</summary>
    public bool TrayMenuStaysOpen { get => _trayMenuStaysOpen; set => Set(ref _trayMenuStaysOpen, value); }
    /// <summary>Welcome tour shown+dismissed. Existing configs migrate to TRUE
    /// (only genuinely fresh installs see the tour).</summary>
    public bool FirstRunCompleted { get => _firstRunCompleted; set => Set(ref _firstRunCompleted, value); }
    /// <summary>Accent color (index into ThemeOptions.Accents). Applied at
    /// startup; changes take effect on restart.</summary>
    public int AccentIndex { get => _accentIndex; set => Set(ref _accentIndex, value); }

    // ── Stealth follow-ups (S26) ────────────────────────────────────────
    // (HotkeyStealth is defined with the other hotkey properties above.)
    /// <summary>Comma/semicolon-separated process names; while ANY is running,
    /// Stealth engages automatically (and releases when they're all gone).</summary>
    public string StealthWatchedProcesses { get => _stealthWatchedProcesses; set => Set(ref _stealthWatchedProcesses, value ?? ""); }
    /// <summary>Hide the zone overlays while stealthed (default ON — overlay
    /// windows are visible surfaces; hiding them completes the disappearance).</summary>
    public bool HideOverlaysInStealth { get => _hideOverlaysInStealth; set => Set(ref _hideOverlaysInStealth, value); }

    // ── Macro module (S27; ships dark behind this master toggle) ────────
    /// <summary>Master toggle — OFF by default. While off, the macro module
    /// installs nothing and plays nothing (SECURITY.md "Macro Module").</summary>
    public bool MacrosEnabled { get => _macrosEnabled; set => Set(ref _macrosEnabled, value); }
    /// <summary>The macro library (global, not per-profile — by design).</summary>
    public List<Macro> Macros { get; set; } = new();
    /// <summary>S28 (schema 19): SHA-256 of every powerful field, written on every
    /// save (SettingsService.ComputePowerfulStamp). A mismatch on load means an
    /// action command / process / macro binding / updater URL / watched list was
    /// edited OUTSIDE OnTop — surfaced once as a toast. Visibility, not
    /// integrity (a local attacker could recompute it; see SECURITY.md).</summary>
    public string PowerfulFieldsStamp { get; set; } = "";

    // ── Macro guard + capture options (S28 Stage B, schema 20) ──────────
    private bool _macroGuardKnownAntiCheat = true;
    private bool _macroKillSwitch = true;
    private bool _macroRecordPath = true;
    private bool _macroWarningAcknowledged;
    private string _hotkeyMacros = "";
    /// <summary>Guard layer 1 also refuses/aborts on the built-in list of known
    /// kernel-level anti-cheat processes (MacroService.KnownAntiCheat), not only
    /// the user's Stealth watched list. Default ON (fail-safe).</summary>
    public bool MacroGuardKnownAntiCheat { get => _macroGuardKnownAntiCheat; set => Set(ref _macroGuardKnownAntiCheat, value); }
    /// <summary>Guard layer 3: a guard hit mid-run doesn't just abort — it flips
    /// the master toggle OFF (unbinds every macro hotkey) until you re-enable it
    /// by hand. Default ON (fail-safe).</summary>
    public bool MacroKillSwitch { get => _macroKillSwitch; set => Set(ref _macroKillSwitch, value); }
    /// <summary>Record the full mouse path (true, full-fidelity) or drop pure
    /// moves at stop (false, actions-only: clicks re-anchor absolutely anyway).</summary>
    public bool MacroRecordPath { get => _macroRecordPath; set => Set(ref _macroRecordPath, value); }
    /// <summary>The blunt "this records literal keystrokes + synthetic input can
    /// get you banned" confirmation was shown and accepted once.</summary>
    public bool MacroWarningAcknowledged { get => _macroWarningAcknowledged; set => Set(ref _macroWarningAcknowledged, value); }
    /// <summary>Optional — open the Macros window.</summary>
    public string HotkeyMacros { get => _hotkeyMacros; set => Set(ref _hotkeyMacros, value ?? ""); }

    // ── Feature switches (S28 Stage C, schema 21 — Standing Design Law) ──
    // Every non-core feature has an explicit off-switch; "Minimal Mode" in
    // Settings → Features flips them all. Defaults preserve prior behavior.
    private bool _statsEnabled = true;
    private bool _zoneActionsEnabled = true;
    private bool _stealthWatcherEnabled = true;
    private bool _encryptMacros = true;   // S28 Stage H: default ON for installed mode (Load turns it off for portable)
    /// <summary>Usage statistics: when off, no counters/events are recorded
    /// (existing stats.json is left untouched, just not written to).</summary>
    public bool StatsEnabled { get => _statsEnabled; set => Set(ref _statsEnabled, value); }
    /// <summary>Per-zone advanced actions (Run Command / Kill / Toggle Window /
    /// Run Macro): when off, such zones toast + log instead of executing.
    /// Exit and Stealth (the core) are unaffected.</summary>
    public bool ZoneActionsEnabled { get => _zoneActionsEnabled; set => Set(ref _zoneActionsEnabled, value); }
    /// <summary>The 5 s auto-stealth process watcher: when off, the timer is
    /// inert regardless of the watched list (manual stealth still works).</summary>
    public bool StealthWatcherEnabled { get => _stealthWatcherEnabled; set => Set(ref _stealthWatcherEnabled, value); }
    /// <summary>Encrypt macro EVENT STREAMS at rest with Windows DPAPI
    /// (per-user; tied to your Windows login; no stored key). Only the
    /// recordings — never zones/profiles/hotkeys/settings. Export always
    /// writes plain JSON (sharing is never blocked). Default OFF.</summary>
    public bool EncryptMacros { get => _encryptMacros; set => Set(ref _encryptMacros, value); }

    // ── S28 Stage D (schema 22): recording hotkey, guard exclusions, status bar, performance ──
    private string _hotkeyMacroRecord = "Ctrl+Alt+M";
    private bool _showMacroStatusBar = true;
    private bool _lowImpactMode;
    /// <summary>Press = start recording (auto-named), press again = stop & save.
    /// Defaults BOUND (Ctrl+Alt+M) because clicking in a window to stop would
    /// pollute the recording; clearable like every optional hotkey.</summary>
    public string HotkeyMacroRecord { get => _hotkeyMacroRecord; set => Set(ref _hotkeyMacroRecord, value ?? ""); }
    /// <summary>Entries of MacroService.KnownAntiCheat the user chose NOT to guard
    /// against (e.g. a persistent service that never minds). Case-insensitive.</summary>
    public List<string> MacroGuardIgnored { get; set; } = new();
    /// <summary>The small always-on-top recording status bar (Ready / Recording
    /// marquee / Saved green / Playing) shown while macros are enabled.</summary>
    public bool ShowMacroStatusBar { get => _showMacroStatusBar; set => Set(ref _showMacroStatusBar, value); }
    /// <summary>Low Impact Mode: BelowNormal process priority + slower background
    /// scanners (auto-pin 3 s → 10 s, stealth watcher 5 s → 15 s). The hook
    /// itself is untouched (it must stay fast or Windows drops it).</summary>
    public bool LowImpactMode { get => _lowImpactMode; set => Set(ref _lowImpactMode, value); }

    // ── S28 Stage E (schema 23): Window Placement & Launcher ────────────
    private bool _placementEnabled;
    private string _hotkeyPlacement = "";
    /// <summary>Feature switch (default OFF). Off = rules are inert: no
    /// startup apply, no hotkey, zone actions refuse.</summary>
    public bool PlacementEnabled { get => _placementEnabled; set => Set(ref _placementEnabled, value); }
    // (S28 Stage H: the global "Allow Launching" sub-gate was REMOVED — the
    // per-rule Launch box + import quarantine already guarantee an import can't
    // arrive launching. Unknown JSON fields are ignored on load.)
    /// <summary>Optional — apply every enabled placement rule.</summary>
    public string HotkeyPlacement { get => _hotkeyPlacement; set => Set(ref _hotkeyPlacement, value ?? ""); }
    public List<PlacementRule> Placements { get; set; } = new();

    // ── S28 Stage F (schema 24): Action Library ────────────────────────
    private bool _actionLibraryEnabled;
    /// <summary>Feature switch (default OFF): no library hotkeys bind, zones
    /// pointing at library actions refuse.</summary>
    public bool ActionLibraryEnabled { get => _actionLibraryEnabled; set => Set(ref _actionLibraryEnabled, value); }
    public List<LibraryAction> LibraryActions { get; set; } = new();

    // ── S28 Stage G (schema 25): additions ──────────────────────────────
    private string _hotkeyPinUnderCursor = "", _hotkeyPinOpacityUp = "", _hotkeyPinOpacityDown = "", _hotkeyPinClickThrough = "", _hotkeyPinCycleCorner = "";
    private string _hotkeyCheatSheet = "", _hotkeyPalette = "";
    private bool _profileRulesEnabled, _displayProfilesEnabled, _doNotDisturb;
    private int _macroCountdownSec = 3;
    /// <summary>Optional: pin/unpin the top-level window under the mouse.</summary>
    public string HotkeyPinUnderCursor { get => _hotkeyPinUnderCursor; set => Set(ref _hotkeyPinUnderCursor, value ?? ""); }
    /// <summary>Optional: make the focused pinned window more/less see-through (10% steps).</summary>
    public string HotkeyPinOpacityUp { get => _hotkeyPinOpacityUp; set => Set(ref _hotkeyPinOpacityUp, value ?? ""); }
    public string HotkeyPinOpacityDown { get => _hotkeyPinOpacityDown; set => Set(ref _hotkeyPinOpacityDown, value ?? ""); }
    /// <summary>Optional: toggle click-through on the window under the cursor
    /// (clicks pass to what's beneath). Always reversible with the same key;
    /// auto-reverted on unpin.</summary>
    public string HotkeyPinClickThrough { get => _hotkeyPinClickThrough; set => Set(ref _hotkeyPinClickThrough, value ?? ""); }
    /// <summary>Optional: hop the focused pinned window to the next screen corner.</summary>
    public string HotkeyPinCycleCorner { get => _hotkeyPinCycleCorner; set => Set(ref _hotkeyPinCycleCorner, value ?? ""); }
    /// <summary>Optional: show/hide the hotkey cheat-sheet overlay.</summary>
    public string HotkeyCheatSheet { get => _hotkeyCheatSheet; set => Set(ref _hotkeyCheatSheet, value ?? ""); }
    /// <summary>Optional: open the command palette.</summary>
    public string HotkeyPalette { get => _hotkeyPalette; set => Set(ref _hotkeyPalette, value ?? ""); }
    /// <summary>Feature switch (default OFF): process → profile rules.</summary>
    public bool ProfileRulesEnabled { get => _profileRulesEnabled; set => Set(ref _profileRulesEnabled, value); }
    public List<ProfileRule> ProfileRules { get; set; } = new();
    /// <summary>Feature switch (default OFF): monitor layout → profile.</summary>
    public bool DisplayProfilesEnabled { get => _displayProfilesEnabled; set => Set(ref _displayProfilesEnabled, value); }
    /// <summary>Display signature → profile name (bound from Settings → Displays).</summary>
    public Dictionary<string, string> DisplayProfiles { get; set; } = new();
    /// <summary>Suppress routine toasts (Info/Success/Warning) while a watched
    /// or known-guard process is running; Error toasts still show.</summary>
    public bool DoNotDisturb { get => _doNotDisturb; set => Set(ref _doNotDisturb, value); }
    /// <summary>Seconds of "3-2-1" on the status strip before playback (0–5).</summary>
    public int MacroCountdownSec { get => _macroCountdownSec; set => Set(ref _macroCountdownSec, value); }

    // ── S28 Stage H (schema 26): hold chords ─────────────────────────────
    private bool _holdChordsEnabled;
    private string _hotkeyHoldPeek = "", _hotkeyHoldPin = "";
    /// <summary>Feature switch (default OFF): the hold-chord keyboard hook exists
    /// only while this is on AND a chord is set (never in stealth).</summary>
    public bool HoldChordsEnabled { get => _holdChordsEnabled; set => Set(ref _holdChordsEnabled, value); }
    /// <summary>Hold to show every zone overlay at full opacity; release hides.</summary>
    public string HotkeyHoldPeek { get => _hotkeyHoldPeek; set => Set(ref _hotkeyHoldPeek, value ?? ""); }
    /// <summary>Hold to keep the focused window on top; release un-pins it.</summary>
    public string HotkeyHoldPin { get => _hotkeyHoldPin; set => Set(ref _hotkeyHoldPin, value ?? ""); }

    // ── S28 Stage I (schema 27): the twenty ─────────────────────────────
    private bool _pinMemoryEnabled, _zoneHitFlash, _idleRuleEnabled, _profileSchedulesEnabled;
    private int _pinTimedSeconds = 30, _idleMinutes = 10, _idleAction, _uiTheme;
    private string _hotkeyPinTimed = "", _hotkeySendToMonitor = "", _hotkeyMacroPause = "", _idleProfile = "";
    public List<PinScene> PinScenes { get; set; } = new();
    /// <summary>Remember how each process was last pinned (opacity, click-through, rect) and reapply on the next pin.</summary>
    public bool PinMemoryEnabled { get => _pinMemoryEnabled; set => Set(ref _pinMemoryEnabled, value); }
    public Dictionary<string, PinMemoryEntry> PinMemory { get; set; } = new();
    /// <summary>Pin the focused window for N seconds, then unpin (timer — no keyboard hook).</summary>
    public string HotkeyPinTimed { get => _hotkeyPinTimed; set => Set(ref _hotkeyPinTimed, value ?? ""); }
    public int PinTimedSeconds { get => _pinTimedSeconds; set => Set(ref _pinTimedSeconds, value); }
    /// <summary>Move the focused window to the next monitor keeping its relative position/size.</summary>
    public string HotkeySendToMonitor { get => _hotkeySendToMonitor; set => Set(ref _hotkeySendToMonitor, value ?? ""); }
    /// <summary>Pulse the zone overlay where a real (non-test) gesture landed.</summary>
    public bool ZoneHitFlash { get => _zoneHitFlash; set => Set(ref _zoneHitFlash, value); }
    /// <summary>Pause / resume macro playback (panic still stops).</summary>
    public string HotkeyMacroPause { get => _hotkeyMacroPause; set => Set(ref _hotkeyMacroPause, value ?? ""); }
    /// <summary>Idle rule: after IdleMinutes without input do IdleAction (0 none, 1 switch to IdleProfile, 2 unpin all, 3 stealth); profile reverts on activity.</summary>
    public bool IdleRuleEnabled { get => _idleRuleEnabled; set => Set(ref _idleRuleEnabled, value); }
    public int IdleMinutes { get => _idleMinutes; set => Set(ref _idleMinutes, value); }
    public int IdleAction { get => _idleAction; set => Set(ref _idleAction, value); }
    public string IdleProfile { get => _idleProfile; set => Set(ref _idleProfile, value ?? ""); }
    /// <summary>Time-of-day profile schedules (checked once a minute by the watcher).</summary>
    public bool ProfileSchedulesEnabled { get => _profileSchedulesEnabled; set => Set(ref _profileSchedulesEnabled, value); }
    public List<ProfileSchedule> ProfileSchedules { get; set; } = new();
    /// <summary>0 Dark (default), 1 High Contrast, 2 Light. Applied live by mutating theme brushes.</summary>
    public int UiTheme { get => _uiTheme; set => Set(ref _uiTheme, value); }
    private int _hotkeyRetrySeconds = 30;
    /// <summary>S28 Stage J: how often OnTop re-tries binding a hotkey another
    /// program already holds (5–600 s; was a fixed 30 s).</summary>
    public int HotkeyRetrySeconds { get => _hotkeyRetrySeconds; set => Set(ref _hotkeyRetrySeconds, value); }
    private bool _clickThroughBorder = true;
    private string _hotkeyMacroStatus = "";
    /// <summary>S28 K: draw a thin orange frame around windows made click-through.</summary>
    public bool ClickThroughBorder { get => _clickThroughBorder; set => Set(ref _clickThroughBorder, value); }
    /// <summary>S28 K: optional — show/hide the macro status strip.</summary>
    public string HotkeyMacroStatus { get => _hotkeyMacroStatus; set => Set(ref _hotkeyMacroStatus, value ?? ""); }
    private int _noticeSeconds = 4;
    /// <summary>0.45.3: seconds a "do this first" hint popup stays up (0 = off:
    /// the hint shows as a toast instead; 1–15). No schema bump: a config
    /// without it gets the default, and older builds ignore it.</summary>
    public int NoticeSeconds { get => _noticeSeconds; set => Set(ref _noticeSeconds, value); }
    /// <summary>On display-configuration changes (dock/undock), automatically
    /// disable zones whose anchor no longer sits on any monitor.</summary>
    public bool AutoDisableOffscreenZones { get => _autoDisableOffscreenZones; set => Set(ref _autoDisableOffscreenZones, value); }
    public string HotkeyPeek { get => _hotkeyPeek; set => Set(ref _hotkeyPeek, value); }
    public string HotkeyRules { get => _hotkeyRules; set => Set(ref _hotkeyRules, value); }
    /// <summary>Optional: cycle to the next profile.</summary>
    public string HotkeyProfiles { get => _hotkeyProfiles; set => Set(ref _hotkeyProfiles, value); }

    // ── Updates (stage-one check & notify; see SECURITY.md) ─────────────
    /// <summary>HTTPS URL of the release manifest. EMPTY = the whole update
    /// feature is dormant — no network I/O ever happens.</summary>
    public string UpdateManifestUrl { get => _updateManifestUrl; set => Set(ref _updateManifestUrl, value ?? ""); }
    /// <summary>Quiet version check shortly after startup (no-op while the
    /// manifest URL is empty; failures never nag).</summary>
    public bool UpdateCheckOnStartup { get => _updateCheckOnStartup; set => Set(ref _updateCheckOnStartup, value); }
    /// <summary>Release version the user chose "Ignore This Version" for —
    /// silenced until a NEWER release appears. Cleared by a manual check.</summary>
    public string IgnoredUpdateVersion { get => _ignoredUpdateVersion; set => Set(ref _ignoredUpdateVersion, value ?? ""); }
    public string HotkeyDesktopOnly { get => _hotkeyDesktopOnly; set => Set(ref _hotkeyDesktopOnly, value); }
    public string HotkeyDebugOverlay { get => _hotkeyDebugOverlay; set => Set(ref _hotkeyDebugOverlay, value); }
    public string HotkeyApplyAll { get => _hotkeyApplyAll; set => Set(ref _hotkeyApplyAll, value); }
    public string HotkeyOverrideExceptions { get => _hotkeyOverrideExceptions; set => Set(ref _hotkeyOverrideExceptions, value); }
    public string HotkeyConfirmExit { get => _hotkeyConfirmExit; set => Set(ref _hotkeyConfirmExit, value); }
    public string HotkeyLockOverlays { get => _hotkeyLockOverlays; set => Set(ref _hotkeyLockOverlays, value); }
    public string HotkeyGuisOnTop { get => _hotkeyGuisOnTop; set => Set(ref _hotkeyGuisOnTop, value); }
    public string HotkeyStealth { get => _hotkeyStealth; set => Set(ref _hotkeyStealth, value); }
    public string HotkeyZonesStealth { get => _hotkeyZonesStealth; set => Set(ref _hotkeyZonesStealth, value); }
    public bool AutoCloseEnabled { get => _autoCloseEnabled; set => Set(ref _autoCloseEnabled, value); }
    public bool AutoCloseOnStartOnly { get => _autoCloseOnStartOnly; set => Set(ref _autoCloseOnStartOnly, value); }
    public int AutoCloseInterval { get => _autoCloseInterval; set => Set(ref _autoCloseInterval, value); }
    public List<string> AutoCloseTriggers { get => _autoCloseTriggers; set => Set(ref _autoCloseTriggers, value); }
    public int LogRetentionDays { get => _logRetentionDays; set => Set(ref _logRetentionDays, value); }
    public string UiMode { get => _uiMode; set => Set(ref _uiMode, value); }

    // Collections stored separately
    public List<Zone> Zones { get; set; } = new();
    public List<ProcessException> Exceptions { get; set; } = new();
}

/// <summary>Persisted window geometry (device-independent units). Plain DTO for
/// JSON — restored via Helpers/WindowPlacement, which clamps it onto a visible
/// monitor so a window saved on a since-removed display can't open off-screen.</summary>
public class WindowRect
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}
