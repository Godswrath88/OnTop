using System;
using System.Windows;
using System.Windows.Controls;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// First-run welcome tour (roadmap: onboarding). Shown once for genuinely fresh
/// installs (existing configs migrate FirstRunCompleted=true), with a short
/// what-is-what and an optional "add starter zones in each corner" offer.
/// Dismissing it (any path) marks the tour done.
/// </summary>
public class WelcomeWindow : Window
{
    private readonly MainViewModel _vm;

    public WelcomeWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "Welcome To OnTop";
        Width = 480;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;

        var root = new StackPanel { Margin = new Thickness(18) };
        void H(string t) => root.Children.Add(new TextBlock
        { Text = t, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
        void P(string t) => root.Children.Add(new TextBlock
        { Text = t, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });

        root.Children.Add(new TextBlock
        {
            Text = "Welcome To OnTop",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
        });
        P("A Quick 20-Second Tour — The Help Button Has The Full Story Anytime.");

        H("Zones");
        P("Zones are spots on your screen that react to your clicks — draw them on the map, or use Record. Double-click a zone's spot on screen to trigger its action (Exit or Stealth). On the map: hold the MIDDLE mouse button to scroll, wheel to zoom, double-click empty space to add.");
        H("Always On Top");
        P("Pin any window above everything with the Always On Top hotkey; manage every pin in Pinned..., or let Auto-Pin rules do it for you.");
        H("Your Hotkeys Right Now");
        string Hk(string k) => string.IsNullOrWhiteSpace(k) ? "not set" : k;
        P($"Always On Top: {Hk(_vm.Cfg.HotkeyToggle)}   ·   Exit OnTop: {Hk(_vm.Cfg.HotkeyExit)}   ·   Show/Hide Window: {Hk(_vm.Cfg.HotkeyConfig)}");
        P($"Pause Detection: Ctrl+Alt+P (fixed)   ·   Record/Stop Macro: {Hk(_vm.Cfg.HotkeyMacroRecord)} (when macros are on)   ·   Change Any Of These In Hotkeys…");
        H("Everything Optional Is Off Until You Turn It On");
        P("Macros, Auto-Pin, Window Placement, Action Library, Profile Rules, Hold Chords, Statistics, Updates… all live behind switches in Settings → Features (with a one-click Minimal Mode). Only pinning and zone detection are always on.");
        H("Lives In The Tray");
        P("Closing the window keeps OnTop running in the tray. Profiles, pausing, and quick actions all live in the tray menu too.");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        var starter = new Button
        {
            Content = "Add 4 Starter Zones",
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Puts A Small Zone In Each Corner Of Your Primary Monitor (Group: Starter) — Easy To Play With, One Undo Removes Them All",
        };
        starter.Click += (_, _) =>
        {
            _vm.AddStarterZones();
            starter.IsEnabled = false;
            starter.Content = "Starter Zones Added ✓";
            _vm.RequestMainWindow();   // S28: show the map with the new zones on it
        };
        var done = new Button { Content = "Get Started", ToolTip = "Close The Tour And Open OnTop (The Tour Won't Show Again)" };
        done.Click += (_, _) => { Close(); _vm.RequestMainWindow(); };
        actions.Children.Add(starter);
        actions.Children.Add(done);
        root.Children.Add(actions);
        Content = root;

        Closed += (_, _) =>
        {
            _vm.Cfg.FirstRunCompleted = true;
            _vm.Settings.SaveNoBackup(_vm.Cfg);
            _vm.Log.Info("Welcome tour completed", "General");
        };
    }
}
