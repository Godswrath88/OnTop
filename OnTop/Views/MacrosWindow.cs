using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using OnTop.Models;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Macro management (S27 — roadmap End Goal core). Master toggle, record/stop,
/// per-macro play/stop/hotkey/speed/repeat/rename/delete. Every hotkey
/// assignment routes through the one-combo-one-feature conflict check.
/// </summary>
public class MacrosWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _status = new() { Opacity = 0.6, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly Button _record = new() { Content = "Record New..." };
    private readonly Button _stopRec = new() { Content = "Stop & Save", Margin = new Thickness(8, 0, 0, 0), IsEnabled = false };
    private readonly Button _discard = new() { Content = "Discard", Margin = new Thickness(8, 0, 0, 0), IsEnabled = false };
    private CheckBox? _policy;
    private CheckBox? _master;
    private bool _syncing;
    private readonly Button _panic = new() { Content = "■ Panic Stop (Ctrl+Alt+X)", IsEnabled = false };

    public MacrosWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Macros";
        Width = 940;
        Height = 660;
        MinWidth = 780;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        // S28: keep GUIs-on-top parity with the other sub-GUIs + remember size/position.
        SetBinding(TopmostProperty, new System.Windows.Data.Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "macros", _vm.Cfg);
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "macros", _vm.Cfg);

        var root = new DockPanel { Margin = new Thickness(14) };

        // Header: master toggle + record controls
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10), LastChildFill = false };
        var master = new CheckBox
        {
            Content = "Macros Enabled",
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = _vm.Cfg.MacrosEnabled,
            ToolTip = Helpers.WarnTip.Make("Master Switch (Ships OFF). While Off: No Macro Hotkeys Bind, Nothing Records, Nothing Plays, No Hooks Exist. If The Kill Switch Turned It Off, Tick It Here To Re-Arm.",
                "Recording Captures Literal Keystrokes; Playback Sends Synthetic Input That Anti-Cheat Can Detect."),
        };
        _master = master;
        master.Checked += (_, _) => { if (_syncing) return; _vm.Cfg.MacrosEnabled = true; _vm.BindHotkeys(); _vm.Log.Action("Macros ENABLED (master toggle)", "Recording"); Rebuild(); };
        master.Unchecked += (_, _) => { if (_syncing) return; _vm.MacroSvc.Stop(); _vm.Cfg.MacrosEnabled = false; _vm.BindHotkeys(); _vm.Log.Action("Macros DISABLED (master toggle)", "Recording"); Rebuild(); };
        DockPanel.SetDock(master, Dock.Left);
        head.Children.Add(master);
        // S28: panic stop is now a visible button — enabled while playing.
        _panic.ToolTip = "Stop Any Playing Macro Immediately. This Is The Same As The Built-In Panic Hotkey Ctrl+Alt+X (Always Active While A Macro Plays).";
        _panic.Click += (_, _) => _vm.MacroSvc.Stop();
        _panic.Margin = new Thickness(14, 0, 0, 0);
        DockPanel.SetDock(_panic, Dock.Left);
        head.Children.Add(_panic);

        var recPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(recPanel, Dock.Right);
        _record.ToolTip = Helpers.WarnTip.Make("Name A Macro And Start Recording Everything You Do (Mouse, Clicks, Wheel, Keys, Real Timing). PREFER THE HOTKEY: Press The Record Hotkey Anywhere To Start, Press It Again To Stop — No Clicking In This Window That Would End Up In The Recording. Hooks Exist ONLY While Recording.",
            "Every Keystroke Is Recorded Literally — Including Passwords — And Stored In Your Config.");
        _record.Click += (_, _) =>
        {
            // S28 (End Goal F — blunt, unavoidable warning, once): literal keys +
            // synthetic input + bans. Acknowledged flag persists in config.
            if (!_vm.Cfg.MacroWarningAcknowledged)
            {
                if (!ConfirmDialog.Confirm(this, "Before You Record",
                        "Macros record EVERY keystroke literally — including anything you type into a password field — and store it in your OnTop config on this PC.\n\nPlayback sends synthetic input. Online games with anti-cheat CAN detect this and CAN ban your account. OnTop's guard refuses and kills macros around known anti-cheat processes, but no guard is perfect. Use macros for single-player, accessibility, and productivity.\n\nI understand.",
                        "I Understand — Record", "Cancel")) return;
                _vm.Cfg.MacroWarningAcknowledged = true;
                _vm.Settings.SaveNoBackup(_vm.Cfg);
                _vm.Log.Action("Macro literal-keystroke / anti-cheat warning acknowledged", "Recording");
            }
            var name = InputDialog.Prompt(this, "New Macro", "Macro Name:", $"Macro {_vm.Cfg.Macros.Count + 1}");
            if (name is null) return;
            if (_vm.MacroSvc.StartRecording(name)) WindowState = WindowState.Minimized;   // out of your way
        };
        _stopRec.ToolTip = "Stop Recording And Save The Macro (The Stop Click Itself Is Trimmed From The Tail)";
        _stopRec.Click += (_, _) => _vm.MacroSvc.StopRecording(save: true);
        _discard.ToolTip = "Stop Recording And Throw It Away";
        _discard.Click += (_, _) => _vm.MacroSvc.StopRecording(save: false);
        recPanel.Children.Add(_record);
        recPanel.Children.Add(_stopRec);
        recPanel.Children.Add(_discard);
        head.Children.Add(recPanel);
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        // S28: warning banner (always visible while the window is open).
        var banner = new Border
        {
            Background = (Brush)Application.Current.FindResource("BgBandBrush"),
            CornerRadius = (CornerRadius)Application.Current.FindResource("RadiusSmall"),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 8),
        };
        var bannerRow = new DockPanel();
        bannerRow.Children.Add(new TextBlock
        {
            Text = "\uE7BA", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16,
            Foreground = (Brush)Application.Current.FindResource("WarnAmberBrush"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
        });
        bannerRow.Children.Add(new TextBlock
        {
            Text = "Recordings capture LITERAL keystrokes (passwords included) and are stored in your config. Synthetic input can be detected by anti-cheat and get accounts banned. Guard: refuses + aborts + kill-switches around watched and known anti-cheat processes. Prefer single-player / productivity use.",
            TextWrapping = TextWrapping.Wrap, FontSize = 11.5, Opacity = 0.85, VerticalAlignment = VerticalAlignment.Center,
        });
        banner.Child = bannerRow;
        DockPanel.SetDock(banner, Dock.Top);
        root.Children.Add(banner);

        // S28: options — every guard layer has its own visible switch (Standing
        // Design Law), capture mode, per-profile policy, export/import.
        var opts = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        CheckBox Opt(string text, string tip, bool initial, Action<bool> set)
        {
            var cb = new CheckBox
            {
                Content = text, IsChecked = initial, Margin = new Thickness(0, 0, 14, 6),
                Style = (Style)Application.Current.FindResource("ToggleSwitch"), ToolTip = tip,
            };
            cb.Checked += (_, _) => { set(true); _vm.Settings.SaveNoBackup(_vm.Cfg); Rebuild(); };
            cb.Unchecked += (_, _) => { set(false); _vm.Settings.SaveNoBackup(_vm.Cfg); Rebuild(); };
            opts.Children.Add(cb);
            return cb;
        }
        Opt("Record Mouse Path", "ON = Full-Fidelity (Every Mouse Move). OFF = Actions Only (Clicks, Wheel, Keys — Clicks Still Land Where Recorded); Much Smaller Recordings",
            _vm.Cfg.MacroRecordPath, v => { _vm.Cfg.MacroRecordPath = v; _vm.Log.Action($"Macro capture: record mouse path = {v}", "Recording"); });
        Opt("Guard: Known Anti-Cheat", "Also Refuse/Abort Around A Built-In List Of Known Anti-Cheat Services (EasyAntiCheat, BattlEye, Vanguard, FACEIT, PunkBuster, GameGuard, XIGNCODE, ...). Fail-Safe Default: ON",
            _vm.Cfg.MacroGuardKnownAntiCheat, v => { _vm.Cfg.MacroGuardKnownAntiCheat = v; _vm.Log.Action($"Macro guard: known anti-cheat list = {v}", "Recording"); });
        Opt("Kill Switch", "A Guard Hit Doesn't Just Stop Playback — It Turns The Master Toggle OFF (Every Macro Hotkey Unbinds) Until You Re-Enable It Here. Fail-Safe Default: ON",
            _vm.Cfg.MacroKillSwitch, v => { _vm.Cfg.MacroKillSwitch = v; _vm.Log.Action($"Macro kill switch = {v}", "Recording"); });
        Opt("Encrypt At Rest (DPAPI)", "Store Recordings Encrypted With Windows DPAPI — Tied To YOUR Windows Login, No Password, No Stored Key. A Copied Or Synced Config Exposes Nothing. Zones/Profiles/Hotkeys/Settings Are Never Encrypted; Export Always Writes Plain JSON. Default ON (OFF In Portable Mode, Where The File Travels).",
            _vm.Cfg.EncryptMacros, v => { _vm.Cfg.EncryptMacros = v; _vm.Log.Action($"Macro at-rest encryption (DPAPI) = {v} — config rewritten", "Recording"); });
        // S28 K: countdown is a plain field (0 = off).
        var cdRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 6), VerticalAlignment = VerticalAlignment.Center };
        cdRow.Children.Add(new TextBlock { Text = "Countdown before play:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        var cdBox = new TextBox { Width = 40, Text = _vm.Cfg.MacroCountdownSec.ToString(), ToolTip = "Seconds Of 3-2-1 On The Status Strip Before A Macro Starts Playing (0 = Off, Max 5)" };
        cdBox.LostFocus += (_, _) => { if (int.TryParse(cdBox.Text, out var n)) _vm.Cfg.MacroCountdownSec = Math.Clamp(n, 0, 5); cdBox.Text = _vm.Cfg.MacroCountdownSec.ToString(); _vm.Settings.SaveNoBackup(_vm.Cfg); };
        cdRow.Children.Add(cdBox);
        cdRow.Children.Add(new TextBlock { Text = "s (0 = off)", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), Opacity = 0.6 });
        opts.Children.Add(cdRow);
        var dry = new CheckBox { Content = "Dry Run (Send Nothing)", IsChecked = _vm.MacroSvc.DryRun, Margin = new Thickness(0, 0, 14, 6), Style = (Style)Application.Current.FindResource("ToggleSwitch"), ToolTip = "Test Mode For Macros: Playback Narrates Every Step On The Status Bar But Sends NO Input. Session-Only — Great For Checking A Chain Before Trusting It." };
        dry.Checked += (_, _) => { _vm.MacroSvc.DryRun = true; _vm.Log.Action("Macro dry run ON", "Recording"); Rebuild(); };
        dry.Unchecked += (_, _) => { _vm.MacroSvc.DryRun = false; _vm.Log.Action("Macro dry run OFF", "Recording"); Rebuild(); };
        opts.Children.Add(dry);
        Opt("Show Status Strip", "A Small Always-On-Top Strip (Bottom-Right, Drag It Anywhere) Shown While This Window Is Open And Whenever A Macro Is Recording Or Playing; It Hides A Few Seconds After Activity Ends. Its × Button (Or The Status-Strip Hotkey) Hides It Until The Next Recording.",
            _vm.Cfg.ShowMacroStatusBar, v => _vm.Cfg.ShowMacroStatusBar = v);
        var acList = new Button { Content = "Known Anti-Cheat...", Margin = new Thickness(0, 0, 8, 6), ToolTip = "Choose Which Known Anti-Cheat Services The Guard Watches For — Untick One That Never Minds Macros On Your Setup (e.g. A Service That Runs Permanently)" };
        acList.Click += (_, _) => { new AntiCheatListWindow(_vm) { Owner = this }.ShowDialog(); Rebuild(); };
        opts.Children.Add(acList);
        _policy = Opt($"Block Macros In Profile \"{_vm.Cfg.ActiveProfile}\"", "While This Profile Is The Active One, Macros Are Completely Switched Off: No Macro Hotkeys, No Playback, No Recording — Even With The Master Switch On. Tick This On The Profiles You Use For Online Games.",
            _vm.ActiveProfileMacroPolicy == Profile.PolicyDeny, v => _vm.ActiveProfileMacroPolicy = v ? Profile.PolicyDeny : Profile.PolicyAllow);
        var export = new Button { Content = "Export...", Margin = new Thickness(0, 0, 8, 6), ToolTip = "Save ALL Macros To A JSON File (Asks First — The File Contains Your Literal Keystrokes)" };
        export.Click += (_, _) => ExportMacros();
        var import = new Button { Content = "Import...", Margin = new Thickness(0, 0, 8, 6), ToolTip = "Load Macros From A JSON File — They Arrive DISABLED With No Hotkey Until You Review Each One (Import Quarantine)" };
        import.Click += (_, _) => ImportMacros();
        opts.Children.Add(export);
        opts.Children.Add(import);
        DockPanel.SetDock(opts, Dock.Top);
        root.Children.Add(opts);

        DockPanel.SetDock(_status, Dock.Bottom);
        root.Children.Add(_status);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _rows };
        root.Children.Add(scroll);
        Content = root;

        _vm.MacroSvc.StateChanged += OnSvcState;
        _vm.MacrosWindowOpen = true;
        Closed += (_, _) => { _vm.MacroSvc.StateChanged -= OnSvcState; _vm.MacrosWindowOpen = false; _vm.Settings.SaveNoBackup(_vm.Cfg); };
        // S28 K: footer with revert (macros + module flags snapshot).
        string snap = System.Text.Json.JsonSerializer.Serialize(new MacroSnapshot(_vm.Cfg));
        Helpers.EditorFooter.Wrap(this, root,
            onSave: () => _vm.Settings.SaveNoBackup(_vm.Cfg),
            onCancel: () =>
            {
                if (_vm.MacroSvc.IsRecording) _vm.MacroSvc.StopRecording(save: false);
                _vm.MacroSvc.Stop();
                var back = System.Text.Json.JsonSerializer.Deserialize<MacroSnapshot>(snap);
                back?.Restore(_vm.Cfg);
                _vm.BindHotkeys();
                _vm.Log.Action("Macros window: changes reverted", "Recording");
            });
        Rebuild();
    }

    private void OnSvcState() => Dispatcher.BeginInvoke(() =>
    {
        if (!_vm.MacroSvc.IsRecording && WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Rebuild();
    });

    private void Rebuild()
    {
        bool on = _vm.Cfg.MacrosEnabled;
        bool rec = _vm.MacroSvc.IsRecording;
        _record.IsEnabled = on && !rec && _vm.MacroSvc.PlayingId is null;
        _stopRec.IsEnabled = rec;
        _discard.IsEnabled = rec;
        bool deny = _vm.MacroSvc.ActiveProfileDenies();
        if (_policy is not null && _policy.IsChecked != deny) _policy.IsChecked = deny;
        if (_policy is not null) _policy.Content = $"Block Macros In Profile \"{_vm.Cfg.ActiveProfile}\"";
        // S28 BUG FIX (from testing: kill switch left the toggle looking ON): mirror cfg → checkbox.
        if (_master is not null && _master.IsChecked != on) { _syncing = true; _master.IsChecked = on; _syncing = false; }
        _panic.IsEnabled = _vm.MacroSvc.PlayingId is not null;
        string recHk = string.IsNullOrWhiteSpace(_vm.Cfg.HotkeyMacroRecord) ? "(record hotkey unbound — set it in Hotkeys…)" : $"press {_vm.Cfg.HotkeyMacroRecord}";
        string? guardHit = on && !rec ? _vm.MacroSvc.FindRunningWatched() : null;
        _status.Text = !on
            ? "Macros Are OFF — Nothing Binds, Records, Or Plays While The Master Toggle Is Off." + (_vm.Cfg.MacroKillSwitch ? " (If The Kill Switch Tripped, Re-Enable Here When It's Safe.)" : "")
            : deny ? $"Macros Are BLOCKED In Profile \"{_vm.Cfg.ActiveProfile}\" — No Hotkeys, No Playback, No Recording Until You Switch Profile Or Untick The Block."
            : rec ? $"● RECORDING — Use The PC Normally; {recHk} To Stop & Save. (Cap: 20,000 Steps.)"
            : _vm.MacroSvc.CountdownRemaining > 0 ? $"▶ Starting In {_vm.MacroSvc.CountdownRemaining}… (Hands Off The Keyboard)"
            : _vm.MacroSvc.IsPaused ? "⏸ PAUSED — Press The Pause Hotkey Again To Resume (Ctrl+Alt+X Stops)"
            : _vm.MacroSvc.DryStep is string ds ? $"▶ DRY RUN — {ds}"
            : _vm.MacroSvc.WaitingFor is string wf ? $"▶ PLAYING — Waiting For A Window Titled '{wf}'…"
            : _vm.MacroSvc.PlayingId is not null ? "▶ PLAYING — Ctrl+Alt+X, The Macro's Hotkey, Or ■ Panic Stop Halts It."
            : guardHit is not null ? $"⚠ GUARD ACTIVE — '{guardHit}' Is Running: Playback Will Be Refused. (Known Anti-Cheat… Lets You Exclude It.)"
            : $"Ready — {recHk} anywhere to start recording, again to stop. Double-click a macro to edit its steps. Ctrl+Alt+X is the panic stop while playing.";

        _rows.Children.Clear();
        foreach (var mac in _vm.Cfg.Macros.ToList())
        {
            var m = mac;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = false, IsEnabled = on, Background = System.Windows.Media.Brushes.Transparent };
            row.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) OpenEditor(m); };
            var rowMenu = new ContextMenu();
            var editItem = new MenuItem { Header = "Edit Steps..." };
            editItem.Click += (_, _) => OpenEditor(m);
            rowMenu.Items.Add(editItem);
            row.ContextMenu = rowMenu;

            var en = new CheckBox
            {
                Style = (Style)Application.Current.FindResource("ToggleSwitch"),
                IsChecked = m.Enabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Enable/Disable This Macro (Its Hotkey Unbinds While Disabled)",
            };
            en.Checked += (_, _) => { m.Enabled = true; _vm.BindHotkeys(); };
            en.Unchecked += (_, _) => { m.Enabled = false; _vm.BindHotkeys(); };
            DockPanel.SetDock(en, Dock.Left);
            row.Children.Add(en);

            var info = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Text = $"{m.Name}  —  {m.Events.Count:N0} steps, {m.DurationMs / 1000.0:0.0}s, speed {m.Speed * 100:0}%, plays {(m.Repeat == 0 ? "until stopped" : m.Repeat == 1 ? "once" : $"{m.Repeat}×")}{(m.Jitter > 0 ? $", wobble {m.Jitter}px" : "")}{(m.Hotkey.Length > 0 ? $"  [{m.Hotkey}]" : "")}",
                ToolTip = "Steps · Length · Speed (100% = As Recorded) · Repeat · Wobble · [Hotkey] — Double-Click To Open The Step Editor",
            };
            DockPanel.SetDock(info, Dock.Left);
            row.Children.Add(info);

            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(btns, Dock.Right);
            void B(string text, string tip, Action act)
            {
                var b = new Button { Content = text, Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
                b.Click += (_, _) => act();
                btns.Children.Add(b);
            }
            bool playingThis = _vm.MacroSvc.PlayingId == m.Id;
            B(playingThis ? "Stop" : "Play",
              playingThis ? "Stop This Playback" : "Play Now (All Guards Apply)",
              () => { if (_vm.MacroSvc.PlayingId == m.Id) _vm.MacroSvc.Stop(); else _vm.MacroSvc.Play(m); Rebuild(); });
            B("Steps...", "Open The Step Editor — See Every Recorded Click, Key, And Delay; Search, Delete Steps, Change Timing", () => OpenEditor(m));
            B("Hotkey...", "Assign An Optional Global Hotkey (e.g. Ctrl+Shift+F7) — Duplicates Across ALL Features Are Refused; Empty Clears", () =>
            {
                var key = HotkeyCaptureDialog.Prompt(this, _vm, $"Hotkey — {m.Name}", m.Hotkey, k => FindMacroConflict(k, m.Id));
                if (key is null) return;
                m.Hotkey = key;
                _vm.Log.Action($"Macro hotkey: {m.Name} → {(key.Length > 0 ? key : "(cleared)")}", "Recording");
                _vm.BindHotkeys();
                Rebuild();
            });
            B("Speed...", "Playback Speed As A Percentage: 100% = Exactly As Recorded, 200% = Twice As Fast, 50% = Half Speed (10%–1000%)", () =>
            {
                var v = InputDialog.Prompt(this, $"Speed — {m.Name}", "Playback speed in % (100 = as recorded, 200 = twice as fast, 50 = half speed):", (m.Speed * 100).ToString("0"));
                if (v is null || !double.TryParse(v.TrimEnd('%', ' '), out var pct)) return;
                m.Speed = Math.Clamp(pct / 100.0, 0.1, 10.0);
                Rebuild();
            });
            B("Repeat...", "How Many Times To Play In A Row: 1 = Once, 3 = Three Times, 0 = Keep Looping Until You Stop It (Max 1000)", () =>
            {
                var v = InputDialog.Prompt(this, $"Repeat — {m.Name}", "How many times to play? (1 = once, 0 = keep looping until stopped):", m.Repeat.ToString());
                if (v is null || !int.TryParse(v, out var n)) return;
                m.Repeat = Math.Clamp(n, 0, 1000);
                Rebuild();
            });
            B("Wobble...", "Human Wobble: Lets Each Mouse Action Land Up To This Many Pixels Off Its Recorded Spot, At Random, So Replays Aren't Pixel-Identical (0 = Exact, 3 = Subtle, 10 = Loose, Max 20)", () =>
            {
                var v = InputDialog.Prompt(this, $"Wobble — {m.Name}", "Wobble in pixels (0 = exact replay, 3 = subtle, 10 = loose, max 20):", m.Jitter.ToString());
                if (v is null || !int.TryParse(v, out var n)) return;
                m.Jitter = Math.Clamp(n, 0, 20);
                Rebuild();
            });
            B(m.NextMacroId.Length > 0 ? "Then Play: ✓" : "Then Play…", "Chain: When This Macro Finishes On Its Own, Play Another One Next (Up To 5 In A Row, Never Itself). Choose '(none)' To Unchain.", () =>
            {
                var names = new List<string> { "(none)" };
                names.AddRange(_vm.Cfg.Macros.Where(o => o.Id != m.Id).Select(o => o.Name));
                var pick = InputDialog.Prompt(this, $"Then Play — {m.Name}", "Type the exact name of the macro to play next (or leave blank for none):\n" + string.Join(", ", names.Skip(1)), _vm.Cfg.Macros.Find(o => o.Id == m.NextMacroId)?.Name ?? "");
                if (pick is null) return;
                var target = _vm.Cfg.Macros.FirstOrDefault(o => o.Id != m.Id && string.Equals(o.Name, pick.Trim(), StringComparison.OrdinalIgnoreCase));
                m.NextMacroId = target?.Id ?? "";
                _vm.Log.Action($"Macro chain: {m.Name} → {(target?.Name ?? "(none)")}", "Recording"); Rebuild();
            });
            B(m.Relative ? "Relative: ON" : "Relative: OFF", $"Relative Replay: Follow The Window That Was In Front When Recording Started{(m.AnchorTitle.Length > 0 ? $" ('{m.AnchorTitle}')" : "")} — If It Has Moved, Every Mouse Position Shifts With It. OFF = Play At The Recorded Screen Positions.", () =>
            {
                if (m.AnchorTitle.Length == 0) { ToastService.Show("This Macro Has No Anchor Window (Recorded Before Relative Replay Existed, Or Nothing Was In Front)", ToastType.Warning, 5000); return; }
                m.Relative = !m.Relative; Rebuild();
            });
            B(m.StopKey > 0 ? $"Stop Key: {KeyName(m.StopKey)}" : "Stop Key…", "Repeat-Until-Key: While This Macro Loops, Pressing This Key Stops It (Besides Ctrl+Alt+X). Type A Key Name Like F8, Escape, Space; Blank Clears.", () =>
            {
                var v = InputDialog.Prompt(this, $"Stop Key — {m.Name}", "Key that stops this macro while it plays (e.g. F8, Escape, Space; blank = none):", m.StopKey > 0 ? KeyName(m.StopKey) : "");
                if (v is null) return;
                v = v.Trim();
                if (v.Length == 0) { m.StopKey = 0; Rebuild(); return; }
                if (Enum.TryParse<System.Windows.Input.Key>(v, true, out var key)) { m.StopKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key); Rebuild(); }
                else ToastService.Show($"Unknown Key Name '{v}'", ToastType.Warning);
            });
            B("Rename...", "Rename This Macro", () =>
            {
                var n = InputDialog.Prompt(this, "Rename Macro", "New Name:", m.Name);
                if (string.IsNullOrWhiteSpace(n)) return;
                m.Name = n.Trim();
                Rebuild();
            });
            B("Delete", "Delete This Macro (Asks First)", () =>
            {
                if (!ConfirmDialog.Confirm(this, "Delete Macro", $"Delete \"{m.Name}\" ({m.Events.Count} Events)?", "Delete", "Keep")) return;
                _vm.MacroSvc.Stop();
                _vm.Cfg.Macros.Remove(m);
                _vm.Log.Action($"Macro deleted: {m.Name}", "Recording");
                _vm.BindHotkeys();
                _vm.Settings.SaveNoBackup(_vm.Cfg);
                Rebuild();
            });
            row.Children.Add(btns);
            _rows.Children.Add(row);
        }
        if (_vm.Cfg.Macros.Count == 0)
            _rows.Children.Add(new TextBlock
            {
                Text = on ? "No Macros Yet — Record New Captures Everything You Do Until You Stop."
                          : "Turn The Master Toggle On To Begin.",
                Opacity = 0.5,
            });
    }

    /// <summary>S28 K: what Cancel (Revert) restores.</summary>
    private sealed class MacroSnapshot
    {
        public List<Macro> Macros { get; set; } = new();
        public bool Enabled { get; set; } public bool RecordPath { get; set; } public bool Guard { get; set; } public bool Kill { get; set; }
        public bool Strip { get; set; } public bool Encrypt { get; set; } public int Countdown { get; set; } public List<string> Ignored { get; set; } = new();
        public MacroSnapshot() { }
        public MacroSnapshot(AppSettings c) { Macros = System.Text.Json.JsonSerializer.Deserialize<List<Macro>>(System.Text.Json.JsonSerializer.Serialize(c.Macros)) ?? new(); Enabled = c.MacrosEnabled; RecordPath = c.MacroRecordPath; Guard = c.MacroGuardKnownAntiCheat; Kill = c.MacroKillSwitch; Strip = c.ShowMacroStatusBar; Encrypt = c.EncryptMacros; Countdown = c.MacroCountdownSec; Ignored = new(c.MacroGuardIgnored); }
        public void Restore(AppSettings c) { c.Macros = Macros; c.MacrosEnabled = Enabled; c.MacroRecordPath = RecordPath; c.MacroGuardKnownAntiCheat = Guard; c.MacroKillSwitch = Kill; c.ShowMacroStatusBar = Strip; c.EncryptMacros = Encrypt; c.MacroCountdownSec = Countdown; c.MacroGuardIgnored = Ignored; }
    }

    private static string KeyName(int vk) { try { return System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk).ToString(); } catch (Exception) { return $"VK{vk}"; } }

    private void OpenEditor(Macro m)
    {
        if (_vm.MacroSvc.IsRecording || _vm.MacroSvc.PlayingId == m.Id) { ToastService.Show("Stop Recording/Playback Before Editing Steps", ToastType.Warning); return; }
        var w = new MacroEditorWindow(_vm, m) { Owner = this };
        w.ShowDialog();
        Rebuild();
    }

    // ── Export / Import (S28) — a SEPARATE deliberate path; sharing is never
    // blocked, but the user is warned the file carries literal keystrokes, and
    // anything imported lands quarantined (disabled, unbound) until reviewed.
    private void ExportMacros()
    {
        if (_vm.Cfg.Macros.Count == 0) { ToastService.Show("No Macros To Export", ToastType.Info); return; }
        if (!ConfirmDialog.Confirm(this, "Export Macros",
                $"The export file will contain every keystroke in all {_vm.Cfg.Macros.Count} macro(s) — as plain text. Anyone who gets the file can read what you typed. Continue?",
                "Export", "Cancel")) return;
        var dlg = new SaveFileDialog { Filter = "OnTop Macros (*.json)|*.json", FileName = "OnTop_Macros.json" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            _vm.Settings.ExportMacrosFile(dlg.FileName, _vm.Cfg.Macros);
            _vm.Log.Action($"Macros exported: {_vm.Cfg.Macros.Count} ({dlg.FileName})", "Recording");
            ToastService.Show($"Exported {_vm.Cfg.Macros.Count} Macro(s)", ToastType.Success);
        }
        catch (Exception ex)
        {
            _vm.Log.Error($"Macro export failed: {ex.Message}", "Recording");
            ToastService.Show("Macro Export Failed — See Log", ToastType.Error);
        }
    }

    private void ImportMacros()
    {
        var dlg = new OpenFileDialog { Filter = "OnTop Macros (*.json)|*.json|All Files (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            string bak = _vm.Settings.WriteBackup(_vm.Cfg);
            _vm.Log.Action($"Pre-import backup written: {bak}", "Config");
            var quarantine = new List<QuarantineItem>();
            var incoming = _vm.Settings.ImportMacrosFile(dlg.FileName, quarantine);
            int room = Math.Max(0, 50 - _vm.Cfg.Macros.Count);
            if (incoming.Count > room)
            {
                ToastService.Show($"Only {room} Of {incoming.Count} Fit Under The 50-Macro Cap — Rest Skipped", ToastType.Warning, 6000);
                incoming = incoming.Take(room).ToList();
            }
            foreach (var m in incoming) _vm.Cfg.Macros.Add(m);
            var included = new HashSet<string>(incoming.Select(m => m.Name));   // cap-skipped ones aren't reviewed
            _vm.Log.Action($"Macros imported (quarantined): {incoming.Count} ({dlg.FileName})", "Recording");
            int kept = ImportReviewWindow.Review(this, "OnTop — Import Review",
                $"These macros came from outside OnTop ({System.IO.Path.GetFileName(dlg.FileName)}). They arrived DISABLED with no hotkey. Tick the ones you recognize to enable them (and restore their hotkeys); unticked ones stay inert until you enable them by hand. Hotkeys are still conflict-checked at bind time.",
                quarantine.Where(q => included.Contains(q.Subject)).ToList());
            foreach (var q in quarantine)
                _vm.Log.Action($"Import review — {(q.Keep ? "ENABLED" : "kept inert")}: {q.Category} / {q.Subject} — {q.Detail}", "Config");
            // A restored hotkey may collide with an existing one — refuse it here.
            foreach (var m in incoming.Where(m => m.Hotkey.Length > 0))
                if (FindMacroConflict(m.Hotkey, m.Id) is string conflict)
                {
                    _vm.Log.Warn($"Imported macro '{m.Name}' hotkey dropped: {conflict}", "Recording");
                    m.Hotkey = "";
                }
            _vm.BindHotkeys();
            _vm.Settings.SaveNoBackup(_vm.Cfg);
            ToastService.Show($"Imported {incoming.Count} Macro(s) — {kept} Enabled", incoming.Count > 0 ? ToastType.Success : ToastType.Info);
            Rebuild();
        }
        catch (Exception ex)
        {
            _vm.Log.Error($"Macro import failed: {ex.Message}", "Recording");
            ToastService.Show("Macro Import Failed — See Log", ToastType.Error);
        }
    }

    /// <summary>One-combo-one-feature — delegates to the VM's single shared check (S28 Stage F).</summary>
    private string? FindMacroConflict(string proposed, string macroId) => _vm.FindHotkeyConflict(proposed, exceptMacroId: macroId);
}
