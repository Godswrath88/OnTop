using System;
using System.Windows;
using OnTop.Models;

namespace OnTop.Helpers;

/// <summary>
/// Save and restore a window's size/position across sessions, keyed by a stable
/// name in AppSettings.WindowPlacements.
///
/// Restore is defensive: a rect saved on a monitor that's since been unplugged
/// (or a resolution that shrank) would otherwise open the window off-screen with
/// no way to grab its title bar. So Restore clamps the geometry onto the current
/// virtual screen — sizes are capped to what fits, and the position is nudged so
/// a usable strip of the title bar always lands on a visible monitor.
///
/// Call Restore from a window's OnSourceInitialized (before it's shown, so there's
/// no reposition flicker) and Save from its OnClosing / before it hides.
/// </summary>
public static class WindowPlacement
{
    private const double MinVisible = 90;   // px of the window kept grabbable
    private const double MinWidth = 320;
    private const double MinHeight = 200;

    /// <summary>Apply the saved placement for <paramref name="key"/> if one exists,
    /// clamped onto a visible monitor. No-op when nothing is saved.</summary>
    public static void Restore(Window w, string key, AppSettings cfg)
    {
        if (cfg.WindowPlacements is null ||
            !cfg.WindowPlacements.TryGetValue(key, out var r) || r is null)
            return;

        double vsL = SystemParameters.VirtualScreenLeft;
        double vsT = SystemParameters.VirtualScreenTop;
        double vsW = SystemParameters.VirtualScreenWidth;
        double vsH = SystemParameters.VirtualScreenHeight;
        if (vsW <= 0 || vsH <= 0) return;   // no display info — leave XAML defaults
        double vsR = vsL + vsW;
        double vsB = vsT + vsH;

        // Size: honor the window's own minimums, cap to the virtual screen.
        double minW = Math.Max(MinWidth, double.IsNaN(w.MinWidth) ? 0 : w.MinWidth);
        double minH = Math.Max(MinHeight, double.IsNaN(w.MinHeight) ? 0 : w.MinHeight);
        double width = Clamp(r.Width, minW, vsW);
        double height = Clamp(r.Height, minH, vsH);

        // Position: keep at least MinVisible px of the window on screen, and never
        // let the title bar go above the top of the virtual screen.
        double left = Clamp(r.Left, vsL - width + MinVisible, vsR - MinVisible);
        double top = Clamp(r.Top, vsT, vsB - MinVisible);

        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = left;
        w.Top = top;
        w.Width = width;
        w.Height = height;
        w.WindowState = r.Maximized ? WindowState.Maximized : WindowState.Normal;
    }

    /// <summary>Capture the window's current placement into settings (in memory).
    /// Uses RestoreBounds when maximized so the un-maximized size is preserved.
    /// The caller is responsible for persisting settings to disk.</summary>
    public static void Save(Window w, string key, AppSettings cfg)
    {
        cfg.WindowPlacements ??= new();

        bool maximized = w.WindowState == WindowState.Maximized;
        // Use ActualWidth/ActualHeight, not Width/Height: with custom WindowChrome
        // a drag-resize updates the actual rendered size but leaves the Width/Height
        // properties at their XAML defaults, so reading those would always persist
        // the default size. RestoreBounds gives the correct normal-state rect when
        // maximized/minimized.
        Rect b = maximized || w.WindowState == WindowState.Minimized
            ? w.RestoreBounds
            : new Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight);

        // A minimized or not-yet-laid-out window reports NaN/empty bounds — skip.
        if (double.IsNaN(b.Left) || double.IsNaN(b.Top) ||
            b.Width <= 0 || b.Height <= 0)
            return;

        cfg.WindowPlacements[key] = new WindowRect
        {
            Left = b.Left,
            Top = b.Top,
            Width = b.Width,
            Height = b.Height,
            Maximized = maximized
        };
    }

    private static double Clamp(double v, double lo, double hi)
        => hi < lo ? lo : Math.Min(Math.Max(v, lo), hi);
}
