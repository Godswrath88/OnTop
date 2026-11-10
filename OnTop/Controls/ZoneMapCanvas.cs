using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OnTop.Helpers;
using OnTop.Models;
using Forms = System.Windows.Forms;

namespace OnTop.Controls;

/// <summary>
/// Screen-scale minimap of all monitors and zones. Click a zone to select it,
/// drag its dot or detection box to reposition it, double-click empty space to
/// create a zone at that screen location.
///
/// Zone coordinates are physical pixels (matching GetCursorPos and the mouse
/// hook), so the map uses System.Windows.Forms.Screen bounds — also physical —
/// for the monitor layout. The canvas only scales coordinates for display;
/// nothing here introduces a DPI-dependent coordinate space.
///
/// This is a view-layer control: it mutates Zone X/Y directly (the zones are
/// observable models) and raises events so the window can forward undo and
/// create requests to the ViewModel.
/// </summary>
public class ZoneMapCanvas : FrameworkElement
{
    // ── Dependency Properties ───────────────────────────────────────────
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ZoneMapCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedZoneProperty =
        DependencyProperty.Register(nameof(SelectedZone), typeof(Zone), typeof(ZoneMapCanvas),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Zone? SelectedZone
    {
        get => (Zone?)GetValue(SelectedZoneProperty);
        set => SetValue(SelectedZoneProperty, value);
    }

    // ── Events (window forwards these to the ViewModel) ─────────────────
    /// <summary>Raised once at the first movement of a drag — push undo here.</summary>
    public event Action<Zone>? ZoneDragStarted;
    /// <summary>Raised at first drag movement so the overlay can show live.</summary>
    public event Action<Zone>? ZonePreviewRequested;
    /// <summary>Raised when a drag that moved the zone ends.</summary>
    public event Action<Zone>? ZoneDragCompleted;
    /// <summary>Raised on double-click of empty space, with physical screen coords.</summary>
    public event Action<int, int>? CreateZoneRequested;
    /// <summary>Right-click on a zone — the window opens the context menu.</summary>
    public event Action<Zone>? ZoneContextRequested;
    /// <summary>Right-click on a monitor (not a zone) — arg is the monitor index.</summary>
    public event Action<int>? MonitorContextRequested;
    /// <summary>Right-click on blank map space (no zone, no monitor).</summary>
    public event Action? MapBlankContextRequested;

    /// <summary>Monitor indices the user chose to hide from the map. Purely a
    /// view concern — detection is unaffected. The window owns the set so it
    /// can persist and drive an unhide UI.</summary>
    public IReadOnlyCollection<int>? HiddenMonitors { get; set; }

    // ── Drawing resources (frozen, shared) ──────────────────────────────
    private static readonly SolidColorBrush BgBrush = Frozen(Color.FromRgb(0x16, 0x16, 0x22));
    private static readonly SolidColorBrush MonitorFill = Frozen(Color.FromRgb(0x20, 0x20, 0x32));
    private static readonly Pen MonitorPen = FrozenPen(Color.FromRgb(0x3A, 0x3A, 0x4A), 1.0);
    private static readonly Pen SelectedRing = FrozenPen(Color.FromRgb(0xF0, 0xF0, 0xF0), 1.5);
    private static readonly SolidColorBrush DimText = Frozen(Color.FromRgb(0x80, 0x80, 0x90));
    private static readonly SolidColorBrush LightText = Frozen(Color.FromRgb(0xE6, 0xE6, 0xE6));
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Color c, double width)
    {
        var p = new Pen(Frozen(c), width);
        p.Freeze();
        return p;
    }

    // ── State ───────────────────────────────────────────────────────────
    private readonly List<Zone> _subscribed = new();
    private Zone? _hoverZone;
    private Zone? _dragZone;
    private bool _dragMoved;
    private Point _dragGrabOffset;                       // physical px: zone center − grab point
    private Rect _virtualBounds = new(0, 0, 1920, 1080); // physical px, union of monitors
    private double _scale = 0.1;
    private Point _origin;                               // canvas point of virtual top-left
    private double _zoom = 1.0;                          // user zoom (1 = fit)
    private Vector _pan;                                 // user pan offset (canvas px)
    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;

