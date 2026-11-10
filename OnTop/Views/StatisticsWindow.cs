using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OnTop.Helpers;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Statistics — this session + lifetime usage counters and the most-triggered
/// zones. Read-only except Reset (confirmed). Everything shown lives in
/// %APPDATA%\OnTop\config\stats.json and never leaves the machine.
/// No hotkey by design (standing rule decision: an occasional-glance window
/// doesn't earn a binding; the APP button is its home).
/// </summary>
public class StatisticsWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly StackPanel _body;

    public StatisticsWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Statistics";
        Width = 460; Height = 480;
        MinWidth = 360; MinHeight = 300;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = vm;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });

        var root = new DockPanel { Margin = new Thickness(12) };

        var bar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        var refresh = new Button { Content = "Refresh", Margin = new Thickness(0, 0, 6, 0), ToolTip = "Re-Read The Counters" };
        refresh.Click += (_, _) => Rebuild();
        var csv = new Button { Content = "Export CSV…", Margin = new Thickness(0, 0, 6, 0), ToolTip = "Save Counters, Per-Zone Triggers, And The Recent Activity Feed As A CSV File" };
        csv.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "OnTop_Stats.csv" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var st = _vm.Stats.Stats;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("section,key,value");
                foreach (var p in typeof(Models.AppStats).GetProperties().Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(long) || p.PropertyType == typeof(int)))
                    sb.AppendLine($"counter,{p.Name},{p.GetValue(st)}");
                foreach (var kv in st.PerZoneTriggers) sb.AppendLine($"zone,\"{st.PerZoneNames.GetValueOrDefault(kv.Key, kv.Key).Replace("\"", "\"\"")}\",{kv.Value}");
                foreach (var ev in st.RecentEvents) sb.AppendLine($"event,{ev.Kind},\"{(ev.Detail ?? "").Replace("\"", "\"\"")}\"");
                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
                _vm.Log.Action($"Statistics exported: {dlg.FileName}", "General");
                Services.ToastService.Show("Statistics Exported", Services.ToastType.Success);
            }
            catch (Exception ex) { _vm.Log.Error($"Stats export failed: {ex.Message}", "General"); Services.ToastService.Show("Export Failed — See Log", Services.ToastType.Error); }
        };
        var reset = new Button { Content = "Reset Statistics", ToolTip = "Wipe All Counters (Settings And Zones Are Untouched)" };
        reset.Click += (_, _) =>
        {
            if (ConfirmDialog.Confirm(this, "Reset Statistics",
                "Wipe all usage statistics?\n\nSettings, zones, and profiles are not affected.",
                "Reset", "Cancel"))
            {
                _vm.Stats.Reset();
                Rebuild();
            }
        };
        DockPanel.SetDock(refresh, Dock.Left);
        DockPanel.SetDock(csv, Dock.Left);
        bar.Children.Add(csv);
        DockPanel.SetDock(reset, Dock.Right);
        bar.Children.Add(refresh);
        bar.Children.Add(reset);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        _body = new StackPanel();
        root.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });
        Content = root;
        Helpers.EditorFooter.Wrap(this, root, onSave: null, onCancel: null, saveText: "Close");
        Rebuild();
    }

    private void Header(string text)
        => _body.Children.Add(new TextBlock
        {
            Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Opacity = 0.65, Margin = new Thickness(0, 10, 0, 4),
        });

    private void Row(string label, string value)
    {
        var g = new DockPanel { Margin = new Thickness(4, 1, 4, 1) };
        var v = new TextBlock { Text = value, FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(v, Dock.Right);
        g.Children.Add(v);
        g.Children.Add(new TextBlock { Text = label, Opacity = 0.8 });
        _body.Children.Add(g);
    }

    private static string Pretty(TimeSpan t)
        => t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m"
         : t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes}m"
         : $"{(int)t.TotalMinutes}m {t.Seconds}s";

    private void Rebuild()
    {
        _body.Children.Clear();
        var s = _vm.Stats.Stats;
        var svc = _vm.Stats;

        Header("THIS SESSION");
        Row("Uptime", Pretty(DateTime.Now - svc.SessionStart));
        Row("Zone Triggers", svc.SessionZoneTriggers.ToString());
        Row("Windows Pinned", svc.SessionPins.ToString());

        Header("LIFETIME" + (s.LastReset is { } r ? $"  (Since Reset {r:yyyy-MM-dd})" : $"  (Since {s.FirstRun:yyyy-MM-dd})"));
        Row("Launches", s.Launches.ToString());
        Row("Total Runtime", Pretty(TimeSpan.FromSeconds(s.TotalRuntimeSeconds) + (DateTime.Now - svc.SessionStart)));
        Row("Zone Triggers", s.ZoneTriggers.ToString());
        Row("…Of Which Stealth", s.StealthTriggers.ToString());
        Row("Windows Pinned", s.Pins.ToString());
        Row("Windows Unpinned", s.Unpins.ToString());
        Row("Auto-Pins (Rules)", s.RulePins.ToString());
        Row("Peeks", s.Peeks.ToString());
        Row("Profile Switches", s.ProfileSwitches.ToString());

        Header("RECENT ACTIVITY (LAST 25)");
        var recent = s.RecentEvents;
        if (recent.Count == 0)
        {
            _body.Children.Add(new TextBlock { Text = "No Activity Recorded Yet", Opacity = 0.5, Margin = new Thickness(4, 1, 4, 1) });
        }
        else
        {
            for (int i = recent.Count - 1; i >= Math.Max(0, recent.Count - 25); i--)
            {
                var ev = recent[i];
                Row($"{ev.When:HH:mm:ss}  {ev.Kind}" + (ev.Detail.Length > 0 ? $" — {ev.Detail}" : ""), "");
            }
        }

        Header("MOST-TRIGGERED ZONES");
        var top = s.PerZoneTriggers.OrderByDescending(kv => kv.Value).Take(5).ToList();
        if (top.Count == 0)
        {
            _body.Children.Add(new TextBlock { Text = "No Zone Triggers Recorded Yet", Opacity = 0.5, Margin = new Thickness(4, 1, 4, 1) });
        }
        else
        {
            long max = top[0].Value;
            foreach (var kv in top)
            {
                string name = s.PerZoneNames.GetValueOrDefault(kv.Key, "");
                bool gone = !_vm.Zones.Any(z => z.Id == kv.Key);
                Row((string.IsNullOrEmpty(name) ? kv.Key : name) + (gone ? "  (Deleted)" : ""), kv.Value.ToString());
                // S28 Stage G: a proportional bar under each row (a simple "heat" view).
                _body.Children.Add(new Border
                {
                    Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(4, 0, 4, 6), HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(6, 320.0 * kv.Value / Math.Max(1, max)),
                    Background = (System.Windows.Media.Brush)Application.Current.FindResource("AccentBlueBrush"), Opacity = 0.8,
                });
            }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowPlacement.Restore(this, "stats", _vm.Cfg);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        WindowPlacement.Save(this, "stats", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }
}
