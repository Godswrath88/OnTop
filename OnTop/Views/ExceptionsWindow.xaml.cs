using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>Two-panel exceptions editor. All mutation goes through the VM;
/// this code-behind only handles selection, filtering, and file dialogs.</summary>
public partial class ExceptionsWindow : Window
{
    private readonly MainViewModel _vm;
    private List<string> _allProcesses = new();

    public ExceptionsWindow(MainViewModel vm)
    {
        _vm = vm;
        SourceInitialized += (_, _) => Helpers.WindowPlacement.Restore(this, "exceptions", _vm.Cfg);
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "exceptions", _vm.Cfg);
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
            _vm.AddException(name);
    }

    private void ProcList_DoubleClick(object sender, RoutedEventArgs e)
    {
        if (ProcList.SelectedItem is string name) _vm.AddException(name);
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ExGrid.SelectedItem is ProcessException exception)
            _vm.RemoveException(exception);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
        if (dlg.ShowDialog(this) == true) _vm.ImportExceptions(dlg.FileName);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt", FileName = "OnTop_Exceptions.txt" };
        if (dlg.ShowDialog(this) == true) _vm.ExportExceptions(dlg.FileName);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
