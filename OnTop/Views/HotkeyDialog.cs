using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// Hotkey configuration with press-to-capture recording: click a box, press the
/// combo, done. Esc (or the Clear button) unbinds a hotkey — empty hotkeys stay
/// unregistered. Kill always falls back to Ctrl+Alt+Q so the escape hatch can
/// never be lost.
///
/// Three sections (a mandatory/optional split): REQUIRED (Kill —
/// rebindable, never unbindable; Pause is fixed and noted), OPTIONAL — ACTIONS,
/// and OPTIONAL — TOGGLES (default unbound). The list is scrollable, labels wrap
/// rather than clip, and every new feature must decide its section (see
/// PROJECT_SCOPE standing requirements).
/// </summary>
public class HotkeyDialog : Window
{
    private readonly AppSettings _cfg;
    private readonly TextBox _tbToggle, _tbExit, _tbConfig, _tbKill;
    private readonly TextBox _tbQuickRecord, _tbViewLogs, _tbRestart, _tbRehook, _tbPins, _tbPeek, _tbRules, _tbProfiles, _tbStealth, _tbMacros, _tbMacroRecord, _tbPlacement, _tbPinCursor, _tbOpUp, _tbOpDown, _tbClickThrough, _tbCorner, _tbCheat, _tbPalette, _tbHoldPeek, _tbHoldPin, _tbPinTimed, _tbSendMon, _tbMacroPause, _tbMacroStatus;

    // Toggle-hotkey boxes, paired with a setter so Apply() can write them back.
    private readonly List<(string Label, TextBox Box, System.Action<string> Set)> _toggleBoxes = new();
    private readonly List<TextBox> _allBoxes = new();
    private bool _reverting;
    /// <summary>S28 K: refuse a conflicting combo the moment it is captured (from testing:
    /// duplicate detection "simply ignored it"). Reverts the box to its previous text.</summary>
    private void WireLiveConflict()
    {
        foreach (var tb in _allBoxes)
        {
            tb.GotFocus += (_, _) => tb.Tag = tb.Text;
            tb.TextChanged += (_, _) =>
            {
                if (_reverting || tb.Text.Length == 0) { if (!_reverting) tb.Tag = tb.Text; return; }
                var c = FindConflict();
                if (c is null) { tb.Tag = tb.Text; return; }
                Services.ToastService.Show(c, Services.ToastType.Warning, 6000);
                _reverting = true; tb.Text = (tb.Tag as string) ?? ""; _reverting = false;
            };
        }
    }

