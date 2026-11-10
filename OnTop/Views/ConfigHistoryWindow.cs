using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>S28 Stage I — browse the rotating .bak1–3 and timestamped backups
/// with dates/sizes; Restore uses the normal (own-path → trusted) restore.</summary>
public class ConfigHistoryWindow : Window
{
    public ConfigHistoryWindow(MainViewModel vm)
    {
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Config History";
        Width = 560; Height = 420; MinWidth = 420; MinHeight = 260; Topmost = true; ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(12) };
        var list = new ListBox();
        var files = new System.Collections.Generic.List<FileInfo>();
        try
        {
            string cfg = Path.Combine(vm.Settings.ConfigDir, "settings.json");
            foreach (var suffix in new[] { ".bak1", ".bak2", ".bak3" }) if (File.Exists(cfg + suffix)) files.Add(new FileInfo(cfg + suffix));
            files.AddRange(new DirectoryInfo(vm.Settings.BackupsDirectory).GetFiles("OnTop_Backup_*.json"));
        }
        catch (Exception ex) { vm.Log.Warn($"Config history listing: {ex.Message}", "Config"); }
        foreach (var f in files.OrderByDescending(f => f.LastWriteTime))
            list.Items.Add(new ListBoxItem { Content = $"{f.LastWriteTime:yyyy-MM-dd HH:mm:ss}   {f.Length / 1024.0:0.0} KB   {f.Name}", Tag = f.FullName });
        var bar = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        var restore = new Button { Content = "Restore Selected…", Margin = new Thickness(0, 0, 8, 0), Style = (Style)Application.Current.FindResource("AccentButton"), ToolTip = "Replace Current Settings With This Snapshot (A Safety Backup Is Written First; Undo Available)" };
        restore.Click += (_, _) =>
        {
            if (list.SelectedItem is not ListBoxItem li) return;
            if (!ConfirmDialog.Confirm(this, "Restore Snapshot", $"Restore {Path.GetFileName((string)li.Tag)}? Current settings are backed up first.", "Restore", "Cancel")) return;
            vm.RestoreFrom((string)li.Tag); Close();
        };
        var open = new Button { Content = "Open Folder", ToolTip = "Open The Config Folder In Explorer" };
        open.Click += (_, _) => vm.OpenConfigFolderCmd.Execute(null);
        bar.Children.Add(restore); bar.Children.Add(open);
        DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
        root.Children.Add(list.Items.Count == 0 ? new TextBlock { Text = "No Backups Yet.", Opacity = 0.5 } : list);
        Content = root;
        Helpers.EditorFooter.Wrap(this, root, onSave: null, onCancel: null, saveText: "Close");
    }
}
