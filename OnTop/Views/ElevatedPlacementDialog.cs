using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// 0.45.3: shown when a Placement rule's window belongs to a program running
/// as administrator, which OnTop (always non-elevated, SECURITY.md) cannot
/// move. One question, one answer: Turn This Rule Off, or Keep It, with
/// "Don't ask again for this placement" so a kept rule never asks twice.
/// Non-modal and never takes focus (it can fire at startup or from a zone
/// action while you're busy elsewhere). Closing it without answering saves nothing.
/// </summary>
public sealed class ElevatedPlacementDialog : Window
{

    public static void Show(PlacementRule rule, string processName, Action<bool /*turnOff*/, bool /*dontAsk*/> onAnswer)
        => new ElevatedPlacementDialog(rule, processName, onAnswer).Show();

    private ElevatedPlacementDialog(PlacementRule rule, string processName, Action<bool, bool> onAnswer)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Placement Can't Move This Window";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = true;          // findable if it ends up behind something
        ShowActivated = false;         // never steals focus
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new StackPanel { Margin = new Thickness(20, 16, 20, 18) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        row.Children.Add(new TextBlock
        {
            Text = "",   // MDL2 Admin (shield)
            FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xA2, 0x3C)),
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 12, 0),
        });
        string prog = string.IsNullOrWhiteSpace(processName) ? "That program" : processName;
        row.Children.Add(new TextBlock
        {
            Text = $"The placement \"{rule.Name}\" can't move its window: {prog} runs as administrator.\n\n" +
                   "Windows doesn't let a normal program move an administrator program's windows, and OnTop never runs as administrator. " +
                   "Most programs remember where you last put them, so placing it by hand once usually sticks.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6)),
            FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxWidth = 370,
        });
        root.Children.Add(row);

        var dontAsk = new CheckBox
        {
            Content = "Don't ask again for this placement",
            Margin = new Thickness(34, 0, 0, 14),
            ToolTip = "If You Keep The Rule, It Stays Quiet Next Time (Logged Only). Re-Enable In The Rule's Editor: 'Ask If It Runs As Admin'.",
        };
        root.Children.Add(dontAsk);

        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        var off = new Button { Content = "Turn This Rule Off", Height = 30, Style = (Style)Application.Current.FindResource("AccentButton") };
        var keep = new Button { Content = "Keep Rule", Height = 30 };
        // Esc closes WITHOUT answering (like ✕): IsCancel on Keep would make Esc
        // an answer and save "don't ask again" if the box happened to be ticked.
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; Close(); } };
        bool answered = false;
        void Answer(bool turnOff)
        {
            if (answered) return;
            answered = true;
            onAnswer(turnOff, dontAsk.IsChecked == true);
            Close();
        }
        off.Click += (_, _) => Answer(true);
        keep.Click += (_, _) => Answer(false);
        // Closing it any other way (title-bar ✕, Esc, Alt+F4, OnTop exiting) is NOT an
        // answer: nothing is saved (no disk write on the exit path), and the
        // question comes back next session.

        // Several rules failing at once: step each open dialog down-right so
        // they don't sit exactly on top of each other.
        // The slot is the lowest one no other open dialog is using (no counter
        // to leak if a dialog never shows; a closed slot is refilled first).
        Loaded += (_, _) =>
        {
            int slot = 0;
            var used = new System.Collections.Generic.HashSet<int>();
            foreach (Window w in Application.Current.Windows)
                if (w is ElevatedPlacementDialog d && !ReferenceEquals(d, this) && d.Tag is int s) used.Add(s);
            while (used.Contains(slot)) slot++;
            Tag = slot;
            var wa = SystemParameters.WorkArea;
            Left = Math.Min(wa.Right - ActualWidth, wa.Left + (wa.Width - ActualWidth) / 2 + 28 * slot);
            Top = Math.Min(wa.Bottom - ActualHeight, wa.Top + (wa.Height - ActualHeight) / 2 + 28 * slot);
        };
        Grid.SetColumn(off, 0); Grid.SetColumn(keep, 2);
        buttons.Children.Add(off); buttons.Children.Add(keep);
        root.Children.Add(buttons);
        Content = root;
    }
}
