using System.Windows.Media;

namespace OnTop.Helpers;

/// <summary>
/// Shared 12-color palette for zone visuals. The zone map, the desktop overlays,
/// and the legend all index into this by the zone's position in the collection,
/// so a zone keeps the same color everywhere it is drawn.
/// </summary>
public static class ZonePalette
{
    /// <summary>Twelve visually distinct colors tuned for dark backgrounds.</summary>
    public static readonly Color[] Palette =
    {
        Color.FromRgb(0xFF, 0x52, 0x52), // Red
        Color.FromRgb(0xFF, 0x9F, 0x40), // Orange
        Color.FromRgb(0xFF, 0xD1, 0x66), // Yellow
        Color.FromRgb(0x9C, 0xCC, 0x65), // Light Green
        Color.FromRgb(0x2E, 0xCC, 0x71), // Green
        Color.FromRgb(0x4D, 0xD0, 0xE1), // Cyan
        Color.FromRgb(0x3A, 0x8B, 0xFF), // Blue
        Color.FromRgb(0x79, 0x86, 0xCB), // Indigo
        Color.FromRgb(0xAB, 0x7D, 0xF6), // Purple
        Color.FromRgb(0xF0, 0x62, 0x92), // Pink
        Color.FromRgb(0xA1, 0x88, 0x7F), // Tan
        Color.FromRgb(0xB0, 0xBE, 0xC5), // Steel
    };

    private static readonly SolidColorBrush[] Brushes = CreateBrushes();

    private static SolidColorBrush[] CreateBrushes()
    {
        var brushes = new SolidColorBrush[Palette.Length];
        for (int i = 0; i < Palette.Length; i++)
        {
            brushes[i] = new SolidColorBrush(Palette[i]);
            brushes[i].Freeze();
        }
        return brushes;
    }

    /// <summary>Color for the given zone index (cycles every 12).</summary>
    public static Color GetColor(int index) => Palette[Mod(index)];

    /// <summary>Frozen brush for the given zone index (cycles every 12).</summary>
    public static SolidColorBrush GetBrush(int index) => Brushes[Mod(index)];

    private static int Mod(int index) => ((index % Palette.Length) + Palette.Length) % Palette.Length;
}
