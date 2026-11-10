using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;   // UniformGrid (hotfix v0.42.1)
using System.Windows.Input;
using System.Windows.Media;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 Stage G — every current hotkey on one always-on-top card.
/// The same hotkey (or Esc / click) hides it. Unbound entries are dimmed.</summary>
public class CheatSheetWindow : Window
{
    public CheatSheetWindow(MainViewModel vm)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Hotkeys";
        Width = 640; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height - 80;
        ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        root.Children.Add(new TextBlock { Text = "Hotkeys Right Now", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
        root.Children.Add(new TextBlock { Text = "Esc, the cheat-sheet hotkey, or a click closes this. Change bindings in Hotkeys….", Opacity = 0.6, FontSize = 11.5, Margin = new Thickness(0, 0, 0, 10) });
        var grid = new UniformGrid { Columns = 2 };
        var dim = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x84));
        var accent = (Brush)Application.Current.FindResource("AccentBlueBrush");
        foreach (var (label, key) in vm.AllBindings())
        {
            bool bound = !string.IsNullOrWhiteSpace(key);
            var row = new DockPanel { Margin = new Thickness(0, 1, 12, 1), Opacity = bound ? 1 : 0.45 };
            var k = new Border
            {
                Background = (Brush)Application.Current.FindResource("BgBandBrush"), CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 8, 0), MinWidth = 96,
                Child = new TextBlock { Text = bound ? key : "—", FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = bound ? accent : dim, TextAlignment = TextAlignment.Center },
            };
            DockPanel.SetDock(k, Dock.Left);
            row.Children.Add(k);
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            grid.Children.Add(row);
        }
        root.Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = SystemParameters.WorkArea.Height - 180 });
        Content = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        MouseLeftButtonUp += (_, _) => Close();
        Deactivated += (_, _) => Close();
    }
}
