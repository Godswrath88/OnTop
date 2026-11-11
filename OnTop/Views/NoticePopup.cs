using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace OnTop.Views;

/// <summary>
/// "Do this first" hint (e.g. "Select A Zone First"): a card centered over the
/// OnTop window the click came from. It fades in, stays for the configured time
/// (Settings → Appearance → Hint Popups), and fades out. No button to press;
/// a click dismisses it early, and hovering keeps it up. It never takes focus
/// (ShowActivated = false, plus WS_EX_NOACTIVATE so a click doesn't either), so
/// typing carries on in the window underneath.
/// Hint Popups = Off shows the same text as a normal toast instead.
/// Only one at a time: a new hint replaces the current one.
/// </summary>
public sealed class NoticePopup : Window
{
    /// <summary>Seconds a hint stays up; 0 = popups off (use a toast). Set by App.</summary>
    public static Func<int>? Seconds { get; set; }

    private static NoticePopup? _current;
    private readonly DispatcherTimer _hold;
    private bool _closing;

    /// <summary>Show a hint over <paramref name="owner"/> (default: the active OnTop window).</summary>
    public static void Show(string title, string message, Window? owner = null)
    {
        int secs = Seconds?.Invoke() ?? 4;
        if (secs <= 0)
        {
            Services.ToastService.Show($"{title}: {message}", Services.ToastType.Warning, 5000);
            return;
        }
        _current?.FadeOut(fast: true);
        owner ??= ActiveOwnWindow();
        _current = new NoticePopup(title, message, secs, owner);
        _current.Show();
    }

    private NoticePopup(string title, string message, int seconds, Window? owner)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Opacity = 0;

        var accent = new SolidColorBrush(Color.FromRgb(0xE6, 0xA2, 0x3C));
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        head.Children.Add(new TextBlock
        {
            Text = "",   // MDL2 Info
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16, Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0),
        });
        head.Children.Add(new TextBlock
        {
            Text = title, FontFamily = new FontFamily("Segoe UI Semibold"), FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF5)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var body = new StackPanel();
        body.Children.Add(head);
        body.Children.Add(new TextBlock
        {
            Text = message, FontFamily = new FontFamily("Segoe UI"), FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6)),
            TextWrapping = TextWrapping.Wrap, MaxWidth = 340, Margin = new Thickness(26, 0, 0, 0),
        });

        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x4A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 22, 16),
            Margin = new Thickness(16),   // room for the shadow
            MinWidth = 280,
            Child = body,
            Cursor = Cursors.Hand,
            ToolTip = "Click To Dismiss",
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.55, Color = Colors.Black },
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.96, 0.96),
        };
        Content = card;

        _hold = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        _hold.Tick += (_, _) => { _hold.Stop(); if (!IsMouseOver) FadeOut(); };
        MouseEnter += (_, _) => _hold.Stop();
        MouseLeave += (_, _) => { if (!_closing) { _hold.Interval = TimeSpan.FromSeconds(1.5); _hold.Start(); } };
        MouseLeftButtonUp += (_, _) => FadeOut();

        // WS_EX_NOACTIVATE: clicking the card (to dismiss it) must not take
        // focus either; ShowActivated only covers showing it. Tool window keeps
        // it out of Alt+Tab.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            int ex = Helpers.Win32.GetWindowLong(hwnd, Helpers.Win32.GWL_EXSTYLE);
            Helpers.Win32.SetWindowLong(hwnd, Helpers.Win32.GWL_EXSTYLE, ex | Helpers.Win32.WS_EX_NOACTIVATE | Helpers.Win32.WS_EX_TOOLWINDOW);
        };

        // Center over the owner once the size is known; fall back to the work area.
        Loaded += (_, _) =>
        {
            // Position in DEVICE pixels: with per-monitor DPI, Left/Top would be
            // read at this popup's monitor scale, not the owner's. Re-center once
            // more if moving it onto the owner's monitor changes its scale.
            var target = OwnerRectPx(owner);
            CenterOn(target);
            DpiChanged += (_, _) => Dispatcher.BeginInvoke(() => CenterOn(target));

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(200);
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur) { EasingFunction = ease });
            var scale = (ScaleTransform)card.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, dur) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, dur) { EasingFunction = ease });
            _hold.Start();
        };
    }

    private void FadeOut(bool fast = false)
    {
        if (_closing) return;
        _closing = true;
        _hold.Stop();
        if (ReferenceEquals(_current, this)) _current = null;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(fast ? 100 : 350))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>Center this popup on <paramref name="targetPx"/> (device pixels),
    /// or on the primary work area if there's no usable owner.</summary>
    private void CenterOn(Rect? targetPx)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var r = targetPx ?? ToRect(System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? System.Windows.Forms.Screen.AllScreens[0].WorkingArea);
        var dpi = VisualTreeHelper.GetDpi(this);
        double w = ActualWidth * dpi.DpiScaleX, h = ActualHeight * dpi.DpiScaleY;
        Helpers.Win32.SetWindowPos(hwnd, IntPtr.Zero, (int)(r.X + (r.Width - w) / 2), (int)(r.Y + (r.Height - h) / 2), 0, 0,
            Helpers.Win32.SWP_NOSIZE | Helpers.Win32.SWP_NOZORDER | Helpers.Win32.SWP_NOACTIVATE);
    }

    private static Rect ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    /// <summary>The owner's on-screen rectangle in device pixels. Left/Top are
    /// wrong for a maximized window (they hold the restore position) and are in
    /// the owner's DIPs, so measure where its content actually is on screen.</summary>
    private static Rect? OwnerRectPx(Window? owner)
    {
        if (owner is not { IsVisible: true } || owner.WindowState == WindowState.Minimized) return null;
        try
        {
            Point tl = owner.PointToScreen(new Point(0, 0));
            Point br = owner.PointToScreen(new Point(owner.ActualWidth, owner.ActualHeight));
            return new Rect(tl, br);
        }
        catch (InvalidOperationException) { return null; }   // not connected to a PresentationSource
    }

    private static Window? ActiveOwnWindow()
    {
        foreach (Window w in Application.Current.Windows)
            if (w.IsActive && w.IsVisible && w is not NoticePopup) return w;
        return Application.Current.MainWindow;
    }
}
