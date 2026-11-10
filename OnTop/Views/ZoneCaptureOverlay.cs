using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using OnTop.Helpers;
using OnTop.Models;
using Forms = System.Windows.Forms;

namespace OnTop.Views;

/// <summary>The outcome of a zone capture — which mode produced it and where.</summary>
public sealed class CaptureResult
{
    /// <summary>Mode that produced the result: 0 Point, 1 Drag Box, 2 Draw
    /// (the user can live-switch during capture, so this may differ from the
    /// mode the overlay opened in).</summary>
    public int Mode;
    /// <summary>Point position or box center (physical px).</summary>
    public int X, Y;
    /// <summary>Box size (physical px). Zero for Point/Draw.</summary>
    public int Width, Height;
    /// <summary>Draw vertices in absolute physical px. Null for Point/Box.</summary>
    public List<ZonePoint>? Points;
}

/// <summary>
/// Full-virtual-screen capture overlay for all three recording modes (the v9.5
/// flow: the main window hides first, this appears with a hint pill, and the
/// gesture places the zone in physical pixels).
///
///  · Point Click — the next left click places the zone.
///  · Drag Box — drag a rectangle; the zone is sized 1:1.
///  · Freeform Draw — hold the button and sketch; releasing closes the shape.
///    Shapes that cross themselves are rejected (self-intersection lockout) so
///    ray-cast detection stays unambiguous — the pill explains and you retry.
///
/// 1 / 2 / 3 (or Tab) switch modes live during capture unless the caller locks
/// the mode (Redraw Shape does). Esc or right-click cancels. The near-invisible
/// background alpha keeps every pixel hit-testable, so capture gestures never
/// reach whatever is underneath.
/// </summary>
public class ZoneCaptureOverlay : Window
{
    private readonly Action<CaptureResult> _onDone;
    private readonly Action _onCancel;
    private readonly bool _lockMode;
    private int _mode;
    private bool _done;
    private double _dipScale = 1.0;
    private Rect _virtualPhys = new(0, 0, 1920, 1080);

    // UI
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly TextBlock _pillText;
    private readonly Rectangle _boxVisual;
    private readonly Polyline _drawVisual;

    // Gesture state
    private bool _boxDragging;
    private Point _boxStartDip;
    private bool _drawing;
    private readonly List<Point> _strokeDip = new();

    private static readonly Color Accent = Color.FromRgb(0x3A, 0x8B, 0xFF);
    private static readonly Color Reject = Color.FromRgb(0xE5, 0x53, 0x53);

    /// <summary>Show the capture overlay in the given mode (0 Point, 1 Box,
    /// 2 Draw). Exactly one of the callbacks fires. lockMode pins the mode
    /// (used by Redraw Shape, which must return a polygon).</summary>
    public static void Capture(int mode, Action<CaptureResult> onDone, Action onCancel, bool lockMode = false)
        => new ZoneCaptureOverlay(mode, onDone, onCancel, lockMode).Show();

    private ZoneCaptureOverlay(int mode, Action<CaptureResult> onDone, Action onCancel, bool lockMode)
    {
        _mode = Math.Clamp(mode, 0, 2);
        _onDone = onDone;
        _onCancel = onCancel;
        _lockMode = lockMode;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(0x01, 0x00, 0x00, 0x00));
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = true;   // needs keyboard focus for Esc / mode keys

