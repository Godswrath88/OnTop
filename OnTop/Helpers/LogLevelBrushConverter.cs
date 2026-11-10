using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OnTop.Helpers;

/// <summary>Maps a log level string to its chip brush in the Log Viewer.</summary>
public sealed class LogLevelBrushConverter : IValueConverter
{
    private static SolidColorBrush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static readonly SolidColorBrush Debug = Make(0x5A, 0x5A, 0x70);
    private static readonly SolidColorBrush Info = Make(0x3A, 0x8B, 0xFF);
    private static readonly SolidColorBrush Act = Make(0x2E, 0xAA, 0x66);
    private static readonly SolidColorBrush Warn = Make(0xE6, 0xA2, 0x3C);
    private static readonly SolidColorBrush Error = Make(0xE5, 0x53, 0x53);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value as string)?.ToUpperInvariant() switch
        {
            "DEBUG" => Debug,
            "ACTION" => Act,
            "WARN" => Warn,
            "ERROR" => Error,
            _ => Info
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
