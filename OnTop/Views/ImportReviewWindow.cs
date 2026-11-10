using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// Import Review (S28 — SECURITY.md "Shared / imported configs"). Lists every
/// powerful item that arrived inert from an OUTSIDE file, VERBATIM, each with a
/// checkbox that defaults to UNCHECKED. Only ticked items are re-enabled, via
/// QuarantineItem.Restore, when the user presses Apply. "Keep Everything Inert"
/// (or closing the window) leaves all of it neutralized — safe by construction.
/// Code-built, dark chrome, Topmost so it can't hide behind pinned windows.
/// </summary>
public class ImportReviewWindow : Window
{
    private static readonly Brush Body = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6));
    private static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xAE));

    private readonly List<QuarantineItem> _items;
    private readonly List<CheckBox> _boxes = new();

    private ImportReviewWindow(string title, string intro, List<QuarantineItem> items)
    {
        _items = items;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = title;
        Width = 620;
        Height = 480;
        MinWidth = 480;
        MinHeight = 320;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(18, 14, 18, 16) };

        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        titleRow.Children.Add(new TextBlock
        {
            Text = "\uE7BA",   // MDL2 Warning
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 20,
            Foreground = (Brush)Application.Current.FindResource("WarnAmberBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = "Powerful Content Arrived Inert — Review Before Enabling",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Body,
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        });
        head.Children.Add(titleRow);
        head.Children.Add(new TextBlock
        {
            Text = intro, Foreground = Dim, FontSize = 12, TextWrapping = TextWrapping.Wrap,
        });
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head);

        var buttons = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var inert = new Button
        {
            Content = "Keep Everything Inert", Height = 30, IsCancel = true, MinWidth = 160,
            ToolTip = "Leave Every Item Neutralized (Safe Default) — You Can Re-Set Actions By Hand Later",
        };
        inert.Click += (_, _) => { foreach (var it in _items) it.Keep = false; DialogResult = false; Close(); };
        var apply = new Button
        {
            Content = "Enable Ticked Items", Height = 30, IsDefault = true, MinWidth = 160,
            Style = (Style)Application.Current.FindResource("AccentButton"),
            ToolTip = "Re-Enable ONLY The Items You Ticked, Exactly As Shown",
        };
        apply.Click += (_, _) =>
        {
            for (int i = 0; i < _items.Count; i++) _items[i].Keep = _boxes[i].IsChecked == true;
            DialogResult = true;
            Close();
        };
        Grid.SetColumn(inert, 1);
        Grid.SetColumn(apply, 3);
        buttons.Children.Add(inert);
        buttons.Children.Add(apply);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var list = new StackPanel();
        foreach (var it in items)
        {
            var row = new Border
            {
                Background = (Brush)Application.Current.FindResource("BgBandBrush"),
                CornerRadius = (CornerRadius)Application.Current.FindResource("RadiusSmall"),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var cb = new CheckBox
            {
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 10, 0),
                ToolTip = "Tick To Re-Enable This Item Exactly As Shown (Unticked = Stays Inert)",
            };
            _boxes.Add(cb);
            Grid.SetColumn(cb, 0);
            grid.Children.Add(cb);
            var text = new StackPanel();
            text.Children.Add(new TextBlock
            {
                Text = $"{it.Category} — {it.Subject}",
                FontWeight = FontWeights.SemiBold, Foreground = Body, TextWrapping = TextWrapping.Wrap,
            });
            text.Children.Add(new TextBlock
            {
                Text = it.Detail,
                FontFamily = new FontFamily("Consolas"),
                Foreground = Dim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0),
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            row.Child = grid;
            list.Children.Add(row);
        }
        root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });
        Content = root;
    }

    /// <summary>Show the review. Applies Restore for every ticked item and
    /// returns the number re-enabled (0 if none / cancelled). No-op on empty.</summary>
    public static int Review(Window? owner, string title, string intro, List<QuarantineItem> items)
    {
        if (items.Count == 0) return 0;
        var dlg = new ImportReviewWindow(title, intro, items);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;   // Owner on a never-shown window throws
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dlg.ShowDialog();
        int kept = 0;
        foreach (var it in items.Where(i => i.Keep)) { it.Restore(); kept++; }
        return kept;
    }
}
