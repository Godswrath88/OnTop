using System.Collections.Generic;
using System.Linq;

namespace OnTop.Models;

/// <summary>
/// A named zone workspace: zones, process exceptions, auto-close triggers, and
/// the default sliders — everything that defines "what OnTop watches and how,"
/// snapshotted so the user can keep separate setups (per game, per task) and
/// switch between them. App-level preferences (hotkeys, theme, window
/// placements, taskbar, rules) are deliberately GLOBAL and not part of a
/// profile — switching changes the workspace, not the app.
///
/// Snapshots are deep clones both directions (Capture/copy) so editing the live
/// workspace never silently mutates a stored profile.
/// </summary>
public class Profile
{
    public string Name { get; set; } = "Default";
    public List<Zone> Zones { get; set; } = new();
    public List<ProcessException> Exceptions { get; set; } = new();
    public List<string> AutoCloseTriggers { get; set; } = new();
    public int DefTolerance { get; set; } = 5;
    public int DefCapacity { get; set; } = 20;
    public int DefVariation { get; set; } = 5;
    /// <summary>S28: per-profile macro policy. 0 = Allow (inherit the global
    /// master toggle), 1 = DENY — while this profile is active no macro hotkey
    /// binds and playback/recording refuse. Fail-safe per game.</summary>
    public int MacroPolicy { get; set; }
    public const int PolicyAllow = 0, PolicyDeny = 1;

    /// <summary>Deep-clone this profile's contents (rename-safe duplicate).</summary>
    public Profile Clone(string? newName = null) => new()
    {
        Name = newName ?? Name,
        Zones = Zones.Select(z => z.Clone()).ToList(),
        Exceptions = Exceptions.Select(e => e.Clone()).ToList(),
        AutoCloseTriggers = AutoCloseTriggers.ToList(),
        DefTolerance = DefTolerance,
        DefCapacity = DefCapacity,
        DefVariation = DefVariation,
        MacroPolicy = MacroPolicy,
    };
}
