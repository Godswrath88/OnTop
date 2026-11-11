using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OnTop.Models;
using OnTop.Services;
using Xunit;

namespace OnTop.Tests;

public class SettingsTests
{
    [Fact]
    public void Config_without_new_fields_loads_with_defaults()
    {
        // An older config (or a hand-trimmed one) simply lacks the 0.45.3 fields.
        var s = JsonSerializer.Deserialize<AppSettings>("{}")!;
        Assert.True(s.HintPopupsEnabled);
        Assert.Equal(4, s.NoticeSeconds);

        var r = JsonSerializer.Deserialize<PlacementRule>("{}")!;
        Assert.False(r.ElevatedNoticeOff);
    }

    [Fact]
    public void Every_scalar_setting_survives_a_json_round_trip()
    {
        // A setting that doesn't round-trip is silently lost on the next save.
        var original = new AppSettings();
        var props = ScalarProps(typeof(AppSettings)).ToList();
        int i = 0;
        foreach (var p in props) SetDistinct(p, original, i++);

        var back = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(original))!;
        foreach (var p in props)
            Assert.True(Equals(p.GetValue(original), p.GetValue(back)), $"AppSettings.{p.Name} did not round-trip");
    }

    [Fact]
    public void PlacementRule_Clone_copies_every_property()
    {
        // Clone is hand-written; a new field that isn't added to it is lost on
        // Duplicate and on the Placement window's Revert snapshot.
        var r = new PlacementRule();
        var props = ScalarProps(typeof(PlacementRule)).ToList();
        int i = 0;
        foreach (var p in props) SetDistinct(p, r, i++);

        var c = r.Clone();
        foreach (var p in props)
            Assert.True(Equals(p.GetValue(r), p.GetValue(c)), $"PlacementRule.Clone() does not copy {p.Name}");
    }

    [Fact]
    public void Powerful_stamp_tracks_launch_paths_but_not_display_only_fields()
    {
        var s = new AppSettings();
        s.Placements.Add(new PlacementRule { Id = "r1", LaunchPath = @"C:\a.exe" });
        string baseline = SettingsService.ComputePowerfulStamp(s);

        s.Placements[0].ElevatedNoticeOff = true;
        s.NoticeSeconds = 9;
        s.HintPopupsEnabled = false;
        Assert.Equal(baseline, SettingsService.ComputePowerfulStamp(s));

        s.Placements[0].LaunchPath = @"C:\b.exe";
        Assert.NotEqual(baseline, SettingsService.ComputePowerfulStamp(s));
    }

    [Fact]
    public void ClampPlacements_bounds_every_numeric_field_and_repairs_ids()
    {
        var rules = new List<PlacementRule>
        {
            new() { Id = "", Monitor = 99, X = -999999, Y = 999999, W = 1, H = 1, DelaySec = 999, LaunchWaitSec = 0 },
        };
        SettingsService.ClampPlacements(rules);
        var r = rules[0];
        Assert.Equal(15, r.Monitor);
        Assert.Equal(-20000, r.X);
        Assert.Equal(20000, r.Y);
        Assert.Equal(50, r.W);
        Assert.Equal(30, r.H);
        Assert.Equal(120, r.DelaySec);
        Assert.Equal(1, r.LaunchWaitSec);
        Assert.Equal(8, r.Id.Length);
    }

    [Fact]
    public void Migrate_from_an_old_schema_does_not_throw_and_keeps_zones()
    {
        var s = new AppSettings { SchemaVersion = 1 };
        s.Zones.Add(new Zone { Name = "a" });
        s.Zones.Add(new Zone { Name = "b" });
        SettingsService.Migrate(s);
        Assert.Equal(2, s.Zones.Count);
        Assert.Equal(new[] { 0, 1 }, s.Zones.Select(z => z.ColorIndex));
    }

    // ── helpers ──────────────────────────────────────────────────────────
    private static IEnumerable<PropertyInfo> ScalarProps(System.Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                     && p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() is null
                     && (p.PropertyType == typeof(bool) || p.PropertyType == typeof(int)
                         || p.PropertyType == typeof(double) || p.PropertyType == typeof(string)));

    /// <summary>Give each property a value that differs from its default, so a
    /// field that isn't carried over shows up as a mismatch.</summary>
    private static void SetDistinct(PropertyInfo p, object target, int i)
    {
        object v = p.PropertyType == typeof(bool) ? !(bool)p.GetValue(target)!
                 : p.PropertyType == typeof(int) ? 7 + i
                 : p.PropertyType == typeof(double) ? 0.5 + i / 1000.0
                 : $"v{i}";
        p.SetValue(target, v);
        // Setters may clamp or normalize; compare against what was actually stored.
    }
}
