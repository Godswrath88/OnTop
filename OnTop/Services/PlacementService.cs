using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;
using Forms = System.Windows.Forms;

namespace OnTop.Services;

/// <summary>
/// S28 Stage E — applies PlacementRules. Foreign windows get POSITION/SIZE/
/// Z-order changes only (SetWindowPos / ShowWindowAsync) — never input, never
/// memory. Launching is delegated to the single launcher choke-point in the VM
/// and is doubly gated (global sub-gate + per-rule). Inert when
/// Cfg.PlacementEnabled is off.
/// </summary>
public class PlacementService
{
    private readonly MainViewModel _vm;
    public PlacementService(MainViewModel vm) => _vm = vm;

    /// <summary>Find the first visible window matching the rule.</summary>
    public IntPtr FindWindow(PlacementRule r)
    {
        string proc = r.Process.Trim();
        if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) proc = proc[..^4];
        string title = r.TitleContains.Trim();
        if (proc.Length == 0 && title.Length == 0) return IntPtr.Zero;
        foreach (var (hwnd, wtitle, wproc) in Win32.GetVisibleWindows())
        {
            if (Win32.IsOwnWindow(hwnd)) continue;
            if (proc.Length > 0 && !string.Equals(wproc, proc, StringComparison.OrdinalIgnoreCase)) continue;
            if (title.Length > 0 && wtitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) continue;
            return hwnd;
        }
        return IntPtr.Zero;
    }

    /// <summary>Apply one rule now (UI thread). Returns true if a window moved.
    /// If nothing matches and launching is allowed, launches and retries for up
    /// to ~10 s on a background wait, then moves.</summary>
    public async Task<bool> ApplyAsync(PlacementRule r, string trigger)
    {
        if (!_vm.Cfg.PlacementEnabled) { ToastService.Show("Window Placement Is Off (Settings → Features)", ToastType.Warning); return false; }
        if (!r.Enabled) return false;
        var hwnd = FindWindow(r);
        if (hwnd == IntPtr.Zero)
        {
            bool mayLaunch = r.LaunchEnabled && r.LaunchPath.Trim().Length > 0;   // S28 H: per-rule gate only
            if (!mayLaunch)
            {
                _vm.Log.Info($"Placement '{r.Name}' ({trigger}): no matching window{(r.LaunchPath.Length > 0 ? " — launch is gated off" : "")}", "Placement");
                ToastService.Show($"Placement '{r.Name}': No Matching Window", ToastType.Info);
                return false;
            }
            // LAUNCH — via the ONE choke-point. Logged + toasted there.
            if (!_vm.LaunchViaShell(r.LaunchPath.Trim(), $"placement '{r.Name}' ({trigger})")) return false;
            for (int i = 0; i < 40 && hwnd == IntPtr.Zero; i++)   // ≤ 10 s
            {
                await Task.Delay(250);
                hwnd = FindWindow(r);
            }
            if (hwnd == IntPtr.Zero)
            {
                _vm.Log.Warn($"Placement '{r.Name}': launched but no window appeared within 10 s", "Placement");
                ToastService.Show($"Placement '{r.Name}': Launched, Window Not Found Yet", ToastType.Warning);
                return false;
            }
        }
        return Move(r, hwnd, trigger);
    }

    private bool Move(PlacementRule r, IntPtr hwnd, string trigger)
    {
        if (!Win32.IsWindow(hwnd)) return false;
        var screens = Forms.Screen.AllScreens;
        int mi = Math.Clamp(r.Monitor, 0, screens.Length - 1);
        var b = screens[mi].Bounds;
        try
        {
            if (r.Maximize)
            {
                // Move onto the target monitor first, then maximize there.
                Win32.ShowWindowAsync(hwnd, Win32.SW_RESTORE);
                Win32.SetWindowPos(hwnd, IntPtr.Zero, b.X + 40, b.Y + 40, Math.Min(800, b.Width - 80), Math.Min(600, b.Height - 80), Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                Win32.ShowWindowAsync(hwnd, Win32.SW_MAXIMIZE);
            }
            else
            {
                Win32.ShowWindowAsync(hwnd, Win32.SW_RESTORE);
                int w = Math.Max(100, r.W), h = Math.Max(60, r.H);
                Win32.SetWindowPos(hwnd, IntPtr.Zero, b.X + r.X, b.Y + r.Y, w, h, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            }
            _vm.Log.Action($"Placement '{r.Name}' applied ({trigger}): {Win32.GetWindowTitle(hwnd)} → monitor {mi + 1} {(r.Maximize ? "maximized" : $"{r.X},{r.Y} {r.W}×{r.H}")}", "Placement");
            ToastService.Show($"Placed: {r.Name}", ToastType.Success);
            return true;
        }
        catch (Exception ex)
        {
            _vm.Log.Error($"Placement '{r.Name}' failed: {ex.Message}", "Placement");
            ToastService.Show($"Placement '{r.Name}' Failed — See Log", ToastType.Error);
            return false;
        }
    }

    /// <summary>Apply every enabled rule (hotkey / button / startup).</summary>
    public async Task ApplyAllAsync(string trigger, bool startupOnly = false)
    {
        if (!_vm.Cfg.PlacementEnabled) return;
        int n = 0;
        foreach (var r in _vm.Placements.ToList())
        {
            if (!r.Enabled || (startupOnly && !r.ApplyOnStartup)) continue;
            if (await ApplyAsync(r, trigger)) n++;
        }
        if (!startupOnly) _vm.Log.Info($"Placement: {n} window(s) placed ({trigger})", "Placement");
    }

    /// <summary>Fill a rule from the foreground window's current geometry —
    /// the easy way to author one ("put it where it is now").</summary>
    public bool CaptureForeground(PlacementRule r)
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || Win32.IsOwnWindow(hwnd) || !Win32.GetWindowRect(hwnd, out var rc)) return false;
        var screens = Forms.Screen.AllScreens;
        int mi = 0;
        for (int i = 0; i < screens.Length; i++)
            if (screens[i].Bounds.Contains(rc.Left + 10, rc.Top + 10)) { mi = i; break; }
        var b = screens[mi].Bounds;
        r.Monitor = mi; r.X = rc.Left - b.X; r.Y = rc.Top - b.Y; r.W = rc.Right - rc.Left; r.H = rc.Bottom - rc.Top;
        r.Process = Win32.GetProcessName(hwnd) ?? "";
        string t = Win32.GetWindowTitle(hwnd);
        if (r.Name == "Placement" || r.Name.Length == 0) r.Name = t.Length > 40 ? t[..40] : t;
        return true;
    }
}
