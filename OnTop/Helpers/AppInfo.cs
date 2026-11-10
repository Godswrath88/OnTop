using System.Reflection;

namespace OnTop.Helpers;

/// <summary>Single source of truth for the app's display version, read from the
/// assembly metadata set in OnTop.csproj (&lt;Version&gt;). Surfaced in the Help
/// window and the main-window subtitle so the running build always identifies
/// itself — no hard-coded string to drift out of sync with the csproj.</summary>
public static class AppInfo
{
    /// <summary>Short "x.y.z" version (trailing ".0" revision trimmed).</summary>
    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v == null) return "0.0.0";
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <summary>"OnTop v0.9.0" — for titles and headers.</summary>
    public static string TitleWithVersion => $"OnTop v{Version}";
}
