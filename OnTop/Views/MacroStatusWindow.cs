using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OnTop.Helpers;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// S28 Stage D — the persistent, always-on-top, never-activating recording
/// status bar (requirement: recording must be hotkey-driven, with a visible
/// progress strip). States: READY (empty gray bar, "press hotkey"), RECORDING
/// (red marquee + elapsed + event count), SAVED (full green bar ~3 s),
/// PLAYING (blue marquee). Draggable; position remembered ("macrostatus").
/// Shown only while macros are enabled AND Cfg.ShowMacroStatusBar. It polls
/// MacroService 5×/s — cheap, no hooks, nothing but reads.
/// </summary>
public class MacroStatusWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly TextBlock _text = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ProgressBar _bar = new() { Height = 7, Minimum = 0, Maximum = 100, BorderThickness = new Thickness(0) };
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTime _savedUntil = DateTime.MinValue;
    private int _lastSavedSeen = -1;

    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xE6, 0x3C, 0x3C));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71));
    private static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x66));

    private readonly Action _onDismiss;
    public MacroStatusWindow(MainViewModel vm, Action onDismiss)
    {
        _vm = vm; _onDismiss = onDismiss;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Width = 360; Height = 54;
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 12; Top = wa.Bottom - Height - 12;

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x1A, 0x28)),
            BorderBrush = (Brush)Application.Current.FindResource("AccentBlueBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            ToolTip = "Macro Recording Status — Drag To Move. Turn Off In The Macros Window (Show Status Bar).",
        };
        var stack = new StackPanel();
        _text.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD6));
        // S28 K: × to hide (until the next recording) + the hide hotkey if one is bound.
        var top = new DockPanel();
        var close = new Button { Content = "✕", Width = 20, Height = 18, Padding = new Thickness(0), FontSize = 10, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Hide This Strip Until The Next Recording Or Playback" + (string.IsNullOrWhiteSpace(_vm.Cfg.HotkeyMacroStatus) ? " (Bind A Hotkey Under 'Macro Status Strip' In Hotkeys…)" : $" (Hotkey: {_vm.Cfg.HotkeyMacroStatus})") };
        close.Click += (_, _) => _onDismiss();
        DockPanel.SetDock(close, Dock.Right);
        top.Children.Add(close);
        if (!string.IsNullOrWhiteSpace(_vm.Cfg.HotkeyMacroStatus))
        {
            var hkLbl = new TextBlock { Text = _vm.Cfg.HotkeyMacroStatus, FontSize = 9.5, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            DockPanel.SetDock(hkLbl, Dock.Right); top.Children.Add(hkLbl);
        }
        top.Children.Add(_text);
        stack.Children.Add(top);
        _bar.Margin = new Thickness(0, 5, 0, 0);
        _bar.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3C));
        stack.Children.Add(_bar);
        border.Child = stack;
        Content = border;

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) { /* DragMove after release — benign */ } } };
        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int ex = Win32.GetWindowLong(h, Win32.GWL_EXSTYLE);
            Win32.SetWindowLong(h, Win32.GWL_EXSTYLE, ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
            WindowPlacement.Restore(this, "macrostatus", _vm.Cfg);
        };
        Closing += (_, _) => { _poll.Stop(); WindowPlacement.Save(this, "macrostatus", _vm.Cfg); };
        _poll.Tick += (_, _) => Update();
        _poll.Start();
        Update();
    }

    private void Update()
    {
        var svc = _vm.MacroSvc;
        string hk = string.IsNullOrWhiteSpace(_vm.Cfg.HotkeyMacroRecord) ? "(no record hotkey set)" : _vm.Cfg.HotkeyMacroRecord;
        if (svc.IsRecording)
        {
            var t = TimeSpan.FromMilliseconds(svc.RecordingElapsedMs);
            _text.Text = $"● RECORDING  {t:mm\\:ss} · {svc.RecordingEventCount:N0} events — {hk} stops";
            _bar.IsIndeterminate = true; _bar.Foreground = Red;
            return;
        }
        if (svc.PlayingId is not null)
        {
            _text.Text = svc.IsPaused ? "⏸ PAUSED — pause hotkey resumes, Ctrl+Alt+X stops"
                : svc.DryStep is string ds ? $"▶ DRY RUN  {ds}"
                : svc.CountdownRemaining > 0 ? $"▶ STARTING IN {svc.CountdownRemaining}…  hands off the keyboard"
                : svc.WaitingFor is string wf ? $"▶ WAITING for window '{wf}' — Ctrl+Alt+X stops"
                : $"▶ PLAYING  {svc.Playing?.Name ?? "macro"} — Ctrl+Alt+X stops";
            _bar.IsIndeterminate = true; _bar.Foreground = (Brush)Application.Current.FindResource("AccentBlueBrush");
            return;
        }
        if (svc.LastSaved is { } saved && saved.Events != _lastSavedSeen)
        {
            _lastSavedSeen = saved.Events;
            _savedUntil = DateTime.Now.AddSeconds(3);
        }
        if (DateTime.Now < _savedUntil && svc.LastSaved is { } s)
        {
            _text.Text = $"✔ SAVED  '{s.Name}' — {s.Events:N0} events";
            _bar.IsIndeterminate = false; _bar.Value = 100; _bar.Foreground = Green;
            return;
        }
        _text.Text = $"READY — {hk} to record";
        _bar.IsIndeterminate = false; _bar.Value = 0; _bar.Foreground = Gray;
    }
}
