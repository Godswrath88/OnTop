using System;
using System.IO.Compression;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Live session log viewer (v9.5 parity): entries appear as they happen, with
/// level filter, text search, follow-scroll, the Verbose (DEBUG) toggle, and
/// per-category tracking checkboxes that control what gets logged at all
/// (persisted via AppSettings.DisabledLogCategories; errors always log).
/// </summary>
public partial class LogViewerWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly ICollectionView _view;

    public LogViewerWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();

        _view = CollectionViewSource.GetDefaultView(vm.Log.Entries);
        _view.Filter = FilterEntry;
        LogList.ItemsSource = _view;

        BuildCategoryChecks();
        vm.Log.Entries.CollectionChanged += OnEntriesChanged;
    }

    // ── Filtering ───────────────────────────────────────────────────────
    private bool FilterEntry(object item)
    {
        if (item is not LogEntry entry) return false;

        string level = (LevelBox.SelectedItem as ComboBoxItem)?.Content as string ?? "All";
        if (level != "All" && !entry.Level.Equals(level, StringComparison.OrdinalIgnoreCase))
            return false;

        string search = SearchBox.Text.Trim();
        if (search.Length > 0 &&
            !entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            !entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        // The ComboBox raises SelectionChanged during InitializeComponent,
        // before the view exists.
        if (!IsLoaded) return;
        _view.Refresh();
    }

    // ── Category tracking ───────────────────────────────────────────────
    private void BuildCategoryChecks()
    {
        foreach (var category in LogService.KnownCategories)
        {
            string cat = category;
            var check = new CheckBox
            {
                Content = cat,
                Margin = new Thickness(0, 0, 14, 4),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = !_vm.Cfg.DisabledLogCategories.Contains(cat, StringComparer.OrdinalIgnoreCase)
            };
            check.Checked += (_, _) => SetCategory(cat, true);
            check.Unchecked += (_, _) => SetCategory(cat, false);
            CategoryPanel.Children.Add(check);
        }
    }

    private void SetCategory(string category, bool enabled)
    {
        var disabled = _vm.Cfg.DisabledLogCategories;
        if (enabled)
            disabled.RemoveAll(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
        else if (!disabled.Contains(category, StringComparer.OrdinalIgnoreCase))
            disabled.Add(category);
        _vm.Log.Action($"Log tracking {(enabled ? "enabled" : "disabled")}: {category}", "Settings");
    }

    // ── Live behavior ───────────────────────────────────────────────────
    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (FollowBox.IsChecked != true) return;
        ScrollToNewest();
    }

    /// <summary>Scroll to the newest entry. Deferred at Background priority:
    /// calling ScrollIntoView synchronously inside CollectionChanged runs before
    /// the virtualized list has generated the new row, so it silently no-ops —
    /// which made Follow appear to do nothing.</summary>
    private void ScrollToNewest()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (LogList.Items.Count > 0)
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Re-checking Follow jumps to the newest entry immediately.</summary>
    private void Follow_Checked(object sender, RoutedEventArgs e) => ScrollToNewest();

    // ── Copy (Ctrl+C / right-click) ─────────────────────────────────────
    private static string Fmt(LogEntry en)
        => $"{en.Time:yyyy-MM-dd HH:mm:ss.fff}  [{en.Level,-6}] [{en.Category}] {en.Message}";

    private void LogList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.C &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            CopySelected_Click(sender, e);
            e.Handled = true;
        }
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e)
    {
        var lines = LogList.SelectedItems.Cast<LogEntry>().OrderBy(en => en.Time).Select(Fmt);
        var text = string.Join(Environment.NewLine, lines);
        if (text.Length > 0)
            try { Clipboard.SetText(text); } catch { /* clipboard busy — ignore */ }
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var lines = LogList.Items.Cast<LogEntry>().Select(Fmt);
        var text = string.Join(Environment.NewLine, lines);
        if (text.Length > 0)
            try { Clipboard.SetText(text); } catch { /* clipboard busy — ignore */ }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _vm.Log.Entries.Clear();

    /// <summary>One-click bug-report bundle: settings.json, stats.json, the
    /// current log file, and a diag.txt with app/system info. User picks where
    /// it goes (SaveFileDialog) — nothing is sent anywhere (SECURITY.md: no
    /// network; the bundle contains the user's own config, no secrets).</summary>
    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Diagnostics Bundle",
            FileName = $"OnTop_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            Filter = "Zip Archive (*.zip)|*.zip",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            Services.DiagnosticsService.WriteBundle(_vm, dlg.FileName);
            _vm.Log.Action($"Diagnostics bundle exported: {dlg.FileName}", "System");
            Services.ToastService.Show("Diagnostics Bundle Exported", Services.ToastType.Success);
        }
        catch (Exception ex)
        {
            _vm.Log.Error($"Diagnostics export failed: {ex.Message}", "System");
            Services.ToastService.Show("Diagnostics Export Failed — See Log", Services.ToastType.Error);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
        => _vm.OpenLogsFolderCmd.Execute(null);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Helpers.WindowPlacement.Restore(this, "logs", _vm.Cfg);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Helpers.WindowPlacement.Save(this, "logs", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _vm.Log.Entries.CollectionChanged -= OnEntriesChanged;
        base.OnClosed(e);
    }
}
