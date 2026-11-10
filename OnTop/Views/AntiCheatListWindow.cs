using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// S28 Stage D — per-entry toggles for the built-in known anti-cheat list
/// (from testing: "if a certain one doesn't fuss about it, a user can toggle
/// it off"). Ticked = guarded (refuse/abort/kill-switch); unticked = ignored.
/// Writes Cfg.MacroGuardIgnored. Select All / None for convenience.
/// </summary>
public class AntiCheatListWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly List<CheckBox> _boxes = new();

    public AntiCheatListWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Known Anti-Cheat Guard List";
        Width = 520; Height = 520; MinWidth = 420; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        Topmost = true;

        var root = new DockPanel { Margin = new Thickness(14) };
        var intro = new TextBlock
        {
            Text = "While any TICKED process is running, macro playback is refused (and the kill switch trips if one appears mid-run). Untick a service that never minds macros on your setup — some (PunkBuster's PnkBstrA, for example) run permanently as a background service even when no game is open.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(intro, Dock.Top);
        root.Children.Add(intro);
        var warn = new TextBlock
        {
            Text = "⚠ Unticking removes a safety net. Synthetic input around an anti-cheat you unticked can get an account banned.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8),
            Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("WarnAmberBrush"),
        };
        DockPanel.SetDock(warn, Dock.Top);
        root.Children.Add(warn);

        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        void B(string t, string tip, System.Action a) { var b = new Button { Content = t, Margin = new Thickness(0, 0, 8, 6), ToolTip = tip }; b.Click += (_, _) => a(); bar.Children.Add(b); }
        B("Guard All", "Tick Every Entry (Safest)", () => { foreach (var cb in _boxes) cb.IsChecked = true; });
        B("Guard None", "Untick Every Entry — Only Your Stealth Watched List Guards", () => { foreach (var cb in _boxes) cb.IsChecked = false; });
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        var list = new StackPanel();
        foreach (var name in MacroService.KnownAntiCheat)
        {
            bool running = false;
            try { var p = System.Diagnostics.Process.GetProcessesByName(name); running = p.Length > 0; foreach (var x in p) x.Dispose(); }
            catch (System.Exception ex) { _vm.Log.Debug($"AC list probe {name}: {ex.Message}", "Recording"); }
            var cb = new CheckBox
            {
                Content = running ? $"{name}   (running now)" : name,
                Style = (Style)Application.Current.FindResource("ToggleSwitch"),
                Margin = new Thickness(0, 2, 0, 2),
                IsChecked = !_vm.Cfg.MacroGuardIgnored.Exists(x => string.Equals(x, name, System.StringComparison.OrdinalIgnoreCase)),
                Tag = name,
                ToolTip = running ? "This Process Is Running Right Now — Playback Is Being Refused Because Of It While Ticked" : "Guard Against This Process",
            };
            _boxes.Add(cb);
            list.Children.Add(cb);
        }
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;

        bool cancelled = false;
        Helpers.EditorFooter.Wrap(this, root, onSave: null, onCancel: () => { cancelled = true; });
        Closed += (_, _) =>
        {
            if (cancelled) { _vm.Log.Action("Known anti-cheat list: changes discarded", "Recording"); return; }
            var ignored = _boxes.Where(b => b.IsChecked != true).Select(b => (string)b.Tag).ToList();
            var before = string.Join(",", _vm.Cfg.MacroGuardIgnored);
            _vm.Cfg.MacroGuardIgnored = ignored;
            if (before != string.Join(",", ignored))
                _vm.Log.Action($"Known anti-cheat guard exclusions: {(ignored.Count == 0 ? "(none)" : string.Join(", ", ignored))}", "Recording");
            _vm.Settings.SaveNoBackup(_vm.Cfg);
        };
    }
}
