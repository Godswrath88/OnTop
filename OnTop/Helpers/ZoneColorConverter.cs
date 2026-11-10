using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OnTop.Helpers;

/// <summary>Converts a zone's ColorIndex into its frozen palette brush
/// (DataGrid swatch column, and anywhere else XAML needs a zone color).</summary>
public sealed class ZoneColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int index ? ZonePalette.GetBrush(index) : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
