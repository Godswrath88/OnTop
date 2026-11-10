using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnTop.Services;

namespace OnTop.Views;

/// <summary>S28 Stage I — everything that toasted this session ("what just flashed?"), suppressed ones dimmed.</summary>
public class ToastHistoryWindow : Window
{
    public ToastHistoryWindow()
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Toast History (This Session)";
        Width = 620; Height = 440; MinWidth = 420; MinHeight = 240; Topmost = true; ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sp = new StackPanel { Margin = new Thickness(12) };
        var amber = (Brush)Application.Current.FindResource("WarnAmberBrush");
        var red = new SolidColorBrush(Color.FromRgb(0xE6, 0x3C, 0x3C));
        (System.DateTime, ToastType, string, bool)[] items;
        lock (ToastService.History) items = ToastService.History.ToArray();
        if (items.Length == 0) sp.Children.Add(new TextBlock { Text = "Nothing Has Toasted Yet This Session.", Opacity = 0.5 });
        foreach (var (time, type, msg, suppressed) in items.Reverse())
        {
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1), Opacity = suppressed ? 0.45 : 1 };
            var t = new TextBlock { Text = time.ToString("HH:mm:ss"), FontFamily = new FontFamily("Consolas"), Opacity = 0.6, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            DockPanel.SetDock(t, Dock.Left); row.Children.Add(t);
            row.Children.Add(new TextBlock { Text = (suppressed ? "[DND] " : "") + msg, TextWrapping = TextWrapping.Wrap, Foreground = type == ToastType.Error ? red : type == ToastType.Warning ? amber : (Brush)Application.Current.FindResource("FgPrimaryBrush") });
            sp.Children.Add(row);
        }
        var sv = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = sv;
        Helpers.EditorFooter.Wrap(this, sv, onSave: null, onCancel: null, saveText: "Close");
    }
}
