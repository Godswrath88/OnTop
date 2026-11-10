using System.Linq;
using Forms = System.Windows.Forms;

namespace OnTop.Helpers;

/// <summary>S28 Stage G — a stable string for the current monitor layout
/// (device names + bounds), used to bind a profile to a docked/undocked setup.</summary>
public static class DisplaySignature
{
    public static string Current()
        => string.Join("|", Forms.Screen.AllScreens.Select(sc => $"{sc.DeviceName}:{sc.Bounds}"));
    public static string Friendly()
        => $"{Forms.Screen.AllScreens.Length} monitor(s): " + string.Join(", ", Forms.Screen.AllScreens.Select(sc => $"{sc.Bounds.Width}×{sc.Bounds.Height}"));
}
