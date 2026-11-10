using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;

using OnTop.Services;

namespace OnTop.Views;

/// <summary>
/// Window Rules manager — auto-pin rules ("process/title contains X → pin it").
/// The feature has a master enable toggle (OFF by default — see SECURITY.md) and
/// each rule can be individually enabled, edited, or removed. Rules apply within
/// a few seconds while enabled; rule pins show in the Pinned Windows viewer as
/// OnTop pins and are released by Unpin All (OnTop Pins).
/// </summary>
public class WindowRulesWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DataGrid _grid;

    public WindowRulesWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Auto-Pin";
        Width = 560; Height = 380;
        MinWidth = 420; MinHeight = 240;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = vm;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });

        var root = new DockPanel { Margin = new Thickness(12) };

        // Toolbar: master toggle + add
        var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        var master = new CheckBox
        {
            Content = "Enable Auto-Pin",
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = Helpers.WarnTip.Make("Master Switch — While On, Windows Matching A Rule Below Are Pinned Automatically (Scans Every Few Seconds).",
                "A Background Window-Title Scanner — Some Anti-Cheat Software Dislikes These. Leave Off On Gaming Profiles If In Doubt."),
            Margin = new Thickness(0, 0, 14, 0),
        };
        master.SetBinding(CheckBox.IsCheckedProperty,
            new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.WindowRulesEnabled)) { Source = vm });
        var add = new Button { Content = "Add Rule", ToolTip = "Add A Blank Rule (Type The Pattern In The List)", Margin = new Thickness(0, 0, 6, 0) };
        add.Click += (_, _) =>
        {
            _vm.WindowRules.Add(new WindowRule { Pattern = "", Enabled = true });
            _grid!.SelectedIndex = _vm.WindowRules.Count - 1;
            _grid.ScrollIntoView(_grid.SelectedItem);
        };
        // S28 K (from testing: "auto pin needs a detection feature like placement"): three
        // ways to add a rule without typing a process name.
        var fromFocus = new Button { Content = "Add From Focused Window (3 s)", ToolTip = "Wait 3 Seconds, Then Make A Rule For Whatever Window Has Focus", Margin = new Thickness(0, 0, 6, 0) };
        fromFocus.Click += async (_, _) =>
        {
            ToastService.Show("Focus The Window To Auto-Pin — 3 Seconds…", ToastType.Info, 3000);
            await System.Threading.Tasks.Task.Delay(3000);
            var fg = Helpers.Win32.GetForegroundWindow();
            if (fg == IntPtr.Zero || Helpers.Win32.IsOwnWindow(fg)) { ToastService.Show("Nothing Captured (Focus A Normal App Window)", ToastType.Warning); return; }
            var proc = Helpers.Win32.GetProcessName(fg) ?? "";
            _vm.WindowRules.Add(new WindowRule { Pattern = proc, Enabled = true });
            _grid!.SelectedIndex = _vm.WindowRules.Count - 1;
            ToastService.Show($"Rule Added: {proc}", ToastType.Success);
        };
        var pick = new Button { Content = "Pick Running…", ToolTip = "Choose From Running Programs, Or Browse To An .exe", Margin = new Thickness(0, 0, 6, 0) };
        pick.Click += (_, _) => { var p = ProcessPickerDialog.Pick(this); if (p is not null) { _vm.WindowRules.Add(new WindowRule { Pattern = p, Enabled = true }); _grid!.SelectedIndex = _vm.WindowRules.Count - 1; } };
        DockPanel.SetDock(master, Dock.Left);
        DockPanel.SetDock(add, Dock.Right); DockPanel.SetDock(pick, Dock.Right); DockPanel.SetDock(fromFocus, Dock.Right);
        bar.Children.Add(master);
        bar.Children.Add(add); bar.Children.Add(pick); bar.Children.Add(fromFocus);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        // Footer hint
        var hint = new TextBlock
        {
            Text = "A Rule Pins Any Visible Window Whose Process Or Title Contains The Pattern. " +
                   "Unpinning A Window By Hand Wins — It Won't Be Re-Pinned Until It Reopens.",
            Opacity = 0.55,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);

        // Grid
        Helpers.GridEdit.EnsureCommitted(_vm.WindowRules);   // S28 K crash fix (same class of bug as Placement)
        _grid = new DataGrid
        {
            ItemsSource = _vm.WindowRules,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
        };
        var enabledCol = new DataGridCheckBoxColumn
        {
            Header = "On",
            Binding = new Binding(nameof(WindowRule.Enabled)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(44),
            MinWidth = 40,
        };
        _grid.Columns.Add(enabledCol);
        _grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Pattern (Contains)",
            Binding = new Binding(nameof(WindowRule.Pattern)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 120,
        });
        var matchCol = new DataGridCheckBoxColumn
        {
            Header = "Match Title",
            Binding = new Binding(nameof(WindowRule.MatchTitle)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(86),
            MinWidth = 70,
        };
        _grid.Columns.Add(matchCol);

        var removeCol = new DataGridTemplateColumn { Header = "", Width = new DataGridLength(72), MinWidth = 60 };
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(Button.ContentProperty, "Remove");
        btnFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        btnFactory.SetValue(Button.ToolTipProperty, "Delete This Rule (Already-Pinned Windows Stay Pinned)");
        btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(RemoveRow_Click));
        removeCol.CellTemplate = new DataTemplate { VisualTree = btnFactory };
        _grid.Columns.Add(removeCol);

        // Click-out commits edits, matching the zone grids.
        PreviewMouseDown += (_, e) => GridEdit.CommitOnOutsideClick(_grid, e);

        root.Children.Add(_grid);
        Content = root;
        string snap = System.Text.Json.JsonSerializer.Serialize(_vm.WindowRules.Select(r => r.Clone()).ToList());
        bool wasOn = _vm.Cfg.WindowRulesEnabled;
        Closing += (_, _) => Helpers.GridEdit.EnsureCommitted(_vm.WindowRules, _grid);
        Helpers.EditorFooter.Wrap(this, root,
            onSave: () => _vm.SaveAll(),
            onCancel: () =>
            {
                var back = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<WindowRule>>(snap) ?? new();
                Helpers.GridEdit.EnsureCommitted(_vm.WindowRules, _grid);
                _vm.WindowRules.Clear(); foreach (var r in back) _vm.WindowRules.Add(r);
                _vm.Cfg.WindowRulesEnabled = wasOn;
                _vm.Log.Action("Auto-Pin window: changes reverted", "Rules");
            });
    }

    private void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WindowRule rule)
            _vm.WindowRules.Remove(rule);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowPlacement.Restore(this, "rules", _vm.Cfg);
        GridColumns.Restore(_grid, "rules", _vm.Cfg);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Drop empty-pattern rows, persist rules + layout.
        foreach (var empty in _vm.WindowRules.Where(r => r.Pattern.Trim().Length == 0).ToList())
            _vm.WindowRules.Remove(empty);
        _vm.Cfg.WindowRules = _vm.WindowRules.ToList();
        WindowPlacement.Save(this, "rules", _vm.Cfg);
        GridColumns.Save(_grid, "rules", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }
}
