using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using OnTop.Models;

namespace OnTop.Services;

/// <summary>
/// Usage statistics: lifetime counters persisted to stats.json (beside the
/// settings, same discipline — size-gated read, validated values, atomic
/// temp-then-move writes; see SECURITY.md), plus this-session values kept in
/// memory. Increments mark the store dirty; a 60s timer flushes only when dirty,
/// and the App flushes (with runtime accumulated) on exit.
/// </summary>
public sealed class StatsService : IDisposable
{
    private const long MaxStatsBytes = 1 * 1024 * 1024;   // stats are tiny; 1 MB is generous
    private const int MaxPerZoneEntries = 50_000;
    private const int MaxRecentEvents = 300;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _path;
    private readonly LogService _log;
    private readonly DispatcherTimer _flush = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool _dirty;

    public AppStats Stats { get; private set; } = new();

    // ── This session (not persisted) ────────────────────────────────────
    public DateTime SessionStart { get; } = DateTime.Now;
    public long SessionZoneTriggers { get; private set; }
    public long SessionPins { get; private set; }

    public StatsService(string configDir, LogService log)
    {
        _path = Path.Combine(configDir, "stats.json");
        _log = log;
        Load();
        _flush.Tick += (_, _) => { if (_dirty) Save(); };
        _flush.Start();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var info = new FileInfo(_path);
            if (info.Length > MaxStatsBytes)
            {
                _log.Warn("stats.json over size cap — starting fresh counters", "Config");
                return;
            }
            var loaded = JsonSerializer.Deserialize<AppStats>(File.ReadAllText(_path), JsonOpts);
            if (loaded == null) return;
            Validate(loaded);
            Stats = loaded;
        }
        catch (Exception ex)
        {
            _log.Warn($"stats.json unreadable — starting fresh counters ({ex.Message})", "Config");
        }
    }

    // SECURITY: same never-trust-disk discipline as settings.
    private static void Validate(AppStats s)
    {
        s.PerZoneTriggers ??= new();
        s.PerZoneNames ??= new();
        s.RecentEvents ??= new();
        if (s.RecentEvents.Count > MaxRecentEvents)
            s.RecentEvents.RemoveRange(0, s.RecentEvents.Count - MaxRecentEvents);
        foreach (var ev in s.RecentEvents)
        {
            ev.Kind ??= "";
            if (ev.Kind.Length > 20) ev.Kind = ev.Kind[..20];
            ev.Detail ??= "";
            if (ev.Detail.Length > 200) ev.Detail = ev.Detail[..200];
        }
        if (s.PerZoneTriggers.Count > MaxPerZoneEntries)
            s.PerZoneTriggers = s.PerZoneTriggers
                .OrderByDescending(kv => kv.Value).Take(MaxPerZoneEntries)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var key in s.PerZoneNames.Keys.ToList())
            if (s.PerZoneNames[key] is { Length: > 200 } v) s.PerZoneNames[key] = v[..200];
        if (s.Launches < 0) s.Launches = 0;
        if (s.TotalRuntimeSeconds < 0) s.TotalRuntimeSeconds = 0;
    }

    public void Save()
    {
        try
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Stats, JsonOpts));
            File.Move(tmp, _path, overwrite: true);
            _dirty = false;
        }
        catch (Exception ex)
        {
            _log.Debug($"Stats save failed: {ex.Message}", "Config");
        }
    }

    /// <summary>Append to the rolling activity feed (oldest evicted).</summary>
    private void AddEvent(string kind, string detail)
    {
        Stats.RecentEvents.Add(new StatEvent { Kind = kind, Detail = detail });
        if (Stats.RecentEvents.Count > MaxRecentEvents)
            Stats.RecentEvents.RemoveRange(0, Stats.RecentEvents.Count - MaxRecentEvents);
    }

    // ── Increments (all mark dirty; flushed by the timer or on exit) ────
    /// <summary>S28: mirrors Cfg.StatsEnabled (Standing Design Law). When
    /// false every Bump is a no-op — nothing is counted, nothing is written.</summary>
    public bool Enabled { get; set; } = true;

    public void BumpLaunch() { if (!Enabled) return; Stats.Launches++; _dirty = true; }

    public void BumpZoneTrigger(string zoneId, string zoneName, bool stealth)
    {
        if (!Enabled) return;
        Stats.ZoneTriggers++;
        if (stealth) Stats.StealthTriggers++;
        SessionZoneTriggers++;
        if (!string.IsNullOrEmpty(zoneId))
        {
            Stats.PerZoneTriggers[zoneId] = Stats.PerZoneTriggers.GetValueOrDefault(zoneId) + 1;
            Stats.PerZoneNames[zoneId] = zoneName ?? "";
            if (Stats.PerZoneTriggers.Count > MaxPerZoneEntries) Validate(Stats);
        }
        AddEvent("Trigger", $"{zoneName}{(stealth ? " (stealth)" : "")}");
        _dirty = true;
    }

    public void BumpPin(string detail = "") { if (!Enabled) return; Stats.Pins++; SessionPins++; AddEvent("Pin", detail); _dirty = true; }
    public void BumpUnpin(int n = 1) { if (!Enabled) return; Stats.Unpins += n; AddEvent("Unpin", n > 1 ? $"{n} windows" : ""); _dirty = true; }
    public void BumpRulePin(string detail = "") { if (!Enabled) return; Stats.RulePins++; AddEvent("Auto-Pin", detail); _dirty = true; }
    public void BumpPeek() { if (!Enabled) return; Stats.Peeks++; AddEvent("Peek", ""); _dirty = true; }
    public void BumpProfileSwitch(string name = "") { if (!Enabled) return; Stats.ProfileSwitches++; AddEvent("Profile", name); _dirty = true; }

    /// <summary>Fold this session's runtime into the lifetime total and flush.
    /// Called once by App.OnExit.</summary>
    public void AccumulateRuntimeAndSave()
    {
        Stats.TotalRuntimeSeconds += (long)(DateTime.Now - SessionStart).TotalSeconds;
        Save();
    }

    /// <summary>Wipe all counters (user-initiated from the Statistics window).</summary>
    public void Reset()
    {
        Stats = new AppStats { FirstRun = Stats.FirstRun, LastReset = DateTime.Now };
        Save();
        _log.Action("Statistics reset by user", "System");
    }

    public void Dispose()
    {
        _flush.Stop();
        if (_dirty) Save();
    }
}
