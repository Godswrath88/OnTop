using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using OnTop.Helpers;
using OnTop.Models;
using Forms = System.Windows.Forms;

namespace OnTop.Views;

/// <summary>
/// Transparent, topmost window spanning the entire virtual screen that draws a
/// dot, semi-transparent detection box, name/size label, and four corner resize
/// handles for every zone with ShowDot enabled.
///
/// Coordinates: zones live in physical pixels (GetCursorPos / mouse hook space).
/// The window converts physical → DIP with the process DPI scale, which is
/// uniform under WPF's default system-DPI awareness, so visuals land exactly on
/// the detection area.
///
/// Hit testing: WPF layered windows are per-pixel — fully transparent areas pass
/// clicks through, but the semi-transparent box fill does not. That is why the
/// click-through lock exists (WS_EX_TRANSPARENT): locked overlays are purely
/// visual and desktop double-click detection works through them; unlocked
/// overlays are edit mode, where dots move zones and corners resize them.
/// </summary>
public class ZoneOverlayWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Dictionary<Zone, ZoneVisual> _visuals = new();
    private double _dipScale = 1.0;                       // physical px per DIP
    private Rect _virtualPhys = new(0, 0, 1920, 1080);    // physical px
    /// <summary>Per-monitor mode (L1, S24): when set, this window covers ONE
    /// screen (matched by DeviceName each layout pass) instead of the virtual
    /// union — so under PerMonitorV2 it gets THAT monitor's DPI and geometry is
    /// exact even with mixed display scaling (the 3-monitor mismatch seen in testing).</summary>
    private readonly string? _targetDevice;
    private bool _clickThrough;

    /// <summary>First movement of any drag/resize — push the undo snapshot.</summary>
    public event Action<Zone>? InteractionStarted;
    public event Action<Zone>? MoveCompleted;
    public event Action<Zone>? ResizeCompleted;

    public ZoneOverlayWindow(string? targetDevice = null)
    {
        _targetDevice = targetDevice;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        Content = _canvas;
    }

    /// <summary>Fade the whole overlay by setting the content root's opacity.
    /// More reliable than Window.Opacity on a transparent, click-through layered
    /// window (which doesn't consistently repaint the child visuals).</summary>
    public void SetOverlayOpacity(double opacity)
        => _canvas.Opacity = Math.Clamp(opacity, 0.25, 1.0);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE,
            ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
        ApplyClickThrough(_clickThrough);   // re-apply whatever was set pre-HWND
        PositionToVirtualScreen();
    }

    // ── Layout ──────────────────────────────────────────────────────────
    /// <summary>Size/position the window over the whole virtual screen.
    /// Call again after display changes (resolution, monitors added/removed).</summary>
    public void PositionToVirtualScreen()
    {
        if (PresentationSource.FromVisual(this) == null) return;
        _dipScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0) return;
        System.Drawing.Rectangle union;
        if (_targetDevice is not null)
        {
            // Per-monitor mode: cover exactly our screen (re-resolved by device
            // name so resolution changes keep working); vanish if it unplugged.
            var mine = screens.FirstOrDefault(sc => sc.DeviceName == _targetDevice);
            if (mine is null) { Hide(); return; }
            union = mine.Bounds;
        }
        else
        {
            union = screens[0].Bounds;
            for (int i = 1; i < screens.Length; i++)
                union = System.Drawing.Rectangle.Union(union, screens[i].Bounds);
        }
        _virtualPhys = new Rect(union.X, union.Y, union.Width, union.Height);

        Left = union.X / _dipScale;
        Top = union.Y / _dipScale;
        Width = union.Width / _dipScale;
        Height = union.Height / _dipScale;

        foreach (var visual in _visuals.Values) visual.Update();
    }

    /// <summary>PMv2: when this window lands on (or its monitor changes to) a
    /// different DPI, WPF rescales — recompute so physical mapping stays exact.
    /// (Also ticks roadmap: "overlay reposition on per-window DPI change".)</summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        PositionToVirtualScreen();
    }

    // ── Membership ──────────────────────────────────────────────────────
    /// <summary>Sync the displayed visuals to the given zones (those with ShowDot).</summary>
    public void SetZones(IReadOnlyList<Zone> zones)
    {
        foreach (var stale in _visuals.Keys.Where(z => !zones.Contains(z)).ToList())
        {
            _visuals[stale].Detach(_canvas);
            _visuals.Remove(stale);
        }
        foreach (var zone in zones)
        {
            if (_visuals.ContainsKey(zone)) continue;
            var visual = new ZoneVisual(this, zone);
            visual.Attach(_canvas);
            visual.SetInteractive(!_clickThrough);
            _visuals[zone] = visual;
        }
        foreach (var visual in _visuals.Values) visual.Update();
    }

    // ── Click-through lock ──────────────────────────────────────────────
    /// <summary>Locked = WS_EX_TRANSPARENT: overlays become purely visual, all
    /// mouse input (including desktop double-clicks) passes through. Unlocked =
    /// edit mode with draggable dots and resize handles.</summary>
    public void ApplyClickThrough(bool locked)
    {
        _clickThrough = locked;
        foreach (var visual in _visuals.Values) visual.SetInteractive(!locked);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        ex = locked ? ex | Win32.WS_EX_TRANSPARENT : ex & ~Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex);
    }

    // ═════ Per-zone visual group ═════════════════════════════════════════
    private sealed class ZoneVisual
    {
        private const double DotSize = 14;     // DIPs
        private const double HandleSize = 9;   // DIPs
        private const int CornerTL = 0, CornerTR = 1, CornerBL = 2, CornerBR = 3;

        private readonly ZoneOverlayWindow _owner;
        private readonly Zone _zone;
        private readonly Rectangle _box;
        private readonly Polygon _poly;    // Draw zones show their real outline
        private readonly Border _labelChip;
        private readonly TextBlock _label;
        private readonly Thumb _dot;
        private readonly Thumb[] _handles = new Thumb[4];

        // Drag state (accumulated DIP deltas + starting values in physical px)
        private double _accX, _accY;
        private int _startX, _startY, _startHalfX, _startHalfY;
        private bool _moved;

        public ZoneVisual(ZoneOverlayWindow owner, Zone zone)
        {
            _owner = owner;
            _zone = zone;

            _box = new Rectangle { StrokeThickness = 2, RadiusX = 3, RadiusY = 3, IsHitTestVisible = false };
            _poly = new Polygon
            {
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            _label = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6))
            };
            _labelChip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x14, 0x14, 0x1E)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 2, 7, 3),
                IsHitTestVisible = false,
                Child = _label
            };

            _dot = new Thumb
            {
                Width = DotSize,
                Height = DotSize,
                Cursor = System.Windows.Input.Cursors.SizeAll,
                Focusable = false
            };
            _dot.DragStarted += (_, _) => BeginInteraction();
            _dot.DragDelta += OnDotDragDelta;
            _dot.DragCompleted += (_, _) => { if (_moved) _owner.MoveCompleted?.Invoke(_zone); _moved = false; };

            for (int corner = 0; corner < 4; corner++)
            {
                var handle = new Thumb
                {
                    Width = HandleSize,
                    Height = HandleSize,
                    Tag = corner,
                    Focusable = false,
                    Cursor = corner is CornerTL or CornerBR
                        ? System.Windows.Input.Cursors.SizeNWSE
                        : System.Windows.Input.Cursors.SizeNESW
                };
                handle.DragStarted += (_, _) => { BeginInteraction(); _zone.BeginScale(); };
                handle.DragDelta += OnHandleDragDelta;
                handle.DragCompleted += (_, _) =>
                {
                    _zone.EndScale();
                    if (_moved) _owner.ResizeCompleted?.Invoke(_zone);
                    _moved = false;
                };
                _handles[corner] = handle;
            }
        }

        public void Attach(Canvas canvas)
        {
            canvas.Children.Add(_box);
            canvas.Children.Add(_poly);
            canvas.Children.Add(_labelChip);
            foreach (var handle in _handles) canvas.Children.Add(handle);
            canvas.Children.Add(_dot);
            _zone.PropertyChanged += OnZoneChanged;
            ApplyColor();
        }

        public void Detach(Canvas canvas)
        {
            _zone.PropertyChanged -= OnZoneChanged;
            canvas.Children.Remove(_box);
            canvas.Children.Remove(_poly);
            canvas.Children.Remove(_labelChip);
            foreach (var handle in _handles) canvas.Children.Remove(handle);
            canvas.Children.Remove(_dot);
        }

        /// <summary>Edit mode on/off: handles hidden and nothing hit-testable when locked.</summary>
        public void SetInteractive(bool interactive)
        {
            var handleVisibility = interactive ? Visibility.Visible : Visibility.Collapsed;
            foreach (var handle in _handles) handle.Visibility = handleVisibility;
            _dot.IsHitTestVisible = interactive;
        }

        private void OnZoneChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Zone.ColorIndex)) ApplyColor();
            Update();
        }

        private void ApplyColor()
        {
            var color = ZonePalette.GetColor(_zone.ColorIndex);
            var line = ZonePalette.GetBrush(_zone.ColorIndex);
            var fill = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B));
            fill.Freeze();

            _box.Stroke = line;
            _box.Fill = fill;
            _poly.Stroke = line;
            _poly.Fill = fill;
            _dot.Template = ShapeTemplate(isEllipse: true, line);
            foreach (var handle in _handles) handle.Template = ShapeTemplate(isEllipse: false, line);
        }

        /// <summary>Reposition every element from the zone's current values.</summary>
        public void Update()
        {
            double s = _owner._dipScale;
            var vb = _owner._virtualPhys;
            double cx = (_zone.X - vb.X) / s;
            double cy = (_zone.Y - vb.Y) / s;
            double hw = _zone.HalfX / s;
            double hh = _zone.HalfY / s;

            // Draw zones show their real outline; Point/Box show the detection box.
            bool isPolygon = _zone.Kind == ZoneKind.Draw && _zone.RelPoints.Count >= 3;
            _box.Visibility = isPolygon ? Visibility.Collapsed : Visibility.Visible;
            _poly.Visibility = isPolygon ? Visibility.Visible : Visibility.Collapsed;
            if (isPolygon)
            {
                var pts = new PointCollection();
                foreach (var p in _zone.RelPoints)
                    pts.Add(new Point(cx + p.X / s, cy + p.Y / s));
                _poly.Points = pts;
            }

            Canvas.SetLeft(_box, cx - hw);
            Canvas.SetTop(_box, cy - hh);
            _box.Width = hw * 2;
            _box.Height = hh * 2;

            Canvas.SetLeft(_dot, cx - DotSize / 2);
            Canvas.SetTop(_dot, cy - DotSize / 2);

            PlaceHandle(CornerTL, cx - hw, cy - hh);
            PlaceHandle(CornerTR, cx + hw, cy - hh);
            PlaceHandle(CornerBL, cx - hw, cy + hh);
            PlaceHandle(CornerBR, cx + hw, cy + hh);

            _label.Text = $"{_zone.Name}  {_zone.HalfX * 2}×{_zone.HalfY * 2}px";
            Canvas.SetLeft(_labelChip, cx - hw);
            Canvas.SetTop(_labelChip, cy - hh - 26);

            double opacity = _zone.Enabled ? 1.0 : 0.4;
            _box.Opacity = opacity;
            _poly.Opacity = opacity;
            _dot.Opacity = opacity;
            _labelChip.Opacity = opacity;
            foreach (var handle in _handles) handle.Opacity = opacity;
        }

        private void PlaceHandle(int corner, double x, double y)
        {
            Canvas.SetLeft(_handles[corner], x - HandleSize / 2);
            Canvas.SetTop(_handles[corner], y - HandleSize / 2);
        }

        // ── Drag: move (dot) ────────────────────────────────────────────
        private void BeginInteraction()
        {
            _accX = _accY = 0;
            _startX = _zone.X;
            _startY = _zone.Y;
            _startHalfX = _zone.HalfX;
            _startHalfY = _zone.HalfY;
            _moved = false;
        }

        private void OnDotDragDelta(object sender, DragDeltaEventArgs e)
        {
            MarkMoved();
            _accX += e.HorizontalChange;
            _accY += e.VerticalChange;
            double s = _owner._dipScale;
            var vb = _owner._virtualPhys;
            _zone.X = (int)Math.Round(Math.Clamp(_startX + _accX * s, vb.Left, vb.Right - 1));
            _zone.Y = (int)Math.Round(Math.Clamp(_startY + _accY * s, vb.Top, vb.Bottom - 1));
        }

        // ── Drag: resize (corner handles, waterfall) ────────────────────
        private void OnHandleDragDelta(object sender, DragDeltaEventArgs e)
        {
            MarkMoved();
            _accX += e.HorizontalChange;
            _accY += e.VerticalChange;
            double s = _owner._dipScale;
            int corner = (int)((Thumb)sender).Tag;

            int signX = corner is CornerTR or CornerBR ? 1 : -1;
            int signY = corner is CornerBL or CornerBR ? 1 : -1;
            int desiredHalfX = _startHalfX + (int)Math.Round(signX * _accX * s);
            int desiredHalfY = _startHalfY + (int)Math.Round(signY * _accY * s);

            _zone.ResizeTo(desiredHalfX, desiredHalfY);
        }

        private void MarkMoved()
        {
            if (_moved) return;
            _moved = true;
            _owner.InteractionStarted?.Invoke(_zone);   // undo snapshot before first change
        }

        // ── Thumb templates (Ellipse dot / Rectangle handle) ────────────
        private static ControlTemplate ShapeTemplate(bool isEllipse, Brush fill)
        {
            var factory = new FrameworkElementFactory(isEllipse ? typeof(Ellipse) : typeof(Rectangle));
            factory.SetValue(Shape.FillProperty, fill);
            factory.SetValue(Shape.StrokeProperty, Brushes.White);
            factory.SetValue(Shape.StrokeThicknessProperty, 1.5);
            if (!isEllipse)
            {
                factory.SetValue(Rectangle.RadiusXProperty, 2.0);
                factory.SetValue(Rectangle.RadiusYProperty, 2.0);
            }
            return new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
        }
    }
}
