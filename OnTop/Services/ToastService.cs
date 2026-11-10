using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using OnTop.Helpers;

namespace OnTop.Services;

public enum ToastType { Info, Success, Warning, Error }

/// <summary>Where a toast appears. v9.5 semantics: routine notices go to the
/// top-right corner; critical ones land in the center of the screen.</summary>
public enum ToastPlacement { TopRight, Center }

/// <summary>
/// Styled, non-blocking toast notifications with the v9.5 two-zone layout:
/// Errors default to screen center, everything else to the top-right. Callers
/// can override placement per toast. Safe to call from any thread — everything
/// is marshaled to the dispatcher, and toasts never steal focus.
/// (Note: the saved-settings confirmation intentionally does NOT toast — it
/// shows in the status bubble instead, per v9.5 feedback.)
/// </summary>
public static class ToastService
{
    private static readonly Dictionary<ToastPlacement, ToastHostWindow> Hosts = new();

    /// <summary>S28: set by the VM — returns true while routine toasts should be suppressed.</summary>
    public static Func<bool>? SuppressRoutine { get; set; }
    /// <summary>S28 Stage I: everything toasted this session (incl. DND-suppressed), newest last, 300 cap.</summary>
    public static readonly List<(DateTime Time, ToastType Type, string Message, bool Suppressed)> History = new();
    private static void Remember(ToastType type, string message, bool suppressed)
    {
        lock (History) { History.Add((DateTime.Now, type, message, suppressed)); if (History.Count > 300) History.RemoveRange(0, History.Count - 300); }
    }

    public static void Show(string message, ToastType type = ToastType.Info,
        int durationMs = 3200, ToastPlacement? placement = null)
    {
        var app = Application.Current;
        if (app?.Dispatcher == null) return;
        // S28 Stage G: Do-Not-Disturb — routine toasts stay quiet while a guarded
        // process runs (the log still gets everything; Errors always show).
        if (type != ToastType.Error && SuppressRoutine is { } pred)
        {
            bool quiet = false;
            try { quiet = pred(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DND predicate: {ex.Message}"); /* justified-silent: toast path must never throw */ }
            if (quiet) { Remember(type, message, true); return; }
        }
        Remember(type, message, false);

        var where = placement ?? (type == ToastType.Error ? ToastPlacement.Center : ToastPlacement.TopRight);

        app.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!Hosts.TryGetValue(where, out var host))
                    Hosts[where] = host = new ToastHostWindow(where);
                host.AddToast(message, type, durationMs);
            }
            catch { /* toasts are best-effort — never let UI sugar crash the app */ }
        });
    }
}

/// <summary>
/// Borderless, transparent, no-activate host window holding one placement's
/// toast stack. Fully transparent pixels are click-through; toast cards
/// themselves are clickable to dismiss early.
/// </summary>
internal sealed class ToastHostWindow : Window
{
    private const double HostWidth = 380;
    private const double EdgeMargin = 16;

    private readonly ToastPlacement _placement;
    private readonly StackPanel _stack = new();

    public ToastHostWindow(ToastPlacement placement)
    {
        _placement = placement;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Width = HostWidth;
        SizeToContent = SizeToContent.Height;
        Content = _stack;

        SourceInitialized += (_, _) =>
        {
            // Never steal focus, never show in Alt+Tab.
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW);
        };
        SizeChanged += (_, _) => Reposition();
    }

    public void AddToast(string message, ToastType type, int durationMs)
    {
        var toast = BuildToast(message, type);
        _stack.Children.Add(toast);
        if (!IsVisible) Show();
        Reposition();

        // Entrance: fade in + slide up.
        var slide = new TranslateTransform(0, 14);
        toast.RenderTransform = slide;
        toast.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        slide.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(180))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });

        // Auto-dismiss timer; click dismisses early.
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(1200, durationMs))
        };
        timer.Tick += (_, _) => { timer.Stop(); Dismiss(toast); };
        timer.Start();
        toast.MouseLeftButtonUp += (_, _) => { timer.Stop(); Dismiss(toast); };
    }

    private void Dismiss(FrameworkElement toast)
    {
        if (!_stack.Children.Contains(toast)) return;

        var fade = new DoubleAnimation(toast.Opacity, 0, TimeSpan.FromMilliseconds(160));
        fade.Completed += (_, _) =>
        {
            _stack.Children.Remove(toast);
            if (_stack.Children.Count == 0) Hide();
            else Reposition();
        };
        toast.BeginAnimation(OpacityProperty, fade);
    }

    private void Reposition()
    {
        var workArea = SystemParameters.WorkArea;
        if (_placement == ToastPlacement.TopRight)
        {
            // Anchored to the corner; the stack grows downward.
            Left = workArea.Right - HostWidth - EdgeMargin;
            Top = workArea.Top + EdgeMargin;
        }
        else
        {
            // Critical: dead center, re-centered as the stack grows.
            Left = workArea.Left + (workArea.Width - HostWidth) / 2;
            Top = workArea.Top + Math.Max(EdgeMargin, (workArea.Height - ActualHeight) / 2);
        }
    }

    private static Border BuildToast(string message, ToastType type)
    {
        (Color accent, string glyph) = type switch
        {
            ToastType.Success => (Color.FromRgb(0x2E, 0xCC, 0x71), "\uE73E"), // CheckMark
            ToastType.Warning => (Color.FromRgb(0xE6, 0xA2, 0x3C), "\uE7BA"), // Warning
            ToastType.Error   => (Color.FromRgb(0xE5, 0x53, 0x53), "\uE783"), // Error
            _                 => (Color.FromRgb(0x3A, 0x8B, 0xFF), "\uE946"), // Info
        };
        var accentBrush = new SolidColorBrush(accent);
        accentBrush.Freeze();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var bar = new Border
        {
            Width = 3,
            Background = accentBrush,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 2, 10, 2)
        };
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            Foreground = accentBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var text = new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 300
        };
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(icon, 1);
        Grid.SetColumn(text, 2);
        grid.Children.Add(bar);
        grid.Children.Add(icon);
        grid.Children.Add(text);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x4A)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 14, 10),
            Margin = new Thickness(0, 8, 0, 0),
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = Cursors.Hand,
            Child = grid,
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 2,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };
    }
}
