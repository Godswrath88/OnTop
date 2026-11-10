using System;
using System.Globalization;
using System.Windows.Data;

namespace OnTop.Helpers;

/// <summary>
/// Picks a short or long label based on the available width (bind the source to a
/// header's ActualWidth), so a column header can read its full name normally but
/// collapse to an abbreviation when dragged narrow.
/// ConverterParameter = "threshold|short|long" (e.g. "58|Mon|Monitor").
/// </summary>
public sealed class WidthLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || parameter is not string p)
            return value ?? "";
        var parts = p.Split('|');
        if (parts.Length != 3 ||
            !double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold))
            return value;
        return width < threshold ? parts[1] : parts[2];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
