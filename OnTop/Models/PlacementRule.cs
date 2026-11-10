using System.Collections.Generic;

namespace OnTop.Models;

/// <summary>
/// S28 Stage E — Window Placement &amp; Launcher rule (ROADMAP "Window
/// Placement &amp; Launcher", SECURITY.md "Internal Capability Isolation").
/// MOVE and LAUNCH are deliberately split: a rule always describes WHERE a
/// window goes (monitor + rect); it may OPTIONALLY name a program to start if
/// none is running — and that half is inert unless this rule's LaunchEnabled
/// box is ticked (S28 H: the former global sub-gate was redundant with import
/// quarantine, which already blanks paths and unticks Launch).
/// Move-only can never escalate to launch. Launch goes through the ONE
/// launcher choke-point (MainViewModel.LaunchViaShell) — config-in, not call-in.
/// </summary>
public class PlacementRule : ObservableObject
{
    private string _id = System.Guid.NewGuid().ToString("N")[..8];
    private bool _enabled = true;
    private string _name = "Placement";
    private string _process = "";
    private string _titleContains = "";
    private int _monitor;
    private int _x, _y, _w = 800, _h = 600;
    private bool _maximize;
    private bool _applyOnStartup;
    private string _launchPath = "";
    private bool _launchEnabled;
    private int _delaySec, _launchWaitSec = 10;
    /// <summary>S28 K: seconds to wait before applying (programs still starting up). 0–120.</summary>
    public int DelaySec { get => _delaySec; set => Set(ref _delaySec, value); }
    /// <summary>S28 K: after launching, keep looking for the window this long. 1–120 (was fixed 10).</summary>
    public int LaunchWaitSec { get => _launchWaitSec; set => Set(ref _launchWaitSec, value); }

    public string Id { get => _id; set => Set(ref _id, value ?? ""); }
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public string Name { get => _name; set => Set(ref _name, value ?? ""); }
    /// <summary>Process name to match (no .exe). Empty = match by title only.</summary>
    public string Process { get => _process; set => Set(ref _process, value ?? ""); }
    /// <summary>Optional title substring (case-insensitive) to narrow the match.</summary>
    public string TitleContains { get => _titleContains; set => Set(ref _titleContains, value ?? ""); }
    /// <summary>Target monitor index (0-based, as the zone map numbers them minus one).</summary>
    public int Monitor { get => _monitor; set => Set(ref _monitor, value); }
    /// <summary>Target rect in PHYSICAL pixels RELATIVE to the monitor's top-left.</summary>
    public int X { get => _x; set => Set(ref _x, value); }
    public int Y { get => _y; set => Set(ref _y, value); }
    public int W { get => _w; set => Set(ref _w, value); }
    public int H { get => _h; set => Set(ref _h, value); }
    /// <summary>Maximize on the target monitor instead of using X/Y/W/H.</summary>
    public bool Maximize { get => _maximize; set => Set(ref _maximize, value); }
    /// <summary>Apply this rule shortly after OnTop starts.</summary>
    public bool ApplyOnStartup { get => _applyOnStartup; set => Set(ref _applyOnStartup, value); }
    /// <summary>OPTIONAL program to start if no matching window exists. Inert
    /// unless the global launch sub-gate AND LaunchEnabled are both on. Shown
    /// VERBATIM in the UI; quarantined (blanked) on import.</summary>
    public string LaunchPath { get => _launchPath; set => Set(ref _launchPath, value ?? ""); }
    public bool LaunchEnabled { get => _launchEnabled; set => Set(ref _launchEnabled, value); }

    public PlacementRule Clone() => new()
    {
        Id = Id, Enabled = Enabled, Name = Name, Process = Process, TitleContains = TitleContains,
        Monitor = Monitor, X = X, Y = Y, W = W, H = H, Maximize = Maximize,
        ApplyOnStartup = ApplyOnStartup, LaunchPath = LaunchPath, LaunchEnabled = LaunchEnabled,
        DelaySec = DelaySec, LaunchWaitSec = LaunchWaitSec,
    };
}
