namespace OnTop.Helpers;

/// <summary>
/// Process-wide appearance flags, set from AppSettings at startup and kept in
/// sync by the ViewModel. Read by Win32.ApplyBackdrop so every window — current
/// and future — honors the Settings dialog's Mica toggle.
/// </summary>
public static class ThemeOptions
{
    public static bool MicaEnabled = true;

    /// <summary>S25 accent picker. Applied ONCE at startup BEFORE any window is
    /// created: theme styles resolve brushes via StaticResource at element
    /// creation, so swapping the Application-level resources early re-skins
    /// everything; changing later needs a restart (Settings says so).</summary>
    public static readonly (string Name, System.Windows.Media.Color Color)[] Accents =
    {
        ("Blue",    System.Windows.Media.Color.FromRgb(0x3A, 0x8B, 0xFF)),
        ("Violet",  System.Windows.Media.Color.FromRgb(0x8B, 0x5C, 0xF6)),
        ("Teal",    System.Windows.Media.Color.FromRgb(0x14, 0xB8, 0xA6)),
        ("Green",   System.Windows.Media.Color.FromRgb(0x2E, 0xCC, 0x71)),
        ("Amber",   System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B)),
        ("Rose",    System.Windows.Media.Color.FromRgb(0xF4, 0x3F, 0x5E)),
    };

    /// <summary>S28 Stage I: 0 Dark (the designed theme), 1 High Contrast, 2 Light.
    /// Mutates the shared token brushes in place (same trick as ApplyAccent) so it
    /// applies live. Light is a best-effort palette swap over the token set — the
    /// app was designed dark, so expect to tune it; High Contrast is the safer choice.</summary>
    public static void ApplyTheme(int theme)
    {
        var res = System.Windows.Application.Current.Resources;
        void Set(string key, byte r, byte g, byte b, byte a = 255)
        {
            var c = System.Windows.Media.Color.FromArgb(a, r, g, b);
            res[key] = c;
            if (res[key + "Brush"] is System.Windows.Media.SolidColorBrush br && !br.IsFrozen) br.Color = c;
            else res[key + "Brush"] = new System.Windows.Media.SolidColorBrush(c);
        }
        switch (theme)
        {
            case 1:   // High contrast: same dark structure, brighter text, harder borders
                Set("BgDark", 0x10, 0x10, 0x18); Set("BgMedium", 0x18, 0x18, 0x26); Set("BgPanel", 0x1C, 0x1C, 0x2A); Set("BgInput", 0x22, 0x22, 0x30); Set("BgBand", 0x0C, 0x0C, 0x14);
                Set("FgPrimary", 0xFF, 0xFF, 0xFF); Set("FgMuted", 0xE8, 0xE8, 0xF0); Set("FgDim", 0xB8, 0xB8, 0xC8); Set("BorderDim", 0x7A, 0x7A, 0x90);
                break;
            case 2:   // Light
                Set("BgDark", 0xF4, 0xF4, 0xF8); Set("BgMedium", 0xEA, 0xEA, 0xF2); Set("BgPanel", 0xFF, 0xFF, 0xFF); Set("BgInput", 0xFF, 0xFF, 0xFF); Set("BgBand", 0xE2, 0xE2, 0xEC);
                Set("FgPrimary", 0x14, 0x14, 0x20); Set("FgMuted", 0x3A, 0x3A, 0x4C); Set("FgDim", 0x6A, 0x6A, 0x7C); Set("BorderDim", 0xC4, 0xC4, 0xD2);
                break;
            default:  // Dark — the designed values
                Set("BgDark", 0x1A, 0x1A, 0x28); Set("BgMedium", 0x22, 0x22, 0x36); Set("BgPanel", 0x25, 0x25, 0x35); Set("BgInput", 0x2A, 0x2A, 0x3A); Set("BgBand", 0x18, 0x18, 0x28);
                Set("FgPrimary", 0xE6, 0xE6, 0xE6); Set("FgMuted", 0xBF, 0xBF, 0xBF); Set("FgDim", 0x80, 0x80, 0x90); Set("BorderDim", 0x3A, 0x3A, 0x4A);
                break;
        }
    }

    public static void ApplyAccent(int index)
    {
        if (index < 0 || index >= Accents.Length) index = 0;
        var c = Accents[index].Color;
        var res = System.Windows.Application.Current.Resources;
        res["AccentBlue"] = c;
        // BUG FIX (S28 Stage D, owner: "I don't see a change from the accent
        // color"): every style Setter captured the ORIGINAL brush object when the
        // theme dictionary loaded, so REPLACING the resource never reached them.
        // Mutate the shared brush in place instead — the theme brushes are not
        // frozen — and the whole app re-tints immediately, no restart needed.
        if (res["AccentBlueBrush"] is System.Windows.Media.SolidColorBrush b && !b.IsFrozen) b.Color = c;
        else res["AccentBlueBrush"] = new System.Windows.Media.SolidColorBrush(c);
        var tint = System.Windows.Media.Color.FromArgb(0x33, c.R, c.G, c.B);
        res["RowAccentTint"] = tint;
        if (res["RowAccentTintBrush"] is System.Windows.Media.SolidColorBrush t && !t.IsFrozen) t.Color = tint;
        else res["RowAccentTintBrush"] = new System.Windows.Media.SolidColorBrush(tint);
    }
}
