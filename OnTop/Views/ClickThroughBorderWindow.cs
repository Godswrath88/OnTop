using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OnTop.Helpers;

namespace OnTop.Views;

/// <summary>S28 K — a thin orange frame drawn around a click-through window so you
/// can see it's in that state. Transparent, topmost, click-through
/// itself, never activates; follows the target every 250 ms; closes when the
/// target vanishes. Purely visual — touches nothing but its own window.</summary>
public class ClickThroughBorderWindow : Window
{
    private readonly IntPtr _target;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    public ClickThroughBorderWindow(IntPtr target)
    {
        _target = target;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false;
        Content = new Border { BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(3), Background = Brushes.Transparent };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int ex = Win32.GetWindowLong(h, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(h, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
            Follow();
        };
        _timer.Tick += (_, _) => Follow();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }
    private void Follow()
    {
        if (!Win32.IsWindow(_target) || !Win32.GetWindowRect(_target, out var rc)) { Close(); return; }
        if (Win32.IsIconic(_target) || !Win32.IsWindowVisible(_target)) { Visibility = Visibility.Hidden; return; }
        var src = PresentationSource.FromVisual(this);
        var m = src?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var tl = m.Transform(new Point(rc.Left, rc.Top)); var br = m.Transform(new Point(rc.Right, rc.Bottom));
        Left = tl.X; Top = tl.Y; Width = Math.Max(4, br.X - tl.X); Height = Math.Max(4, br.Y - tl.Y);
        if (Visibility != Visibility.Visible) Visibility = Visibility.Visible;
    }
}
