using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OnTop.Helpers;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Profiles manager — named zone workspaces (zones, exceptions, triggers,
/// slider defaults). Switch / New (copies the current workspace) / Rename /
/// Delete, with the active profile marked. Switching is also available from the
/// main window combo, the tray submenu, and the optional cycle hotkey.
/// </summary>
public class ProfilesWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ListBox _list;

    public ProfilesWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Profiles";
        Width = 440; Height = 360;
        MinWidth = 340; MinHeight = 240;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (owner request, S22)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = vm;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });

        var root = new DockPanel { Margin = new Thickness(12) };

        var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        Button Mk(string text, string tip, RoutedEventHandler click)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0), ToolTip = tip };
            b.Click += click;
            bar.Children.Add(b);
            return b;
        }
        Mk("Switch To", "Make The Selected Profile Active", (_, _) => SwitchSelected());
        Mk("New", "Create A New Profile (Copies The Current Workspace) And Switch To It", (_, _) =>
        {
            var name = InputDialog.Prompt(this, "New Profile", "Name For The New Profile:", "");
            if (!string.IsNullOrWhiteSpace(name)) _vm.CreateProfile(name);
        });
        Mk("Rename", "Rename The Selected Profile", (_, _) =>
        {
            if (Selected() is not { } sel) return;
            var name = InputDialog.Prompt(this, "Rename Profile", $"New Name For \"{sel}\":", sel);
            if (!string.IsNullOrWhiteSpace(name)) _vm.RenameProfile(sel, name);
        });
        Mk("Delete", "Delete The Selected Profile (The Last One Can't Be Deleted)", (_, _) =>
        {
            if (Selected() is not { } sel) return;
            if (ConfirmDialog.Confirm(this, "Delete Profile",
                $"Delete the profile \"{sel}\" and its zones?\n\nThis cannot be undone.",
                "Delete", "Cancel"))
                _vm.DeleteProfile(sel);
        });
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        var hint = new TextBlock
        {
            Text = "A Profile Holds Zones, Exceptions, Auto-Close Triggers, And The Default Sliders. " +
                   "App Settings, Hotkeys, And Auto-Pin Rules Stay Global. Double-Click A Profile To Switch.",
            Opacity = 0.55,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);

        _list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        _list.MouseDoubleClick += (_, _) => SwitchSelected();
        root.Children.Add(_list);
        Content = root;

        _vm.ProfilesChanged += Rebuild;
        Closed += (_, _) => _vm.ProfilesChanged -= Rebuild;
        Rebuild();
    }

    private string? Selected()
        => (_list.SelectedItem as ListBoxItem)?.Tag as string;

    private void SwitchSelected()
    {
        if (Selected() is { } sel) _vm.SwitchProfile(sel);
    }

    private void Rebuild()
    {
        _list.Items.Clear();
        foreach (var p in _vm.Cfg.Profiles)
        {
            bool active = string.Equals(p.Name, _vm.ActiveProfileName, StringComparison.OrdinalIgnoreCase);
            var row = new DockPanel { Margin = new Thickness(4, 2, 4, 2) };
            var name = new TextBlock
            {
                Text = (active ? "●  " : "○  ") + p.Name,
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var count = new TextBlock
            {
                Text = $"{p.Zones.Count} Zone{(p.Zones.Count == 1 ? "" : "s")}",
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(count, Dock.Right);
            row.Children.Add(count);
            row.Children.Add(name);
            _list.Items.Add(new ListBoxItem { Content = row, Tag = p.Name });
            if (active) _list.SelectedIndex = _list.Items.Count - 1;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowPlacement.Restore(this, "profiles", _vm.Cfg);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        WindowPlacement.Save(this, "profiles", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }
}
