using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>Builds and opens the right-click menu for a zone on the map
/// (used by both the embedded map and the pop-out map window).</summary>
public static class ZoneContextMenuFactory
{
    /// <summary>Open the zone context menu for the row under a right-click in a
    /// zone-list DataGrid. Selects that row first so the menu's commands (which
    /// act on the selected zone) target it. Wire from the grid's
    /// PreviewMouseRightButtonUp.</summary>
    public static void ShowForRow(MainViewModel vm, Window owner, DataGrid grid, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is not Zone zone) return;

        // Multi-select aware: right-clicking INSIDE an existing multi-selection
        // keeps it and offers bulk actions; right-clicking elsewhere selects that
        // row alone (the classic behavior).
        var selected = grid.SelectedItems.OfType<Zone>().ToList();
        if (selected.Count > 1 && selected.Contains(zone))
        {
            Show(vm, owner, grid, zone, selected);
        }
        else
        {
            grid.SelectedItem = zone;
            Show(vm, owner, grid, zone);
        }
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null and not T)
            node = VisualTreeHelper.GetParent(node);
        return node as T;
    }

    public static void Show(MainViewModel vm, Window owner, FrameworkElement placementTarget, Zone zone,
        System.Collections.Generic.IList<Zone>? bulk = null)
    {
        var menu = new ContextMenu { PlacementTarget = placementTarget };

        // Per-zone actions (S26; SECURITY.md "Per-Zone Actions" — the current
        // command/process is shown VERBATIM in the header so nothing is hidden).
        var actionMenu = new MenuItem { Header = "Set Action" };
        void ActItem(string header, string tip, bool check, Action onClick)
        {
            var mi = Item(header, onClick);
            mi.IsChecked = check;
            mi.ToolTip = tip;
            actionMenu.Items.Add(mi);
        }
        ActItem("Exit OnTop", "The Classic Behavior — Triggering This Zone Closes OnTop",
            zone.TriggerAction == ZoneAction.Exit,
            () => { zone.TriggerAction = ZoneAction.Exit; vm.Log.Action($"Zone action: {zone.Name} → Exit", "Zones"); });
        ActItem("Stealth (Unhook Mouse)", "Unhook Instead Of Exiting — Nothing Left For Anti-Cheats To See",
            zone.TriggerAction == ZoneAction.Stealth,
            () => { zone.TriggerAction = ZoneAction.Stealth; vm.Log.Action($"Zone action: {zone.Name} → Stealth", "Zones"); });
        ActItem(zone.ActionCommand.Length > 0 ? $"Run Command…  ({zone.ActionCommand})" : "Run Command…",
            "Run A Command/Program When This Zone Triggers — Shown Verbatim Here, Logged And Toasted On Every Run",
            zone.TriggerAction == ZoneAction.RunCommand,
            () =>
            {
                var cmd = InputDialog.Prompt(owner, "Zone Action — Run Command",
                    "Command To Run (Exactly As You'd Type It Into Win+R):", zone.ActionCommand);
                if (cmd is null) return;
                zone.ActionCommand = cmd.Trim();
                zone.TriggerAction = ZoneAction.RunCommand;
                vm.Log.Action($"Zone action: {zone.Name} → Run Command '{zone.ActionCommand}'", "Zones");
            });
        ActItem(zone.ActionProcess.Length > 0 && zone.TriggerAction == ZoneAction.KillProcess
                ? $"Kill Process…  ({zone.ActionProcess})" : "Kill Process…",
            "Terminate Every Process With This Name — Protected System Processes Are Refused; Logged With The Count",
            zone.TriggerAction == ZoneAction.KillProcess,
            () =>
            {
                var proc = InputDialog.Prompt(owner, "Zone Action — Kill Process",
                    "Process Name To Kill (e.g. notepad — No .exe Needed):", zone.ActionProcess);
                if (proc is null) return;
                zone.ActionProcess = proc.Trim();
                zone.TriggerAction = ZoneAction.KillProcess;
                vm.Log.Action($"Zone action: {zone.Name} → Kill Process '{zone.ActionProcess}'", "Zones");
            });
        ActItem(zone.ActionProcess.Length > 0 && zone.TriggerAction == ZoneAction.ToggleWindow
                ? $"Toggle Window…  ({zone.ActionProcess})" : "Toggle Window…",
            "Minimize/Restore That Process's Main Window Each Trigger",
            zone.TriggerAction == ZoneAction.ToggleWindow,
            () =>
            {
                var proc = InputDialog.Prompt(owner, "Zone Action — Toggle Window",
                    "Process Name Whose Window To Toggle (e.g. spotify):", zone.ActionProcess);
                if (proc is null) return;
                zone.ActionProcess = proc.Trim();
                zone.TriggerAction = ZoneAction.ToggleWindow;
                vm.Log.Action($"Zone action: {zone.Name} → Toggle Window '{zone.ActionProcess}'", "Zones");
            });
        var runMacro = new MenuItem
        {
            Header = "Run Macro",
            ToolTip = "Play A Recorded Macro When This Zone Triggers (Macros Window Manages Them; All Playback Guards Apply)",
        };
        if (vm.Cfg.Macros.Count == 0)
        {
            runMacro.Items.Add(new MenuItem { Header = "(No Macros Recorded Yet)", IsEnabled = false });
        }
        else
        {
            foreach (var macRef in vm.Cfg.Macros)
            {
                var mac = macRef;
                var mi = Item($"{mac.Name}  ({mac.Events.Count} ev)", () =>
                {
                    zone.ActionMacroId = mac.Id;
                    zone.TriggerAction = ZoneAction.RunMacro;
                    vm.Log.Action($"Zone action: {zone.Name} → Run Macro '{mac.Name}'", "Zones");
                });
                mi.IsChecked = zone.TriggerAction == ZoneAction.RunMacro && zone.ActionMacroId == mac.Id;
                runMacro.Items.Add(mi);
            }
        }
        actionMenu.Items.Add(runMacro);
        // S28 Stage E: Apply Placement — moves (and, if doubly gated on, launches) a program.
        var applyPlace = new MenuItem { Header = "Apply Placement", ToolTip = "Apply A Window Placement Rule When This Zone Triggers (Placement… Manages Them; Launch Stays Gated)" };
        if (vm.Placements.Count == 0) applyPlace.Items.Add(new MenuItem { Header = "(No Placement Rules Yet — APP → Placement…)", IsEnabled = false });
        else
            foreach (var prRef in vm.Placements)
            {
                var pr = prRef;
                var mi = Item($"{pr.Name}  ({pr.Process}{(pr.LaunchPath.Length > 0 ? " · launch: " + pr.LaunchPath : "")})", () =>
                {
                    zone.ActionPlacementId = pr.Id;
                    zone.TriggerAction = ZoneAction.ApplyPlacement;
                    vm.Log.Action($"Zone action: {zone.Name} → Apply Placement '{pr.Name}'", "Zones");
                });
                mi.IsChecked = zone.TriggerAction == ZoneAction.ApplyPlacement && zone.ActionPlacementId == pr.Id;
                applyPlace.Items.Add(mi);
            }
        actionMenu.Items.Add(applyPlace);
        // S28 Stage F: Library actions.
        var lib = new MenuItem { Header = "Library Action", ToolTip = "Run A Named Action From The Action Library (APP → Actions…)" };
        if (vm.Cfg.LibraryActions.Count == 0) lib.Items.Add(new MenuItem { Header = "(No Library Actions Yet — APP → Actions…)", IsEnabled = false });
        else
            foreach (var laRef in vm.Cfg.LibraryActions)
            {
                var la = laRef;
                var mi = Item($"{la.Name}  ({la.Summary})", () =>
                {
                    zone.ActionLibraryId = la.Id;
                    zone.TriggerAction = ZoneAction.Library;
                    vm.Log.Action($"Zone action: {zone.Name} → Library '{la.Name}'", "Zones");
                });
                mi.IsChecked = zone.TriggerAction == ZoneAction.Library && zone.ActionLibraryId == la.Id;
                lib.Items.Add(mi);
            }
        actionMenu.Items.Add(lib);
        menu.Items.Add(actionMenu);
        // S28 Stage G: which gesture fires this zone.
        var gest = new MenuItem { Header = $"Trigger Gesture  ({Zone.GestureNames[Math.Clamp(zone.Gesture, 0, 4)]})", ToolTip = "What You Do Inside The Zone To Fire It" };
        for (int gi = 0; gi < Zone.GestureNames.Length; gi++)
        {
            int g = gi;
            var mi = Item(Zone.GestureNames[g], () => { zone.Gesture = g; vm.Log.Action($"Zone gesture: {zone.Name} → {Zone.GestureNames[g]}", "Zones"); });
            mi.IsChecked = zone.Gesture == g;
            gest.Items.Add(mi);
        }
        menu.Items.Add(gest);
        // S28 Stage I: cooldown + schedule.
        var cool = Item($"Cooldown…  ({(zone.CooldownSec > 0 ? zone.CooldownSec + "s" : "none")})", () =>
        {
            var v = InputDialog.Prompt(owner, "Zone Cooldown", "Ignore repeat triggers for this many seconds after a hit (0 = none, max 3600):", zone.CooldownSec.ToString());
            if (v is not null && int.TryParse(v, out var n)) { zone.CooldownSec = Math.Clamp(n, 0, 3600); vm.Log.Action($"Zone cooldown: {zone.Name} → {zone.CooldownSec}s", "Zones"); }
        });
        cool.ToolTip = "A Safety Net For Kill / Run Command Zones — A Jittery Extra Click Can't Fire Them Twice";
        menu.Items.Add(cool);
        var sched = Item($"Schedule…  ({(zone.SchedStart < 0 ? "always" : $"{zone.SchedStart / 60:00}:{zone.SchedStart % 60:00}-{zone.SchedEnd / 60:00}:{zone.SchedEnd % 60:00}")})", () =>
        {
            var v = InputDialog.Prompt(owner, "Zone Schedule", "Active window as HH:MM-HH:MM (blank = always). Then days on the next prompt.", zone.SchedStart < 0 ? "" : $"{zone.SchedStart / 60:00}:{zone.SchedStart % 60:00}-{zone.SchedEnd / 60:00}:{zone.SchedEnd % 60:00}");
            if (v is null) return;
            v = v.Trim();
            if (v.Length == 0) { zone.SchedStart = -1; zone.SchedEnd = -1; zone.SchedDays = 0x7F; vm.Log.Action($"Zone schedule cleared: {zone.Name}", "Zones"); return; }
            var parts = v.Split('-');
            if (parts.Length != 2 || !ProfileSchedule.TryParseHm(parts[0], out var s0) || !ProfileSchedule.TryParseHm(parts[1], out var s1)) { Services.ToastService.Show("Use HH:MM-HH:MM", Services.ToastType.Warning); return; }
            var d = InputDialog.Prompt(owner, "Zone Schedule", "Days (e.g. MoTuWeThFr, or MoTuWeThFrSaSu for every day):", new ProfileSchedule { Days = zone.SchedDays }.DaysText);
            if (d is null) return;
            var tmp = new ProfileSchedule { DaysText = d }; if (tmp.Days == 0) tmp.Days = 0x7F;
            zone.SchedStart = s0; zone.SchedEnd = s1; zone.SchedDays = tmp.Days;
            vm.Log.Action($"Zone schedule: {zone.Name} → {parts[0]}-{parts[1]} {tmp.DaysText}", "Zones");
        });
        sched.ToolTip = "Only Fire This Zone During A Time Window On Chosen Days (e.g. Streaming Hours)";
        menu.Items.Add(sched);

        // Per-zone color picker (roadmap; swatch click-to-cycle remains too).
        var colorMenu = new MenuItem { Header = "Color" };
        for (int ci = 0; ci < Helpers.ZonePalette.Palette.Length; ci++)
        {
            int colorIdx = ci;
            var swatch = new System.Windows.Shapes.Rectangle
            {
                Width = 12, Height = 12,
                Fill = Helpers.ZonePalette.GetBrush(colorIdx),
                RadiusX = 2, RadiusY = 2,
            };
            var mi = new MenuItem { Header = $"Color {colorIdx + 1}", Icon = swatch };
            mi.IsChecked = zone.ColorIndex == colorIdx;
            mi.Click += (_, _) => { zone.ColorIndex = colorIdx; vm.Log.Action($"Zone color set: {zone.Name} → {colorIdx + 1}", "Zones"); };
            colorMenu.Items.Add(mi);
        }
        menu.Items.Add(colorMenu);
        menu.Items.Add(new Separator());

        // Bulk section first when several zones are selected (single undo each).
        if (bulk is { Count: > 1 })
        {
            int n = bulk.Count;
            menu.Items.Add(Item($"Remove Selected ({n})", () => vm.RemoveZones(bulk)));
            menu.Items.Add(Item($"Set Group For Selected ({n})...", () =>
            {
                var group = InputDialog.Prompt(owner, "Set Group (Bulk)", $"Group Name For {n} Zones:", zone.Group);
                if (group != null) vm.SetGroupFor(bulk, group);
            }));
            menu.Items.Add(Item($"Enable Selected ({n})", () => vm.SetEnabledFor(bulk, true)));
            menu.Items.Add(Item($"Disable Selected ({n})", () => vm.SetEnabledFor(bulk, false)));
            menu.Items.Add(new Separator());
        }

        menu.Items.Add(Item("Rename...", () =>
        {
            var name = InputDialog.Prompt(owner, "Rename Zone", "New Name:", zone.Name);
            if (!string.IsNullOrWhiteSpace(name)) vm.RenameZone(zone, name);
        }));
        menu.Items.Add(Item("Set Group...", () =>
        {
            var group = InputDialog.Prompt(owner, "Set Group", "Group Name:", zone.Group);
            if (group != null) vm.SetSelectedZoneGroup(group);
        }));
        menu.Items.Add(Item("Cycle Color", () => vm.CycleZoneColor(zone)));
        menu.Items.Add(Item(zone.TriggerAction == ZoneAction.Exit
            ? "Trigger Stealth Instead Of Exit"
            : "Trigger Exit Instead Of Stealth", () => vm.CycleZoneAction(zone)));

        // Draw zones can be re-sketched in place; Point/Box zones can't (there's
        // no polygon to redraw — resize them with the sliders or overlay handles).
        if (zone.Kind == ZoneKind.Draw)
            menu.Items.Add(Item("Redraw Shape...", () => Redraw(vm, owner, zone)));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item(zone.Enabled ? "Disable" : "Enable", () =>
        {
            vm.BeginZoneDrag();   // one undo step
            zone.Enabled = !zone.Enabled;
        }));
        menu.Items.Add(Item(zone.ShowDot ? "Hide Desktop Overlay" : "Show Desktop Overlay",
            () => zone.ShowDot = !zone.ShowDot));

        // Detection is first-match by list order, so reordering sets priority.
        int idx = vm.Zones.IndexOf(zone);
        menu.Items.Add(new Separator());
        var moveUp = Item("Move Up (Higher Priority)", () => vm.MoveZoneUp(zone));
        moveUp.IsEnabled = idx > 0;
        menu.Items.Add(moveUp);
        var moveDown = Item("Move Down (Lower Priority)", () => vm.MoveZoneDown(zone));
        moveDown.IsEnabled = idx >= 0 && idx < vm.Zones.Count - 1;
        menu.Items.Add(moveDown);

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Copy Details", () => CopyZoneDetails(zone)));
        menu.Items.Add(Item("Duplicate", () => vm.DuplicateZoneCmd.Execute(null)));

        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length > 1)
        {
            var dupTo = new MenuItem { Header = "Duplicate To Monitor" };
            for (int i = 0; i < screens.Length; i++)
            {
                int mi = i;   // capture per iteration
                dupTo.Items.Add(Item($"Monitor {i + 1}{(screens[i].Primary ? " (Primary)" : "")}",
                    () => vm.DuplicateZoneToMonitor(zone, mi)));
            }
            menu.Items.Add(dupTo);
        }

        menu.Items.Add(Item("Remove", () => vm.RemoveZoneCmd.Execute(null)));

        menu.IsOpen = true;
    }

    private static void CopyZoneDetails(Zone z)
    {
        string s = $"Name: {z.Name}\r\nPosition: {z.X}, {z.Y}\r\nMonitor: {z.Monitor + 1}\r\n" +
                   $"Kind: {z.Kind}   Action: {z.TriggerAction}   Group: {z.Group}\r\n" +
                   $"Tolerance: {z.Tolerance}   Cap: {z.CapX}x{z.CapY}   Variation: {z.Variation}\r\n" +
                   $"Enabled: {z.Enabled}   Overlay: {z.ShowDot}";
        try { System.Windows.Clipboard.SetText(s); } catch { /* clipboard busy — ignore */ }
    }

    /// <summary>Re-record a Draw zone's polygon in place. The owner window hides
    /// during capture so the gesture reaches the desktop, then returns; the
    /// zone's name, group, color, action, and enabled state all survive.</summary>
    private static void Redraw(MainViewModel vm, Window owner, Zone zone)
    {
        owner.Hide();
        ZoneCaptureOverlay.Capture(2,
            onDone: result => owner.Dispatcher.BeginInvoke(() =>
            {
                if (result.Points is { Count: >= 3 }) vm.ReplaceDrawPoints(zone, result.Points);
                owner.Show();
                owner.Activate();
            }),
            onCancel: () => owner.Dispatcher.BeginInvoke(() =>
            {
                owner.Show();
                owner.Activate();
                vm.StatusText = "Redraw Canceled";
            }),
            lockMode: true);
    }

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }
}
