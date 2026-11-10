using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OnTop.Views;

/// <summary>
/// Dark-chrome single-field text prompt (zone naming, future rename flows).
/// Topmost — the v9.5 fix: it must never open behind the always-on-top main
/// window. Enter = OK, Esc = cancel.
/// </summary>
public class InputDialog : Window
{
    private readonly TextBox _input;

    private InputDialog(string title, string label, string initial)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        root.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        _input = new TextBox { Text = initial, Height = 28, Padding = new Thickness(6, 3, 6, 3) };
        root.Children.Add(_input);

        var buttons = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var ok = new Button
        {
            Content = "OK", Height = 30, IsDefault = true,
            Style = (Style)Application.Current.FindResource("AccentButton")
        };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "Cancel", Height = 30, IsCancel = true };
        Grid.SetColumn(ok, 0);
        Grid.SetColumn(cancel, 2);
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        Content = root;
        Loaded += (_, _) => { _input.Focus(); _input.SelectAll(); };
    }

    /// <summary>Show the prompt; returns the entered text, or null on cancel.</summary>
    public static string? Prompt(Window? owner, string title, string label, string initial = "")
    {
        var dialog = new InputDialog(title, label, initial);
        if (owner is { IsVisible: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true ? dialog._input.Text : null;
    }
}
