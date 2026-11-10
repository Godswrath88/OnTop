using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 K — press-to-capture hotkey prompt for macros and library actions
/// (replaces typing a combo into a text box). While it is open, EVERY global
/// hotkey is suspended so the keys you press are captured instead of executed;
/// a conflicting combo is refused the moment you press it. Esc clears.</summary>
public class HotkeyCaptureDialog : Window
{
    private string _combo;
    private readonly TextBlock _shown = new() { FontSize = 20, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _msg = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75, MinHeight = 34 };
    private readonly Func<string, string?> _conflict;

    private HotkeyCaptureDialog(string title, string current, Func<string, string?> conflict)
    {
        _combo = current; _conflict = conflict;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = title; Width = 420; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sp = new StackPanel { Margin = new Thickness(20, 14, 20, 16) };
        sp.Children.Add(new TextBlock { Text = "Press the key combination you want. All OnTop hotkeys are paused while this is open, so nothing fires. Esc = clear (no hotkey).", TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        sp.Children.Add(_shown); sp.Children.Add(_msg);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var ok = new Button { Content = "Use This Hotkey", Height = 30, MinWidth = 120, Style = (Style)Application.Current.FindResource("AccentButton") };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var clear = new Button { Content = "Clear", Height = 30, MinWidth = 70, Margin = new Thickness(8, 0, 0, 0) };
        clear.Click += (_, _) => { _combo = ""; Render(); };
        var cancel = new Button { Content = "Cancel", Height = 30, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        bar.Children.Add(ok); bar.Children.Add(clear); bar.Children.Add(cancel);
        sp.Children.Add(bar);
        Content = sp;
        Helpers.EditorFooter.HideCloseButton(this);
        Focusable = true;
        PreviewKeyDown += OnKey;
        Loaded += (_, _) => { Focus(); Render(); };
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { _combo = ""; Render(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var parts = new List<string>();
        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        string candidate = string.Join("+", parts);
        var c = _conflict(candidate);
        if (c is not null) { _msg.Text = "✖ " + c; _msg.Foreground = (Brush)Application.Current.FindResource("WarnAmberBrush"); return; }
        _combo = candidate; Render();
    }
    private void Render()
    {
        _shown.Text = _combo.Length == 0 ? "(no hotkey)" : _combo;
        _msg.Text = _combo.Length == 0 ? "No hotkey. Press a combination, or Use This Hotkey to save it cleared." : "✔ Available. Press another combination to change it.";
        _msg.Foreground = (Brush)Application.Current.FindResource("FgMutedBrush");
    }
    /// <summary>Returns the combo ("" = cleared) or null if cancelled. Suspends global hotkeys for the duration.</summary>
    public static string? Prompt(Window owner, MainViewModel vm, string title, string current, Func<string, string?> conflict)
    {
        var d = new HotkeyCaptureDialog(title, current, conflict);
        if (owner is { IsVisible: true }) d.Owner = owner; else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        vm.SuspendGlobalHotkeys();
        try { return d.ShowDialog() == true ? d._combo : null; }
        finally { vm.ResumeGlobalHotkeys(); }
    }
}
