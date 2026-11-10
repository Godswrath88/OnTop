using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 Stage G — keyboard-first command palette: type to filter every
/// action OnTop can take (built-ins, windows, profiles, zone toggles, library
/// actions, macros), Enter runs it, Esc closes. Only things that are currently
/// ENABLED appear (a dark feature contributes nothing).</summary>
public class CommandPaletteWindow : Window
{
    private readonly List<(string Label, string Group, Action Run)> _all = new();
    private readonly TextBox _box = new() { FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };
    private readonly ListBox _list = new() { MaxHeight = 380 };

    public CommandPaletteWindow(MainViewModel vm, MainWindow main)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Command Palette";
        Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;

        void Add(string group, string label, Action run) => _all.Add((label, group, run));
        Add("OnTop", "Show / Hide Main Window", () => vm.RequestMainWindow());
        Add("OnTop", vm.DetectionPaused ? "Resume Zone Detection" : "Pause Zone Detection", () => vm.DetectionPaused = !vm.DetectionPaused);
        Add("OnTop", vm.Cfg.StealthMode ? "Leave Stealth Mode" : "Enter Stealth Mode", () => vm.Cfg.StealthMode = !vm.Cfg.StealthMode);
        Add("OnTop", "Peek At All Zones", vm.RequestPeek);
        Add("OnTop", "Pin / Unpin Window Under Cursor", vm.TogglePinUnderCursor);
        Add("OnTop", "Minimal Mode (Everything Optional Off)", () => { if (ConfirmDialog.Confirm(this, "Minimal Mode", "Turn every optional feature off?", "Go Minimal", "Cancel")) vm.ApplyMinimalMode(); });
        Add("Windows", "Open Macros", main.ShowMacros);
        Add("Windows", "Open Action Library", main.ShowLibrary);
        Add("Windows", "Open Window Placement", main.ShowPlacement);
        Add("Windows", "Open Pinned Windows", main.ShowPinnedWindows);
        Add("Windows", "Open Auto-Pin", main.ShowWindowRules);
        foreach (var sc in vm.Cfg.PinScenes) { var scene = sc; Add("Scene", $"Apply Pin Scene: {scene.Name}", () => vm.ApplyScene(scene, "palette")); }
        foreach (var p in vm.Cfg.Profiles) { var name = p.Name; Add("Profile", $"Switch To Profile: {name}", () => vm.SwitchProfile(name)); }
        foreach (var z in vm.Zones) { var zone = z; Add("Zone", $"{(zone.Enabled ? "Disable" : "Enable")} Zone: {zone.Name}", () => zone.Enabled = !zone.Enabled); }
        if (vm.Cfg.ActionLibraryEnabled) foreach (var a in vm.Cfg.LibraryActions.Where(a => a.Enabled)) { var act = a; Add("Action", $"Run Action: {act.Name}", () => vm.RunLibraryAction(act, "palette")); }
        if (vm.Cfg.MacrosEnabled) foreach (var m in vm.Cfg.Macros.Where(m => m.Enabled)) { var mac = m; Add("Macro", $"Play Macro: {mac.Name}", () => vm.MacroSvc.Play(mac)); }

        var root = new StackPanel { Margin = new Thickness(14) };
        root.Children.Add(_box);
        root.Children.Add(_list);
        root.Children.Add(new TextBlock { Text = "Type To Filter · Enter Runs · Esc Closes", Opacity = 0.5, FontSize = 11, Margin = new Thickness(0, 6, 0, 0) });
        Content = root;
        _box.TextChanged += (_, _) => Refill();
        _box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && _list.Items.Count > 0) { _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _list.Items.Count - 1); _list.ScrollIntoView(_list.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Up && _list.Items.Count > 0) { _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0); _list.ScrollIntoView(_list.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Enter) { RunSelected(); e.Handled = true; }
        };
        _list.MouseDoubleClick += (_, _) => RunSelected();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.Enter && _list.IsKeyboardFocusWithin) { RunSelected(); e.Handled = true; } };
        Deactivated += (_, _) => Close();
        Loaded += (_, _) => { _box.Focus(); Refill(); };
    }

    private void Refill()
    {
        string q = _box.Text.Trim();
        _list.Items.Clear();
        foreach (var (label, group, run) in _all.Where(x => q.Length == 0 || x.Label.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Group.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(60))
            _list.Items.Add(new ListBoxItem { Content = $"{group}  ›  {label}", Tag = run });
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void RunSelected()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: Action run })
        {
            Close();
            try { run(); } catch (Exception ex) { Services.ToastService.Show($"Palette Action Failed: {ex.Message}", Services.ToastType.Error); }
        }
    }
}
