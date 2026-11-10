using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// Window Rules (auto-pin): every few seconds, scan visible top-level windows and
/// pin any that match an enabled rule (process/title contains pattern). Pins are
/// tracked in MainViewModel.PinnedByUs, so the Pinned Windows viewer badges them
/// and Unpin All (OnTop Pins) releases them. A window unpinned by the user is NOT
/// re-pinned until it goes away and comes back (no fighting the user).
///
/// SECURITY (see SECURITY.md): the feature is OFF by default; a scan only reads
/// window title/process and applies a Z-order change on matches — never input,
/// never memory access, never process launch. Own windows are excluded at the
/// enumeration level, every HWND is re-validated before use, and pattern matching
/// is plain case-insensitive substring (no regex/eval on untrusted data).
/// </summary>
public sealed class WindowRuleService : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    /// <summary>S28 Low Impact Mode: scan every 10 s instead of 3 s.</summary>
    public void ApplyLowImpact(bool low) => _timer.Interval = TimeSpan.FromSeconds(low ? 10 : 3);

    /// <summary>Windows already handled this lifetime (pinned by a rule, or
    /// deliberately unpinned by the user) — never auto-repinned while alive.</summary>
    private readonly HashSet<IntPtr> _handled = new();

    public WindowRuleService(MainViewModel vm)
    {
        _vm = vm;
        _timer.Tick += (_, _) => Scan();
        _vm.Cfg.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.WindowRulesEnabled)) Sync();
        };
        Sync();
    }

    private void Sync()
    {
        if (_vm.Cfg.WindowRulesEnabled) { _timer.Start(); Scan(); }
        else _timer.Stop();
    }

    private void Scan()
    {
        if (!_vm.Cfg.WindowRulesEnabled) return;
        var rules = _vm.WindowRules.Where(r => r.Enabled && r.Pattern.Trim().Length > 0).ToList();
        if (rules.Count == 0) return;

        // Forget dead windows so a relaunched app can match again.
        _handled.RemoveWhere(h => !Win32.IsWindow(h));

        foreach (var (hwnd, title, process) in Win32.GetVisibleWindows())
        {
            if (_handled.Contains(hwnd)) continue;
            if (Win32.IsTopmost(hwnd)) continue;              // already pinned (by anyone)

            foreach (var rule in rules)
            {
                string hay = rule.MatchTitle ? title : process;
                if (!hay.Contains(rule.Pattern.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                // SECURITY: re-validate the handle right before acting; the window
                // may have closed between enumeration and now.
                if (!Win32.IsWindow(hwnd)) break;
                Win32.SetTopmost(hwnd, true);
                _handled.Add(hwnd);
                _vm.PinnedByUs.Add(hwnd);
                _vm.Stats.BumpRulePin(process);
                _vm.Log.Action($"Rule pinned — {process} (\"{rule.Pattern}\")", "Rules");
                ToastService.Show($"Rule Pinned — {process}", ToastType.Success);
                break;
            }
        }

        // If the user unpins a rule-pinned window (viewer/hotkey), leave it be:
        // mark any handled-but-no-longer-topmost window as still handled (no-op —
        // the set already contains it), so we don't re-pin against their intent.
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
