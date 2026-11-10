using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>Pop-out zone list — same VM and selection as the main window.</summary>
public partial class ZoneListWindow : Window
{
    private readonly MainViewModel _vm;

    public ZoneListWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        PreviewMouseDown += (_, e) => Helpers.GridEdit.CommitOnOutsideClick(ZonesGrid, e);
        ZonesGrid.PreviewMouseRightButtonUp += (_, e) => ZoneContextMenuFactory.ShowForRow(_vm, this, ZonesGrid, e);
        ZonesGrid.PreviewKeyDown += (_, e) => ZoneListKeys.Handle(e, _vm, this, ZonesGrid);
        ZonesGrid.MouseDoubleClick += (_, _) =>
        {
            if (_vm.SelectedZone is { } z) _vm.RequestFlashZone(z);
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Helpers.WindowPlacement.Restore(this, "list", _vm.Cfg);
        Helpers.GridColumns.Restore(ZonesGrid, "list", _vm.Cfg);
        Helpers.GridEdit.AttachFocusCommit(ZonesGrid);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        Helpers.WindowPlacement.Save(this, "list", _vm.Cfg);
        Helpers.GridColumns.Save(ZonesGrid, "list", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);   // persist placement now
        base.OnClosing(e);
    }

    private void ZonesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is Zone zone)
            _vm.OnZoneSelected(zone);
    }

    private void ZonesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column.Header as string == "Group")
            Dispatcher.BeginInvoke(new System.Action(_vm.RefreshGroupNames));
    }

    private void Swatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            _vm.CycleZoneColor(zone);
    }

    private void ZoneAction_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            _vm.CycleZoneAction(zone);
    }

    private void LocateCell_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Zone zone)
            _vm.RequestFlashZone(zone);
    }
}
