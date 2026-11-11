using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// App-level settings, fleshed out to v9.5 shape: grouped sections for
/// Appearance, Logging, and Data, with backup/restore, folder shortcuts, and
/// Log Viewer access. Recording mode + theme/density slots land with the
/// Draw-zone and visual ports.
/// </summary>
public class SettingsDialog : Window
{
    private readonly MainViewModel _vm;
    private readonly Action? _openLogViewer;

    public SettingsDialog(MainViewModel vm, Action? openLogViewer = null)
    {
        _vm = vm;
        _openLogViewer = openLogViewer;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Settings";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(20, 14, 20, 18) };

        // ── Features (S28 — Standing Design Law: every non-core feature has an
        // explicit off-switch, all in one place; Minimal Mode flips them all) ──
        var features = Section("Features");
        features.Children.Add(Note("Only Always-On-Top Pinning + Zone Detection Are Always On. Everything Below Is Optional And Genuinely Inert When Off — Strip OnTop Down For Anti-Cheat Compatibility Or Just Because It's Yours."));
        var featWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        CheckBox Feat(string label, string prop, string tip)
        {
            var cb = Toggle(label, prop);
            cb.Margin = new Thickness(0, 2, 16, 4);
            cb.ToolTip = tip;
            featWrap.Children.Add(cb);
            return cb;
        }
        var macrosCb = Feat("Macros", nameof(AppSettings.MacrosEnabled), "The Macro Module (Record/Playback). Off = No Hooks, No Hotkeys, Nothing Plays. Same Switch As The Macros Window's Master Toggle.");
        macrosCb.ToolTip = Helpers.WarnTip.Make("The Macro Module (Record/Playback). Off = No Hooks, No Hotkeys, Nothing Plays. Same Switch As The Macros Window's Master Toggle.", "Records Literal Keystrokes; Synthetic Playback Can Be Detected By Anti-Cheat.");
        macrosCb.Checked += (_, _) => _vm.BindHotkeys();
        macrosCb.Unchecked += (_, _) => { _vm.MacroSvc.Stop(); _vm.BindHotkeys(); };
        Feat("Auto-Pin Rules", nameof(AppSettings.WindowRulesEnabled), "").ToolTip = Helpers.WarnTip.Make("Window Rules Scanner (Every 3 s). Off = No Scanning, No Automatic Pins.", "A Background Window-Title Scanner — Some Anti-Cheat Software Dislikes These. Leave Off On Gaming Profiles If In Doubt.");
        Feat("Auto-Close", nameof(AppSettings.AutoCloseEnabled), "Exit OnTop When A Trigger Process Runs. Off = No Polling.");
        Feat("Update Check At Startup", nameof(AppSettings.UpdateCheckOnStartup), "").ToolTip = Helpers.WarnTip.Make("Quiet Version Check After Launch (Only If A Manifest URL Is Set). Off = No Network Call Ever.", "The Only Feature That Uses The Network — A Tiny Https Text Fetch, Never A Download-And-Run.");
        Feat("Statistics", nameof(AppSettings.StatsEnabled), "Usage Counters + Activity Feed. Off = Nothing Is Counted Or Written (Existing Stats Are Kept).");
        Feat("Zone Actions", nameof(AppSettings.ZoneActionsEnabled), "").ToolTip = Helpers.WarnTip.Make("Run Command / Kill Process / Toggle Window / Run Macro Zone Actions. Off = Such Zones Refuse And Toast. Exit And Stealth Always Work.", "Run Command Launches Whatever The Zone Says; Kill Process Ends Programs By Name. Review Imported Zones Before Enabling.");
        Feat("Stealth Auto-Watcher", nameof(AppSettings.StealthWatcherEnabled), "").ToolTip = Helpers.WarnTip.Make("The Watcher That Engages Stealth When A Watched Process Runs. Off = Timer Inert (Manual Stealth Still Works).", "A Background Process-Name Scanner (Names Only, Never Memory).");
        Feat("Display Auto-Disable", nameof(AppSettings.AutoDisableOffscreenZones), "Disable Zones That Fall Off Every Monitor After A Display Change.");
        Feat("Profile Rules", nameof(AppSettings.ProfileRulesEnabled), "").ToolTip = Helpers.WarnTip.Make("Process → Profile: While A Listed Program Runs, Switch To Its Profile (And Back When It Exits). Edit Rules With The Button Below.", "A Background Process-Name Scanner (Names Only).");
        Feat("Display Profiles", nameof(AppSettings.DisplayProfilesEnabled), "Monitor Layout → Profile: When You Dock/Undock, Switch To The Profile You Bound To That Layout (Settings → Displays → Bind).");
        Feat("Pin Memory", nameof(AppSettings.PinMemoryEnabled), "Remember How Each Program Was Last Pinned (Position, Size, Opacity, Click-Through) And Reapply On Its Next Pin.");
        Feat("Zone Hit Flash", nameof(AppSettings.ZoneHitFlash), "Pulse The Zone Overlay Where A Real Gesture Landed (Not Just In Test Mode).");
        Feat("Idle Rule", nameof(AppSettings.IdleRuleEnabled), "After N Minutes Without Input: Switch Profile / Unpin All / Enter Stealth (Configure Below). Profile Reverts When You're Back.");
        Feat("Profile Schedules", nameof(AppSettings.ProfileSchedulesEnabled), "Time-Of-Day Profiles (Work Hours, Weekends) — Edit With The Button Below. Process Rules Win Over Schedules.");
        Feat("Hold Chords", nameof(AppSettings.HoldChordsEnabled), "").ToolTip = Helpers.WarnTip.Make("Hold-To-Peek And Hold-To-Pin (Set The Chords In Hotkeys…). Off = No Keyboard Hook Exists.", "Installs A Keyboard Hook ONLY While On AND A Chord Is Set — Removed In Stealth And On Exit. It Compares Two Chords And Stores Nothing.");
        Feat("Do Not Disturb", nameof(AppSettings.DoNotDisturb), "Quiet Routine Toasts While A Stealth-Watched Or Known Anti-Cheat Process Is Running. Errors Still Show; The Log Still Gets Everything.");
        Feat("Action Library", nameof(AppSettings.ActionLibraryEnabled), "").ToolTip = Helpers.WarnTip.Make("Named Reusable Actions Bound To Hotkeys And/Or Zones (APP → Actions…). Off = No Library Hotkeys, Zones Refuse.", "Library Actions Can Run Commands, Kill Processes And Play Macros From A Hotkey — The Same Guards Apply.");
        Feat("Window Placement", nameof(AppSettings.PlacementEnabled), "").ToolTip = Helpers.WarnTip.Make("Move Programs To Set Monitors/Positions On Startup, Hotkey, Or Zone (APP → Placement…). Off = Rules Inert.", "Its Optional LAUNCH Half Starts Programs By Path — Gated Off Separately Inside Placement…, Off By Default.");
        features.Children.Add(featWrap);
        var rulesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        rulesRow.Children.Add(Btn("Profile Rules…", (_, _) => new Views.ProfileRulesWindow(_vm) { Owner = this }.ShowDialog()));
        rulesRow.Children.Add(Btn("Schedules…", (_, _) => new Views.ProfileSchedulesWindow(_vm) { Owner = this }.ShowDialog()));
        rulesRow.Children.Add(Btn("Idle Rule…", (_, _) =>
        {
            var m = InputDialog.Prompt(this, "Idle Rule", "Minutes without input before it fires (1–1440):", _vm.Cfg.IdleMinutes.ToString());
            if (m is null || !int.TryParse(m, out var mins)) return;
            _vm.Cfg.IdleMinutes = Math.Clamp(mins, 1, 1440);
            var a = InputDialog.Prompt(this, "Idle Rule", "Action: 1 = switch to a profile, 2 = unpin all, 3 = enter stealth, 0 = nothing:", _vm.Cfg.IdleAction.ToString());
            if (a is null || !int.TryParse(a, out var act)) return;
            _vm.Cfg.IdleAction = Math.Clamp(act, 0, 3);
            if (_vm.Cfg.IdleAction == 1) { var p = InputDialog.Prompt(this, "Idle Rule", "Profile name to switch to while idle:\n" + string.Join(", ", _vm.ProfileNames), _vm.Cfg.IdleProfile); if (p is not null) _vm.Cfg.IdleProfile = p.Trim(); }
            Services.ToastService.Show($"Idle Rule: {_vm.Cfg.IdleMinutes} min → action {_vm.Cfg.IdleAction}", Services.ToastType.Success);
        }));
        rulesRow.Children.Add(new TextBlock { Text = "Process → Profile Rules (Autopilot For Game Profiles).", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        features.Children.Add(rulesRow);
        var minimalRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        minimalRow.Children.Add(Btn("Minimal Mode", (_, _) =>
        {
            if (ConfirmDialog.Confirm(this, "Minimal Mode",
                    "Turn OFF every optional feature — macros, auto-pin, auto-close, update check, statistics, zone actions, stealth auto-watcher, display auto-disable? Only pinning + zone detection stay active. You can switch any of them back on individually.",
                    "Go Minimal", "Cancel"))
                _vm.ApplyMinimalMode();
        }));
        minimalRow.Children.Add(new TextBlock
        {
            Text = "One Click: Everything Optional Off. Reversible Per Switch.",
            Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
        });
        features.Children.Add(minimalRow);
        root.Children.Add(Wrap("Features", features));

        // ── Performance (S28) ────────────────────────────────────────────
        var perf = Section("Performance");
        var lowImpact = Toggle("Low Impact Mode", nameof(AppSettings.LowImpactMode));
        lowImpact.ToolTip = "Below-Normal Process Priority + Slower Background Timers (Auto-Pin 3 s → 10 s, Stealth Watcher 5 s → 15 s). OnTop Always Yields To A Game. The Mouse Hook Is Never Slowed.";
        perf.Children.Add(lowImpact);
        var retryRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
        retryRow.Children.Add(new TextBlock { Text = "Hotkey Re-Bind Retry:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var retryBox = new TextBox { Width = 60, Text = _vm.Cfg.HotkeyRetrySeconds.ToString(), ToolTip = "When Another Program Already Holds One Of Your Hotkeys, OnTop Re-Tries Binding It Every N Seconds (5–600). Shorter = Faster Takeover Once The Other App Releases It; Longer = Fewer Log Lines." };
        retryBox.LostFocus += (_, _) => { if (int.TryParse(retryBox.Text, out var n)) { _vm.Cfg.HotkeyRetrySeconds = Math.Clamp(n, 5, 600); retryBox.Text = _vm.Cfg.HotkeyRetrySeconds.ToString(); } else retryBox.Text = _vm.Cfg.HotkeyRetrySeconds.ToString(); };
        retryRow.Children.Add(retryBox);
        retryRow.Children.Add(new TextBlock { Text = "seconds (5–600; default 30)", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        perf.Children.Add(retryRow);
        perf.Children.Add(Note("OnTop Is Idle Almost All The Time. Low Impact Mode Makes Windows Give Every Core To Your Game First. (Pinning OnTop To Specific Cores Would Not Help — See Help → Performance.)"));
        root.Children.Add(Wrap("Performance", perf));

        // ── Appearance ──────────────────────────────────────────────────
        var appearance = Section("Appearance");
        appearance.Children.Add(Toggle("Mica Backdrop (Windows 11)", nameof(AppSettings.MicaEnabled)));
        var minTray = Toggle("Minimize To Tray", nameof(AppSettings.MinimizeToTray));
        minTray.ToolTip = "Minimizing The Main Window Hides It To The Tray Instead Of The Taskbar (It Always Shows In The Taskbar While Open)";
        appearance.Children.Add(minTray);
        var trayStay = Toggle("Tray Menu Stays Open After Clicking", nameof(AppSettings.TrayMenuStaysOpen));
        trayStay.ToolTip = "Keep The Tray Menu Open After Selecting An Item — Toggle Several Things In One Visit. It Closes Shortly After Your Cursor Leaves It.";
        appearance.Children.Add(trayStay);
        // S28 Stage I: theme (dark / high contrast / light) — live.
        var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        themeRow.Children.Add(new TextBlock { Text = "Theme:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var themeCombo = new ComboBox { MinWidth = 130, ToolTip = "Dark Is The Designed Look. High Contrast Brightens Text And Borders. Light Is A Best-Effort Palette Swap — Expect Rough Edges." };
        foreach (var n in new[] { "Dark", "High Contrast", "Light" }) themeCombo.Items.Add(n);
        themeCombo.SelectedIndex = Math.Clamp(_vm.Cfg.UiTheme, 0, 2);
        themeCombo.SelectionChanged += (_, _) => { if (themeCombo.SelectedIndex >= 0 && themeCombo.SelectedIndex != _vm.Cfg.UiTheme) _vm.Cfg.UiTheme = themeCombo.SelectedIndex; };
        themeRow.Children.Add(themeCombo);
        appearance.Children.Add(themeRow);
        var accentRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        accentRow.Children.Add(new TextBlock { Text = "Accent Color:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var accentCombo = new ComboBox { MinWidth = 110, ToolTip = "Pick The Accent Color Used Across The App — Applies Immediately" };
        foreach (var a in Helpers.ThemeOptions.Accents) accentCombo.Items.Add(a.Name);
        accentCombo.SelectedIndex = Math.Clamp(_vm.Cfg.AccentIndex, 0, Helpers.ThemeOptions.Accents.Length - 1);
        accentCombo.SelectionChanged += (_, _) =>
        {
            if (accentCombo.SelectedIndex >= 0 && accentCombo.SelectedIndex != _vm.Cfg.AccentIndex)
            {
                _vm.Cfg.AccentIndex = accentCombo.SelectedIndex;
                Helpers.ThemeOptions.ApplyAccent(_vm.Cfg.AccentIndex);   // S28: live (the brush is mutated in place)
                Services.ToastService.Show($"Accent: {Helpers.ThemeOptions.Accents[_vm.Cfg.AccentIndex].Name}", Services.ToastType.Success);
            }
        };
        accentRow.Children.Add(accentCombo);
        accentRow.Children.Add(new TextBlock { Text = "(Restart To Apply)", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
        appearance.Children.Add(accentRow);
        // 0.45.3: "do this first" hint popups (NoticePopup) — on/off + seconds.
        var hintRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };
        var hintToggle = Toggle("Hint Popups", nameof(AppSettings.HintPopupsEnabled));
        hintToggle.VerticalAlignment = VerticalAlignment.Center;
        hintToggle.ToolTip = "Hints Like \"Select A Zone First\" Fade In Over The Window You Clicked In, Then Fade Out. Off Shows Them As A Small Toast Instead.";
        hintRow.Children.Add(hintToggle);
        var hintSecsLabel = new TextBlock { Text = "Stay Up For", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 6, 0) };
        var hintSecs = new TextBox
        {
            Width = 44, Text = _vm.Cfg.NoticeSeconds.ToString(), MaxLength = 2,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Seconds A Hint Stays Up (1–60). Hovering The Hint Keeps It Up Longer.",
        };
        var hintSecsUnit = new TextBlock { Text = "Seconds", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        // Digits only while typing; the value is checked when you leave the box
        // or press Enter. Anything outside 1–60 (including 0 and blank) snaps
        // back to the last good value.
        hintSecs.PreviewTextInput += (_, e) => e.Handled = !e.Text.All(char.IsDigit);
        DataObject.AddPastingHandler(hintSecs, (_, e) =>
        {
            if (e.DataObject.GetData(typeof(string)) is not string t || !t.All(char.IsDigit)) e.CancelCommand();
        });
        void CommitHintSecs()
        {
            if (int.TryParse(hintSecs.Text, out int v) && v is >= 1 and <= 60) _vm.Cfg.NoticeSeconds = v;
            else Services.ToastService.Show("Hint Time Must Be 1–60 Seconds", Services.ToastType.Warning, 3000);
            hintSecs.Text = _vm.Cfg.NoticeSeconds.ToString();
        }
        hintSecs.LostKeyboardFocus += (_, _) => CommitHintSecs();
        hintSecs.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { CommitHintSecs(); e.Handled = true; } };
        foreach (var el in new FrameworkElement[] { hintSecsLabel, hintSecs, hintSecsUnit })
            el.SetBinding(IsEnabledProperty, Bind(nameof(AppSettings.HintPopupsEnabled), BindingMode.OneWay));
        hintRow.Children.Add(hintSecsLabel);
        hintRow.Children.Add(hintSecs);
        hintRow.Children.Add(hintSecsUnit);
        var hintPreview = new Button { Content = "Preview", Margin = new Thickness(8, 0, 0, 0), ToolTip = "Show A Sample Hint With The Current Setting" };
        hintPreview.Click += (_, _) => NoticePopup.Show("Sample Hint", "This is how a \"do this first\" hint looks. Click it to dismiss early; hover to keep it up.", this);
        hintRow.Children.Add(hintPreview);
        appearance.Children.Add(hintRow);
        var opacityRow = new Grid { Margin = new Thickness(0, 8, 0, 2) };
        opacityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        opacityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        opacityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        var opacityLabel = Body("Overlay Opacity:");
        opacityLabel.Margin = new Thickness(0, 0, 10, 0);
        var opacitySlider = new Slider { Minimum = 0.25, Maximum = 1.0, VerticalAlignment = VerticalAlignment.Center };
        opacitySlider.SetBinding(Slider.ValueProperty,
            Bind(nameof(AppSettings.OverlayOpacity), BindingMode.TwoWay));
        var opacityValue = Body("");
        opacityValue.HorizontalAlignment = HorizontalAlignment.Right;
        opacityValue.SetBinding(TextBlock.TextProperty,
            new Binding(nameof(AppSettings.OverlayOpacity)) { Source = vm.Cfg, StringFormat = "{0:P0}" });
        Grid.SetColumn(opacityLabel, 0);
        Grid.SetColumn(opacitySlider, 1);
        Grid.SetColumn(opacityValue, 2);
        opacityRow.Children.Add(opacityLabel);
        opacityRow.Children.Add(opacitySlider);
        opacityRow.Children.Add(opacityValue);
        appearance.Children.Add(opacityRow);
        appearance.Children.Add(Note("Mica Applies To Open Windows Immediately. Theme And Density Return With The Visual Port."));
        root.Children.Add(Wrap("Appearance", appearance));

        // ── Displays ────────────────────────────────────────────────────
        var displays = Section("Displays");
        var bindRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        bindRow.Children.Add(Btn("Bind This Layout → Active Profile", (_, _) => _vm.BindActiveProfileToDisplays()));
        bindRow.Children.Add(new TextBlock { Text = $"Now: {Helpers.DisplaySignature.Friendly()} (Needs 'Display Profiles' In Features)", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 300 });
        displays.Children.Add(bindRow);
        var autoDisable = Toggle("Auto-Disable Zones That Fall Off-Screen", nameof(AppSettings.AutoDisableOffscreenZones));
        autoDisable.ToolTip = "When Monitors Are Added/Removed (Dock/Undock), Zones Left With No Monitor Under Them Are Disabled Automatically (A Toast Lists Them)";
        displays.Children.Add(autoDisable);
        root.Children.Add(Wrap("Displays", displays));

        // ── Updates (stage-one check & notify — see SECURITY.md) ────────
        var updates = Section("Updates");
        var urlRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = true };
        var urlLabel = new TextBlock
        {
            Text = "Release Manifest URL (HTTPS):",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        DockPanel.SetDock(urlLabel, Dock.Left);
        var urlBox = new TextBox
        {
            ToolTip = "HTTPS Address Of The Tiny JSON Release Manifest (See installer\\UPDATES.md). Leave EMPTY To Keep The Update Feature Fully Dormant — No Network Access Ever Happens.",
        };
        urlBox.SetBinding(TextBox.TextProperty, Bind(nameof(AppSettings.UpdateManifestUrl), BindingMode.TwoWay));
        urlRow.Children.Add(urlLabel);
        urlRow.Children.Add(urlBox);
        updates.Children.Add(urlRow);
        var updStartup = Toggle("Check For Updates At Startup", nameof(AppSettings.UpdateCheckOnStartup));
        updStartup.ToolTip = "Quiet Version Check A Few Seconds After Launch (Does Nothing While The URL Is Empty; Failures Never Nag)";
        // Overshadowed-control cue (S25 audit): the toggle is meaningless while
        // no manifest URL is set — dim it live, same pattern as Slider Lock.
        void SyncStartupEnabled() => updStartup.IsEnabled = !string.IsNullOrWhiteSpace(urlBox.Text);
        SyncStartupEnabled();
        urlBox.TextChanged += (_, _) => SyncStartupEnabled();
        updates.Children.Add(updStartup);
        var checkRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        checkRow.Children.Add(Btn("Check For Updates Now", async (_, _) =>
            await Services.UpdateService.CheckAsync(_vm, manual: true)));
        updates.Children.Add(checkRow);
        root.Children.Add(Wrap("Updates", updates));

        // ── Stealth follow-ups (S26) ────────────────────────────────────
        var stealth = Section("Stealth");
        var watchRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = true };
        var watchLabel = new TextBlock
        {
            Text = "Auto-Stealth While Running:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        DockPanel.SetDock(watchLabel, Dock.Left);
        var watchBox = new TextBox
        {
            ToolTip = "Process Names, Comma Or Semicolon Separated (e.g. FortniteClient-Win64-Shipping; valorant). While ANY Is Running, Stealth Engages Automatically And Releases When They're All Gone. Leave Empty To Disable.",
        };
        watchBox.SetBinding(TextBox.TextProperty, Bind(nameof(AppSettings.StealthWatchedProcesses), BindingMode.TwoWay));
        watchRow.Children.Add(watchLabel);
        watchRow.Children.Add(watchBox);
        stealth.Children.Add(watchRow);
        var hideOv = Toggle("Hide Overlays While Stealthed", nameof(AppSettings.HideOverlaysInStealth));
        hideOv.ToolTip = "Zone Overlays Are Visible Windows — Hiding Them While Stealthed Completes The Disappearance (Recommended)";
        stealth.Children.Add(hideOv);
        stealth.Children.Add(new TextBlock
        {
            Text = "A Toggle-Stealth Hotkey Is Available In The Hotkeys Editor.",
            Opacity = 0.5,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(Wrap("Stealth", stealth));

        // ── Maintenance (S27: tour re-show + soft restart) ──
        var maint = Section("Maintenance");
        var tourCb = new CheckBox
        {
            Content = "Show Welcome Tour On Next Launch",
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            Margin = new Thickness(0, 2, 0, 2),
            IsChecked = !_vm.Cfg.FirstRunCompleted,
            ToolTip = "Re-Runs The First-Launch Tour (With The Starter-Zones Offer) The Next Time OnTop Starts",
        };
        tourCb.Checked += (_, _) => { _vm.Cfg.FirstRunCompleted = false; };
        tourCb.Unchecked += (_, _) => { _vm.Cfg.FirstRunCompleted = true; };
        maint.Children.Add(tourCb);
        var restartRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        restartRow.Children.Add(Btn("Restart OnTop", (_, _) =>
        {
            if (ConfirmDialog.Confirm(this, "Restart OnTop", "Save Everything And Restart The App Now?", "Restart", "Cancel"))
                _vm.RequestRestart();
        }));
        restartRow.Children.Add(new TextBlock
        {
            Text = "Saves, Closes, And Relaunches (Same As The Restart Hotkey).",
            Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
        });
        maint.Children.Add(restartRow);
        root.Children.Add(Wrap("Maintenance", maint));

        // ── Logging ─────────────────────────────────────────────────────
        var logging = Section("Logging");
        var logRow = new StackPanel { Orientation = Orientation.Horizontal };
        var retentionLabel = Body("Log Retention (Days):");
        retentionLabel.Margin = new Thickness(0, 0, 8, 0);
        logRow.Children.Add(retentionLabel);
        var retention = new TextBox { Width = 56, VerticalAlignment = VerticalAlignment.Center };
        retention.SetBinding(TextBox.TextProperty, new Binding(nameof(AppSettings.LogRetentionDays))
        {
            Source = vm.Cfg,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        logRow.Children.Add(retention);
        logging.Children.Add(logRow);
        var verbose = Toggle("Verbose Logging (DEBUG Level)", nameof(AppSettings.VerboseLogging));
        verbose.Margin = new Thickness(0, 8, 0, 2);
        logging.Children.Add(verbose);
        var viewerRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        viewerRow.Children.Add(Btn("Open Log Viewer", (_, _) =>
        {
            _openLogViewer?.Invoke();
        }));
        viewerRow.Children.Add(Btn("Open Logs Folder", (_, _) => _vm.OpenLogsFolderCmd.Execute(null)));
        logging.Children.Add(viewerRow);
        logging.Children.Add(Note("Category Tracking Checkboxes Live In The Log Viewer. Old Session Logs Purge At Startup."));
        root.Children.Add(Wrap("Logging", logging));

        // ── Data ────────────────────────────────────────────────────────
        var data = Section("Data");
        var backupRow = new WrapPanel();
        backupRow.Children.Add(Btn("Backup Now", (_, _) => _vm.BackupCmd.Execute(null)));
        backupRow.Children.Add(Btn("Restore From Backup...", Restore_Click));
        backupRow.Children.Add(Btn("Open Config Folder", (_, _) => _vm.OpenConfigFolderCmd.Execute(null)));
        backupRow.Children.Add(Btn("Config History…", (_, _) => new Views.ConfigHistoryWindow(_vm) { Owner = this }.ShowDialog()));
        backupRow.Children.Add(Btn("Toast History…", (_, _) => new Views.ToastHistoryWindow { Owner = this }.ShowDialog()));
        data.Children.Add(backupRow);
        data.Children.Add(Note("Backups Are Timestamped JSON Snapshots Of All Settings + Zones. Restore Is Undoable."));
        root.Children.Add(Wrap("Data", data));

        // ── Footer ──────────────────────────────────────────────────────
        var footer = new Grid();
        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "dev";
        var versionText = Note($"OnTop {version} — WPF Rebuild");
        versionText.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(versionText);
        // S28 K: explicit Save & Close + Revert (scalar snapshot taken when the
        // dialog opened; lists such as rules/macros live in their own windows).
        var snapshot = typeof(AppSettings).GetProperties()
            .Where(p => p.CanRead && p.CanWrite && (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int) || p.PropertyType == typeof(double) || p.PropertyType == typeof(string))
                        && p.Name is not (nameof(AppSettings.SchemaVersion) or nameof(AppSettings.PowerfulFieldsStamp)))
            .ToDictionary(p => p, p => p.GetValue(_vm.Cfg));
        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var revert = new Button { Content = "Cancel (Revert)", Height = 32, MinWidth = 130, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Put Every Setting Back The Way It Was When This Window Opened, Then Close" };
        revert.Click += (_, _) => { foreach (var (p, v) in snapshot) { try { p.SetValue(_vm.Cfg, v); } catch (Exception ex) { _vm.Log.Debug($"Revert {p.Name}: {ex.Message}", "Settings"); } } _vm.Log.Action("Settings: changes reverted", "Settings"); Close(); };
        var close = new Button
        {
            Content = "Save & Close", Height = 32, Width = 130, IsCancel = true,
            Style = (Style)Application.Current.FindResource("AccentButton"),
            ToolTip = "Keep The Changes (They're Already Live) And Close",
        };
        btns.Children.Add(revert); btns.Children.Add(close);
        footer.Children.Add(btns);
        Helpers.EditorFooter.HideCloseButton(this);
        // S28 UI LAYOUT REVIEW: the dialog grew past a 1080p work area with the
        // Features section — size to content but cap at the work area and scroll.
        // 0.45.3: the footer sits OUTSIDE the scroll area, docked to the bottom,
        // so Save & Close / Cancel (Revert) are always visible; at the end of
        // the scroll the last section lands right above it.
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 40);
        // Primary-monitor cap above is a first guess; once the window exists,
        // fit it to the monitor it actually opened on (a shorter secondary
        // screen) and pull it fully on-screen (CenterOwner doesn't clamp), so
        // the footer can never sit below the bottom edge.
        Loaded += (_, _) => FitToMonitor();
        root.Margin = new Thickness(20, 14, 20, 10);
        var footerBar = new Border
        {
            Child = footer,
            Padding = new Thickness(20, 10, 20, 14),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x90)),
        };
        var shell = new DockPanel();
        DockPanel.SetDock(footerBar, Dock.Bottom);
        shell.Children.Add(footerBar);
        shell.Children.Add(new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });
        Content = shell;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Confirm(this, "Restore From Backup",
                "Restoring replaces your current zones and settings. Undo is available afterward. Continue?",
                "Restore", "Cancel"))
            return;

        var dlg = new OpenFileDialog
        {
            Filter = "OnTop Backups (*.json)|*.json|All Files (*.*)|*.*",
            InitialDirectory = _vm.Settings.BackupsDirectory
        };
        if (dlg.ShowDialog(this) == true) _vm.RestoreFrom(dlg.FileName);
    }

    // ── Layout helpers ──────────────────────────────────────────────────
    private Binding Bind(string path, BindingMode mode = BindingMode.OneWay)
        => new(path) { Source = _vm.Cfg, Mode = mode };

    private CheckBox Toggle(string label, string cfgProperty)
    {
        var check = new CheckBox
        {
            Content = label,
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            Margin = new Thickness(0, 2, 0, 2)
        };
        check.SetBinding(ToggleButton.IsCheckedProperty, Bind(cfgProperty, BindingMode.TwoWay));
        return check;
    }

    private void FitToMonitor()
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget is null) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var wa = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;   // device pixels
        var toDip = src.CompositionTarget.TransformFromDevice;
        Point tl = toDip.Transform(new Point(wa.Left, wa.Top));
        Point br = toDip.Transform(new Point(wa.Right, wa.Bottom));
        double margin = 20;
        MaxHeight = Math.Max(300, br.Y - tl.Y - 2 * margin);
        UpdateLayout();
        double h = Math.Min(ActualHeight, MaxHeight);
        if (Top + h > br.Y - margin) Top = br.Y - margin - h;
        if (Top < tl.Y + margin) Top = tl.Y + margin;
        if (Left + ActualWidth > br.X) Left = br.X - ActualWidth;
        if (Left < tl.X) Left = tl.X;
    }

    private static StackPanel Section(string _) => new() { Margin = new Thickness(4, 2, 4, 4) };

    private static GroupBox Wrap(string header, UIElement content) => new()
    {
        Header = header,
        Content = content,
        Margin = new Thickness(0, 0, 0, 10)
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        FontSize = 13,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6))
    };

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6, 0, 0)
    };

    private static Button Btn(string text, RoutedEventHandler onClick)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 6, 6) };
        button.Click += onClick;
        return button;
    }
}
