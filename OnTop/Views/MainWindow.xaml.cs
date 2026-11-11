using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using OnTop.Models;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

public partial class MainWindow : Window
{
    private MainViewModel VM => (MainViewModel)DataContext;

    private MapWindow? _mapWindow;
    private ZoneListWindow? _listWindow;
    private LogViewerWindow? _logWindow;
    private PinnedWindowsWindow? _pinsWindow;
    private WindowRulesWindow? _rulesWindow;
    private ProfilesWindow? _profilesWindow;
    private StatisticsWindow? _statsWindow;
    private bool _profileComboSyncing;
    private bool _applyingViewMode;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"v{Helpers.AppInfo.Version}";
        // Commit an in-progress zone-list edit when the user clicks anywhere else.
        PreviewMouseDown += (_, e) => Helpers.GridEdit.CommitOnOutsideClick(ZonesGrid, e);
        // Right-click a list row → same zone context menu the map uses.
        ZonesGrid.PreviewMouseRightButtonUp += (_, e) =>
        {
            if (DataContext is MainViewModel vm) ZoneContextMenuFactory.ShowForRow(vm, this, ZonesGrid, e);
        };
        // Keep the saved window size/position current as the user resizes/moves,
        // so any settings save (incl. Save and Close) persists the latest.
        SizeChanged += (_, _) => SaveMainPlacement();
        LocationChanged += (_, _) => SaveMainPlacement();
        // Delete removes / F2 renames the selected zone (when not editing a cell).
        ZonesGrid.PreviewKeyDown += (_, e) =>
        {
            if (DataContext is MainViewModel vm) ZoneListKeys.Handle(e, vm, this, ZonesGrid);
        };
        // Locate: double-click a list row to flash that zone on the map.
        ZonesGrid.MouseDoubleClick += (_, _) =>
        {
            if (DataContext is MainViewModel vm && vm.SelectedZone is { } z) vm.RequestFlashZone(z);
        };
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                VM.StartAutoDraft();
                // Soft fade-in each time the window is shown from the tray.
                BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            }
            else
            {
                VM.StopAutoDraft();
            }
        };

        // Zone map interaction → ViewModel / shared context menu.
        ZoneMap.ZoneDragStarted += _ => VM.BeginZoneDrag();
        ZoneMap.ZonePreviewRequested += z => VM.BeginZonePreviewFor(z);
        ZoneMap.ZoneDragCompleted += _ => VM.EndZonePreview();
        ZoneMap.ZoneDragCompleted += z => VM.EndZoneDrag(z);
        ZoneMap.CreateZoneRequested += (x, y) => VM.AddZoneAt(x, y);
        ZoneMap.ZoneContextRequested += z => ZoneContextMenuFactory.Show(VM, this, ZoneMap, z);
        ZoneMap.MonitorContextRequested += MonitorContext;
        ZoneMap.MapBlankContextRequested += () => ShowMapBlankMenu(ZoneMap);

        // DataContext is assigned by App AFTER construction, so anything that
        // reads VM directly (not via a deferred lambda) must wait for it.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                ZoneMap.HiddenMonitors = vm.Cfg.HiddenMonitors;
                vm.FlashZoneRequested -= ZoneMap.FlashZone;   // avoid double-subscribe
                vm.FlashZoneRequested += ZoneMap.FlashZone;
            }
        };
    }

    // ── Monitor hide / unhide (map view only) ───────────────────────────
    private void MonitorContext(int monitorIndex)
    {
        var menu = new ContextMenu { PlacementTarget = ZoneMap };
        var hide = new MenuItem { Header = $"Hide Monitor {monitorIndex + 1}" };
        hide.Click += (_, _) =>
        {
            if (!VM.Cfg.HiddenMonitors.Contains(monitorIndex))
            {
                VM.Cfg.HiddenMonitors.Add(monitorIndex);
                VM.StatusText = $"Monitor {monitorIndex + 1} Hidden From The Map";
                RefreshMap();
            }
        };
        menu.Items.Add(hide);

        if (VM.Cfg.HiddenMonitors.Count > 0)
        {
            var unhide = new MenuItem { Header = "Show Hidden Monitors..." };
            unhide.Click += (_, _) => ShowUnhideDialog();
            menu.Items.Add(unhide);
        }
        var reset = new MenuItem { Header = "Reset Zoom / Pan" };
        reset.Click += (_, _) => ZoneMap.ResetView();
        menu.Items.Add(reset);
        menu.IsOpen = true;
    }

    /// <summary>Right-click on blank map space: one-click unhide per monitor
    /// (replacing the clunky type-the-numbers dialog as the primary path),
    /// plus map utilities.</summary>
    private void ShowMapBlankMenu(Controls.ZoneMapCanvas canvas)
    {
        var menu = new ContextMenu();

        var hidden = VM.Cfg.HiddenMonitors;
        if (hidden.Count > 0)
        {
            foreach (int mi in hidden.OrderBy(i => i).ToList())
            {
                int m = mi;   // capture per iteration
                var item = new MenuItem { Header = $"Show Monitor {m + 1}" };
                item.Click += (_, _) =>
                {
                    VM.Cfg.HiddenMonitors.Remove(m);
                    VM.StatusText = $"Monitor {m + 1} Restored";
                    RefreshMap();
                };
                menu.Items.Add(item);
            }
            if (hidden.Count > 1)
            {
                var all = new MenuItem { Header = "Show All Hidden Monitors" };
                all.Click += (_, _) =>
                {
                    VM.Cfg.HiddenMonitors.Clear();
                    VM.StatusText = "All Monitors Restored";
                    RefreshMap();
                };
                menu.Items.Add(all);
            }
            menu.Items.Add(new Separator());
        }

        var fit = new MenuItem { Header = "Fit / Reset View" };
        fit.Click += (_, _) => canvas.ResetView();
        menu.Items.Add(fit);
        var flash = new MenuItem { Header = "Flash All Zones" };
        flash.Click += (_, _) => canvas.FlashAllZones();
        menu.Items.Add(flash);

        menu.IsOpen = true;
    }

    private void ShowUnhideDialog()
    {
        var hidden = VM.Cfg.HiddenMonitors;
        if (hidden.Count == 0) return;
        // Reuse the input dialog: comma-separated monitor numbers to restore,
        // or blank to restore all.
        string list = string.Join(", ", hidden.Select(i => i + 1));
        var answer = InputDialog.Prompt(this, "Show Hidden Monitors",
            $"Hidden Monitors: {list}\nEnter Numbers To Show (Comma-Separated), Or Leave Blank To Show All:");
        if (answer == null) return;

        if (string.IsNullOrWhiteSpace(answer))
        {
            hidden.Clear();
        }
        else
        {
            foreach (var token in answer.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (int.TryParse(token, out int n)) hidden.Remove(n - 1);
        }
        VM.StatusText = "Monitor Visibility Updated";
        RefreshMap();
    }

    private void RefreshMap()
    {
        ZoneMap.HiddenMonitors = VM.Cfg.HiddenMonitors;
        ZoneMap.InvalidateVisual();
        if (_mapWindow is { IsLoaded: true }) _mapWindow.RefreshHiddenMonitors(VM.Cfg.HiddenMonitors);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyViewMode(Math.Clamp(VM.Cfg.ZoneViewMode, 0, 2));
        // Column widths persist in the user config (survive restarts/updates).
        Helpers.GridColumns.Restore(ZonesGrid, "main", VM.Cfg);
        Helpers.GridEdit.AttachFocusCommit(ZonesGrid);

        // Taskbar semantics (v13): the main window ALWAYS shows
        // in the taskbar while open — no runtime style flipping at all (the old
        // toggle churned EX styles under WindowChrome+Mica and broke the GUI).
        // MinimizeToTray carries the tray behavior, handled in OnStateChanged.
        SyncProfileCombo();
        VM.ProfilesChanged += () => Dispatcher.Invoke(SyncProfileCombo);
        VM.DisplaysChanged += () => ZoneMap.InvalidateVisual();   // monitor outlines
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ctrl+Enter = Save and Close (Ctrl+Z / Ctrl+Y live in InputBindings)
        if (e.Key == System.Windows.Input.Key.Enter &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            SaveAndClose_Click(sender, e);
        }
    }

    // ── Recording (v9.5 click-to-place / drag-box / freeform-draw flow) ──
    private void Record_Click(object sender, RoutedEventArgs e) => StartRecording(quick: false);

    /// <summary>Quick Record hotkey entry point: record in the current mode
    /// without a naming prompt, and only restore the main window if it was
    /// already open (so it won't pop up over a game).</summary>
    public void QuickRecord() => StartRecording(quick: true);

    private void StartRecording(bool quick)
    {
        bool wasVisible = IsVisible;
        int mode = Math.Clamp(VM.Cfg.RecordMode, 0, 2);
        Hide();   // get out of the way; the gesture places the zone
        ZoneCaptureOverlay.Capture(mode,
            onDone: result => Dispatcher.BeginInvoke(() =>
            {
                var zone = result.Mode switch
                {
                    1 => VM.AddBoxZone(result.X, result.Y, result.Width, result.Height),
                    2 => VM.AddDrawZone(result.Points!),
                    _ => VM.AddZoneAt(result.X, result.Y)
                };
                if (quick)
                {
                    ToastService.Show($"Recorded {zone.Name}", ToastType.Success);
                    if (wasVisible) { Show(); Activate(); }
                    return;
                }
                Show();
                Activate();
                var name = InputDialog.Prompt(this, "Name Zone", "Name For The New Zone:", zone.Name);
                if (!string.IsNullOrWhiteSpace(name)) VM.RenameZone(zone, name);
            }),
            onCancel: () => Dispatcher.BeginInvoke(() =>
            {
                if (wasVisible) { Show(); Activate(); }
                VM.StatusText = "Recording Canceled";
            }));
    }

    // ── Zone view modes (Map / List / Split) ────────────────────────────
    private void ViewMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_applyingViewMode ||
            sender is not System.Windows.Controls.Primitives.ToggleButton button ||
            button.Tag is not string tag)
            return;
        ApplyViewMode(int.Parse(tag));
    }

    private void ViewMode_Unchecked(object sender, RoutedEventArgs e)
    {
        // Radio behavior — clicking the active segment must not leave nothing selected.
        if (!_applyingViewMode) ApplyViewMode(Math.Clamp(VM.Cfg.ZoneViewMode, 0, 2));
    }

    private void ApplyViewMode(int mode)
    {
        _applyingViewMode = true;
        VM.Cfg.ZoneViewMode = mode;   // persisted + logged by the settings tracker
        ViewMapBtn.IsChecked = mode == 0;
        ViewListBtn.IsChecked = mode == 1;
        ViewSplitBtn.IsChecked = mode == 2;

        bool showMap = mode is 0 or 2;
        bool showList = mode is 1 or 2;
        ZoneMap.Visibility = showMap ? Visibility.Visible : Visibility.Collapsed;
        ListPane.Visibility = showList ? Visibility.Visible : Visibility.Collapsed;
        MapCol.Width = showMap ? new GridLength(mode == 2 ? 4.5 : 1, GridUnitType.Star) : new GridLength(0);
        SplitGap.Width = new GridLength(mode == 2 ? 10 : 0);
        ListCol.Width = showList ? new GridLength(mode == 2 ? 5.5 : 1, GridUnitType.Star) : new GridLength(0);
        _applyingViewMode = false;
    }

    // ── Pop-outs + Log Viewer (single instance each, selection stays synced) ──
    private void PopOutMap_Click(object sender, RoutedEventArgs e)
    {
        if (_mapWindow is { IsLoaded: true }) { _mapWindow.Show(); _mapWindow.Activate(); return; }
        _mapWindow = new MapWindow(VM);
        _mapWindow.Closed += (_, _) => _mapWindow = null;
        _mapWindow.Show();
    }

    private void FitMap_Click(object sender, RoutedEventArgs e) => ZoneMap.ResetView();
    private void FlashAll_Click(object sender, RoutedEventArgs e) => ZoneMap.FlashAllZones();

    // ── Map/list split: center magnet v4 — a gravity well, not a state machine ──
    // v2/v3 post-mortem (from testing: "glitches once per swipe, doesn't hold"): any
    // instant lock/unlock produces a visible jump at capture, holds for only a
    // frame or two at swipe speed, and needs arm/re-arm bookkeeping that broke
    // the common start-at-center case. v4 is a pure position CURVE instead:
    //
    //   applied(offset) = 0                      |offset| ≤ dead   (exact center)
    //                   = sign·offset²/band      |offset| ≤ band   (gravity well)
    //                   = offset                 otherwise         (free)
    //
    // Response is continuous everywhere (no jumps possible), flattens to zero
    // slope at center (the divider decelerates in and genuinely resists leaving —
    // the "hold"), and rejoins 1:1 tracking exactly at the band edge. Stateless,
    // so it behaves identically for slow drags, fast swipes, and direction
    // changes. Widths stay pixel-snapped with no-op writes skipped.
    // v6 (ROOT CAUSE of every prior glitch): Thumb.DragDelta reports movement
    // relative to THE THUMB — and the magnet holds the thumb while the cursor
    // keeps going, so accumulated deltas diverge from true cursor travel (a
    // feedback loop). v6 ignores the deltas entirely and samples the ABSOLUTE
    // cursor position against the host grid every event: no accumulation, no
    // feedback, the curve maps true cursor intent every tick.
    private double _dragStartFraction, _dragTotalWidth, _dragStartMouseX;
    private double _lastAppliedPx = -1;

    private const double DefaultMapFraction = 0.45;
    private const double MagnetBand = 0.075;   // attraction reaches ±7.5%
    private const double MagnetDead = 0.025;   // exact-center HOLD within ±2.5%
    // v5 tuning (from testing: "only works on release, a bit glitchy"): the v4 dead
    // zone (±1.2% ≈ 9 px) was too small to feel during a drag, so the only
    // visible magnetism was the release settle. v5 doubles the flat hold and
    // uses f = |o|·(|o|−dead)/(band−dead), which is 0 across the whole dead
    // zone, position-continuous at BOTH edges (dead and band), and rejoins 1:1
    // exactly at the band — the hold is now a real flat spot mid-drag.

    private void SplitDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
    {
        _dragTotalWidth = MapCol.ActualWidth + ListCol.ActualWidth;
        _dragStartFraction = _dragTotalWidth > 0 ? MapCol.ActualWidth / _dragTotalWidth : DefaultMapFraction;
        _dragStartMouseX = System.Windows.Input.Mouse.GetPosition(SplitHost).X;
        _lastAppliedPx = -1;
    }

    private void SplitDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (_dragTotalWidth <= 0) return;
        double mouseX = System.Windows.Input.Mouse.GetPosition(SplitHost).X;
        double intended = _dragStartFraction + (mouseX - _dragStartMouseX) / _dragTotalWidth;
        double offset = intended - DefaultMapFraction;
        double abs = Math.Abs(offset);

        double applied;
        if (abs <= MagnetDead)
            applied = DefaultMapFraction;                                   // held flat
        else if (abs <= MagnetBand)
            applied = DefaultMapFraction + Math.Sign(offset) *
                      (abs * (abs - MagnetDead) / (MagnetBand - MagnetDead));
        else
            applied = intended;                                             // free

        applied = Math.Clamp(applied, 0.12, 0.88);

        double px = Math.Round(applied * _dragTotalWidth);
        if (Math.Abs(px - _lastAppliedPx) < 0.5) return;
        _lastAppliedPx = px;
        MapCol.Width = new GridLength(px, GridUnitType.Star);
        ListCol.Width = new GridLength(_dragTotalWidth - px, GridUnitType.Star);
    }

    private void SplitDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        // Released inside the well's inner region → settle to the exact default
        // and restore clean 4.5/5.5 star sizing so window resizes stay
        // proportional. Outside it, the pixel-star weights left by the drag
        // already behave proportionally.
        if (_dragTotalWidth <= 0) return;
        double frac = (MapCol.ActualWidth) / _dragTotalWidth;
        // Settle only from inside the flat hold — the in-drag magnet already put
        // us at center there, so this is a zero-pixel normalize, never a jump.
        if (Math.Abs(frac - DefaultMapFraction) <= MagnetDead)
        {
            MapCol.Width = new GridLength(4.5, GridUnitType.Star);
            ListCol.Width = new GridLength(5.5, GridUnitType.Star);
        }
    }

    private void PopOutList_Click(object sender, RoutedEventArgs e)
    {
        if (_listWindow is { IsLoaded: true }) { _listWindow.Show(); _listWindow.Activate(); return; }
        _listWindow = new ZoneListWindow(VM);
        _listWindow.Closed += (_, _) => _listWindow = null;
        _listWindow.Show();
    }

    private void Logs_Click(object sender, RoutedEventArgs e) => OpenLogViewer();

    private void Pins_Click(object sender, RoutedEventArgs e) => ShowPinnedWindows();

    private void Peek_Click(object sender, RoutedEventArgs e) => VM.RequestPeek();
    private void Legend_Click(object sender, RoutedEventArgs e) => VM.RequestLegend();

    // ── Profiles UI ─────────────────────────────────────────────────────
    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        if (_profilesWindow is { IsLoaded: true }) { _profilesWindow.Show(); _profilesWindow.Activate(); return; }
        _profilesWindow = new ProfilesWindow(VM);
        _profilesWindow.Closed += (_, _) => _profilesWindow = null;
        _profilesWindow.Show();
    }

    private Views.MacrosWindow? _macrosWin;
    private void Macros_Click(object sender, RoutedEventArgs e) => ShowMacros();

    private void RecordMacro_Click(object sender, RoutedEventArgs e)
    {
        if (!VM.Cfg.MacrosEnabled) { NoticePopup.Show("Turn Macros On First", "Macro recording is off. Open Macros… and switch on Macros Enabled, then press Record again."); return; }
        VM.MacroSvc.ToggleRecording();
    }
    private void PanicMacro_Click(object sender, RoutedEventArgs e) => VM.MacroSvc.Stop();

    /// <summary>S28: open (or focus) the Macros window — button, tray, hotkey.</summary>
    public void ShowMacros()
    {
        if (_macrosWin is { IsLoaded: true }) { _macrosWin.Show(); _macrosWin.Activate(); return; }
        _macrosWin = new Views.MacrosWindow(VM);
        _macrosWin.Closed += (_, _) => _macrosWin = null;
        _macrosWin.Show();
    }

    private void Stats_Click(object sender, RoutedEventArgs e)
    {
        if (_statsWindow is { IsLoaded: true }) { _statsWindow.Show(); _statsWindow.Activate(); return; }
        _statsWindow = new StatisticsWindow(VM);
        _statsWindow.Closed += (_, _) => _statsWindow = null;
        _statsWindow.Show();
    }

    private void ProfileCombo_DropDownOpened(object? sender, EventArgs e) => SyncProfileCombo();

    private void ProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_profileComboSyncing) return;
        if (ProfileCombo.SelectedItem is string name &&
            !string.Equals(name, VM.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
        {
            VM.SwitchProfile(name);
        }
    }

    /// <summary>Refill the combo from the VM without triggering a switch.</summary>
    public void SyncProfileCombo()
    {
        _profileComboSyncing = true;
        try
        {
            ProfileCombo.Items.Clear();
            foreach (var n in VM.ProfileNames) ProfileCombo.Items.Add(n);
            ProfileCombo.SelectedItem = VM.ProfileNames
                .FirstOrDefault(n => string.Equals(n, VM.ActiveProfileName, StringComparison.OrdinalIgnoreCase));
        }
        finally { _profileComboSyncing = false; }
    }

    private void Rules_Click(object sender, RoutedEventArgs e) => ShowWindowRules();
    private Views.ActionLibraryWindow? _libraryWindow;
    private void Library_Click(object sender, RoutedEventArgs e) => ShowLibrary();
    /// <summary>S28 Stage F: open (or focus) the Action Library.</summary>
    public void ShowLibrary()
    {
        if (_libraryWindow is { IsLoaded: true }) { _libraryWindow.Show(); _libraryWindow.Activate(); return; }
        _libraryWindow = new Views.ActionLibraryWindow(VM);
        _libraryWindow.Closed += (_, _) => _libraryWindow = null;
        _libraryWindow.Show();
    }
    private Views.PlacementWindow? _placementWindow;
    private void Placement_Click(object sender, RoutedEventArgs e) => ShowPlacement();
    /// <summary>S28 Stage E: open (or focus) the Window Placement manager.</summary>
    public void ShowPlacement()
    {
        if (_placementWindow is { IsLoaded: true }) { _placementWindow.Show(); _placementWindow.Activate(); return; }
        _placementWindow = new Views.PlacementWindow(VM);
        _placementWindow.Closed += (_, _) => _placementWindow = null;
        _placementWindow.Show();
    }

    /// <summary>Open (or focus) the Window Rules manager (button, tray, hotkey).</summary>
    public void ShowWindowRules()
    {
        if (_rulesWindow is { IsLoaded: true }) { _rulesWindow.Show(); _rulesWindow.Activate(); return; }
        _rulesWindow = new WindowRulesWindow(VM);
        _rulesWindow.Closed += (_, _) => _rulesWindow = null;
        _rulesWindow.Show();
    }

    /// <summary>View Logs hotkey entry point.</summary>
    public void ShowLogs() => OpenLogViewer();

    /// <summary>Open (or focus) the Pinned Windows viewer; refresh on re-open.</summary>
    public void ShowPinnedWindows()
    {
        if (_pinsWindow is { IsLoaded: true }) { _pinsWindow.Refresh(); _pinsWindow.Show(); _pinsWindow.Activate(); return; }
        _pinsWindow = new PinnedWindowsWindow(VM);
        _pinsWindow.Closed += (_, _) => _pinsWindow = null;
        _pinsWindow.Show();
    }

    private void OpenLogViewer()
    {
        if (_logWindow is { IsLoaded: true }) { _logWindow.Show(); _logWindow.Activate(); return; }
        _logWindow = new LogViewerWindow(VM);
        _logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow.Show();
    }

    // ── Grid / groups plumbing ──────────────────────────────────────────
    private void ZonesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is Zone zone)
            VM.OnZoneSelected(zone);
    }

    private void ZonesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column.Header as string == "Group")
            Dispatcher.BeginInvoke(new Action(VM.RefreshGroupNames));
    }

    private void Swatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            VM.CycleZoneColor(zone);
    }

    private void ZoneAction_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            VM.CycleZoneAction(zone);
    }

    // Locate/health cell — flash the zone on the map (works on any row, warning or not).
    private void LocateCell_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            VM.RequestFlashZone(zone);
    }

    private void SetGroup_Click(object sender, RoutedEventArgs e)
        => VM.SetSelectedZoneGroup(GroupNameBox.Text);

    private void GroupFilterBox_DropDownOpened(object sender, EventArgs e)
        => VM.RefreshGroupNames();

    // ── Save / Close ────────────────────────────────────────────────────
    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        VM.SaveAll();
        Hide();
    }

    private void CloseNoSave_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    // ── Sub-GUIs ────────────────────────────────────────────────────────
    private void OpenHotkeys_Click(object sender, RoutedEventArgs e)
    {
        VM.SuspendGlobalHotkeys();   // S28 K: keys pressed in the recorder are captured, never executed
        var dlg = new HotkeyDialog(VM.Cfg) { Owner = this };
        dlg.Closed += (_, _) => VM.ResumeGlobalHotkeys();
        if (dlg.ShowDialog() == true)
        {
            VM.BindHotkeys();
            VM.SaveAll();
        }
    }

    private void ShowHelp_Click(object sender, RoutedEventArgs e)
        => new HelpWindow(VM.Cfg) { Owner = this }.ShowDialog();

    private void OpenExceptions_Click(object sender, RoutedEventArgs e)
        => new ExceptionsWindow(VM) { Owner = this }.ShowDialog();

    private void OpenAutoClose_Click(object sender, RoutedEventArgs e)
        => new AutoCloseWindow(VM) { Owner = this }.ShowDialog();

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
        => new SettingsDialog(VM, OpenLogViewer) { Owner = this }.ShowDialog();

    // ── Zone Import / Export ────────────────────────────────────────────
    private void ExportZones_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Zone Files (*.json)|*.json",
            FileName = "OnTop_Zones.json"
        };
        if (dlg.ShowDialog(this) == true) VM.ExportZones(dlg.FileName);
    }

    private void ImportZones_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Zone Files (*.json)|*.json|All Files (*.*)|*.*"
        };
        if (dlg.ShowDialog(this) == true) VM.ImportZones(dlg.FileName);
    }

    // Restore saved size/position before the window is shown (no flicker).
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Helpers.WindowPlacement.Restore(this, "main", VM.Cfg);
    }

    /// <summary>Keep the in-memory placement current (called on resize/move).
    /// Guarded because SizeChanged can fire before DataContext is assigned.</summary>
    private void SaveMainPlacement()
    {
        if (DataContext is MainViewModel vm)
            Helpers.WindowPlacement.Save(this, "main", vm.Cfg);
    }

    // Override close to just hide (tray app keeps running)
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        Helpers.WindowPlacement.Save(this, "main", VM.Cfg);
        Helpers.GridColumns.Save(ZonesGrid, "main", VM.Cfg);
        VM.Settings.SaveNoBackup(VM.Cfg);   // persist now — don't wait for app exit
        e.Cancel = true;
        Hide();
    }

    /// <summary>Called by App.OnExit so column widths persist on real shutdown
    /// (the hide-on-close path above only covers hiding).</summary>
    public void SaveGridLayout() => Helpers.GridColumns.Save(ZonesGrid, "main", VM.Cfg);

    // Minimize honors Cfg.MinimizeToTray: off = a normal taskbar minimize;
    // on = hide to the tray (the classic tray-utility behavior).
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && VM.Cfg.MinimizeToTray)
        {
            WindowState = WindowState.Normal;
            Helpers.WindowPlacement.Save(this, "main", VM.Cfg);
            VM.Settings.SaveNoBackup(VM.Cfg);
            Hide();
        }
    }
}
