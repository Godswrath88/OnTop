using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OnTop.Views;

/// <summary>
/// Dark-chrome Yes/No confirmation — replaces the stock light MessageBox for
/// restore, confirm-before-exit, and future destructive actions. Topmost so it
/// can never hide behind always-on-top windows.
/// </summary>
public class ConfirmDialog : Window
{
    private ConfirmDialog(string title, string message, string yesText, string? noText, bool info = false)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = title;
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        row.Children.Add(new TextBlock
        {
            Text = info ? "\uE946" : "\uE7BA",   // MDL2 Info / Warning
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 22,
            Foreground = info ? new SolidColorBrush(Color.FromRgb(0x5B, 0xA8, 0xE6))
                              : new SolidColorBrush(Color.FromRgb(0xE6, 0xA2, 0x3C)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 12, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 330
        });
        root.Children.Add(row);

        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var yes = new Button
        {
            Content = yesText, Height = 30, IsDefault = true,
            Style = (Style)Application.Current.FindResource("AccentButton")
        };
        yes.Click += (_, _) => { DialogResult = true; Close(); };
        Grid.SetColumn(yes, 0);
        buttons.Children.Add(yes);
        if (noText is null)
        {
            // Notice: one OK button across the full width; Esc closes too.
            yes.IsCancel = true;
            Grid.SetColumnSpan(yes, 3);
        }
        else
        {
            var no = new Button { Content = noText, Height = 30, IsCancel = true };
            Grid.SetColumn(no, 2);
            buttons.Children.Add(no);
        }
        root.Children.Add(buttons);

        Content = root;
    }

    /// <summary>
    /// OK-only popup for "you need to do X first" messages that are easy to miss
    /// in the status bar or a toast. Centered on the active OnTop window. Only
    /// call this from a click in OnTop's own UI, never from a hotkey or a
    /// background path: a modal popup would steal focus from a game.
    /// </summary>
    public static void Notice(string title, string message, Window? owner = null)
    {
        owner ??= ActiveOwnWindow();
        var dialog = new ConfirmDialog(title, message, "OK", null, info: true);
        if (owner is { IsVisible: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.ShowDialog();
    }

    private static Window? ActiveOwnWindow()
    {
        foreach (Window w in Application.Current.Windows)
            if (w.IsActive && w.IsVisible) return w;
        return Application.Current.MainWindow;
    }

    /// <summary>Show the confirmation; true = the affirmative button.</summary>
    public static bool Confirm(Window? owner, string title, string message,
        string yesText = "Yes", string noText = "Cancel")
    {
        var dialog = new ConfirmDialog(title, message, yesText, noText);
        if (owner is { IsVisible: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }
}
