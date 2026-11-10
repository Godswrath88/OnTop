using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using OnTop.Models;
using OnTop.Services;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// S28 Stage D — per-macro STEP EDITOR (owner request). Every recorded event is
/// a row: #, time, delay since the previous step (editable), what it is, and
/// where/which key — with a search box, Delete Selected, Remove All Mouse
/// Moves, and Save/Cancel. Edits apply to the live macro ONLY on Save. Nothing
/// here can add a step that wasn't recorded or change a step's KIND — only
/// remove steps and change timing (SECURITY.md: the verb list is unchanged).
/// </summary>
public class MacroEditorWindow : Window
{
    /// <summary>One row of the grid — a view over a MacroEvent.</summary>
    public class StepRow : INotifyPropertyChanged
    {
        public MacroEvent Ev { get; }
        public int Index { get; set; }
        public StepRow(MacroEvent ev) => Ev = ev;
        public double TimeS => Ev.T / 1000.0;
        private int _delay;
        /// <summary>Milliseconds since the previous step (editable).</summary>
        public int DelayMs { get => _delay; set { if (value < 0) value = 0; if (value > 600_000) value = 600_000; _delay = value; OnPropertyChanged(nameof(DelayMs)); } }
        public string Kind => KindName(Ev.K);
        public string Detail => DetailText(Ev);
        /// <summary>S28 Stage I: editable user note.</summary>
        public string Note { get => Ev.L ?? ""; set { Ev.L = string.IsNullOrWhiteSpace(value) ? null : (value.Length > 100 ? value[..100] : value); OnPropertyChanged(nameof(Note)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public void NotifyTime() { OnPropertyChanged(nameof(TimeS)); OnPropertyChanged(nameof(Index)); }
    }

    public static string KindName(byte k) => k switch
    {
        0 => "Mouse Move", 1 => "Left Click ↓", 2 => "Left Click ↑", 3 => "Right Click ↓", 4 => "Right Click ↑",
        5 => "Middle ↓", 6 => "Middle ↑", 7 => "Scroll Wheel", 8 => "Key ↓", 9 => "Key ↑", 10 => "Wait For Window", _ => $"Kind {k}",
    };

    public static string DetailText(MacroEvent e)
    {
        if (e.K == 10) return $"title contains \"{e.S}\" (up to {e.D / 1000.0:0.#} s)";
        if (e.K <= 6) return $"{e.X * 100.0 / 65535:0.0}% across, {e.Y * 100.0 / 65535:0.0}% down";
        if (e.K == 7) return $"{(e.D >= 0 ? "Up" : "Down")} {Math.Abs(e.D) / 120} notch(es)";
        try { return KeyInterop.KeyFromVirtualKey(e.D).ToString(); } catch (Exception) { return $"VK {e.D}"; }
    }

    private readonly MainViewModel _vm;
    private readonly Macro _macro;
    private readonly ObservableCollection<StepRow> _rows = new();
    private readonly ICollectionView _view;
    private readonly TextBox _search = new() { Width = 220, ToolTip = "Filter Steps — Type Part Of A Kind (\"key\", \"click\", \"move\") Or A Detail (A Key Name, \"notch\", A Percentage)" };
    private readonly TextBlock _summary = new() { Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly DataGrid _grid = new();
    private bool _hideMoves;

    public MacroEditorWindow(MainViewModel vm, Macro macro)
    {
        _vm = vm; _macro = macro;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = $"OnTop — Macro Steps: {macro.Name}";
        Width = 860; Height = 560; MinWidth = 640; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = true;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "macroeditor", _vm.Cfg);
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "macroeditor", _vm.Cfg);

        // Rows are built from a CLONE so Cancel is a true cancel.
        uint prev = 0;
        foreach (var ev in macro.Events)
        {
            var copy = new MacroEvent { T = ev.T, K = ev.K, X = ev.X, Y = ev.Y, D = ev.D };
            _rows.Add(new StepRow(copy) { DelayMs = (int)(ev.T - prev) });
            prev = ev.T;
        }
        Renumber();
        _view = CollectionViewSource.GetDefaultView(_rows);
        _view.Filter = o =>
        {
            if (o is not StepRow r) return false;
            if (_hideMoves && r.Ev.K == 0) return false;
            string q = _search.Text.Trim();
            if (q.Length == 0) return true;
            return r.Kind.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Detail.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Note.Contains(q, StringComparison.OrdinalIgnoreCase);
        };
        _search.TextChanged += (_, _) => { _view.Refresh(); UpdateSummary(); };

        var root = new DockPanel { Margin = new Thickness(14) };

        var intro = new TextBlock
        {
            Text = "Each row is one recorded step. Delay = how long OnTop waits after the previous step before doing this one. Edit a delay, delete steps you don't want, or strip all mouse movement (clicks still land where recorded). Nothing changes until you press Save.",
            TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 0, 0, 8),
        };
        DockPanel.SetDock(intro, Dock.Top);
        root.Children.Add(intro);

        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = false };
        var left = new StackPanel { Orientation = Orientation.Horizontal };
        left.Children.Add(new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        left.Children.Add(_search);
        var hideMoves = new CheckBox { Content = "Hide Mouse Moves", Style = (Style)Application.Current.FindResource("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), ToolTip = "Collapse The Mouse Path So Long Recordings Read As Clicks, Keys, Waits (View Only — Nothing Is Removed)" };
        hideMoves.Checked += (_, _) => { _hideMoves = true; _view.Refresh(); UpdateSummary(); };
        hideMoves.Unchecked += (_, _) => { _hideMoves = false; _view.Refresh(); UpdateSummary(); };
        left.Children.Add(hideMoves);
        left.Children.Add(_summary);
        DockPanel.SetDock(left, Dock.Left);
        bar.Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        void B(string text, string tip, Action act, bool accent = false)
        {
            var b = new Button { Content = text, Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
            if (accent) b.Style = (Style)Application.Current.FindResource("AccentButton");
            b.Click += (_, _) => act();
            right.Children.Add(b);
        }
        B("Delete Selected", "Remove The Highlighted Step(s) — Following Steps Keep Their Own Delays", DeleteSelected);
        B("Remove Mouse Moves", "Strip Every 'Mouse Move' Step (The Path). Clicks Still Land Where They Were Recorded — Much Smaller Macro", RemoveMoves);
        B("Insert Wait For Window…", "Insert A Step BEFORE The Selected Row That Pauses Until A Window With That Title Appears (Or Gives Up After A Timeout). The Only Kind Of Step That Can Be Added — It Waits, It Does Nothing.", () =>
        {
            var title = InputDialog.Prompt(this, "Wait For Window", "Wait until a window whose title CONTAINS this text is open:", "");
            if (string.IsNullOrWhiteSpace(title)) return;
            var t = InputDialog.Prompt(this, "Wait For Window", "Give up after how many seconds? (1–120)", "15");
            if (t is null || !double.TryParse(t, out var secs)) return;
            var ev = new MacroEvent { K = 10, S = title.Trim(), D = (int)Math.Clamp(secs * 1000, 100, 120_000) };
            int at = _grid.SelectedItem is StepRow sel ? _rows.IndexOf(sel) : _rows.Count;
            _rows.Insert(Math.Max(0, at), new StepRow(ev) { DelayMs = 0 });
            Retime();
        });
        B("Cancel", "Close Without Changing The Macro", () => { DialogResult = false; Close(); });
        B("Save", "Write These Steps And Delays Back Into The Macro", Save, accent: true);
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(right);
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);

        _grid.AutoGenerateColumns = false;
        _grid.CanUserAddRows = false;
        _grid.CanUserDeleteRows = false;
        _grid.SelectionMode = DataGridSelectionMode.Extended;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _grid.ItemsSource = _view;
        DataGridTextColumn Col(string header, string path, double width, bool ro = true, string? fmt = null)
        {
            var c = new DataGridTextColumn { Header = header, Binding = new Binding(path) { StringFormat = fmt, Mode = ro ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = width, IsReadOnly = ro };
            _grid.Columns.Add(c); return c;
        }
        Col("#", nameof(StepRow.Index), 50);
        Col("Time (s)", nameof(StepRow.TimeS), 80, fmt: "0.000");
        Col("Delay (ms)", nameof(StepRow.DelayMs), 90, ro: false);
        Col("What", nameof(StepRow.Kind), 130);
        var det = Col("Where / Which Key", nameof(StepRow.Detail), 0);
        det.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        Col("Note", nameof(StepRow.Note), 160, ro: false);
        _grid.CellEditEnding += (_, e) => Dispatcher.BeginInvoke(() => Retime());
        _grid.PreviewKeyDown += (_, e) => { if (e.Key == Key.Delete && Keyboard.FocusedElement is not TextBox) { DeleteSelected(); e.Handled = true; } };
        root.Children.Add(_grid);
        Content = root;
        UpdateSummary();
    }

    private void Renumber()
    {
        int i = 1; uint t = 0;
        foreach (var r in _rows) { r.Index = i++; t += (uint)r.DelayMs; r.Ev.T = t; r.NotifyTime(); }
    }
    private void Retime() { Renumber(); UpdateSummary(); }
    private void UpdateSummary()
    {
        int shown = _view.Cast<object>().Count();
        double total = _rows.Count == 0 ? 0 : _rows[^1].Ev.T / 1000.0;
        _summary.Text = $"{_rows.Count:N0} steps ({shown:N0} shown) · {total:0.0} s total";
    }
    private void DeleteSelected()
    {
        var sel = _grid.SelectedItems.Cast<StepRow>().ToList();
        if (sel.Count == 0) return;
        foreach (var r in sel) _rows.Remove(r);
        Retime();
    }
    private void RemoveMoves()
    {
        int n = _rows.Count(r => r.Ev.K == 0);
        if (n == 0) { ToastService.Show("No Mouse-Move Steps To Remove", ToastType.Info); return; }
        if (!ConfirmDialog.Confirm(this, "Remove Mouse Moves", $"Remove all {n:N0} mouse-move steps? Clicks keep their recorded positions. (Not applied until you Save.)", "Remove", "Keep")) return;
        // Fold each removed move's delay into the following step so overall timing survives.
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (_rows[i].Ev.K != 0) continue;
            if (i + 1 < _rows.Count) _rows[i + 1].DelayMs += _rows[i].DelayMs;
            _rows.RemoveAt(i);
        }
        Retime();
    }
    private void Save()
    {
        Renumber();
        _macro.Events = _rows.Select(r => r.Ev).ToList();
        _vm.Log.Action($"Macro steps edited: {_macro.Name} → {_macro.Events.Count} steps, {_macro.DurationMs / 1000.0:0.0}s", "Recording");
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        ToastService.Show($"Saved {_macro.Events.Count:N0} Steps To '{_macro.Name}'", ToastType.Success);
        DialogResult = true; Close();
    }
}
