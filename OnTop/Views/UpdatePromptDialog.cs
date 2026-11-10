using System.Windows;
using System.Windows.Controls;

namespace OnTop.Views;

public enum UpdateChoice { Cancel, Open, IgnoreVersion }

/// <summary>
/// The three-way update prompt: Open Download Page (browser
/// handoff — the app itself downloads nothing), Ignore This Version (silenced
/// until the NEXT release), or Cancel (re-prompts on next startup).
/// </summary>
public class UpdatePromptDialog : Window
{
    private UpdateChoice _choice = UpdateChoice.Cancel;

    private UpdatePromptDialog(string version, string notes, bool canOpen)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Update Available";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;
        Topmost = true;

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(new TextBlock
        {
            Text = $"A Newer Version Of OnTop Is Available: {version}",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (notes.Trim().Length > 0)
            root.Children.Add(new TextBlock
            {
                Text = notes.Trim(),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                MaxHeight = 120,
            });
        root.Children.Add(new TextBlock
        {
            Text = "Nothing Downloads Automatically — \"Open Download Page\" Just Opens Your Browser.",
            Opacity = 0.55,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 12),
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button Mk(string text, string tip, UpdateChoice c, bool enabled = true)
        {
            var b = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0), ToolTip = tip, IsEnabled = enabled };
            b.Click += (_, _) => { _choice = c; Close(); };
            buttons.Children.Add(b);
            return b;
        }
        Mk("Open Download Page", "Open The Release Page In Your Browser", UpdateChoice.Open, canOpen);
        Mk("Ignore This Version", "Don't Ask Again Until The NEXT Release Ships", UpdateChoice.IgnoreVersion);
        Mk("Cancel", "Dismiss For Now — Asks Again On Next Startup", UpdateChoice.Cancel);
        root.Children.Add(buttons);
        Content = root;
    }

    public static UpdateChoice Show(string version, string notes, bool canOpen)
    {
        var dlg = new UpdatePromptDialog(version, notes, canOpen);
        dlg.ShowDialog();
        return dlg._choice;
    }
}