    public HotkeyDialog(AppSettings cfg)
    {
        _cfg = cfg;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Hotkey Configuration";
        Width = 560; Height = 660; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x28));
        Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 13;

        var sp = new StackPanel { Margin = new Thickness(20, 18, 20, 8) };

        sp.Children.Add(MakeLabel("Hotkey Configuration", 18, true));
        sp.Children.Add(MakeLabel("Click A Box, Then Press The Keys. Esc Or Clear = Unbound.", 11, false));
        sp.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        sp.Children.Add(new TextBlock
        {
            Text = "Click a box and PRESS the combination. While this window is open every OnTop hotkey is paused, so keys are captured instead of executed. A combination already used by another feature (or a macro / library action) is refused the moment you press it. Esc in a box = unbound.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.75, FontSize = 12, Margin = new Thickness(0, 0, 0, 10),
        });
        sp.Children.Add(MakeLabel("REQUIRED", 11, true));
        sp.Children.Add(MakeLabel("These Can Be Rebound But Never Unbound — The Escape Hatches.", 11, false));
        _tbKill = AddRow(sp, "Kill Script:", cfg.HotkeyKill);
        sp.Children.Add(MakeLabel("Kill Falls Back To Ctrl+Alt+Q If Cleared, And Stays Armed Even While Recording Here (Pressing It Stops OnTop). Pause Is Fixed: Ctrl+Alt+P.", 11, false));

        sp.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 8) });
        sp.Children.Add(MakeLabel("OPTIONAL — ACTIONS (DEFAULT VARIES, CLEAR = UNBOUND)", 11, true));
        _tbToggle = AddRow(sp, "Always On Top:", cfg.HotkeyToggle);
        _tbExit = AddRow(sp, "Exit Script:", cfg.HotkeyExit);
        _tbConfig = AddRow(sp, "Toggle Settings:", cfg.HotkeyConfig);
        _tbQuickRecord = AddRow(sp, "Quick Record:", cfg.HotkeyQuickRecord);
        _tbViewLogs = AddRow(sp, "View Logs:", cfg.HotkeyViewLogs);
        _tbRestart = AddRow(sp, "Soft Restart:", cfg.HotkeyRestart);
        _tbRehook = AddRow(sp, "Re-Enable Mouse Hook:", cfg.HotkeyRehook);
        _tbPins = AddRow(sp, "Pinned Windows Viewer:", cfg.HotkeyPins);
        _tbPeek = AddRow(sp, "Peek At All Zones:", cfg.HotkeyPeek);
        _tbRules = AddRow(sp, "Auto-Pin Manager:", cfg.HotkeyRules);
        _tbProfiles = AddRow(sp, "Cycle Profiles:", cfg.HotkeyProfiles);
        _tbStealth = AddRow(sp, "Toggle Stealth Mode:", cfg.HotkeyStealth);
        _tbMacros = AddRow(sp, "Macros Window:", cfg.HotkeyMacros);
        _tbMacroRecord = AddRow(sp, "Record / Stop Macro:", cfg.HotkeyMacroRecord);
        _tbPlacement = AddRow(sp, "Apply All Placements:", cfg.HotkeyPlacement);
        _tbPinCursor = AddRow(sp, "Pin Window Under Cursor:", cfg.HotkeyPinUnderCursor);
        _tbOpUp = AddRow(sp, "Focused Window Opacity +:", cfg.HotkeyPinOpacityUp);
        _tbOpDown = AddRow(sp, "Focused Window Opacity −:", cfg.HotkeyPinOpacityDown);
        _tbClickThrough = AddRow(sp, "Click-Through Under Cursor:", cfg.HotkeyPinClickThrough);
        _tbCorner = AddRow(sp, "Cycle Focused Window Corner:", cfg.HotkeyPinCycleCorner);
        _tbCheat = AddRow(sp, "Hotkey Cheat Sheet:", cfg.HotkeyCheatSheet);
        _tbPalette = AddRow(sp, "Command Palette:", cfg.HotkeyPalette);
        _tbHoldPeek = AddRow(sp, "HOLD To Peek (Needs Hold Chords On):", cfg.HotkeyHoldPeek);
        _tbHoldPin = AddRow(sp, "HOLD To Pin Focused Window:", cfg.HotkeyHoldPin);
        _tbPinTimed = AddRow(sp, "Pin Focused Window For N Seconds:", cfg.HotkeyPinTimed);
        _tbSendMon = AddRow(sp, "Send Focused Window To Next Monitor:", cfg.HotkeySendToMonitor);
        _tbMacroPause = AddRow(sp, "Pause / Resume Macro Playback:", cfg.HotkeyMacroPause);
        _tbMacroStatus = AddRow(sp, "Show / Hide Macro Status Strip:", cfg.HotkeyMacroStatus);
        sp.Children.Add(MakeLabel("Quick Record Captures A Zone Without Opening The Window. Soft Restart Saves And Relaunches. Re-Enable Mouse Hook Restores Detection After Stealth (Default Ctrl+Alt+R).", 11, false));

        sp.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 8) });
        sp.Children.Add(MakeLabel("OPTIONAL — TOGGLES (DEFAULT UNBOUND)", 11, true));
        sp.Children.Add(MakeLabel("Press A Bound Toggle Hotkey Anytime To Flip That Setting (A Toast Confirms The New State).", 11, false));

        AddToggleRow(sp, "Desktop Only:", cfg.HotkeyDesktopOnly, v => cfg.HotkeyDesktopOnly = v);
        AddToggleRow(sp, "Debug Overlay:", cfg.HotkeyDebugOverlay, v => cfg.HotkeyDebugOverlay = v);
        AddToggleRow(sp, "Apply To All Zones:", cfg.HotkeyApplyAll, v => cfg.HotkeyApplyAll = v);
        AddToggleRow(sp, "Override Exceptions:", cfg.HotkeyOverrideExceptions, v => cfg.HotkeyOverrideExceptions = v);
        AddToggleRow(sp, "Confirm Before Exit:", cfg.HotkeyConfirmExit, v => cfg.HotkeyConfirmExit = v);
        AddToggleRow(sp, "Lock Overlays:", cfg.HotkeyLockOverlays, v => cfg.HotkeyLockOverlays = v);
        AddToggleRow(sp, "Keep GUIs On Top:", cfg.HotkeyGuisOnTop, v => cfg.HotkeyGuisOnTop = v);
        AddToggleRow(sp, "Stealth Mode:", cfg.HotkeyStealth, v => cfg.HotkeyStealth = v);
        AddToggleRow(sp, "Zones Trigger Stealth:", cfg.HotkeyZonesStealth, v => cfg.HotkeyZonesStealth = v);

        var scroll = new ScrollViewer
        {
            Content = sp,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(20, 10, 20, 16),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var saveBtn = new Button { Content = "Save and Close", Width = 220, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
        saveBtn.Click += (_, _) =>
        {
            // Conflict PREVENTION (S26): duplicates are refused
            // at the door — the OS would silently fail the second registration,
            // leaving a feature dead. Central rule: one combo, one feature.
            var conflict = FindConflict();
            if (conflict is not null)
            {
                Services.ToastService.Show(conflict, Services.ToastType.Warning, 6000);
                return;   // dialog stays open for the fix
            }
            Apply();
            DialogResult = true;
            Close();
        };
        var cancelBtn = new Button { Content = "Cancel", Width = 220, Height = 34 };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        btnPanel.Children.Add(saveBtn);
        btnPanel.Children.Add(cancelBtn);

        var root = new DockPanel();
        DockPanel.SetDock(btnPanel, Dock.Bottom);
        WireLiveConflict();
        root.Children.Add(btnPanel);
        root.Children.Add(scroll);
        Content = root;
    }

    /// <summary>Returns a human-readable conflict description, or null when
    /// every non-empty binding is unique. "Ctrl+Alt+P" is reserved (built-in
    /// Pause). Comparison is normalized (trim/case/spaces) — the capture box
    /// writes canonical combos, so that's sufficient.</summary>
    private string? FindConflict()
    {
        var all = new List<(string Label, string Key)>
        {
            ("Pause (Built-In)", "Ctrl+Alt+P"),
            ("Macro Panic Stop (Built-In)", "Ctrl+Alt+X"),
            ("Always On Top Toggle", _tbToggle.Text),
            ("Exit", _tbExit.Text),
            ("Open Config", _tbConfig.Text),
            ("Kill Switch", _tbKill.Text),
            ("Quick Record", _tbQuickRecord.Text),
            ("View Logs", _tbViewLogs.Text),
            ("Restart", _tbRestart.Text),
            ("Re-Hook", _tbRehook.Text),
            ("Pinned Windows", _tbPins.Text),
            ("Peek", _tbPeek.Text),
            ("Auto-Pin Manager", _tbRules.Text),
            ("Cycle Profiles", _tbProfiles.Text),
            ("Toggle Stealth", _tbStealth.Text),
            ("Macros Window", _tbMacros.Text),
            ("Record / Stop Macro", _tbMacroRecord.Text),
            ("Apply All Placements", _tbPlacement.Text),
            ("Pin Under Cursor", _tbPinCursor.Text), ("Opacity +", _tbOpUp.Text), ("Opacity −", _tbOpDown.Text),
            ("Click-Through", _tbClickThrough.Text), ("Cycle Corner", _tbCorner.Text), ("Cheat Sheet", _tbCheat.Text), ("Command Palette", _tbPalette.Text),
            ("Hold To Peek", _tbHoldPeek.Text), ("Hold To Pin", _tbHoldPin.Text),
            ("Timed Pin", _tbPinTimed.Text), ("Send To Next Monitor", _tbSendMon.Text), ("Pause/Resume Macro", _tbMacroPause.Text), ("Macro Status Strip", _tbMacroStatus.Text),
        };
        foreach (var (label, box, _) in _toggleBoxes) all.Add((label, box.Text));
        foreach (var mac in _cfg.Macros) all.Add(($"Macro \"{mac.Name}\"", mac.Hotkey));
        foreach (var act in _cfg.LibraryActions) all.Add(($"Action \"{act.Name}\"", act.Hotkey));

        var seen = new Dictionary<string, string>();   // normalized key → label
        foreach (var (label, raw) in all)
        {
            string key = (raw ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            if (key.Length == 0) continue;
            if (seen.TryGetValue(key, out var first))
                return $"Hotkey Conflict: \"{(raw ?? "").Trim()}\" Is Assigned To Both {first} And {label} — Each Combo Can Only Do One Thing";
            seen[key] = label;
        }
        return null;
    }

    private void Apply()
    {
        // Empty = intentionally unbound (the VM skips registration), except Kill.
        _cfg.HotkeyToggle = _tbToggle.Text.Trim();
        _cfg.HotkeyExit = _tbExit.Text.Trim();
        _cfg.HotkeyConfig = _tbConfig.Text.Trim();
        _cfg.HotkeyKill = string.IsNullOrWhiteSpace(_tbKill.Text) ? "Ctrl+Alt+Q" : _tbKill.Text.Trim();
        _cfg.HotkeyQuickRecord = _tbQuickRecord.Text.Trim();
        _cfg.HotkeyViewLogs = _tbViewLogs.Text.Trim();
        _cfg.HotkeyRestart = _tbRestart.Text.Trim();
        _cfg.HotkeyRehook = _tbRehook.Text.Trim();
        _cfg.HotkeyPins = _tbPins.Text.Trim();
        _cfg.HotkeyPeek = _tbPeek.Text.Trim();
        _cfg.HotkeyRules = _tbRules.Text.Trim();
        _cfg.HotkeyProfiles = _tbProfiles.Text.Trim();
        _cfg.HotkeyStealth = _tbStealth.Text.Trim();
        _cfg.HotkeyMacros = _tbMacros.Text.Trim();
        _cfg.HotkeyMacroRecord = _tbMacroRecord.Text.Trim();
        _cfg.HotkeyPlacement = _tbPlacement.Text.Trim();
        _cfg.HotkeyPinUnderCursor = _tbPinCursor.Text.Trim(); _cfg.HotkeyPinOpacityUp = _tbOpUp.Text.Trim(); _cfg.HotkeyPinOpacityDown = _tbOpDown.Text.Trim();
        _cfg.HotkeyPinClickThrough = _tbClickThrough.Text.Trim(); _cfg.HotkeyPinCycleCorner = _tbCorner.Text.Trim(); _cfg.HotkeyCheatSheet = _tbCheat.Text.Trim(); _cfg.HotkeyPalette = _tbPalette.Text.Trim();
        _cfg.HotkeyHoldPeek = _tbHoldPeek.Text.Trim(); _cfg.HotkeyHoldPin = _tbHoldPin.Text.Trim();
        _cfg.HotkeyPinTimed = _tbPinTimed.Text.Trim(); _cfg.HotkeySendToMonitor = _tbSendMon.Text.Trim(); _cfg.HotkeyMacroPause = _tbMacroPause.Text.Trim(); _cfg.HotkeyMacroStatus = _tbMacroStatus.Text.Trim();
        foreach (var (_, box, set) in _toggleBoxes) set(box.Text.Trim());
    }

    // ── Capture wiring ──────────────────────────────────────────────────
    private static void WireCapture(TextBox tb)
    {
        tb.IsReadOnly = true;
        tb.IsReadOnlyCaretVisible = false;
        tb.Cursor = Cursors.Hand;
        tb.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Pure modifier presses wait for the real key.
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
                return;

            if (key == Key.Escape) { tb.Text = ""; return; }   // unbind

            var parts = new List<string>();
            var mods = Keyboard.Modifiers;
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());   // enum names round-trip through the parser (R, F10, D1…)
            tb.Text = string.Join("+", parts);
        };
    }

    // ── Layout helpers ──────────────────────────────────────────────────
    private static TextBlock MakeLabel(string text, double size, bool bold)
    {
        var tb = new TextBlock
        {
            Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2),
            Foreground = new SolidColorBrush(bold
                ? Color.FromRgb(0xE6, 0xE6, 0xE6)
                : Color.FromRgb(0x9A, 0x9A, 0xAC))
        };
        if (bold) tb.FontWeight = FontWeights.SemiBold;
        return tb;
    }

    private TextBox AddRow(StackPanel parent, string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock
        {
            Text = label, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,   // long labels wrap, never clip
            Margin = new Thickness(0, 0, 8, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6))
        };
        Grid.SetColumn(lbl, 0);

        var tb = new TextBox
        {
            Text = value, Height = 26,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3A)),
            Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("FgPrimaryBrush"),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x4A)),
            CaretBrush = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("FgPrimaryBrush"),
            Padding = new Thickness(6, 2, 4, 2)
        };
        WireCapture(tb);
        _allBoxes.Add(tb);
        Grid.SetColumn(tb, 1);

        var clear = new Button
        {
            Content = "Clear", Width = 56, Height = 26, Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Unbind This Hotkey"
        };
        clear.Click += (_, _) => tb.Text = "";
        Grid.SetColumn(clear, 2);

        grid.Children.Add(lbl);
        grid.Children.Add(tb);
        grid.Children.Add(clear);
        parent.Children.Add(grid);
        return tb;
    }

    private void AddToggleRow(StackPanel parent, string label, string value, System.Action<string> set)
    {
        var tb = AddRow(parent, label, value);
        _toggleBoxes.Add((label, tb, set));
    }
}
