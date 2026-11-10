using System.Collections.Generic;

namespace OnTop.Models;

/// <summary>S28 Stage I — a saved set of pinned windows with geometry, opacity
/// and click-through; one action restores it (Library kind ApplyScene, palette).</summary>
public class PinScene
{
    public string Id { get; set; } = System.Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Scene";
    public List<PinSceneItem> Items { get; set; } = new();
}

public class PinSceneItem
{
    public string Process { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int Opacity { get; set; } = 100;
    public bool ClickThrough { get; set; }
}

/// <summary>S28 Stage I — per-app pin memory: how you last had that process pinned.</summary>
public class PinMemoryEntry
{
    public int Opacity { get; set; } = 100;
    public bool ClickThrough { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

/// <summary>S28 Stage I — time-of-day profile schedule (minutes since midnight; Days = bit 0 Mon … bit 6 Sun).</summary>
public class ProfileSchedule : ObservableObject
{
    private bool _enabled = true; private int _start = 9 * 60, _end = 17 * 60, _days = 0x1F; private string _profile = "";
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public int StartMin { get => _start; set => Set(ref _start, value); }
    public int EndMin { get => _end; set => Set(ref _end, value); }
    public int Days { get => _days; set => Set(ref _days, value); }
    public string Profile { get => _profile; set => Set(ref _profile, value ?? ""); }
    public string StartText { get => $"{StartMin / 60:00}:{StartMin % 60:00}"; set { if (TryParseHm(value, out var m)) StartMin = m; } }
    public string EndText { get => $"{EndMin / 60:00}:{EndMin % 60:00}"; set { if (TryParseHm(value, out var m)) EndMin = m; } }
    public string DaysText
    {
        get { var n = new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" }; var sb = new System.Text.StringBuilder(); for (int i = 0; i < 7; i++) if ((Days & (1 << i)) != 0) sb.Append(n[i]); return sb.ToString(); }
        set { var v = (value ?? "").ToLowerInvariant(); int d = 0; var n = new[] { "mo", "tu", "we", "th", "fr", "sa", "su" }; for (int i = 0; i < 7; i++) if (v.Contains(n[i])) d |= 1 << i; Days = d; }
    }
    public static bool TryParseHm(string? s, out int minutes)
    {
        minutes = 0; var p = (s ?? "").Trim().Split(':');
        if (p.Length != 2 || !int.TryParse(p[0], out var h) || !int.TryParse(p[1], out var m) || h < 0 || h > 23 || m < 0 || m > 59) return false;
        minutes = h * 60 + m; return true;
    }
    public bool IsActive(System.DateTime now)
    {
        if (!Enabled) return false;
        int dow = ((int)now.DayOfWeek + 6) % 7;   // Monday = 0
        if ((Days & (1 << dow)) == 0) return false;
        int cur = now.Hour * 60 + now.Minute;
        return StartMin <= EndMin ? cur >= StartMin && cur < EndMin : cur >= StartMin || cur < EndMin;
    }
}