        _boxVisual = new Rectangle
        {
            Stroke = new SolidColorBrush(Accent),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0x2C, Accent.R, Accent.G, Accent.B)),
            RadiusX = 3, RadiusY = 3,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _drawVisual = new Polyline
        {
            Stroke = new SolidColorBrush(Accent),
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = new SolidColorBrush(Color.FromArgb(0x22, Accent.R, Accent.G, Accent.B)),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false
        };
        _canvas.Children.Add(_boxVisual);
        _canvas.Children.Add(_drawVisual);

        _pillText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13
        };
        var pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, Accent.R, Accent.G, Accent.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 8, 18, 9),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 28, 0, 0),
            IsHitTestVisible = false,
            Child = _pillText
        };

        var root = new Grid();
        root.Children.Add(_canvas);
        root.Children.Add(pill);
        Content = root;

        ApplyModeVisuals();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TOOLWINDOW);

        _dipScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var screens = Forms.Screen.AllScreens;
        if (screens.Length > 0)
        {
            var union = screens[0].Bounds;
            for (int i = 1; i < screens.Length; i++)
                union = System.Drawing.Rectangle.Union(union, screens[i].Bounds);
            _virtualPhys = new Rect(union.X, union.Y, union.Width, union.Height);
            Left = union.X / _dipScale;
            Top = union.Y / _dipScale;
            Width = union.Width / _dipScale;
            Height = union.Height / _dipScale;
        }
        Activate();
        Focus();
    }

    // ── Mode handling ───────────────────────────────────────────────────
    private void SetMode(int mode)
    {
        if (_lockMode || _boxDragging || _drawing) return;
        _mode = Math.Clamp(mode, 0, 2);
        ApplyModeVisuals();
    }

    private void ApplyModeVisuals()
    {
        Cursor = _mode == 2 ? Cursors.Pen : Cursors.Cross;
        UpdatePill();
    }

    /// <summary>The pill doubles as the capture-time legend: current mode, its
    /// gesture, and the live mode-switch keys.</summary>
    private void UpdatePill(string? warning = null)
    {
        if (warning != null)
        {
            _pillText.Text = warning;
            return;
        }
        string line = _mode switch
        {
            1 => "Drag Box:  Drag A Rectangle — The Zone Is Sized 1:1",
            2 => "Freeform Draw:  Hold The Button And Draw A Shape — Release To Close It",
            _ => "Point Click:  Click Anywhere To Place The Zone"
        };
        _pillText.Text = _lockMode
            ? $"{line}  —  Esc Cancels"
            : $"{line}  —  1 Point · 2 Box · 3 Draw · Esc Cancels";
    }

    // ── Coordinate helpers ──────────────────────────────────────────────
    private ZonePoint ToPhysical(Point dip) => new(
        (int)Math.Round(_virtualPhys.X + dip.X * _dipScale),
        (int)Math.Round(_virtualPhys.Y + dip.Y * _dipScale));

    // ── Gestures ────────────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        UpdatePill();   // clear any lingering rejection message
        var p = e.GetPosition(this);

        switch (_mode)
        {
            case 0:
                var phys = ToPhysical(p);
                Finish(new CaptureResult { Mode = 0, X = phys.X, Y = phys.Y });
                break;

            case 1:
                _boxDragging = true;
                _boxStartDip = p;
                Canvas.SetLeft(_boxVisual, p.X);
                Canvas.SetTop(_boxVisual, p.Y);
                _boxVisual.Width = 0;
                _boxVisual.Height = 0;
                _boxVisual.Visibility = Visibility.Visible;
                CaptureMouse();
                break;

            case 2:
                _drawing = true;
                _strokeDip.Clear();
                _strokeDip.Add(p);
                _drawVisual.Stroke = new SolidColorBrush(Accent);
                _drawVisual.Points = new PointCollection { p };
                _drawVisual.Visibility = Visibility.Visible;
                CaptureMouse();
                break;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);

        if (_boxDragging)
        {
            double x = Math.Min(p.X, _boxStartDip.X);
            double y = Math.Min(p.Y, _boxStartDip.Y);
            Canvas.SetLeft(_boxVisual, x);
            Canvas.SetTop(_boxVisual, y);
            _boxVisual.Width = Math.Abs(p.X - _boxStartDip.X);
            _boxVisual.Height = Math.Abs(p.Y - _boxStartDip.Y);
            return;
        }

        if (_drawing)
        {
            // Thin the stroke: a vertex every ~4 DIPs keeps polygons light
            // without visibly changing the drawn shape.
            var last = _strokeDip[^1];
            if ((p - last).Length >= 4)
            {
                _strokeDip.Add(p);
                _drawVisual.Points.Add(p);
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_boxDragging)
        {
            _boxDragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();

            var a = ToPhysical(_boxStartDip);
            var b = ToPhysical(e.GetPosition(this));
            int w = Math.Abs(b.X - a.X);
            int h = Math.Abs(b.Y - a.Y);
            if (w < 5 || h < 5)
            {
                _boxVisual.Visibility = Visibility.Collapsed;
                UpdatePill("That Box Was Too Small — Drag A Larger Rectangle (Esc Cancels)");
                return;
            }
            Finish(new CaptureResult
            {
                Mode = 1,
                X = (a.X + b.X) / 2,
                Y = (a.Y + b.Y) / 2,
                Width = w,
                Height = h
            });
            return;
        }

        if (_drawing)
        {
            _drawing = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            TryFinishDraw();
        }
    }

    private void TryFinishDraw()
    {
        var stroke = _strokeDip;

        if (stroke.Count < 3)
        {
            ResetStroke();
            UpdatePill("Keep The Button Held And Draw A Full Shape — Then Release To Close It");
            return;
        }

        // Self-intersection lockout (v9.5): a shape that crosses itself makes
        // ray-cast detection ambiguous, so it is rejected and redrawn.
        if (IsSelfIntersecting(stroke))
        {
            _drawVisual.Stroke = new SolidColorBrush(Reject);
            _strokeDip.Clear();
            UpdatePill("The Shape Crosses Itself — Draw It Again Without Overlapping The Line");
            return;
        }

        var phys = stroke.Select(ToPhysical).ToList();
        int minX = phys.Min(p => p.X), maxX = phys.Max(p => p.X);
        int minY = phys.Min(p => p.Y), maxY = phys.Max(p => p.Y);
        if (maxX - minX < 10 || maxY - minY < 10)
        {
            ResetStroke();
            UpdatePill("That Shape Was Too Small To Detect Reliably — Draw A Larger One (Esc Cancels)");
            return;
        }

        Finish(new CaptureResult { Mode = 2, Points = phys });
    }

    private void ResetStroke()
    {
        _strokeDip.Clear();
        _drawVisual.Points = new PointCollection();
        _drawVisual.Visibility = Visibility.Collapsed;
    }

    /// <summary>True when any two non-adjacent edges of the CLOSED polygon
    /// properly cross (shared endpoints between neighbors are fine).</summary>
    private static bool IsSelfIntersecting(IReadOnlyList<Point> pts)
    {
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            var a1 = pts[i];
            var a2 = pts[(i + 1) % n];
            for (int j = i + 1; j < n; j++)
            {
                // Skip edges that share a vertex (neighbors, incl. the closure).
                if (j == i || (j + 1) % n == i || (i + 1) % n == j) continue;
                var b1 = pts[j];
                var b2 = pts[(j + 1) % n];
                if (SegmentsCross(a1, a2, b1, b2)) return true;
            }
        }
        return false;
    }

    private static bool SegmentsCross(Point p1, Point p2, Point p3, Point p4)
    {
        double d1 = Cross(p3, p4, p1), d2 = Cross(p3, p4, p2);
        double d3 = Cross(p1, p2, p3), d4 = Cross(p1, p2, p4);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Cross(Point o, Point a, Point b)
        => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

    // ── Cancel / keys ───────────────────────────────────────────────────
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        Cancel();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape: Cancel(); break;
            case Key.D1: case Key.NumPad1: SetMode(0); break;
            case Key.D2: case Key.NumPad2: SetMode(1); break;
            case Key.D3: case Key.NumPad3: SetMode(2); break;
            case Key.Tab: SetMode((_mode + 1) % 3); e.Handled = true; break;
        }
    }

    // ── Finish ──────────────────────────────────────────────────────────
    private void Finish(CaptureResult result)
    {
        if (_done) return;
        _done = true;
        Close();
        _onDone(result);
    }

    private void Cancel()
    {
        if (_done) return;
        _done = true;
        Close();
        _onCancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        // Alt+F4 or anything else that closed us without a decision = cancel.
        if (!_done)
        {
            _done = true;
            _onCancel();
        }
    }
}
