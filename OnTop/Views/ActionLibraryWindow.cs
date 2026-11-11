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
/// S28 Stage F — Action Library manager: named, reusable actions that can be
/// hotkey-bound and/or assigned to zones. Each row: enabled, name, kind
/// (combo), the one payload that kind needs (edited via prompt so the right
/// picker appears), hotkey (conflict-checked), Run Now.
/// </summary>
public class ActionLibraryWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly StackPanel _rows = new();

    public ActionLibraryWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Action Library";
        Width = 900; Height = 540; MinWidth = 720; MinHeight = 360;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "library", _vm.Cfg);
        Closing += (_, _) => { Helpers.WindowPlacement.Save(this, "library", _vm.Cfg); _vm.BindHotkeys(); _vm.Settings.SaveNoBackup(_vm.Cfg); };

        var root = new DockPanel { Margin = new Thickness(14) };
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var master = new CheckBox
        {
            Content = "Enable Action Library", Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 6),
            ToolTip = Helpers.WarnTip.Make("Feature Switch (Ships OFF). While Off, No Library Hotkey Binds And Zones Assigned To Library Actions Refuse.",
                "Library Actions Can Run Commands, Kill Processes, And Play Macros — The Same Guarded Verbs Zones Already Have, Reachable From A Hotkey."),
        };
        master.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(MainViewModel.Cfg) + "." + nameof(AppSettings.ActionLibraryEnabled)) { Source = vm });
        master.Checked += (_, _) => { _vm.BindHotkeys(); Rebuild(); };
        master.Unchecked += (_, _) => { _vm.BindHotkeys(); Rebuild(); };
        bar.Children.Add(master);
        var add = new Button { Content = "New Action…", Margin = new Thickness(0, 0, 8, 6), ToolTip = "Create A Named Action — Then Pick Its Kind And Payload In Its Row" };
        add.Click += (_, _) =>
        {
            var name = InputDialog.Prompt(this, "New Action", "Action Name:", $"Action {_vm.Cfg.LibraryActions.Count + 1}");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_vm.Cfg.LibraryActions.Count >= 200) { ToastService.Show("Library Limit Reached (200)", ToastType.Warning); return; }
            _vm.Cfg.LibraryActions.Add(new LibraryAction { Name = name.Trim() });
            _vm.Log.Action($"Library action created: {name.Trim()}", "Zones");
            Rebuild();
        };
        bar.Children.Add(add);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        var hint = new TextBlock
        {
            Text = "An action is a thing OnTop can do, given a name. Bind it to a hotkey (Hotkey…), point zones at it (right-click a zone → Set Action → Library), or Run it here. Kinds that launch, kill, or play macros go through the same guards and logging as zone actions.",
            Opacity = 0.55, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        };
        DockPanel.SetDock(hint, Dock.Bottom);
        root.Children.Add(hint);
        root.Children.Add(new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        string snap = System.Text.Json.JsonSerializer.Serialize(_vm.Cfg.LibraryActions.Select(a => a.Clone()).ToList());
        bool wasOn = _vm.Cfg.ActionLibraryEnabled;
        Helpers.EditorFooter.Wrap(this, root,
            onSave: () => { _vm.BindHotkeys(); _vm.Settings.SaveNoBackup(_vm.Cfg); },
            onCancel: () =>
            {
                _vm.Cfg.LibraryActions = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<LibraryAction>>(snap) ?? new();
                _vm.Cfg.ActionLibraryEnabled = wasOn; _vm.BindHotkeys();
                _vm.Log.Action("Action Library window: changes reverted", "Zones");
            });
        Rebuild();
    }

    private void Rebuild()
    {
        bool on = _vm.Cfg.ActionLibraryEnabled;
        _rows.Children.Clear();
        foreach (var act in _vm.Cfg.LibraryActions.ToList())
        {
            var a = act;
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = false, IsEnabled = on };
            var en = new CheckBox { Style = (Style)Application.Current.FindResource("ToggleSwitch"), IsChecked = a.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Enable/Disable This Action (Its Hotkey Unbinds While Disabled)" };
            en.Checked += (_, _) => { a.Enabled = true; _vm.BindHotkeys(); };
            en.Unchecked += (_, _) => { a.Enabled = false; _vm.BindHotkeys(); };
            DockPanel.SetDock(en, Dock.Left); row.Children.Add(en);

            var kind = new ComboBox { Width = 150, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), ToolTip = "What This Action Does" };
            foreach (var k in Enum.GetValues<LibraryKind>()) kind.Items.Add(KindLabel(k));
            kind.SelectedIndex = (int)a.Kind;
            kind.SelectionChanged += (_, _) => { if (kind.SelectedIndex >= 0) { a.Kind = (LibraryKind)kind.SelectedIndex; EditPayload(a); Rebuild(); } };
            DockPanel.SetDock(kind, Dock.Left); row.Children.Add(kind);

            var info = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Text = $"{a.Name}  —  {Describe(a)}{(a.Hotkey.Length > 0 ? $"  [{a.Hotkey}]" : "")}", ToolTip = "Name — What It Does (Verbatim) — [Hotkey]" };
            DockPanel.SetDock(info, Dock.Left); row.Children.Add(info);

            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(btns, Dock.Right);
            void B(string t, string tip, Action f) { var b = new Button { Content = t, Margin = new Thickness(6, 0, 0, 0), ToolTip = tip }; b.Click += (_, _) => f(); btns.Children.Add(b); }
            B("Run", "Run This Action Now (All Guards Apply)", () => _vm.RunLibraryAction(a, "library window"));
            B("Payload…", "Change What This Action Targets (Command, Process, Macro, Placement, Or Profile)", () => { EditPayload(a); Rebuild(); });
            B("Hotkey…", "Assign An Optional Global Hotkey — Duplicates Across ALL Features Are Refused; Empty Clears", () =>
            {
                var key = HotkeyCaptureDialog.Prompt(this, _vm, $"Hotkey — {a.Name}", a.Hotkey, k => _vm.FindHotkeyConflict(k, exceptLibraryId: a.Id));
                if (key is null) return;
                a.Hotkey = key; _vm.Log.Action($"Library hotkey: {a.Name} → {(key.Length > 0 ? key : "(cleared)")}", "Hotkey"); _vm.BindHotkeys(); Rebuild();
            });
            B("Rename…", "Rename", () => { var n = InputDialog.Prompt(this, "Rename Action", "New Name:", a.Name); if (!string.IsNullOrWhiteSpace(n)) { a.Name = n.Trim(); Rebuild(); } });
            B("Delete", "Delete This Action (Zones Pointing At It Fall Back To Exit)", () =>
            {
                if (!ConfirmDialog.Confirm(this, "Delete Action", $"Delete \"{a.Name}\"?", "Delete", "Keep")) return;
                _vm.Cfg.LibraryActions.Remove(a);
                foreach (var z in _vm.Zones.Where(z => z.TriggerAction == ZoneAction.Library && z.ActionLibraryId == a.Id)) { z.TriggerAction = ZoneAction.Exit; z.ActionLibraryId = ""; }
                _vm.Log.Action($"Library action deleted: {a.Name}", "Zones"); _vm.BindHotkeys(); Rebuild();
            });
            row.Children.Add(btns);
            _rows.Children.Add(row);
        }
        if (_vm.Cfg.LibraryActions.Count == 0)
            _rows.Children.Add(new TextBlock { Text = on ? "No Actions Yet — New Action… To Create One." : "Turn The Switch On To Begin.", Opacity = 0.5 });
    }

    private string Describe(LibraryAction a) => a.Kind switch
    {
        LibraryKind.RunMacro => "Play macro: " + (_vm.Cfg.Macros.Find(m => m.Id == a.MacroId)?.Name ?? "(none chosen)"),
        LibraryKind.ApplyPlacement => "Placement: " + (_vm.Placements.FirstOrDefault(p => p.Id == a.PlacementId)?.Name ?? "(none chosen)"),
        LibraryKind.ApplyScene => "Scene: " + (_vm.Cfg.PinScenes.Find(sc => sc.Id == a.PlacementId)?.Name ?? "(none chosen)"),
        _ => a.Summary,
    };

    public static string KindLabel(LibraryKind k) => k switch
    {
        LibraryKind.RunCommand => "Run Command", LibraryKind.KillProcess => "Kill Process", LibraryKind.ToggleWindow => "Toggle Window",
        LibraryKind.RunMacro => "Play Macro", LibraryKind.ApplyPlacement => "Apply Placement", LibraryKind.SwitchProfile => "Switch Profile",
        LibraryKind.PauseToggle => "Pause / Resume", LibraryKind.Peek => "Peek", LibraryKind.StealthToggle => "Toggle Stealth",
        LibraryKind.PinUnderCursor => "Pin Under Cursor", LibraryKind.ApplyScene => "Apply Pin Scene", _ => k.ToString(),
    };

    private void EditPayload(LibraryAction a)
    {
        switch (a.Kind)
        {
            case LibraryKind.RunCommand:
                var cmd = InputDialog.Prompt(this, "Run Command", "Command Or Path (Exactly As You'd Type Into Win+R):", a.Command);
                if (cmd is not null) a.Command = cmd.Trim(); break;
            case LibraryKind.KillProcess:
            case LibraryKind.ToggleWindow:
                var p = InputDialog.Prompt(this, KindLabel(a.Kind), "Process Name (e.g. spotify):", a.Process);
                if (p is not null) a.Process = p.Trim(); break;
            case LibraryKind.RunMacro:
                a.MacroId = PickFrom("Play Macro", _vm.Cfg.Macros.Select(m => (m.Id, m.Name)).ToList(), a.MacroId); break;
            case LibraryKind.ApplyPlacement:
                a.PlacementId = PickFrom("Apply Placement", _vm.Placements.Select(pl => (pl.Id, pl.Name)).ToList(), a.PlacementId); break;
            case LibraryKind.SwitchProfile:
                a.ProfileName = PickFrom("Switch Profile", _vm.Cfg.Profiles.Select(pr => (pr.Name, pr.Name)).ToList(), a.ProfileName); break;
            case LibraryKind.ApplyScene:
                a.PlacementId = PickFrom("Apply Pin Scene", _vm.Cfg.PinScenes.Select(sc => (sc.Id, sc.Name)).ToList(), a.PlacementId); break;
        }
    }

    /// <summary>Tiny picker: a dialog with a ListBox of names; returns the chosen id (or current).</summary>
    private string PickFrom(string title, System.Collections.Generic.List<(string Id, string Name)> items, string current)
    {
        if (items.Count == 0) { ConfirmDialog.Notice($"{title}: Nothing To Choose From Yet", "There is nothing of this kind to pick yet. Create one first (for example a macro, a placement rule or a pin scene), then come back and choose it here.", this); return current; }
        var dlg = new Window { Style = (Style)Application.Current.FindResource("DarkChromeWindow"), Title = title, Width = 380, Height = 360, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Topmost = true, ShowInTaskbar = false };
        var lb = new ListBox { Margin = new Thickness(12) };
        foreach (var (id, name) in items) lb.Items.Add(new ListBoxItem { Content = name, Tag = id, IsSelected = id == current });
        lb.MouseDoubleClick += (_, _) => { dlg.DialogResult = true; dlg.Close(); };
        var ok = new Button { Content = "Choose", Margin = new Thickness(12, 0, 12, 12), Height = 30, Style = (Style)Application.Current.FindResource("AccentButton"), IsDefault = true };
        ok.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };
        var dp = new DockPanel(); DockPanel.SetDock(ok, Dock.Bottom); dp.Children.Add(ok); dp.Children.Add(lb);
        dlg.Content = dp;
        if (dlg.ShowDialog() == true && lb.SelectedItem is ListBoxItem li) return (string)li.Tag;
        return current;
    }
}
