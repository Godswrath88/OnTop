using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using OnTop.Controls;
using OnTop.ViewModels;

namespace OnTop.Views;

/// <summary>
/// Resizable pop-out of the zone map. Binds the same collections and the same
/// SelectedZone as the main window, so selection stays in sync across the main
/// map, the list, and every pop-out. Honors the Keep GUIs On Top setting.
/// </summary>
public class MapWindow : Window
{
    private readonly MainViewModel _vm;

    public MapWindow(MainViewModel vm)
    {
        _vm = vm;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "OnTop — Zone Map";
        Width = 780; Height = 460;
        MinWidth = 420; MinHeight = 260;
        ShowInTaskbar = true;    // sub-GUIs surface in taskbar hover (S22)
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        DataContext = vm;
        SetBinding(TopmostProperty, new Binding(nameof(MainViewModel.Cfg) + ".GuiAlwaysOnTop") { Source = vm });

        var canvas = new ZoneMapCanvas { Margin = new Thickness(12) };
        canvas.SetBinding(ZoneMapCanvas.ItemsSourceProperty, new Binding(nameof(MainViewModel.Zones)) { Source = vm });
        canvas.SetBinding(ZoneMapCanvas.SelectedZoneProperty,
            new Binding(nameof(MainViewModel.SelectedZone)) { Source = vm, Mode = BindingMode.TwoWay });
        canvas.HiddenMonitors = vm.Cfg.HiddenMonitors;
        canvas.ZoneDragStarted += _ => vm.BeginZoneDrag();
        canvas.ZonePreviewRequested += z => vm.BeginZonePreviewFor(z);
        canvas.ZoneDragCompleted += _ => vm.EndZonePreview();
        canvas.ZoneDragCompleted += z => vm.EndZoneDrag(z);
        canvas.CreateZoneRequested += (x, y) => vm.AddZoneAt(x, y);
        canvas.ZoneContextRequested += z => ZoneContextMenuFactory.Show(vm, this, canvas, z);
        canvas.MonitorContextRequested += _ =>
        {
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = canvas };
            var reset = new System.Windows.Controls.MenuItem { Header = "Reset Zoom / Pan" };
            reset.Click += (_, _) => canvas.ResetView();
            menu.Items.Add(reset);
            menu.IsOpen = true;
        };
        vm.DisplaysChanged += canvas.InvalidateVisual;
        Closed += (_, _) => vm.DisplaysChanged -= canvas.InvalidateVisual;
        canvas.MapBlankContextRequested += () =>
        {
            var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = canvas };
            foreach (int mi in vm.Cfg.HiddenMonitors.OrderBy(i => i).ToList())
            {
                int m = mi;
                var item = new System.Windows.Controls.MenuItem { Header = $"Show Monitor {m + 1}" };
                item.Click += (_, _) =>
                {
                    vm.Cfg.HiddenMonitors.Remove(m);
                    canvas.HiddenMonitors = vm.Cfg.HiddenMonitors;
                    canvas.InvalidateVisual();
                };
                menu.Items.Add(item);
            }
            if (menu.Items.Count > 0) menu.Items.Add(new System.Windows.Controls.Separator());
            var fit = new System.Windows.Controls.MenuItem { Header = "Fit / Reset View" };
            fit.Click += (_, _) => canvas.ResetView();
            menu.Items.Add(fit);
            var flash = new System.Windows.Controls.MenuItem { Header = "Flash All Zones" };
            flash.Click += (_, _) => canvas.FlashAllZones();
            menu.Items.Add(flash);
            menu.IsOpen = true;
        };
        _canvas = canvas;

        Content = canvas;
    }

    private ZoneMapCanvas? _canvas;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Helpers.WindowPlacement.Restore(this, "map", _vm.Cfg);
        if (_canvas is not null) _vm.FlashZoneRequested += _canvas.FlashZone;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_canvas is not null) _vm.FlashZoneRequested -= _canvas.FlashZone;
        Helpers.WindowPlacement.Save(this, "map", _vm.Cfg);
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        base.OnClosing(e);
    }

    /// <summary>Called by the main window when hidden monitors change.</summary>
    public void RefreshHiddenMonitors(System.Collections.Generic.IReadOnlyCollection<int> hidden)
    {
        if (_canvas == null) return;
        _canvas.HiddenMonitors = hidden;
        _canvas.InvalidateVisual();
    }
}
