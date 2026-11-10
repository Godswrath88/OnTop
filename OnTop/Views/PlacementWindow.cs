using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Models;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// S28 Stage E — Window Placement &amp; Launcher manager. A grid of rules
/// (where a window goes), an optional launch half that is visibly sub-gated,
/// "Capture From Focused Window" to author a rule in one click, Apply / Apply All.
/// </summary>
public class PlacementWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly DataGrid _grid;

    public PlacementWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Window Placement & Launcher";
        Width = 980; Height = 520; MinWidth = 760; MinHeight = 340;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "placement", _vm.Cfg);
        Closing += (_, _) => { Helpers.GridEdit.EnsureCommitted(_vm.Placements, _grid); Helpers.WindowPlacement.Save(this, "placement", _vm.Cfg); _vm.SaveAll(); };
        Helpers.GridEdit.EnsureCommitted(_vm.Placements);   // S28 K crash fix (see GridEdit)

        var root = new DockPanel { Margin = new Thickness(12) };

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var master = new CheckBox
        {
            Content = "Enable Window Placement", Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 6),
            ToolTip = Helpers.WarnTip.Make("Feature Switch (Ships OFF). While Off, Rules Are Inert: No Startup Placement, No Hotkey, Zone Actions Refuse.",
                "A Rule Only Starts A Program If Its Own 'Launch' Box Is Ticked. Launch Paths Are Shown Verbatim; Imports Arrive With Them Blanked."),
        };
        master.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.PlacementEnabled)) { Source = vm });
        bar.Children.Add(master);
        void B(string t, string tip, Action a)
        {
            var b = new Button { Content = t, Margin = new Thickness(0, 0, 8, 6), ToolTip = tip };
            b.Click += (_, _) => a(); bar.Children.Add(b);
        }
        B("Add Rule…", "Create A Rule In The Full Editor (Process Picker, Monitor, Position, Delay, Launch)", () => { var r = new PlacementRule(); if (PlacementRuleDialog.Edit(this, r)) { _vm.Placements.Add(r); _grid!.SelectedItem = r; } });
        B("Edit Selected…", "Open The Selected Rule In The Full Editor (Or Double-Click / Right-Click A Row)", () => { if (_grid!.SelectedItem is PlacementRule r) { PlacementRuleDialog.Edit(this, r); _grid.Items.Refresh(); } });
        B("Capture From Focused Window", "Wait 3 Seconds, Then Read The Focused Window's Process, Monitor, Position And Size Into A New Rule — 'Put It Where It Is Now'", async () =>
        {
            ToastService.Show("Focus The Window To Capture — 3 Seconds…", ToastType.Info, 3000);
            await System.Threading.Tasks.Task.Delay(3000);
            var r = new PlacementRule();
            if (_vm.PlacementSvc.CaptureForeground(r)) { _vm.Placements.Add(r); _grid!.SelectedItem = r; ToastService.Show($"Captured: {r.Name}", ToastType.Success); }
            else ToastService.Show("Nothing Captured (Focus A Normal App Window, Not OnTop)", ToastType.Warning);
        });
        B("Apply Selected", "Move (And If Gated On, Launch) The Selected Rule's Window Now", async () => { if (_grid!.SelectedItem is PlacementRule r) await _vm.PlacementSvc.ApplyAsync(r, "button"); });
        B("Apply All", $"Apply Every Enabled Rule Now (Hotkey: {(string.IsNullOrWhiteSpace(_vm.Cfg.HotkeyPlacement) ? "unbound" : _vm.Cfg.HotkeyPlacement)})", async () => await _vm.PlacementSvc.ApplyAllAsync("button"));
        B("Remove Selected", "Delete The Selected Rule", () => { if (_grid!.SelectedItem is PlacementRule r) _vm.Placements.Remove(r); });
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        var hint = new TextBlock
        {
            Text = "X / Y / W / H are pixels from the top-left of the chosen monitor (1 = primary). Tick Maximize to ignore them. 'On Start' applies the rule a few seconds after OnTop launches. A rule starts its program only if its Launch box is ticked AND no window already matches.",
            Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);

        _grid = new DataGrid
        {
            ItemsSource = _vm.Placements, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single,
        };
        // S28 K: every column header carries a tooltip; cells stay editable inline,
        // and the full editor is a double-click / right-click / Edit Selected… away.
        static object H(string text, string tip) => new TextBlock { Text = text, ToolTip = tip };
        void Check(string h, string tip, string p, double w) => _grid.Columns.Add(new DataGridCheckBoxColumn { Header = H(h, tip), Binding = new Binding(p) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(w) });
        void Text(string h, string tip, string p, double w, bool star = false) => _grid.Columns.Add(new DataGridTextColumn { Header = H(h, tip), Binding = new Binding(p) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w), MinWidth = 40 });
        Check("On", "Rule Enabled", nameof(PlacementRule.Enabled), 44);
        Text("Name", "Label For This Rule", nameof(PlacementRule.Name), 120);
        Text("Process", "Process Name Without .exe (e.g. spotify)", nameof(PlacementRule.Process), 100);
        Text("Title Contains", "Optional Title Text To Narrow The Match", nameof(PlacementRule.TitleContains), 120);
        _grid.Columns.Add(new DataGridTextColumn { Header = H("Mon", "Target Monitor (1 = Primary, Numbered Like The Zone Map)"), Binding = new Binding(nameof(PlacementRule.Monitor)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, Converter = new MonitorOneBased() }, Width = new DataGridLength(46) });
        Text("X", "Left Edge, Pixels From That Monitor's Left", nameof(PlacementRule.X), 52); Text("Y", "Top Edge, Pixels From That Monitor's Top", nameof(PlacementRule.Y), 52); Text("W", "Width In Pixels", nameof(PlacementRule.W), 56); Text("H", "Height In Pixels", nameof(PlacementRule.H), 56);
        Check("Max", "Maximize On That Monitor Instead Of Using X/Y/W/H", nameof(PlacementRule.Maximize), 46);
        Check("On Start", "Apply This Rule A Few Seconds After OnTop Launches", nameof(PlacementRule.ApplyOnStartup), 64);
        Text("Delay", "Seconds To Wait Before Applying (For Programs Still Starting Up)", nameof(PlacementRule.DelaySec), 50);
        Check("Launch", "Start The Program Below If No Window Matches (Off = Move Only)", nameof(PlacementRule.LaunchEnabled), 58);
        Text("Launch Path (Verbatim)", "Program To Start When Launch Is Ticked And Nothing Matches — Shown Exactly As Stored", nameof(PlacementRule.LaunchPath), 0, star: true);
        _grid.MouseDoubleClick += (_, _) => { if (_grid.SelectedItem is PlacementRule r) { PlacementRuleDialog.Edit(this, r); _grid.Items.Refresh(); } };
        var ctx = new ContextMenu();
        var editMi = new MenuItem { Header = "Edit Rule…" }; editMi.Click += (_, _) => { if (_grid.SelectedItem is PlacementRule r) { PlacementRuleDialog.Edit(this, r); _grid.Items.Refresh(); } };
        var applyMi = new MenuItem { Header = "Apply Now" }; applyMi.Click += async (_, _) => { if (_grid.SelectedItem is PlacementRule r) await _vm.PlacementSvc.ApplyAsync(r, "context menu"); };
        var dupMi = new MenuItem { Header = "Duplicate" }; dupMi.Click += (_, _) => { if (_grid.SelectedItem is PlacementRule r) { var c = r.Clone(); c.Id = Guid.NewGuid().ToString("N")[..8]; c.Name += " (copy)"; _vm.Placements.Add(c); } };
        var delMi = new MenuItem { Header = "Remove" }; delMi.Click += (_, _) => { if (_grid.SelectedItem is PlacementRule r) _vm.Placements.Remove(r); };
        ctx.Items.Add(editMi); ctx.Items.Add(applyMi); ctx.Items.Add(dupMi); ctx.Items.Add(delMi);
        _grid.ContextMenu = ctx;
        root.Children.Add(_grid);
        Content = root;
        // S28 K: explicit footer + revert (snapshot of the rules at open).
        string snap = System.Text.Json.JsonSerializer.Serialize(_vm.Placements.Select(p => p.Clone()).ToList());
        Helpers.EditorFooter.Wrap(this, root,
            onSave: () => _vm.SaveAll(),
            onCancel: () =>
            {
                var back = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<PlacementRule>>(snap) ?? new();
                Helpers.GridEdit.EnsureCommitted(_vm.Placements, _grid);
                _vm.Placements.Clear(); foreach (var p in back) _vm.Placements.Add(p);
                _vm.Log.Action("Placement window: changes reverted", "Placement");
            });
    }

    /// <summary>Shows monitors 1-based like the rest of the app; stores 0-based.</summary>
    private class MonitorOneBased : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => v is int i ? (i + 1).ToString() : "1";
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => int.TryParse(v?.ToString(), out var i) ? Math.Max(0, i - 1) : 0;
    }
}
