using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using OnTop.Helpers;
using OnTop.Models;

namespace OnTop.Views;

/// <summary>
/// Floating panel listing every active overlay with its color swatch and current
/// size, plus the click-through lock toggle. Stays interactive even when the
/// overlays themselves are locked (it is a separate window). Closing hides it;
/// it returns the next time overlays are activated, or via the tray menu.
/// </summary>
public class OverlayLegendWindow : Window
{
    private readonly StackPanel _rows = new();

    private readonly AppSettings _cfg;

    public OverlayLegendWindow(AppSettings cfg)
    {
        _cfg = cfg;
        Style = (Style)Application.Current.FindResource("DarkChromeWindow");
        Title = "Overlay Legend";
        Width = 280;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;

        var root = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };

        var lockToggle = new CheckBox
        {
            Content = "Lock Overlays (Click-Through)",
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        lockToggle.SetBinding(ToggleButton.IsCheckedProperty,
            new Binding(nameof(AppSettings.OverlaysLocked)) { Source = cfg, Mode = BindingMode.TwoWay });
        root.Children.Add(lockToggle);

        var opacityRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
        opacityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        opacityRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var opacityLabel = new TextBlock
        {
            Text = "Opacity",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
            Margin = new Thickness(0, 0, 9, 0)
        };
        var opacitySlider = new Slider
        {
            Minimum = 0.25, Maximum = 1.0,
            VerticalAlignment = VerticalAlignment.Center
        };
        opacitySlider.SetBinding(Slider.ValueProperty,
            new Binding(nameof(AppSettings.OverlayOpacity)) { Source = cfg, Mode = BindingMode.TwoWay });
        Grid.SetColumn(opacityLabel, 0);
        Grid.SetColumn(opacitySlider, 1);
        opacityRow.Children.Add(opacityLabel);
        opacityRow.Children.Add(opacitySlider);
        root.Children.Add(opacityRow);

        root.Children.Add(new TextBlock
        {
            Text = "Unlocked: Drag Dots To Move, Corners To Resize",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });
        root.Children.Add(new Separator { Margin = new Thickness(0, 0, 0, 8) });
        root.Children.Add(_rows);

        Content = root;

        // Default: bottom-right of the primary work area (routine toasts own
        // the top-right corner, matching v9.5). Positioned on Loaded so the
        // SizeToContent height is known.
        Loaded += (_, _) =>
        {
            // A saved position wins; otherwise the v9.5 default (bottom-right).
            if (_cfg.WindowPlacements != null && _cfg.WindowPlacements.ContainsKey("legend"))
            {
                Helpers.WindowPlacement.Restore(this, "legend", _cfg);
                return;
            }
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 16;
            Top = workArea.Bottom - ActualHeight - 16;
        };
        Closing += (_, _) => Helpers.WindowPlacement.Save(this, "legend", _cfg);
    }

    /// <summary>Rebuild the swatch list from the zones currently shown as overlays.</summary>
    public void Refresh(IReadOnlyList<Zone> zones)
    {
        _rows.Children.Clear();
        foreach (var zone in zones)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var swatch = new Border
            {
                Width = 14, Height = 14,
                CornerRadius = new CornerRadius(3),
                Background = ZonePalette.GetBrush(zone.ColorIndex),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 9, 0)
            };
            var name = new TextBlock
            {
                Text = zone.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6)),
                Opacity = zone.Enabled ? 1.0 : 0.45
            };
            var dims = new TextBlock
            {
                Text = $"{zone.HalfX * 2}×{zone.HalfY * 2}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x90)),
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(swatch, 0);
            Grid.SetColumn(name, 1);
            Grid.SetColumn(dims, 2);
            row.Children.Add(swatch);
            row.Children.Add(name);
            row.Children.Add(dims);
            _rows.Children.Add(row);
        }
    }

    // Hide instead of closing — the overlay session may still be active.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
