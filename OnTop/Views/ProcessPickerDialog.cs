using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OnTop.Helpers;

namespace OnTop.Views;

/// <summary>S28 K — pick a process from what's running (visible windows, grouped
/// by process) or browse to an .exe. Returns the process NAME (no .exe), which is
/// what every matcher in OnTop compares against. Shared by Auto-Pin and Placement.</summary>
public class ProcessPickerDialog : Window
{
    private string? _result;
    private ProcessPickerDialog()
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "Pick A Process";
        Width = 520; Height = 460; MinWidth = 380; MinHeight = 300; Topmost = true; ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(14) };
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 8), ToolTip = "Type To Filter By Process Or Window Title" };
        DockPanel.SetDock(search, Dock.Top); root.Children.Add(search);
        var list = new ListBox();
        var all = Win32.GetVisibleWindows().Where(w => !Win32.IsOwnWindow(w.Hwnd) && w.Title.Length > 0 && !Win32.IsCloaked(w.Hwnd))
            .GroupBy(w => w.Process, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Process: g.Key, Title: g.First().Title, Count: g.Count()))
            .OrderBy(x => x.Process, StringComparer.OrdinalIgnoreCase).ToList();
        void Fill()
        {
            string q = search.Text.Trim(); list.Items.Clear();
            foreach (var (proc, title, n) in all.Where(x => q.Length == 0 || x.Process.Contains(q, StringComparison.OrdinalIgnoreCase) || x.Title.Contains(q, StringComparison.OrdinalIgnoreCase)))
                list.Items.Add(new ListBoxItem { Content = $"{proc}   —   {title}{(n > 1 ? $"  (+{n - 1} more)" : "")}", Tag = proc });
        }
        search.TextChanged += (_, _) => Fill();
        list.MouseDoubleClick += (_, _) => Accept();
        void Accept() { if (list.SelectedItem is ListBoxItem li) { _result = (string)li.Tag; DialogResult = true; Close(); } }
        var bar = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
        var browse = new Button { Content = "Browse .exe…", ToolTip = "Pick The Program's .exe File Instead — Its File Name Becomes The Process Name" };
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Programs (*.exe)|*.exe|All Files (*.*)|*.*" };
            if (dlg.ShowDialog(this) == true) { _result = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName); DialogResult = true; Close(); }
        };
        var ok = new Button { Content = "Use Selected", Margin = new Thickness(8, 0, 0, 0), Style = (Style)Application.Current.FindResource("AccentButton"), IsDefault = true };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        DockPanel.SetDock(browse, Dock.Left); DockPanel.SetDock(cancel, Dock.Right); DockPanel.SetDock(ok, Dock.Right);
        bar.Children.Add(browse); bar.Children.Add(cancel); bar.Children.Add(ok);
        DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        root.Children.Add(list);
        Content = root;
        Helpers.EditorFooter.HideCloseButton(this);
        Loaded += (_, _) => { Fill(); search.Focus(); };
    }
    /// <summary>Returns a process name, or null if cancelled.</summary>
    public static string? Pick(Window? owner)
    {
        var d = new ProcessPickerDialog();
        if (owner is { IsVisible: true }) d.Owner = owner; else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return d.ShowDialog() == true ? d._result : null;
    }
}
