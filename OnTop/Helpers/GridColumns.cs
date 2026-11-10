using System.Windows.Controls;
using OnTop.Models;

namespace OnTop.Helpers;

/// <summary>
/// Persists DataGrid column widths into AppSettings.ColumnWidths (which lives in
/// the user config), so column sizing survives restarts and version updates.
/// Call Restore after the grid is loaded and Save on window close.
///
/// Widths are keyed "gridKey/index". Only star and absolute pixel widths are
/// stored; Auto/SizeToHeader columns are left alone. Restored values are clamped
/// to a sane floor so a bad value can't collapse a column to nothing.
/// </summary>
public static class GridColumns
{
    private const double MinWidth = 24;
    private const double MaxWidth = 4000;

    public static void Restore(DataGrid grid, string gridKey, AppSettings cfg)
    {
        if (grid == null || cfg.ColumnWidths == null) return;
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            if (cfg.ColumnWidths.TryGetValue($"{gridKey}/{i}", out double w)
                && w >= MinWidth && w <= MaxWidth)
            {
                grid.Columns[i].Width = new DataGridLength(w, DataGridLengthUnitType.Pixel);
            }
        }
    }

    public static void Save(DataGrid grid, string gridKey, AppSettings cfg)
    {
        if (grid == null) return;
        cfg.ColumnWidths ??= new();
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            double w = grid.Columns[i].ActualWidth;
            if (w >= MinWidth && w <= MaxWidth)
                cfg.ColumnWidths[$"{gridKey}/{i}"] = w;
        }
    }
}
