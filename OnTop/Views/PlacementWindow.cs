using System;
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
        Closing += (_, _) => { Helpers.WindowPlacement.Save(this, "placement", _vm.Cfg); _vm.SaveAll(); };

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
        B("Add Rule", "Add An Empty Rule (Edit It In The Grid)", () => { _vm.Placements.Add(new PlacementRule()); _grid!.SelectedIndex = _vm.Placements.Count - 1; });
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
        void Check(string h, string p, double w) => _grid.Columns.Add(new DataGridCheckBoxColumn { Header = h, Binding = new Binding(p) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(w) });
        void Text(string h, string p, double w, bool star = false) => _grid.Columns.Add(new DataGridTextColumn { Header = h, Binding = new Binding(p) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(w), MinWidth = 40 });
        Check("On", nameof(PlacementRule.Enabled), 44);
        Text("Name", nameof(PlacementRule.Name), 130);
        Text("Process", nameof(PlacementRule.Process), 110);
        Text("Title Contains", nameof(PlacementRule.TitleContains), 130);
        _grid.Columns.Add(new DataGridTextColumn { Header = "Mon", Binding = new Binding(nameof(PlacementRule.Monitor)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, Converter = new MonitorOneBased() }, Width = new DataGridLength(46) });
        Text("X", nameof(PlacementRule.X), 56); Text("Y", nameof(PlacementRule.Y), 56); Text("W", nameof(PlacementRule.W), 60); Text("H", nameof(PlacementRule.H), 60);
        Check("Max", nameof(PlacementRule.Maximize), 46);
        Check("On Start", nameof(PlacementRule.ApplyOnStartup), 64);
        Check("Launch", nameof(PlacementRule.LaunchEnabled), 58);
        Text("Launch Path (Verbatim)", nameof(PlacementRule.LaunchPath), 0, star: true);
        root.Children.Add(_grid);
        Content = root;
    }

    /// <summary>Shows monitors 1-based like the rest of the app; stores 0-based.</summary>
    private class MonitorOneBased : IValueConverter
    {
        public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) => v is int i ? (i + 1).ToString() : "1";
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => int.TryParse(v?.ToString(), out var i) ? Math.Max(0, i - 1) : 0;
    }
}