    public ZoneMapCanvas()
    {
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Focusable = false;
    }

    // ── ItemsSource subscription management ─────────────────────────────
    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (ZoneMapCanvas)d;
        if (e.OldValue is INotifyCollectionChanged oldNcc) oldNcc.CollectionChanged -= canvas.OnZonesChanged;
        if (e.NewValue is INotifyCollectionChanged newNcc) newNcc.CollectionChanged += canvas.OnZonesChanged;
        canvas.Resubscribe();
    }

    private void OnZonesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Resubscribe();
        InvalidateVisual();
    }

    private void Resubscribe()
    {
        foreach (var zone in _subscribed) zone.PropertyChanged -= OnZoneProperty;
        _subscribed.Clear();
        foreach (var zone in CurrentZones())
        {
            zone.PropertyChanged += OnZoneProperty;
            _subscribed.Add(zone);
        }
    }

    private void OnZoneProperty(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private List<Zone> CurrentZones() => ItemsSource?.OfType<Zone>().ToList() ?? new List<Zone>();

    // ── Coordinate mapping (physical px ⇄ canvas units) ─────────────────
    private void ComputeTransform()
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0) return;

        var union = screens[0].Bounds;
        for (int i = 1; i < screens.Length; i++)
            union = System.Drawing.Rectangle.Union(union, screens[i].Bounds);
        _virtualBounds = new Rect(union.X, union.Y, Math.Max(1, union.Width), Math.Max(1, union.Height));

        const double pad = 10;
        double availW = Math.Max(1, ActualWidth - pad * 2);
        double availH = Math.Max(1, ActualHeight - pad * 2);
        double fit = Math.Min(availW / _virtualBounds.Width, availH / _virtualBounds.Height);
        _scale = fit * _zoom;

        double mapW = _virtualBounds.Width * _scale;
        double mapH = _virtualBounds.Height * _scale;
        _origin = new Point((ActualWidth - mapW) / 2 + _pan.X, (ActualHeight - mapH) / 2 + _pan.Y);
    }

    private Point ToCanvas(double physX, double physY) => new(
        _origin.X + (physX - _virtualBounds.X) * _scale,
        _origin.Y + (physY - _virtualBounds.Y) * _scale);

    private Point ToPhysical(Point canvasPt) => new(
        (canvasPt.X - _origin.X) / _scale + _virtualBounds.X,
        (canvasPt.Y - _origin.Y) / _scale + _virtualBounds.Y);

    private Point ClampToVirtual(Point physical) => new(
        Math.Min(Math.Max(physical.X, _virtualBounds.Left), _virtualBounds.Right - 1),
        Math.Min(Math.Max(physical.Y, _virtualBounds.Top), _virtualBounds.Bottom - 1));

    // ── Rendering ───────────────────────────────────────────────────────
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 4 || ActualHeight < 4) return;
        ComputeTransform();

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Background
        dc.DrawRoundedRectangle(BgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight), 6, 6);

        // Monitors
        var screens = Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            if (HiddenMonitors?.Contains(i) == true) continue;
            var b = screens[i].Bounds;
            var topLeft = ToCanvas(b.X, b.Y);
            var rect = new Rect(topLeft, new Size(b.Width * _scale, b.Height * _scale));
            dc.DrawRectangle(MonitorFill, MonitorPen, rect);

            var label = new FormattedText(
                $"{i + 1}  ({b.Width}×{b.Height})", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 10, DimText, dpi);
            dc.DrawText(label, new Point(rect.X + 6, rect.Y + 4));
        }

        // Zones
        var zones = CurrentZones();
        if (zones.Count == 0)
        {
            var hint = new FormattedText(
                "Double-Click To Add A Zone — Or Use Record   ·   Hold Middle Mouse To Scroll, Wheel To Zoom", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 12, DimText, dpi);
            dc.DrawText(hint, new Point((ActualWidth - hint.Width) / 2, (ActualHeight - hint.Height) / 2));
            return;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            bool selected = ReferenceEquals(zone, SelectedZone);
            bool hovered = ReferenceEquals(zone, _hoverZone);
            var color = ZonePalette.GetColor(zone.ColorIndex);
            double alpha = zone.Enabled ? 1.0 : 0.35;

            var center = ToCanvas(zone.X, zone.Y);
            // Visual minimum (owner report): at fit-3-monitors zoom the true
            // to-scale box is 2–3 px and hides UNDER the fixed center dot,
            // reading as "way smaller than the overlay". Below ~9 px half-extent
            // the box is drawn at a floor that always encloses the dot — it's a
            // marker at that point, not a measurement; zoom in for true scale.
            double halfW = Math.Max(9, zone.HalfX * _scale);
            double halfH = Math.Max(9, zone.HalfY * _scale);
            var box = new Rect(center.X - halfW, center.Y - halfH, halfW * 2, halfH * 2);

            // Detection box (the real HalfX × HalfY area, to scale)
            var fill = new SolidColorBrush(Color.FromArgb(
                (byte)((selected ? 0x58 : 0x2C) * alpha), color.R, color.G, color.B));
            fill.Freeze();
            var edgeBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(0xE0 * alpha), color.R, color.G, color.B));
            edgeBrush.Freeze();
            var edge = new Pen(edgeBrush, selected ? 2.0 : 1.2);
            edge.Freeze();

            if (zone.Kind == ZoneKind.Draw && zone.RelPoints.Count >= 3)
            {
                // Freeform zones draw their real outline, to scale.
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var first = ToCanvas(zone.X + zone.RelPoints[0].X, zone.Y + zone.RelPoints[0].Y);
                    ctx.BeginFigure(first, isFilled: true, isClosed: true);
                    for (int p = 1; p < zone.RelPoints.Count; p++)
                        ctx.LineTo(ToCanvas(zone.X + zone.RelPoints[p].X, zone.Y + zone.RelPoints[p].Y),
                            isStroked: true, isSmoothJoin: true);
                }
                geometry.Freeze();
                dc.DrawGeometry(fill, edge, geometry);
            }
            else
            {
                dc.DrawRectangle(fill, edge, box);
            }

            // Center dot
            double radius = selected ? 5.5 : 4.0;
            var dotBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(0xFF * alpha), color.R, color.G, color.B));
            dotBrush.Freeze();
            dc.DrawEllipse(dotBrush, selected ? SelectedRing : null, center, radius, radius);

            // Label for the selected / hovered zone (live coords while dragging)
            if (selected || hovered)
            {
                string caption = ReferenceEquals(zone, _dragZone)
                    ? $"{zone.Name}  ({zone.X}, {zone.Y})"
                    : zone.Name;
                var ft = new FormattedText(caption, CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, LabelTypeface, 11, LightText, dpi);
                double tx = Math.Min(Math.Max(2, center.X + 9), ActualWidth - ft.Width - 2);
                double ty = Math.Min(Math.Max(2, center.Y - ft.Height - 7), ActualHeight - ft.Height - 2);
                dc.DrawText(ft, new Point(tx, ty));
            }
        }

        // Locate flash: an expanding, fading ring pulsed on a zone when its list
        // row is double-clicked (FlashZone), or every zone at once (FlashAllZones).
        if (_flashPhase < 1 && (_flashAll || _flashZone is not null))
        {
            double fr = 8 + _flashPhase * 30;
            double fa = Math.Max(0, 1 - _flashPhase);
            var fpen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(230 * fa), 0x5A, 0xA6, 0xFF)), 2.5);
            fpen.Freeze();
            if (_flashAll)
                foreach (var z in CurrentZones())
                    dc.DrawEllipse(null, fpen, ToCanvas(z.X, z.Y), fr, fr);
            else if (_flashZone is not null)
                dc.DrawEllipse(null, fpen, ToCanvas(_flashZone.X, _flashZone.Y), fr, fr);
        }
    }

    // ── Locate flash + view reset ───────────────────────────────────────
    private Zone? _flashZone;
    private bool _flashAll;
    private double _flashPhase;
    private System.Windows.Threading.DispatcherTimer? _flashTimer;

    /// <summary>Pulse an expanding, fading ring on a zone for ~0.7s to draw the
    /// eye to it (Locate). Safe to call repeatedly; restarts the pulse.</summary>
    public void FlashZone(Zone zone)
    {
        _flashZone = zone;
        _flashAll = false;
        StartFlash();
    }

    /// <summary>Pulse every zone at once — a "where are my zones" peek.</summary>
    public void FlashAllZones()
    {
        _flashZone = null;
        _flashAll = true;
        StartFlash();
    }

    private void StartFlash()
    {
        _flashPhase = 0;
        _flashTimer?.Stop();
        var start = DateTime.Now;
        _flashTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(16) };
        _flashTimer.Tick += (_, _) =>
        {
            _flashPhase = (DateTime.Now - start).TotalMilliseconds / 700.0;
            if (_flashPhase >= 1)
            {
                _flashTimer?.Stop();
                _flashTimer = null;
                _flashZone = null;
                _flashAll = false;
            }
            InvalidateVisual();
        };
        _flashTimer.Start();
        InvalidateVisual();
    }

    // ── Hit testing & interaction ───────────────────────────────────────
    private Zone? HitTest(Point canvasPt)
    {
        var zones = CurrentZones();
        for (int i = zones.Count - 1; i >= 0; i--) // topmost (last drawn) first
        {
            var zone = zones[i];
            var center = ToCanvas(zone.X, zone.Y);
            bool onDot = (canvasPt - center).Length <= 8;

            if (zone.Kind == ZoneKind.Draw && zone.RelPoints.Count >= 3)
            {
                // Freeform zones hit-test against the real outline (plus the
                // dot, which stays grabbable even for thin shapes).
                var physical = ToPhysical(canvasPt);
                if (onDot || zone.Contains(
                        (int)Math.Round(physical.X), (int)Math.Round(physical.Y)))
                    return zone;
                continue;
            }

            double halfW = Math.Max(4, zone.HalfX * _scale);
            double halfH = Math.Max(4, zone.HalfY * _scale);
            bool inBox = Math.Abs(canvasPt.X - center.X) <= halfW &&
                         Math.Abs(canvasPt.Y - center.Y) <= halfH;
            if (inBox || onDot) return zone;
        }
        return null;
    }

    /// <summary>Which monitor index (if any) is under this canvas point.</summary>
    private int MonitorAt(Point canvasPt)
    {
        var screens = Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            if (HiddenMonitors?.Contains(i) == true) continue;
            var b = screens[i].Bounds;
            var tl = ToCanvas(b.X, b.Y);
            var rect = new Rect(tl, new Size(b.Width * _scale, b.Height * _scale));
            if (rect.Contains(canvasPt)) return i;
        }
        return -1;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var before = ToPhysical(e.GetPosition(this));       // keep cursor point fixed
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        _zoom = Math.Clamp(_zoom * factor, 1.0, 12.0);
        ComputeTransform();
        var after = ToPhysical(e.GetPosition(this));
        _pan += (Vector)(ToCanvas(after.X, after.Y) - ToCanvas(before.X, before.Y));
        if (Math.Abs(_zoom - 1.0) < 0.001) _pan = new Vector();   // snap back to centered fit
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Reset zoom and pan to the fitted view.</summary>
    public void ResetView()
    {
        _zoom = 1.0;
        _pan = new Vector();
        InvalidateVisual();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        // Middle-button drag = pan.
        if (e.ChangedButton == MouseButton.Middle)
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartOffset = _pan;
            Cursor = Cursors.ScrollAll;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var pt = e.GetPosition(this);
        var hit = HitTest(pt);

        if (hit == null)
        {
            if (e.ClickCount == 2)
            {
                var physical = ClampToVirtual(ToPhysical(pt));
                CreateZoneRequested?.Invoke(
                    (int)Math.Round(physical.X), (int)Math.Round(physical.Y));
                // Paint the new zone right away. Record-created zones repaint when
                // the window re-shows; a map double-click keeps the window visible,
                // so force the redraw here rather than wait for another trigger.
                InvalidateVisual();
            }
            return;
        }

        SelectedZone = hit;          // flows to the VM via the two-way binding
        _dragZone = hit;
        _dragMoved = false;
        var grabPhys = ToPhysical(pt);
        _dragGrabOffset = new Point(hit.X - grabPhys.X, hit.Y - grabPhys.Y);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pt = e.GetPosition(this);

        if (_panning)
        {
            _pan = _panStartOffset + (pt - _panStart);
            InvalidateVisual();
            return;
        }

        if (_dragZone != null && IsMouseCaptured)
        {
            if (!_dragMoved)
            {
                _dragMoved = true;
                ZoneDragStarted?.Invoke(_dragZone); // undo snapshot before first change
                ZonePreviewRequested?.Invoke(_dragZone); // live overlay while dragging
            }
            var physical = ToPhysical(pt);
            var target = ClampToVirtual(new Point(
                physical.X + _dragGrabOffset.X, physical.Y + _dragGrabOffset.Y));

            // Edge/corner snapping (roadmap UX): near a monitor edge, the zone's
            // anchor snaps to it — corners come free when both axes snap. Hold
            // SHIFT to bypass. Threshold is ~12 map pixels, converted to physical
            // so the feel is zoom-independent.
            if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)
                && _scale > 0)
            {
                double thresh = 12.0 / _scale;
                double sx = target.X, sy = target.Y;
                double bestDx = thresh, bestDy = thresh;
                foreach (var scr in System.Windows.Forms.Screen.AllScreens)
                {
                    var b = scr.Bounds;
                    foreach (double edge in new[] { (double)b.Left, (double)b.Right })
                    {
                        double d = Math.Abs(target.X - edge);
                        if (d < bestDx) { bestDx = d; sx = edge; }
                    }
                    foreach (double edge in new[] { (double)b.Top, (double)b.Bottom })
                    {
                        double d = Math.Abs(target.Y - edge);
                        if (d < bestDy) { bestDy = d; sy = edge; }
                    }
                }
                target = new Point(sx, sy);
            }

            _dragZone.X = (int)Math.Round(target.X);
            _dragZone.Y = (int)Math.Round(target.Y);
            return; // zone INPC triggers the redraw
        }

        var hover = HitTest(pt);
        if (!ReferenceEquals(hover, _hoverZone))
        {
            _hoverZone = hover;
            Cursor = hover != null ? Cursors.Hand : Cursors.Arrow;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragZone == null) return;

        if (IsMouseCaptured) ReleaseMouseCapture();
        if (_dragMoved) ZoneDragCompleted?.Invoke(_dragZone);
        _dragZone = null;
        _dragMoved = false;
        InvalidateVisual(); // drop the live-coords caption
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panning && e.ChangedButton == MouseButton.Middle)
        {
            _panning = false;
            Cursor = Cursors.Arrow;
            if (IsMouseCaptured) ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        var pt = e.GetPosition(this);
        var hit = HitTest(pt);
        if (hit != null)
        {
            SelectedZone = hit;
            ZoneContextRequested?.Invoke(hit);
            e.Handled = true;
            return;
        }
        int monitor = MonitorAt(pt);
        if (monitor >= 0)
        {
            MonitorContextRequested?.Invoke(monitor);
            e.Handled = true;
            return;
        }
        // Blank gray space (no zone, no monitor): map-level menu — unhide
        // monitors directly, fit view, flash all.
        MapBlankContextRequested?.Invoke();
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverZone != null && _dragZone == null)
        {
            _hoverZone = null;
            Cursor = Cursors.Arrow;
            InvalidateVisual();
        }
    }
}
