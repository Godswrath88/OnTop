using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;

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
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (owner request, S22)
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
        var add = new Button { Content = "Add Rule", ToolTip = "Add A New Rule (Edit The Pattern In The List)" };
        add.Click += (_, _) =>
        {
            _vm.WindowRules.Add(new WindowRule { Pattern = "", Enabled = true });
            _grid!.SelectedIndex = _vm.WindowRules.Count - 1;
            _grid.ScrollIntoView(_grid.SelectedItem);
        };
        DockPanel.SetDock(master, Dock.Left);
        DockPanel.SetDock(add, Dock.Right);
        bar.Children.Add(master);
        bar.Children.Add(add);
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
