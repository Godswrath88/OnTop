using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 Stage I — time-of-day profile schedules grid.</summary>
public class ProfileSchedulesWindow : Window
{
    public ProfileSchedulesWindow(MainViewModel vm)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Profile Schedules";
        Width = 640; Height = 400; MinWidth = 480; MinHeight = 260; ShowInTaskbar = true; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        var rows = new System.Collections.ObjectModel.ObservableCollection<ProfileSchedule>(vm.Cfg.ProfileSchedules);
        Closing += (_, _) => { vm.Cfg.ProfileSchedules = new System.Collections.Generic.List<ProfileSchedule>(rows); vm.Settings.SaveNoBackup(vm.Cfg); };
        var root = new DockPanel { Margin = new Thickness(12) };
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var master = new CheckBox { Content = "Enable Schedules", Style = (Style)Application.Current.FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 6), ToolTip = "Checked Once A Minute; Process Rules Take Priority" };
        master.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.ProfileSchedulesEnabled)) { Source = vm });
        bar.Children.Add(master);
        DataGrid? grid = null;
        var add = new Button { Content = "Add", Margin = new Thickness(0, 0, 8, 6) }; add.Click += (_, _) => { rows.Add(new ProfileSchedule { Profile = vm.Cfg.ActiveProfile }); grid!.SelectedIndex = rows.Count - 1; }; bar.Children.Add(add);
        var del = new Button { Content = "Remove Selected", Margin = new Thickness(0, 0, 8, 6) }; del.Click += (_, _) => { if (grid!.SelectedItem is ProfileSchedule r) rows.Remove(r); }; bar.Children.Add(del);
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        var hint = new TextBlock { Text = "Times are HH:MM (24 h); a window that crosses midnight is fine (22:00-02:00). Days like MoTuWeThFr or MoTuWeThFrSaSu.", Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(hint, Dock.Bottom); root.Children.Add(hint);
        grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "On", Binding = new Binding(nameof(ProfileSchedule.Enabled)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(44) });
        grid.Columns.Add(new DataGridTextColumn { Header = "From", Binding = new Binding(nameof(ProfileSchedule.StartText)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = "To", Binding = new Binding(nameof(ProfileSchedule.EndText)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Days", Binding = new Binding(nameof(ProfileSchedule.DaysText)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(150) });
        grid.Columns.Add(new DataGridComboBoxColumn { Header = "Profile", SelectedValueBinding = new Binding(nameof(ProfileSchedule.Profile)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, ItemsSource = vm.ProfileNames, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        root.Children.Add(grid);
        Content = root;
    }
}
