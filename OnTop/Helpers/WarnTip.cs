using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OnTop.Helpers;

/// <summary>
/// S28: tooltip content with an amber "⚠" caution line under the normal text —
/// for features that carry a security or comfort trade-off (scanners, input
/// recording, launching, network). Owner request: warnings must be visible at
/// the point of use, not only in Help. Use in code-built UIs; XAML tooltips
/// build the same shape inline.
/// </summary>
public static class WarnTip
{
    public static object Make(string body, string warning)
    {
        var sp = new StackPanel { MaxWidth = 380 };
        sp.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        sp.Children.Add(new TextBlock
        {
            Text = "⚠ " + warning,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = (Brush)Application.Current.FindResource("WarnAmberBrush"),
        });
        return sp;
    }
}
