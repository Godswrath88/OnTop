using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 Stage G — process → profile rules grid (off by default).</summary>
public class ProfileRulesWindow : Window
{
    public ProfileRulesWindow(MainViewModel vm)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Profile Rules (Process → Profile)";
        Width = 620; Height = 400; MinWidth = 480; MinHeight = 260;
        ShowInTaskbar = true; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        var rules = new System.Collections.ObjectModel.ObservableCollection<ProfileRule>(vm.Cfg.ProfileRules);
        bool cancelled = false; bool wasOn = vm.Cfg.ProfileRulesEnabled;
        Closing += (_, _) => { if (cancelled) { vm.Cfg.ProfileRulesEnabled = wasOn; return; } vm.Cfg.ProfileRules = new System.Collections.Generic.List<ProfileRule>(rules); vm.Settings.SaveNoBackup(vm.Cfg); };

        var root = new DockPanel { Margin = new Thickness(12) };
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var master = new CheckBox { Content = "Enable Profile Rules", Style = (Style)Application.Current.FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 6),
            ToolTip = Helpers.WarnTip.Make("While A Listed Process Is Running, OnTop Switches To That Profile (And Back When It Exits, If Ticked). Checked Every 5 s With Auto-Stealth.", "A Background Process-Name Scanner (Names Only, Never Memory).") };
        master.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.ProfileRulesEnabled)) { Source = vm });
        bar.Children.Add(master);
        var add = new Button { Content = "Add Rule", Margin = new Thickness(0, 0, 8, 6) };
        DataGrid? grid = null;
        add.Click += (_, _) => { rules.Add(new ProfileRule { Profile = vm.Cfg.ActiveProfile }); grid!.SelectedIndex = rules.Count - 1; };
        bar.Children.Add(add);
        var del = new Button { Content = "Remove Selected", Margin = new Thickness(0, 0, 8, 6) };
        del.Click += (_, _) => { if (grid!.SelectedItem is ProfileRule r) rules.Remove(r); };
        bar.Children.Add(del);
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        var hint = new TextBlock { Text = "Process = the program's name without .exe (e.g. valorant). Profile = an existing profile name. Great for putting 'Block Macros' game profiles on autopilot.", Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(hint, Dock.Bottom); root.Children.Add(hint);
        grid = new DataGrid { ItemsSource = rules, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "On", Binding = new Binding(nameof(ProfileRule.Enabled)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(44) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Process (No .exe)", Binding = new Binding(nameof(ProfileRule.Process)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var profCol = new DataGridComboBoxColumn { Header = "Profile", SelectedValueBinding = new Binding(nameof(ProfileRule.Profile)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, ItemsSource = vm.ProfileNames, Width = new DataGridLength(160) };
        grid.Columns.Add(profCol);
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Revert When Gone", Binding = new Binding(nameof(ProfileRule.RevertWhenGone)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(120) });
        root.Children.Add(grid);
        Content = root;
        Helpers.EditorFooter.Wrap(this, root, onSave: null, onCancel: () => { cancelled = true; vm.Log.Action("Profile Rules window: changes discarded", "Profiles"); });
    }
}
