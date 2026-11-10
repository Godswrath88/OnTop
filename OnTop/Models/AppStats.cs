using System;
using System.Collections.Generic;

namespace OnTop.Models;

/// <summary>
/// Lifetime usage statistics, persisted separately from settings (stats.json) so
/// the settings file stays lean and a stats reset can't touch configuration.
/// All values are plain counters — nothing identifying, nothing leaves the
/// machine (SECURITY.md: no telemetry, ever; this is for the user's own eyes).
/// </summary>
public class AppStats
{
    public DateTime FirstRun { get; set; } = DateTime.Now;
    public DateTime? LastReset { get; set; }

    public long Launches { get; set; }
    public long TotalRuntimeSeconds { get; set; }

    public long ZoneTriggers { get; set; }
    public long StealthTriggers { get; set; }
    public long Pins { get; set; }
    public long Unpins { get; set; }
    public long RulePins { get; set; }
    public long Peeks { get; set; }
    public long ProfileSwitches { get; set; }

    /// <summary>Rolling recent-activity feed (newest last, capped — see
    /// StatsService.MaxRecentEvents). Zone triggers, pins, rule pins, peeks,
    /// profile switches — timestamped, for the Statistics window's feed.</summary>
    public List<StatEvent> RecentEvents { get; set; } = new();

    /// <summary>Trigger count per zone Id (survives renames).</summary>
    public Dictionary<string, long> PerZoneTriggers { get; set; } = new();
    /// <summary>Last-known display name per zone Id (so deleted zones still show).</summary>
    public Dictionary<string, string> PerZoneNames { get; set; } = new();
}

/// <summary>One timestamped entry in the recent-activity feed.</summary>
public class StatEvent
{
    public DateTime When { get; set; } = DateTime.Now;
    /// <summary>Short kind tag: Trigger / Pin / Unpin / RulePin / Peek / Profile.</summary>
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
}
