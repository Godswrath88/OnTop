using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace OnTop.Helpers;

/// <summary>
/// Attached behavior used by the DarkChromeWindow style. It applies the DWM
/// dark-frame attributes once the window has an HWND, and wires the caption
/// buttons with DIRECT Click handlers found by template part name.
///
/// History: the buttons originally used SystemCommands RoutedCommands, which
/// proved unreliable (routing depends on command targets/keyboard focus, and a
/// tray-shown window often has neither) — the X/− appeared dead. Direct event
/// wiring has no routing to fail.
/// </summary>
public static class ChromeWindowBehavior
{
    public static readonly DependencyProperty EnableSystemCommandsProperty =
        DependencyProperty.RegisterAttached(
            "EnableSystemCommands", typeof(bool), typeof(ChromeWindowBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static bool GetEnableSystemCommands(DependencyObject obj)
        => (bool)obj.GetValue(EnableSystemCommandsProperty);

    public static void SetEnableSystemCommands(DependencyObject obj, bool value)
        => obj.SetValue(EnableSystemCommandsProperty, value);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not true) return;

        // Dark frame, rounded corners, Mica — applied as soon as the HWND exists.
        window.SourceInitialized += (_, _) =>
            Win32.ApplyDarkMode(new WindowInteropHelper(window).Handle);

        // Caption buttons — template is applied by Loaded; wire them directly.
        window.Loaded += (_, _) =>
        {
            if (window.Template?.FindName("MinButton", window) is Button min)
            {
                min.Click -= MinClick;   // idempotent across re-loads
                min.Click += MinClick;
            }
            if (window.Template?.FindName("CloseButton", window) is Button close)
            {
                close.Click -= CloseClick;
                close.Click += CloseClick;
            }
        };

        static void MinClick(object sender, RoutedEventArgs args)
        {
            if (Window.GetWindow((DependencyObject)sender) is { } w)
                w.WindowState = WindowState.Minimized;
        }

        static void CloseClick(object sender, RoutedEventArgs args)
        {
            // Window.Close() raises Closing, so hide-on-close windows (the main
            // settings window) keep their tray behavior with the custom X.
            Window.GetWindow((DependencyObject)sender)?.Close();
        }
    }
}
