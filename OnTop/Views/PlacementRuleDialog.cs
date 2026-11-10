using System;
using System.Windows;
using System.Windows.Controls;
using OnTop.Models;
using OnTop.Services;

namespace OnTop.Views;

/// <summary>S28 K — full-form editor for one placement rule (from testing: "no way to
/// adjust a rule except capture"). Edits a clone; OK copies back. Every field
/// has a tooltip; process and launch path have pickers.</summary>
public class PlacementRuleDialog : Window
{
    private readonly PlacementRule _r;
    private readonly TextBox _name = new(), _proc = new(), _title = new(), _mon = new(), _x = new(), _y = new(), _w = new(), _h = new(), _delay = new(), _launch = new(), _wait = new();
    private readonly CheckBox _max = new(), _start = new(), _launchOn = new();

    private PlacementRuleDialog(PlacementRule r)
    {
        _r = r;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "Placement Rule";
        Width = 560; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var g = new Grid { Margin = new Thickness(18, 14, 18, 14) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        int row = 0;
        void Row(string label, string tip, UIElement ctl)
        {
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 10, 3), ToolTip = tip };
            Grid.SetRow(l, row); Grid.SetColumn(l, 0); g.Children.Add(l);
            if (ctl is FrameworkElement fe) { fe.ToolTip ??= tip; fe.Margin = new Thickness(0, 3, 0, 3); }
            Grid.SetRow(ctl, row); Grid.SetColumn(ctl, 1); g.Children.Add(ctl); row++;
        }
        StackPanel WithPicker(TextBox tb, string btnText, string tip, Action act)
        {
            var sp = new DockPanel(); var b = new Button { Content = btnText, Margin = new Thickness(6, 0, 0, 0), ToolTip = tip }; b.Click += (_, _) => act();
            DockPanel.SetDock(b, Dock.Right); sp.Children.Add(b); sp.Children.Add(tb);
            var wrap = new StackPanel(); wrap.Children.Add(sp); return wrap;
        }
        _name.Text = r.Name; _proc.Text = r.Process; _title.Text = r.TitleContains; _mon.Text = (r.Monitor + 1).ToString();
        _x.Text = r.X.ToString(); _y.Text = r.Y.ToString(); _w.Text = r.W.ToString(); _h.Text = r.H.ToString();
        _max.IsChecked = r.Maximize; _start.IsChecked = r.ApplyOnStartup; _delay.Text = r.DelaySec.ToString();
        _launch.Text = r.LaunchPath; _launchOn.IsChecked = r.LaunchEnabled; _wait.Text = r.LaunchWaitSec.ToString();
        foreach (var cb in new[] { _max, _start, _launchOn }) cb.Style = (Style)Application.Current.FindResource("ToggleSwitch");
        Row("Name", "A Label For This Rule (Shown In Menus And Toasts)", _name);
        Row("Process (no .exe)", "The Program's Process Name, e.g. spotify. Pick From Running Programs Or Browse To The .exe.", WithPicker(_proc, "Pick…", "Choose From Running Programs Or Browse To An .exe", () => { var p = ProcessPickerDialog.Pick(this); if (p is not null) _proc.Text = p; }));
        Row("Title contains", "Optional: Only Match A Window Whose Title Contains This Text (Narrows Programs With Several Windows)", _title);
        Row("Monitor (1 = primary)", "Which Monitor To Place The Window On, Numbered Like The Zone Map", _mon);
        Row("X, Y (px from monitor top-left)", "Where The Window's Top-Left Corner Goes, In Pixels From That Monitor's Top-Left", Pair(_x, _y));
        Row("Width, Height (px)", "The Window's Size In Pixels", Pair(_w, _h));
        Row("Maximize", "Maximize On That Monitor Instead Of Using X/Y/W/H", _max);
        Row("Apply on OnTop start", "Run This Rule A Few Seconds After OnTop Launches", _start);
        Row("Delay before applying (s)", "Wait This Many Seconds Before Moving/Launching — For Programs That Are Still Starting Up (0–120)", _delay);
        Row("Launch path (optional)", "Program To START If No Matching Window Exists. Shown Verbatim; Does Nothing Unless 'Launch' Below Is Ticked.", WithPicker(_launch, "Browse…", "Pick The Program's .exe", () => { var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All Files (*.*)|*.*" }; if (dlg.ShowDialog(this) == true) _launch.Text = dlg.FileName; }));
        Row("Launch when not running", "Tick To Allow This Rule To Start The Program Above. Off = Move Only.", _launchOn);
        Row("Wait for window after launch (s)", "How Long To Keep Looking For The Window After Launching Before Giving Up (1–120)", _wait);
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var warn = new TextBlock { Text = "⚠ OnTop runs non-elevated by design and cannot move windows of programs running as administrator (the log will say 'access denied').", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("WarnAmberBrush"), FontSize = 11.5 };
        Grid.SetRow(warn, row); Grid.SetColumnSpan(warn, 2); g.Children.Add(warn); row++;
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "Save Rule", Height = 30, MinWidth = 110, Style = (Style)Application.Current.FindResource("AccentButton"), IsDefault = true };
        ok.Click += (_, _) => { if (Apply()) { DialogResult = true; Close(); } };
        var cancel = new Button { Content = "Cancel", Height = 30, MinWidth = 90, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        bar.Children.Add(ok); bar.Children.Add(cancel);
        Grid.SetRow(bar, row); Grid.SetColumnSpan(bar, 2); g.Children.Add(bar);
        Content = g;
        Helpers.EditorFooter.HideCloseButton(this);
    }
    private static StackPanel Pair(TextBox a, TextBox b) { var sp = new StackPanel { Orientation = Orientation.Horizontal }; a.Width = 90; b.Width = 90; b.Margin = new Thickness(8, 0, 0, 0); sp.Children.Add(a); sp.Children.Add(b); return sp; }
    private bool Apply()
    {
        int I(TextBox t, int lo, int hi, int dflt) => int.TryParse(t.Text.Trim(), out var v) ? Math.Clamp(v, lo, hi) : dflt;
        if (_proc.Text.Trim().Length == 0 && _title.Text.Trim().Length == 0) { ToastService.Show("Give The Rule A Process Or A Title To Match", ToastType.Warning); return false; }
        _r.Name = _name.Text.Trim().Length > 0 ? _name.Text.Trim() : "Placement";
        _r.Process = _proc.Text.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? _proc.Text.Trim()[..^4] : _proc.Text.Trim();
        _r.TitleContains = _title.Text.Trim();
        _r.Monitor = I(_mon, 1, 16, 1) - 1;
        _r.X = I(_x, -20000, 20000, 0); _r.Y = I(_y, -20000, 20000, 0); _r.W = I(_w, 50, 20000, 800); _r.H = I(_h, 30, 20000, 600);
        _r.Maximize = _max.IsChecked == true; _r.ApplyOnStartup = _start.IsChecked == true;
        _r.DelaySec = I(_delay, 0, 120, 0);
        _r.LaunchPath = _launch.Text.Trim(); _r.LaunchEnabled = _launchOn.IsChecked == true && _r.LaunchPath.Length > 0;
        _r.LaunchWaitSec = I(_wait, 1, 120, 10);
        return true;
    }
    /// <summary>Edit in place; returns true if saved.</summary>
    public static bool Edit(Window owner, PlacementRule r)
    {
        var d = new PlacementRuleDialog(r);
        if (owner is { IsVisible: true }) d.Owner = owner; else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return d.ShowDialog() == true;
    }
}
