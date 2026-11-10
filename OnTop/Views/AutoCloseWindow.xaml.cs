using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>Two-panel auto-close trigger editor. Mutation goes through the VM;
/// this code-behind only handles selection, filtering, and file dialogs.</summary>
public partial class AutoCloseWindow : Window
{
    private readonly MainViewModel _vm;
    private List<string> _allProcesses = new();

    public AutoCloseWindow(MainViewModel vm)
    {
        _vm = vm;
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "autoclose", _vm.Cfg);
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "autoclose", _vm.Cfg);
        DataContext = vm;
        InitializeComponent();
        RefreshProcesses();
    }

    private void RefreshProcesses()
    {
        _allProcesses = _vm.GetRunningProcessNames();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string filter = FilterBox.Text.Trim();
        ProcList.ItemsSource = filter.Length == 0
            ? _allProcesses
            : _allProcesses.Where(n => n.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshProcesses();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        foreach (var name in ProcList.SelectedItems.Cast<string>().ToList())
            _vm.AddAutoCloseTrigger(name);
    }

    private void ProcList_DoubleClick(object sender, RoutedEventArgs e)
    {
        if (ProcList.SelectedItem is string name) _vm.AddAutoCloseTrigger(name);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var name in TrigList.SelectedItems.Cast<string>().ToList())
            _vm.RemoveAutoCloseTrigger(name);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
        if (dlg.ShowDialog(this) == true) _vm.ImportAutoClose(dlg.FileName);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = "OnTop_AutoClose.txt" };
        if (dlg.ShowDialog(this) == true) _vm.ExportAutoClose(dlg.FileName);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
