using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Windows.Input;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Shared Delete/F2 handling for the zone-list DataGrids (main + pop-out).
/// Delete removes the selected zone (undoably, through the VM command); F2 opens
/// the rename prompt. Both no-op while a cell is being edited so the keys keep
/// their normal in-editor behavior. Wire from the grid's PreviewKeyDown.
/// </summary>
public static class ZoneListKeys
{
    public static void Handle(KeyEventArgs e, MainViewModel vm, Window owner, System.Windows.Controls.DataGrid? grid = null)
    {
        if (e.OriginalSource is TextBox) return;     // editing a cell — leave keys alone
        if (vm.SelectedZone is not Zone zone) return;

        // Ctrl+Arrow nudges the selected zone (Shift = 10px). Plain arrows stay
        // list navigation.
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
            e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10 : 1;
            int dx = e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0;
            int dy = e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0;
            vm.NudgeSelectedZone(dx, dy);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            // Multi-select aware: several rows selected → one bulk (single-undo)
            // removal; otherwise the classic single-zone path.
            var selected = grid?.SelectedItems.OfType<Models.Zone>().ToList();
            if (selected is { Count: > 1 })
                vm.RemoveZones(selected);
            else if (vm.RemoveZoneCmd.CanExecute(null))
                vm.RemoveZoneCmd.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            var name = InputDialog.Prompt(owner, "Rename Zone", "New Name:", zone.Name);
            if (!string.IsNullOrWhiteSpace(name)) vm.RenameZone(zone, name);
            e.Handled = true;
        }
    }
}
