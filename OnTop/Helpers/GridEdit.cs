using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OnTop.Helpers;

/// <summary>
/// WPF's DataGrid keeps an in-progress cell edit "open" when focus moves away by
/// a mouse click elsewhere — the typed value sits uncommitted until you press
/// Enter or click another row. This commits the active edit when the click lands
/// anywhere that isn't an actual grid row: outside the grid entirely, or on the
/// empty area below the rows.
///
/// Wire it from a window's PreviewMouseDown (tunneling), passing the grid and the
/// mouse event. The outside-the-grid test is coordinate-based (immune to
/// mouse-capture quirks); the empty-area test walks up from the clicked element
/// to see whether a DataGridRow was hit. Clicking a real row is left alone so you
/// can keep editing it; committing when nothing is being edited is a harmless
/// no-op.
/// </summary>
public static class GridEdit
{
    /// <summary>S28 K CRASH FIX: a DataGrid bound to a SHARED collection leaves that
    /// collection's default view in an EditItem/AddNew transaction if the window
    /// closes mid-edit; the next window to bind the same collection then throws
    /// "'DeferRefresh' is not allowed during an AddNew or EditItem transaction".
    /// Call before binding AND on Closing.</summary>
    public static void EnsureCommitted(System.Collections.IEnumerable source, System.Windows.Controls.DataGrid? grid = null)
    {
        try { grid?.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true); }
        catch (System.Exception ex) { System.Diagnostics.Debug.WriteLine($"CommitEdit: {ex.Message}"); /* justified-silent: best-effort before the view check below */ }
        if (System.Windows.Data.CollectionViewSource.GetDefaultView(source) is System.ComponentModel.IEditableCollectionView v)
        {
            if (v.IsEditingItem) v.CommitEdit();
            if (v.IsAddingNew) v.CommitNew();
        }
    }

    /// <summary>Roadmap polish: commit + release the edit when keyboard focus
    /// leaves the grid entirely (Tab/Enter chains out; click-out was S12).</summary>
    public static void AttachFocusCommit(DataGrid grid)
    {
        grid.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is DependencyObject d && grid.IsAncestorOf2(d)) return;
            grid.CommitEdit(DataGridEditingUnit.Row, true);
        };
    }

    private static bool IsAncestorOf2(this DataGrid grid, DependencyObject d)
    {
        while (d is not null)
        {
            if (ReferenceEquals(d, grid)) return true;
            d = System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    public static void CommitOnOutsideClick(DataGrid? grid, MouseButtonEventArgs e)
    {
        if (grid is null) return;

        Point p = e.GetPosition(grid);
        bool insideBounds = p.X >= 0 && p.Y >= 0 && p.X <= grid.ActualWidth && p.Y <= grid.ActualHeight;
        bool onRow = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) != null;

        // Leave the edit alone ONLY when the click is inside the grid AND on an
        // actual row (you're still interacting with a row). Everything else —
        // outside the grid, or the grid's empty space — commits.
        if (insideBounds && onRow) return;

        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null and not T)
            node = VisualTreeHelper.GetParent(node);
        return node as T;
    }
}
