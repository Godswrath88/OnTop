using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;
using OnTop.Views;

namespace OnTop.Services;

/// <summary>
/// Coordinates the zone overlay system: shows/hides the full-screen overlay and
/// the legend based on which zones have ShowDot, applies the click-through lock
/// from settings, follows display changes, and routes overlay interactions
/// (move/resize) to the ViewModel for undo and logging.
/// </summary>
public sealed class OverlayService : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly List<Zone> _subscribed = new();
    private readonly DispatcherTimer _legendDebounce;
    // L1 (S24): one overlay window PER MONITOR so PerMonitorV2 gives each its
    // own DPI — exact geometry under mixed display scaling. Rebuilt when the
    // screen set changes (signature below).
    private readonly List<ZoneOverlayWindow> _overlays2 = new();
    private string _screenSig = "";
    private OverlayLegendWindow? _legend;
    private bool _overlaysActive;

    public OverlayService(MainViewModel vm)
    {
        _vm = vm;
        _vm.Zones.CollectionChanged += OnZonesCollectionChanged;
        _vm.Cfg.PropertyChanged += OnCfgChanged;
        _vm.ZonePreviewStarted += OnZonePreviewStarted;
        _vm.ZonePreviewEnded += OnZonePreviewEnded;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        ResubscribeZones();

        // Slider drags fire a property change per zone per tick — coalesce the
        // legend rebuilds (the overlay visuals update themselves cheaply).
        _legendDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _legendDebounce.Tick += (_, _) =>
        {
            _legendDebounce.Stop();
            if (_overlaysActive) _legend?.Refresh(DottedZones());
        };
    }

    private List<Zone> DottedZones() => _vm.Zones.Where(z => z.ShowDot).ToList();

    // ── Sync ────────────────────────────────────────────────────────────
    /// <summary>Show, hide, or update the overlay windows to match current state.</summary>
    public void Refresh()
    {
        var dotted = DottedZones();
        if (dotted.Count == 0)
        {
            foreach (var w in _overlays2) w.Hide();
            _legend?.Hide();
            _overlaysActive = false;
            return;
        }

        bool activating = !_overlaysActive;
        EnsureWindows();

        Distribute(dotted, _vm.Cfg.OverlaysLocked, _vm.Cfg.OverlayOpacity);

        _legend!.Refresh(dotted);
        if (activating && !_legend.IsVisible) _legend.Show();

        _overlaysActive = true;
    }

    // ── Zone drag preview (owner request: live overlay while map-dragging) ──
    private DispatcherTimer? _previewLinger;
    private Zone? _previewZone;

    private void OnZonePreviewStarted(Zone z)
    {
        BringOverlaysToFront();   // S28: preview/flash must show above OnTop's own GUIs
        _previewLinger?.Stop();
        _previewLinger = null;
        _previewZone = z;
        EnsureWindows();
        var zones = DottedZones();
        if (!zones.Contains(z)) zones.Add(z);   // dot-hidden zones still preview
        Distribute(zones, clickThrough: true, opacity: 1.0);   // full visibility while dragging
        _overlaysActive = true;
        // Live movement needs no extra work: the overlay subscribes each zone's
        // PropertyChanged, so map-drag X/Y updates reposition it in real time.
    }

    private void OnZonePreviewEnded()
    {
        if (_previewZone is null) return;
        _previewZone = null;
        _previewLinger?.Stop();
        _previewLinger = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _previewLinger.Tick += (_, _) =>
        {
            _previewLinger?.Stop();
            _previewLinger = null;
            Refresh();                          // back to the normal view
        };
        _previewLinger.Start();
    }

    // ── Peek ────────────────────────────────────────────────────────────
    private DispatcherTimer? _peekTimer;

    /// <summary>Show EVERY zone on the desktop overlay at full opacity for a
    /// moment ("where are my zones?"), then restore the normal dotted view.
    /// Press-to-peek rather than hold-to-peek by design: detecting key-up would
    /// require a keyboard hook, and SECURITY.md limits our input hook to mouse
    /// observation only — a keyboard hook for a cosmetic feature is exactly the
    /// attack-surface growth we refuse.</summary>
    /// <summary>S28 (owner: overlays fell UNDER other OnTop GUIs on map ping).
    /// All topmost windows share one Z band, ordered by last activation — our
    /// GUI windows (GuiAlwaysOnTop) were newer than the overlays. Re-inserting
    /// each overlay at HWND_TOPMOST without activating moves it to the top of
    /// the band. Called on Peek and on flash/preview.</summary>
    public void BringOverlaysToFront()
    {
        foreach (var w in _overlays2)
        {
            try
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                if (h != IntPtr.Zero && w.IsVisible) Win32.SetTopmost(h, true);
            }
            catch (Exception ex) { _vm.Log.Debug($"Overlay front re-assert: {ex.Message}", "Overlay"); }
        }
    }

    /// <summary>S28 Stage H: hold-to-peek — show full-opacity while held, restore on release.</summary>
    public void PeekHold(bool held)
    {
        if (held)
        {
            var all = _vm.Zones.ToList();
            if (all.Count == 0) return;
            EnsureWindows();
            Distribute(all, clickThrough: true, opacity: 1.0);
            _overlaysActive = true;
            BringOverlaysToFront();
        }
        else Refresh();
    }

    public void Peek()
    {
        var all = _vm.Zones.ToList();
        if (all.Count == 0) return;
        EnsureWindows();

        Distribute(all, clickThrough: true, opacity: 1.0);   // a peek is never interactive
        _overlaysActive = true;
        BringOverlaysToFront();   // S28: above OnTop's own topmost GUIs too

        _peekTimer?.Stop();
        _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer?.Stop();
            _peekTimer = null;
            Refresh();                              // back to the normal view
        };
        _peekTimer.Start();
    }

    /// <summary>Reopen the legend (tray menu) — also activates overlays' windows if needed.</summary>
    public void ShowLegend()
    {
        Refresh();
        if (_overlaysActive && _legend != null)
        {
            _legend.Refresh(DottedZones());
            _legend.Show();
            _legend.Activate();
        }
        else
        {
            ToastService.Show("No Overlays Active — Turn On A Zone's Dot Column First", ToastType.Info, 4000);
        }
    }

    private void EnsureWindows()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        string sig = string.Join("|", screens.Select(sc => $"{sc.DeviceName}:{sc.Bounds}"));
        if (sig != _screenSig || _overlays2.Count == 0)
        {
            foreach (var old in _overlays2)
            {
                try { old.Close(); }
                catch (Exception ex) { _vm.Log.Debug($"Overlay rebuild close failed: {ex.Message}", "Overlay"); }
            }
            _overlays2.Clear();
            foreach (var sc in screens)
            {
                var w = new ZoneOverlayWindow(sc.DeviceName);
                w.InteractionStarted += _ => _vm.BeginZoneDrag();
                w.MoveCompleted += z => _vm.EndZoneDrag(z);
                w.ResizeCompleted += z => _vm.EndZoneResize(z);
                _overlays2.Add(w);
            }
            _screenSig = sig;
            _vm.Log.Info($"Overlay windows built: one per monitor ({_overlays2.Count})", "Overlay");
        }
        _legend ??= new OverlayLegendWindow(_vm.Cfg);
    }

    /// <summary>Split zones across the per-monitor windows (anchor containment;
    /// off-every-screen zones draw nowhere — matching their ⚠ health), apply
    /// shared opacity/click-through, and show only windows that have content.</summary>
    private void Distribute(List<Zone> zones, bool clickThrough, double opacity)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < _overlays2.Count; i++)
        {
            var w = _overlays2[i];
            var scr = i < screens.Length ? screens[i] : null;
            var mine = scr is null
                ? new List<Zone>()
                : zones.Where(z => scr.Bounds.Contains(z.X, z.Y)).ToList();
            w.SetZones(mine);
            w.ApplyClickThrough(clickThrough);
            w.SetOverlayOpacity(opacity);
            if (mine.Count > 0) { if (!w.IsVisible) w.Show(); }
            else w.Hide();
        }
    }

    // ── Change tracking ─────────────────────────────────────────────────
    private void OnZonesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ResubscribeZones();
        Refresh();
    }

    private void ResubscribeZones()
    {
        foreach (var zone in _subscribed) zone.PropertyChanged -= OnZoneChanged;
        _subscribed.Clear();
        foreach (var zone in _vm.Zones)
        {
            zone.PropertyChanged += OnZoneChanged;
            _subscribed.Add(zone);
        }
    }

    private void OnZoneChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Zone.ShowDot))
        {
            Refresh();   // membership changed
            return;
        }
        // Geometry/name/enabled changes: visuals self-update; legend catches up debounced.
        _legendDebounce.Stop();
        _legendDebounce.Start();
    }

    private void OnCfgChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.OverlaysLocked))
            foreach (var w in _overlays2) w.ApplyClickThrough(_vm.Cfg.OverlaysLocked);
        else if (e.PropertyName == nameof(AppSettings.OverlayOpacity))
            foreach (var w in _overlays2) w.SetOverlayOpacity(_vm.Cfg.OverlayOpacity);
        else if (e.PropertyName is nameof(AppSettings.StealthMode) or nameof(AppSettings.HideOverlaysInStealth))
        {
            // Stealth follow-up (S26): overlays are visible surfaces — hiding
            // them completes the disappearance. Opt-out via the toggle.
            if (_vm.Cfg.StealthMode && _vm.Cfg.HideOverlaysInStealth)
            {
                foreach (var w in _overlays2) w.Hide();
                _vm.Log.Info("Overlays hidden (stealth)", "Overlay");
            }
            else
            {
                Refresh();
            }
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        // SystemEvents raises on a system thread — marshal to the UI thread.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            EnsureWindows();   // topology change → rebuild the per-monitor set
            foreach (var w in _overlays2) w.PositionToVirtualScreen();
            Refresh();
            _vm.Log.Info("Display settings changed — overlay windows rebuilt/repositioned", "Overlay");
        });
    }

    public void Dispose()
    {
        // Static event — must unhook or the service (and windows) leak.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        _vm.Zones.CollectionChanged -= OnZonesCollectionChanged;
        _vm.Cfg.PropertyChanged -= OnCfgChanged;
        _vm.ZonePreviewStarted -= OnZonePreviewStarted;
        _vm.ZonePreviewEnded -= OnZonePreviewEnded;
        foreach (var zone in _subscribed) zone.PropertyChanged -= OnZoneChanged;
        _subscribed.Clear();
        _legendDebounce.Stop();
        foreach (var w in _overlays2)
            try { w.Close(); } catch (Exception ex) { _vm.Log.Debug($"Overlay close on dispose failed: {ex.Message}", "Overlay"); }
        try { _legend?.Hide(); } catch (Exception ex) { _vm.Log.Debug($"Legend hide on dispose failed: {ex.Message}", "Overlay"); }
    }
}
