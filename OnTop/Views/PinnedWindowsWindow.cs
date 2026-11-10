using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using OnTop.Helpers;
using OnTop.ViewModels;

using OnTop.Services;

using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// Pinned Windows viewer — every currently-topmost visible window on the system,
/// with an "OnTop" badge on the ones this app pinned this run. Per-row Unpin,
/// Unpin All (Ours), and Refresh. Enumeration is on-demand (open + Refresh),
/// not a live watcher — cheap and predictable.
/// </summary>
public class PinnedWindowsWindow : Window
{
    public sealed class Row
    {
        public IntPtr Hwnd { get; init; }
        public string Title { get; init; } = "";
        public string Process { get; init; } = "";
        public bool ByUs { get; init; }
        public string ByUsLabel => ByUs ? "OnTop" : "Other";
        /// <summary>S28 K: live state — opacity % and click-through — so the buttons have visible feedback.</summary>
        public string State { get; init; } = "";
    }

    private readonly MainViewModel _vm;
    private readonly ObservableCollection<Row> _rows = new();
    private readonly TextBlock _empty;
    private readonly DataGrid _grid;

    public PinnedWindowsWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Pinned Windows";
        Width = 560; Height = 380;
        MinWidth = 400; MinHeight = 240;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = vm;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });

        var root = new DockPanel { Margin = new Thickness(12) };

        // Toolbar
        var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(0, 0, 6, 0), ToolTip = "Re-Scan For Topmost Windows" };
        refresh.Click += (_, _) => Refresh();
        var unpinAll = new Button { Content = "Unpin All (OnTop Pins)", ToolTip = "Remove Always-On-Top From Every Window OnTop Pinned This Run" };
        unpinAll.Click += (_, _) => { _vm.UnpinAllOurs(); ReassertTopmost(); Refresh(); };
        // S28 Stage I: pin scenes.
        var saveScene = new Button { Content = "Save Scene…", Margin = new Thickness(0, 0, 6, 0), ToolTip = "Snapshot Every Window OnTop Pinned (Position, Size, Opacity, Click-Through) As A Named Scene" };
        saveScene.Click += (_, _) => { var n = InputDialog.Prompt(this, "Save Scene", "Scene Name:", $"Scene {_vm.Cfg.PinScenes.Count + 1}"); if (!string.IsNullOrWhiteSpace(n)) _vm.SaveScene(n.Trim()); };
        var applyScene = new Button { Content = "Apply Scene…", Margin = new Thickness(0, 0, 6, 0), ToolTip = "Restore A Saved Scene (Also Available As A Library Action And In The Command Palette)" };
        applyScene.Click += (_, _) =>
        {
            if (_vm.Cfg.PinScenes.Count == 0) { ToastService.Show("No Scenes Saved Yet", ToastType.Info); return; }
            var pick = InputDialog.Prompt(this, "Apply Scene", "Scene name:\n" + string.Join(", ", _vm.Cfg.PinScenes.Select(s => s.Name)), _vm.Cfg.PinScenes[^1].Name);
            var sc = pick is null ? null : _vm.Cfg.PinScenes.FirstOrDefault(s => string.Equals(s.Name, pick.Trim(), StringComparison.OrdinalIgnoreCase));
            if (sc is null) return;
            _vm.ApplyScene(sc, "pinned window"); ReassertTopmost(); Refresh();
        };
        // S28 K: orange frame around click-through windows.
        var border = new CheckBox { Content = "Orange Frame On Click-Through", Style = (Style)Application.Current.FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), ToolTip = "Draw A Thin Orange Frame Around Any Window That Is Currently Click-Through, So Its State Is Visible On Screen" };
        border.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.ClickThroughBorder)) { Source = _vm });
        DockPanel.SetDock(border, Dock.Left);
        DockPanel.SetDock(refresh, Dock.Left);
        DockPanel.SetDock(unpinAll, Dock.Left);
        DockPanel.SetDock(saveScene, Dock.Left);
        DockPanel.SetDock(applyScene, Dock.Left);
        bar.Children.Add(refresh);
        bar.Children.Add(unpinAll);
        bar.Children.Add(saveScene);
        bar.Children.Add(applyScene);
        bar.Children.Add(border);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        // Empty hint (shown when nothing is pinned)
        _empty = new TextBlock
        {
            Text = "No Topmost Windows Found — Pin One With The Toggle Hotkey, Then Refresh",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.55,
            Visibility = Visibility.Collapsed,
        };

        // List
        var grid = _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Process", Binding = new Binding(nameof(Row.Process)), Width = new DataGridLength(120), MinWidth = 70 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Window Title", Binding = new Binding(nameof(Row.Title)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Pinned By", Binding = new Binding(nameof(Row.ByUsLabel)), Width = new DataGridLength(76), MinWidth = 60 });

        var unpinCol = new DataGridTemplateColumn { Header = "", Width = new DataGridLength(64), MinWidth = 56 };
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "Unpin");
        btnFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        btnFactory.SetValue(Button.ToolTipProperty, "Remove Always-On-Top From This Window");
        btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(UnpinRow_Click));
        unpinCol.CellTemplate = new DataTemplate { VisualTree = btnFactory };
        grid.Columns.Add(unpinCol);
        // S28 Stage G: pin extras — opacity −/+ and click-through per row.
        var extraCol = new DataGridTemplateColumn { Header = "Opacity / Click-Through", Width = new DataGridLength(200), MinWidth = 180 };
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        void Btn(string text, string tip, RoutedEventHandler h)
        {
            var f = new FrameworkElementFactory(typeof(Button));
            f.SetValue(Button.ContentProperty, text);
            f.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            f.SetValue(Button.ToolTipProperty, tip);
            f.AddHandler(Button.ClickEvent, h);
            panel.AppendChild(f);
        }
        Btn("−", "Make This Window 10% More See-Through (Down To 20%)", (s, e) => { if (RowOf(s) is { } r) { _vm.NudgePinOpacity(r.Hwnd, -10); Refresh(); } });
        Btn("+", "Make This Window 10% More Solid (Up To 100%)", (s, e) => { if (RowOf(s) is { } r) { _vm.NudgePinOpacity(r.Hwnd, +10); Refresh(); } });
        Btn("Click-Through", "Let Clicks Pass Through This Window To What's Beneath (Press Again, Or Unpin, To Revert). The State Column And The Orange Frame Show It's On.", (s, e) => { if (RowOf(s) is { } r) { _vm.ToggleClickThrough(r.Hwnd); Refresh(); } });
        extraCol.CellTemplate = new DataTemplate { VisualTree = panel };
        grid.Columns.Add(extraCol);
        grid.Columns.Add(new DataGridTextColumn { Header = "State", Binding = new Binding(nameof(Row.State)), Width = new DataGridLength(150), IsReadOnly = true });

        var host = new Grid();
        host.Children.Add(grid);
        host.Children.Add(_empty);
        root.Children.Add(host);
        Content = root;
        Helpers.EditorFooter.Wrap(this, root, onSave: null, onCancel: null, saveText: "Close");

        Refresh();
    }

    private static Row? RowOf(object sender) => (sender as FrameworkElement)?.DataContext as Row;

    private void UnpinRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Row row)
        {
            _vm.Unpin(row.Hwnd);
            ReassertTopmost();
            Refresh();
        }
    }

    /// <summary>QOL: honor the "Keep GUIs On Top" setting after a bulk unpin.
    /// Even though our own windows are excluded from unpinning, a Z-order shuffle
    /// can drop us — nudge ourselves back to the top if that setting is on.</summary>
    private void ReassertTopmost()
    {
        if (_vm.Cfg.GuiAlwaysOnTop)
        {
            Topmost = false;
            Topmost = true;
            Activate();
        }
    }

    /// <summary>Re-enumerate topmost windows (excluding our own GUI windows).
    /// S28 L (from testing: opening this window froze OnTop and the Exit hotkey
    /// with it): the scan walks every top-level window and asks each one's
    /// process for its name — foreign processes we do not control. It now runs
    /// on a pool thread and lands here when done, so the UI thread is never
    /// held by it; a scan that overruns 3 s is reported in the log with its
    /// duration instead of freezing the app. Row state (ours / opacity /
    /// click-through) is computed back on the UI thread — cheap, local calls.</summary>
    private int _scanGen;
    public void Refresh()
    {
        int gen = ++_scanGen;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _vm.Log.Debug("Pinned Windows: scan started (background)", "Pins");
        _empty.Text = "Scanning For Topmost Windows…";
        _empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var scan = Task.Run(() => Win32.GetTopmostWindows());
        _ = Task.Delay(3000).ContinueWith(delayed =>
        {
            if (!scan.IsCompleted) _vm.Log.Warn("Pinned Windows: scan still running after 3 s — a window's process is slow to answer; the UI is not blocked", "Pins");
        });
        scan.ContinueWith(t => Dispatcher.BeginInvoke(() =>
        {
            if (gen != _scanGen) return;   // a newer Refresh superseded this one
            if (t.IsFaulted || t.Result is null)
            {
                _vm.Log.Error($"Pinned Windows: scan failed after {sw.ElapsedMilliseconds} ms: {t.Exception?.GetBaseException().Message}", "Pins");
                _empty.Text = "Scan Failed — See Log";
                _empty.Visibility = Visibility.Visible;
                return;
            }
            _rows.Clear();
            foreach (var (hwnd, title, process) in t.Result)
            {
                if (string.Equals(process, "OnTop", StringComparison.OrdinalIgnoreCase)) continue;
                if (!Win32.IsWindow(hwnd)) continue;   // gone since the scan
                _rows.Add(new Row { Hwnd = hwnd, Title = title, Process = process, ByUs = _vm.PinnedByUs.Contains(hwnd), State = $"{_vm.GetOpacityPct(hwnd)}%{(_vm.IsClickThrough(hwnd) ? "  ·  CLICK-THROUGH" : "")}" });
            }
            // Prune tracked pins whose windows are gone.
            foreach (var dead in _vm.PinnedByUs.Where(h => !Win32.IsWindow(h)).ToList())
                _vm.PinnedByUs.Remove(dead);
            _empty.Text = "No Topmost Windows Found — Pin One With The Toggle Hotkey, Then Refresh";
            _empty.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _vm.Log.Debug($"Pinned Windows: scan done — {_rows.Count} row(s) in {sw.ElapsedMilliseconds} ms", "Pins");
        }));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowPlacement.Restore(this, "pins", _vm.Cfg);
        GridColumns.Restore(_grid, "pins", _vm.Cfg);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        WindowPlacement.Save(this, "pins", _vm.Cfg);
        GridColumns.Save(_grid, "pins", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }
}
