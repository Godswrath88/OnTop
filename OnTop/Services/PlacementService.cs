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
        // S28 K: rank candidates instead of taking the first — programs own several
        // top-level windows (tray helpers, 0×0 message windows, cloaked ghosts).
        IntPtr best = IntPtr.Zero; long bestScore = long.MinValue; int seen = 0;
        foreach (var (hwnd, wtitle, wproc) in Win32.GetVisibleWindows())
        {
            if (Win32.IsOwnWindow(hwnd)) continue;
            if (proc.Length > 0 && !string.Equals(wproc, proc, StringComparison.OrdinalIgnoreCase)) continue;
            if (title.Length > 0 && wtitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0) continue;
            seen++;
            long area = Win32.GetWindowRect(hwnd, out var rc) ? (long)Math.Max(0, rc.Right - rc.Left) * Math.Max(0, rc.Bottom - rc.Top) : 0;
            long score = (Win32.IsCloaked(hwnd) ? -1_000_000_000L : 0) + (wtitle.Length > 0 ? 100_000_000L : 0) + area;
            if (_vm.Cfg.VerboseLogging) _vm.Log.Debug($"Placement '{r.Name}' candidate: '{wtitle}' [{wproc}] area={area} cloaked={Win32.IsCloaked(hwnd)} score={score}", "Placement");
            if (score > bestScore) { bestScore = score; best = hwnd; }
        }
        if (_vm.Cfg.VerboseLogging) _vm.Log.Debug($"Placement '{r.Name}': {seen} candidate(s), chose {(best == IntPtr.Zero ? "none" : $"'{Win32.GetWindowTitle(best)}'")}", "Placement");
        return best;
    }

    /// <summary>Apply one rule now (UI thread). Returns true if a window moved.
    /// If nothing matches and launching is allowed, launches and retries for up
    /// to ~10 s on a background wait, then moves.</summary>
    public async Task<bool> ApplyAsync(PlacementRule r, string trigger)
    {
        if (!_vm.Cfg.PlacementEnabled) { ToastService.Show("Window Placement Is Off (Settings → Features)", ToastType.Warning); return false; }
        if (!r.Enabled) return false;
        if (r.DelaySec > 0) { _vm.Log.Info($"Placement '{r.Name}' ({trigger}): waiting {r.DelaySec}s first", "Placement"); await Task.Delay(r.DelaySec * 1000); }
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
            for (int i = 0; i < Math.Clamp(r.LaunchWaitSec, 1, 120) * 4 && hwnd == IntPtr.Zero; i++)   // S28 K: per-rule wait
            {
                await Task.Delay(250);
                hwnd = FindWindow(r);
            }
            if (hwnd == IntPtr.Zero)
            {
                _vm.Log.Warn($"Placement '{r.Name}': launched but no window appeared within {r.LaunchWaitSec} s", "Placement");
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
            // S28 K BUG FIX (from testing: "placement didn't move the window" — the log said
            // "applied" regardless): SetWindowPos's result is now CHECKED. The usual
            // failure is error 5 (access denied): the target runs elevated (GPU utilities,
            // most hardware tools) and a non-elevated process cannot move it — by
            // design, OnTop never elevates. Say so instead of claiming success.
            bool ok;
            Win32.ShowWindowAsync(hwnd, Win32.SW_RESTORE);
            if (r.Maximize)
            {
                ok = Win32.SetWindowPos(hwnd, IntPtr.Zero, b.X + 40, b.Y + 40, Math.Min(800, b.Width - 80), Math.Min(600, b.Height - 80), Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
                if (ok) Win32.ShowWindowAsync(hwnd, Win32.SW_MAXIMIZE);
            }
            else
            {
                int w = Math.Max(100, r.W), h = Math.Max(60, r.H);
                ok = Win32.SetWindowPos(hwnd, IntPtr.Zero, b.X + r.X, b.Y + r.Y, w, h, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            }
            if (!ok)
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                if (err == 5) { ElevatedTarget(r, hwnd, trigger); return false; }
                _vm.Log.Error($"Placement '{r.Name}' FAILED ({trigger}): '{Win32.GetWindowTitle(hwnd)}' — Win32 error {err}", "Placement");
                ToastService.Show($"Placement '{r.Name}' Failed — See Log", ToastType.Error, 7000);
                return false;
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

    /// <summary>Rule ids already asked about this session (one dialog per rule per run).</summary>
    private readonly HashSet<string> _elevatedAsked = new();

    /// <summary>0.45.3: the target runs as administrator (SetWindowPos error 5).
    /// OnTop stays non-elevated (SECURITY.md), so this is reported, never worked
    /// around. Asks once per rule per session: Turn Rule Off / Keep Rule, with
    /// "Don't ask again for this placement". A rule marked don't-ask just logs.</summary>
    private void ElevatedTarget(PlacementRule r, IntPtr hwnd, string trigger)
    {
        string proc = Win32.GetProcessName(hwnd) ?? "";
        string what = $"Placement '{r.Name}' ({trigger}): '{Win32.GetWindowTitle(hwnd)}' runs as administrator; OnTop runs non-elevated by design and cannot move it";
        if (r.ElevatedNoticeOff) { _vm.Log.Warn($"{what} (skipped quietly: 'don't ask again' is set for this rule)", "Placement"); return; }
        _vm.Log.Error($"{what} — access denied", "Placement");
        // Once per rule per session, and never while a game/guard is running
        // (DND): fall back to the toast, which DND handles.
        if (!_elevatedAsked.Add(r.Id) || _vm.ShouldSuppressRoutineToasts())
        {
            ToastService.Show($"Can't Move '{r.Name}' — It Runs As Administrator", ToastType.Error, 7000);
            return;
        }
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            Views.ElevatedPlacementDialog.Show(r, proc, (turnOff, dontAsk) =>
            {
                if (turnOff)
                {
                    r.Enabled = false;
                    _vm.Log.Action($"Placement '{r.Name}' turned off (its window runs as administrator)", "Placement");
                    ToastService.Show($"Placement '{r.Name}' Turned Off", ToastType.Info);
                }
                else if (dontAsk)
                {
                    r.ElevatedNoticeOff = true;
                    _vm.Log.Action($"Placement '{r.Name}' kept; won't ask again about administrator windows", "Placement");
                }
                if (turnOff || dontAsk) _vm.SaveAll();
            }));
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
