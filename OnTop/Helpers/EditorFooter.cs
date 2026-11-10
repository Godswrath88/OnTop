using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OnTop.Helpers;

/// <summary>
/// S28 K: every sub-GUI gets an explicit footer — "Save &amp; Close"
/// (or just "Close" for viewers) and, where edits can be reverted, "Cancel
/// (Revert)" — and loses its title-bar ✕ so the buttons are the only way out.
/// Everything still applies live while the window is open; Cancel restores the
/// snapshot the window took when it opened. Esc = Cancel/Close, Enter = Save.
/// </summary>
public static class EditorFooter
{
    /// <summary>Wrap an already-built content element with the footer.</summary>
    public static void Wrap(Window w, UIElement content, Action? onSave, Action? onCancel, string saveText = "Save & Close")
    {
        var host = new DockPanel();
        var bar = new DockPanel { Margin = new Thickness(14, 0, 14, 12), LastChildFill = false };
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        bool done = false;
        if (onCancel is not null)
        {
            var cancel = new Button { Content = "Cancel (Revert)", Height = 30, MinWidth = 120, Margin = new Thickness(0, 0, 8, 0), ToolTip = "Undo Everything Changed Since This Window Opened, Then Close" };
            cancel.Click += (_, _) => { done = true; onCancel(); w.Close(); };
            right.Children.Add(cancel);
        }
        var save = new Button { Content = saveText, Height = 30, MinWidth = 120, Style = (Style)Application.Current.FindResource("AccentButton"), ToolTip = onCancel is null ? "Close This Window" : "Keep The Changes (They're Already Live) And Close" };
        save.Click += (_, _) => { done = true; onSave?.Invoke(); w.Close(); };
        right.Children.Add(save);
        DockPanel.SetDock(right, Dock.Right); bar.Children.Add(right);
        DockPanel.SetDock(bar, Dock.Bottom); host.Children.Add(bar);
        host.Children.Add(content);
        w.Content = host;
        w.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && !(Keyboard.FocusedElement is TextBox tb && tb.IsKeyboardFocused && tb.Text.Length > 0 && false)) { done = true; (onCancel ?? onSave)?.Invoke(); w.Close(); e.Handled = true; }
        };
        // Alt+F4 / programmatic close = Save (changes are live anyway).
        w.Closing += (_, _) => { if (!done) { done = true; onSave?.Invoke(); } };
        HideCloseButton(w);
    }

    /// <summary>Collapse the chrome's ✕ (the footer is the way out).</summary>
    public static void HideCloseButton(Window w)
    {
        void Apply() { if (w.Template?.FindName("CloseButton", w) is Button close) close.Visibility = Visibility.Collapsed; }
        w.Loaded += (_, _) => Apply();
        if (w.IsLoaded) Apply();
    }
}
