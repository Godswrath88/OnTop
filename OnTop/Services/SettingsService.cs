using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using OnTop.Models;

namespace OnTop.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string AppDir { get; }
    public string ConfigDir { get; }
    public bool IsPortableMode { get; }
    public string LogDir { get; }
    public string IconPath { get; }
    private string SettingsPath => Path.Combine(ConfigDir, "settings.json");

    public SettingsService()
    {
        // Config lives in the user profile (%APPDATA%\OnTop) so it survives app
        // updates and works when the app is installed to a read-only location
        // like Program Files. Older builds stored it beside the exe — that
        // legacy location is still probed once for migration (see below), and
        // MUST keep being probed until we're confident no user has config there.
        // PORTABLE MODE (roadmap): an `OnTop.portable` marker file beside the
        // exe (or a --portable argument) keeps ALL data in <exe folder>\Data —
        // true run-from-USB behavior. No marker (the default) = %APPDATA% home.
        string exeDir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
        bool portable = (exeDir.Length > 0 && File.Exists(Path.Combine(exeDir, "OnTop.portable")))
                        || Array.Exists(Environment.GetCommandLineArgs(),
                               a => string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase));
        AppDir = portable
            ? Path.Combine(exeDir, "Data")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OnTop");
        IsPortableMode = portable;

        ConfigDir = Path.Combine(AppDir, "config");
        LogDir = Path.Combine(AppDir, "logs");
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(Path.Combine(AppDir, "assets"));

        // One-time move of an existing beside-exe config into the new location.
        MigrateLegacyConfig();

        // Icon resolution order (keep BOTH — the fallback is the shipped default
        // and must always remain a valid path even with no user override):
        //   first  the user-supplied  %APPDATA%\OnTop\assets\thumbtack.ico
        //   then   the shipped default beside the exe  Assets\thumbtack.ico  [FALLBACK]
        string userIcon = Path.Combine(AppDir, "assets", "thumbtack.ico");
        IconPath = File.Exists(userIcon)
            ? userIcon
            : Path.Combine(AppContext.BaseDirectory, "Assets", "thumbtack.ico");
    }

    /// <summary>One-time migration: if the new user-profile config doesn't exist
    /// yet but an old build left one beside the exe, copy it (and its backups and
    /// any user icon) across so upgrading users keep their zones and settings.
    /// Best-effort — failures fall through to a clean default config.</summary>
    private void MigrateLegacyConfig()
    {
        try
        {
            if (File.Exists(SettingsPath)) return;   // already have config here

            // Legacy location: beside the exe, self-nested in an "OnTop" folder.
            string baseDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            string leaf = Path.GetFileName(baseDir);
            string legacyAppDir = string.Equals(leaf, "OnTop", StringComparison.OrdinalIgnoreCase)
                ? baseDir
                : Path.Combine(baseDir, "OnTop");
            string legacyConfig = Path.Combine(legacyAppDir, "config");
            if (!File.Exists(Path.Combine(legacyConfig, "settings.json"))) return;

            foreach (var name in new[]
                { "settings.json", "settings.json.bak1", "settings.json.bak2", "settings.json.bak3" })
            {
                string src = Path.Combine(legacyConfig, name);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(ConfigDir, name), overwrite: false);
            }

            string legacyIcon = Path.Combine(legacyAppDir, "assets", "thumbtack.ico");
            string newIcon = Path.Combine(AppDir, "assets", "thumbtack.ico");
            if (File.Exists(legacyIcon) && !File.Exists(newIcon))
                File.Copy(legacyIcon, newIcon, overwrite: false);

            RecoveryNote = "migrated";
        }
        catch { /* migration is best-effort — a clean default is fine */ }
    }

    /// <summary>Current settings schema. Bump when stored shape changes and add a
    /// step to Migrate() — old files upgrade in place on load.</summary>
    public const int CurrentSchemaVersion = 30;

    // ── Inert-load guard (SECURITY.md "Config & Trusted-Data Integrity", S28) ──
    /// <summary>TRUE while any settings file is being read/validated. The three
    /// execution choke-points (RunZoneCommand, KillZoneProcess, MacroService
    /// Play/StartRecording) REFUSE and log an Error if ever invoked while this is
    /// set — a runtime form of the standing "nothing dangerous executes on load"
    /// regression test. It should never fire; if it does, that is a bug report.</summary>
    public static bool IsLoading { get; private set; }

    /// <summary>Non-fatal things Validate neutralized on load (blanked updater
    /// URL, powerful-field stamp mismatch, ...). The VM logs + toasts them once
    /// the logger exists. Cleared at the start of every Load.</summary>
    public List<string> LoadWarnings { get; } = new();

    /// <summary>Set when a corrupt settings.json was rescued this session —
    /// the backup filename that loaded, or "defaults" if nothing was readable.
    /// Null on a clean load. The VM surfaces this as a toast.</summary>
    public string? RecoveryNote { get; private set; }

    // Logging audit (S20 cont.): the settings service is built before the logger,
    // so the VM attaches it right after — file-I/O failures then surface as
    // Config-category entries instead of vanishing in silent catches.
    private LogService? _log;
    public void AttachLogger(LogService log) => _log = log;

    /// <summary>Populated when Load hit a situation the user should be asked about
    /// (corrupt file, or a config from a NEWER build loaded best-effort). Null on a
    /// clean load. The View shows a plain-language Continue/Cancel prompt.</summary>
    public ConfigConflict? Conflict { get; private set; }

    // SECURITY: hard cap before we ever hand a file to the JSON parser, so a
    // hostile or corrupted %APPDATA% file can't exhaust memory. 4 MB is far above
    // any legitimate config (thousands of zones) yet trivially bounded.
    private const long MaxConfigBytes = 4 * 1024 * 1024;

    public AppSettings Load()
    {
        LoadWarnings.Clear();
        IsLoading = true;
        try
        {
            var s = LoadCore();
            // S28 Stage H: portable mode → macro encryption off by default (the
            // file travels; DPAPI is bound to one Windows login). Only forced on
            // a FRESH config or a just-migrated one; a user's explicit later choice
            // (schema already current) is respected.
            if (IsPortableMode && (s.SchemaVersion < 26 || _freshConfig)) s.EncryptMacros = false;
            return s;
        }
        finally { IsLoading = false; }
    }

    private AppSettings LoadCore()
    {
        string[] candidates =
        {
            SettingsPath,
            SettingsPath + ".bak1",
            SettingsPath + ".bak2",
            SettingsPath + ".bak3",
        };

        bool primaryExists = File.Exists(candidates[0]);
        bool sawNewer = false;
        bool sawCorrupt = false;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (!File.Exists(candidates[i])) continue;
            try
            {
                // SECURITY: size-gate before reading/parsing.
                var info = new FileInfo(candidates[i]);
                if (info.Length > MaxConfigBytes) { sawCorrupt = true; continue; }

                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(candidates[i]), JsonOpts);
                if (loaded == null) { sawCorrupt = true; continue; }

                // A config written by a NEWER build has fields we don't know — but
                // our System.Text.Json loader simply IGNORES unknown fields, so
                // everything we DO understand (zones, hotkeys, window placements,
                // column widths, ...) still loads correctly. So we keep it
                // best-effort rather than tossing the whole file and losing usable
                // settings. We clamp the schema down to ours but DO NOT save over
                // the file until the user acts (no downgrade clobber — SECURITY.md).
                if (loaded.SchemaVersion > CurrentSchemaVersion)
                {
                    sawNewer = true;
                    Conflict = new ConfigConflict(ConfigConflictKind.NewerVersion,
                        loaded.SchemaVersion, CurrentSchemaVersion, Path.GetFileName(candidates[i]));
                    loaded.SchemaVersion = CurrentSchemaVersion;
                    Validate(loaded);
                    RecoveryNote = "loaded newer config (best-effort)";
                    return loaded;
                }

                Migrate(loaded);
                Validate(loaded);
                if (i > 0) RecoveryNote = Path.GetFileName(candidates[i]);
                return loaded;
            }
            catch { sawCorrupt = true; /* corrupt — try the next candidate */ }
        }

        // Nothing loaded. If a file existed but we couldn't use any candidate, ask.
        if (sawCorrupt || primaryExists)
        {
            RecoveryNote = "defaults";
            Conflict = new ConfigConflict(
                sawNewer ? ConfigConflictKind.NewerVersion : ConfigConflictKind.Corrupt,
                0, CurrentSchemaVersion, null);
        }
        _freshConfig = true;
        return new AppSettings { SchemaVersion = CurrentSchemaVersion };
    }
    private bool _freshConfig;

    // SECURITY: never trust values from disk — clamp everything to sane ranges so
    // a hand-edited or hostile config can't drive the UI/engine into a bad state.
    private void Validate(AppSettings s)
    {
        s.OverlayOpacity = Math.Clamp(s.OverlayOpacity, 0.05, 1.0);
        if (s.AutoCloseInterval < 1) s.AutoCloseInterval = 1;
        if (s.AutoCloseInterval > 3600) s.AutoCloseInterval = 3600;
        s.Zones ??= new();
        s.Exceptions ??= new();
        s.AutoCloseTriggers ??= new();
        s.WindowPlacements ??= new();
        s.ColumnWidths ??= new();
        s.WindowRules ??= new();
        s.Profiles ??= new();
        if (s.Profiles.Count > 200) s.Profiles = s.Profiles.Take(200).ToList();
        foreach (var pr in s.Profiles)
        {
            pr.Name ??= "Profile";
            if (pr.Name.Length > 60) pr.Name = pr.Name[..60];
            pr.Zones ??= new();
            pr.Exceptions ??= new();
            pr.AutoCloseTriggers ??= new();
            if (pr.MacroPolicy is < 0 or > 1) pr.MacroPolicy = Profile.PolicyAllow;
            ClampZones(pr.Zones);   // shared clamps
        }
        if (string.IsNullOrWhiteSpace(s.ActiveProfile)) s.ActiveProfile = "Default";
        s.UpdateManifestUrl ??= "";
        if (s.UpdateManifestUrl.Length > 2000) s.UpdateManifestUrl = "";
        // S28: re-assert trust AT LOAD, not just at use — a non-https or
        // unparseable manifest URL is blanked here (the check-only updater
        // would refuse it anyway, but a tampered value must not persist quietly).
        if (s.UpdateManifestUrl.Length > 0 && !IsValidUpdateUrl(s.UpdateManifestUrl))
        {
            LoadWarnings.Add($"Updater URL was not a valid https URL and was cleared: {s.UpdateManifestUrl}");
            s.UpdateManifestUrl = "";
        }
        s.IgnoredUpdateVersion ??= "";
        if (s.AccentIndex < 0 || s.AccentIndex >= 6) s.AccentIndex = 0;
        s.HotkeyMacros ??= "";
        if (s.HotkeyMacros.Length > 60) s.HotkeyMacros = "";
        s.HotkeyMacroRecord ??= "";
        if (s.HotkeyMacroRecord.Length > 60) s.HotkeyMacroRecord = "";
        s.HotkeyPlacement ??= "";
        if (s.HotkeyPlacement.Length > 60) s.HotkeyPlacement = "";
        s.Placements ??= new();
        ClampPlacements(s.Placements);
        s.LibraryActions ??= new();
        ClampLibrary(s.LibraryActions);
        foreach (var hkName in new[] { "PinUnderCursor", "PinOpacityUp", "PinOpacityDown", "PinClickThrough", "PinCycleCorner", "CheatSheet", "Palette" })
        {
            var p = typeof(AppSettings).GetProperty("Hotkey" + hkName)!;
            string v = (string?)p.GetValue(s) ?? "";
            if (v.Length > 60) v = "";
            p.SetValue(s, v);
        }
        s.ProfileRules ??= new();
        if (s.ProfileRules.Count > 200) s.ProfileRules.RemoveRange(200, s.ProfileRules.Count - 200);
        foreach (var pr in s.ProfileRules)
        {
            pr.Process ??= ""; if (pr.Process.Length > 100) pr.Process = pr.Process[..100];
            pr.Profile ??= ""; if (pr.Profile.Length > 60) pr.Profile = pr.Profile[..60];
        }
        s.DisplayProfiles ??= new();
        foreach (var k in s.DisplayProfiles.Keys.Where(k => k.Length > 1000 || (s.DisplayProfiles[k] ?? "").Length > 60).ToList()) s.DisplayProfiles.Remove(k);
        if (s.DisplayProfiles.Count > 50) s.DisplayProfiles = s.DisplayProfiles.Take(50).ToDictionary(x => x.Key, x => x.Value);
        s.MacroCountdownSec = Math.Clamp(s.MacroCountdownSec, 0, 5);
        s.HotkeyHoldPeek ??= ""; if (s.HotkeyHoldPeek.Length > 60) s.HotkeyHoldPeek = "";
        s.HotkeyHoldPin ??= ""; if (s.HotkeyHoldPin.Length > 60) s.HotkeyHoldPin = "";
        s.HotkeyPinTimed ??= ""; if (s.HotkeyPinTimed.Length > 60) s.HotkeyPinTimed = "";
        s.HotkeySendToMonitor ??= ""; if (s.HotkeySendToMonitor.Length > 60) s.HotkeySendToMonitor = "";
        s.HotkeyMacroPause ??= ""; if (s.HotkeyMacroPause.Length > 60) s.HotkeyMacroPause = "";
        s.HotkeyMacroStatus ??= ""; if (s.HotkeyMacroStatus.Length > 60) s.HotkeyMacroStatus = "";
        s.PinTimedSeconds = Math.Clamp(s.PinTimedSeconds, 3, 3600);
        s.IdleMinutes = Math.Clamp(s.IdleMinutes, 1, 1440);
        if (s.IdleAction < 0 || s.IdleAction > 3) s.IdleAction = 0;
        s.IdleProfile ??= ""; if (s.IdleProfile.Length > 60) s.IdleProfile = "";
        if (s.UiTheme < 0 || s.UiTheme > 2) s.UiTheme = 0;
        s.HotkeyRetrySeconds = Math.Clamp(s.HotkeyRetrySeconds, 5, 600);
        s.NoticeSeconds = Math.Clamp(s.NoticeSeconds, 1, 60);
        s.PinScenes ??= new(); if (s.PinScenes.Count > 50) s.PinScenes.RemoveRange(50, s.PinScenes.Count - 50);
        foreach (var sc in s.PinScenes)
        {
            sc.Id ??= ""; if (sc.Id.Length is 0 or > 40) sc.Id = Guid.NewGuid().ToString("N")[..8];
            sc.Name ??= "Scene"; if (sc.Name.Length > 100) sc.Name = sc.Name[..100];
            sc.Items ??= new(); if (sc.Items.Count > 50) sc.Items.RemoveRange(50, sc.Items.Count - 50);
            foreach (var it in sc.Items) { it.Process ??= ""; if (it.Process.Length > 100) it.Process = it.Process[..100]; it.TitleContains ??= ""; if (it.TitleContains.Length > 200) it.TitleContains = it.TitleContains[..200]; it.Opacity = Math.Clamp(it.Opacity, 20, 100); it.W = Math.Clamp(it.W, 50, 20000); it.H = Math.Clamp(it.H, 30, 20000); }
        }
        s.PinMemory ??= new();
        foreach (var k in s.PinMemory.Keys.Where(k => k.Length > 100).ToList()) s.PinMemory.Remove(k);
        if (s.PinMemory.Count > 200) s.PinMemory = s.PinMemory.Take(200).ToDictionary(x => x.Key, x => x.Value);
        foreach (var e in s.PinMemory.Values) { e.Opacity = Math.Clamp(e.Opacity, 20, 100); }
        s.ProfileSchedules ??= new(); if (s.ProfileSchedules.Count > 100) s.ProfileSchedules.RemoveRange(100, s.ProfileSchedules.Count - 100);
        foreach (var ps in s.ProfileSchedules) { ps.Profile ??= ""; if (ps.Profile.Length > 60) ps.Profile = ps.Profile[..60]; ps.StartMin = Math.Clamp(ps.StartMin, 0, 1439); ps.EndMin = Math.Clamp(ps.EndMin, 0, 1439); ps.Days &= 0x7F; }
        s.MacroGuardIgnored ??= new();
        s.MacroGuardIgnored.RemoveAll(n => string.IsNullOrWhiteSpace(n) || n.Length > 60);
        if (s.MacroGuardIgnored.Count > 100) s.MacroGuardIgnored.RemoveRange(100, s.MacroGuardIgnored.Count - 100);
        s.StealthWatchedProcesses ??= "";
        if (s.StealthWatchedProcesses.Length > 500) s.StealthWatchedProcesses = s.StealthWatchedProcesses[..500];
        s.Macros ??= new();
        UnsealMacros(s.Macros);  // S28: DPAPI at-rest → live Events (no-op when plain)
        ClampMacros(s.Macros);   // shared with macro import — clamps can't drift (S28)
        if (s.IgnoredUpdateVersion.Length > 40) s.IgnoredUpdateVersion = "";
        if (s.WindowRules.Count > 1000) s.WindowRules = s.WindowRules.Take(1000).ToList();
        foreach (var r in s.WindowRules)
        {
            r.Pattern ??= "";
            if (r.Pattern.Length > 200) r.Pattern = r.Pattern[..200];
        }
        ClampZones(s.Zones);   // shared with ImportZonesFile — clamps can't drift

        // S28: powerful-field change visibility. The stamp is written on every
        // save; a stored stamp that no longer matches means an action command /
        // process / macro binding / updater URL / watched list changed OUTSIDE
        // OnTop since the last save (external edit, sync, hand tampering). Surface
        // it once — nothing is executed on load either way.
        s.PowerfulFieldsStamp ??= "";
        if (s.PowerfulFieldsStamp.Length > 0 && s.PowerfulFieldsStamp != ComputePowerfulStamp(s))
            LoadWarnings.Add("POWERFUL FIELDS CHANGED OUTSIDE ONTOP since the last save (zone commands/processes, macro hotkeys, updater URL, or watched list) — review Set Action / Settings before relying on them");
    }

    /// <summary>Shared macro clamps (S28) — used by Validate() AND macro import,
    /// so a hand-edited or hostile macro can never feed malformed input to
    /// SendInput: event kinds 0..9 only, mouse coords within the 0..65535
    /// normalized space, wheel deltas bounded, virtual-key codes 1..254,
    /// timestamps non-decreasing and ≤ 24 h. Invalid EVENTS are dropped (not
    /// clamped) so a corrupted stream can't be silently reinterpreted.</summary>
    public static void ClampMacros(List<Macro> macros)
    {
        if (macros.Count > 50) macros.RemoveRange(50, macros.Count - 50);
        foreach (var mac in macros)
        {
            mac.Id ??= Guid.NewGuid().ToString("N");
            if (mac.Id.Length > 40) mac.Id = Guid.NewGuid().ToString("N");
            mac.Name ??= "Macro";
            if (mac.Name.Length > 100) mac.Name = mac.Name[..100];
            mac.Hotkey ??= "";
            if (mac.Hotkey.Length > 60) mac.Hotkey = "";
            if (double.IsNaN(mac.Speed) || double.IsInfinity(mac.Speed)) mac.Speed = 1.0;
            mac.Speed = Math.Clamp(mac.Speed, 0.1, 10.0);
            mac.Repeat = Math.Clamp(mac.Repeat, 0, 1000);
            mac.Jitter = Math.Clamp(mac.Jitter, 0, 20);
            mac.NextMacroId ??= ""; if (mac.NextMacroId.Length > 40 || mac.NextMacroId == mac.Id) mac.NextMacroId = "";
            mac.AnchorTitle ??= ""; if (mac.AnchorTitle.Length > 200) mac.AnchorTitle = mac.AnchorTitle[..200];
            mac.AnchorLeft = Math.Clamp(mac.AnchorLeft, -50000, 50000); mac.AnchorTop = Math.Clamp(mac.AnchorTop, -50000, 50000);
            if (mac.StopKey < 0 || mac.StopKey > 254) mac.StopKey = 0;
            mac.Events ??= new();
            if (mac.Events.Count > 20_000) mac.Events.RemoveRange(20_000, mac.Events.Count - 20_000);
            uint lastT = 0;
            mac.Events.RemoveAll(e =>
            {
                if (e is null) return true;
                if (e.K > 10) return true;
                if (e.T > 24 * 3600 * 1000u) return true;
                if (e.T < lastT) return true;                       // must be non-decreasing
                if (e.K == 10)                                      // wait-for-window: a wait, not a verb
                {
                    e.S ??= ""; if (e.S.Length > 200) e.S = e.S[..200];
                    if (e.S.Length == 0) return true;
                    e.D = Math.Clamp(e.D, 100, 120_000); e.X = 0; e.Y = 0;
                    lastT = e.T; return false;
                }
                if (e.K != 10) e.S = null;
                if (e.L is { Length: > 100 }) e.L = e.L[..100];
                if (e.K <= 7 && (e.X < 0 || e.X > 65535 || e.Y < 0 || e.Y > 65535)) return true;
                if (e.K == 7 && (e.D < -12_000 || e.D > 12_000)) return true;   // 100 notches of WHEEL_DELTA
                if (e.K >= 8 && (e.D < 1 || e.D > 254)) return true;            // valid VK range
                if (e.K < 7) e.D = 0;                                           // no stray payload on moves/buttons
                lastT = e.T;
                return false;
            });
        }
    }

    // ── At-rest confidentiality (S28 Stage C, SECURITY.md "Confidentiality vs
    // tamper-proofing"): OPTIONAL Windows DPAPI (CurrentUser) sealing of macro
    // event streams ONLY. Tied to the Windows login; no app-stored key; a
    // copied/synced/leaked file exposes nothing. Never applied to zones,
    // profiles, hotkeys or settings; export always writes plain JSON. ────────
    private static readonly byte[] MacroEntropy = Encoding.UTF8.GetBytes("OnTop.Macros.DPAPI.v1");

    /// <summary>Serialize for disk. When EncryptMacros is on, each macro's Events
    /// are sealed into EncryptedEvents for the write and the LIVE objects are
    /// restored immediately after — in memory nothing changes.</summary>
    private string SerializeSettings(AppSettings settings)
    {
        if (!settings.EncryptMacros || settings.Macros is null || settings.Macros.Count == 0)
        {
            foreach (var m in settings.Macros ?? new()) m.EncryptedEvents = null;   // never write stale seals
            return JsonSerializer.Serialize(settings, JsonOpts);
        }
        var live = new List<(Macro M, List<MacroEvent> Events)>();
        try
        {
            foreach (var m in settings.Macros)
            {
                live.Add((m, m.Events));
                byte[] plain = JsonSerializer.SerializeToUtf8Bytes(m.Events, JsonOpts);
                m.EncryptedEvents = Convert.ToBase64String(ProtectedData.Protect(plain, MacroEntropy, DataProtectionScope.CurrentUser));
                m.Events = new List<MacroEvent>();
            }
            return JsonSerializer.Serialize(settings, JsonOpts);
        }
        finally
        {
            foreach (var (m, ev) in live) { m.Events = ev; m.EncryptedEvents = null; }
        }
    }

    /// <summary>Turn sealed EncryptedEvents back into live Events. A seal that
    /// won't open (different Windows user, corrupted) leaves the macro EMPTY
    /// and DISABLED with a load warning — never a crash, never silent.</summary>
    private void UnsealMacros(List<Macro> macros)
    {
        foreach (var m in macros)
        {
            if (string.IsNullOrEmpty(m.EncryptedEvents)) continue;
            try
            {
                byte[] plain = ProtectedData.Unprotect(Convert.FromBase64String(m.EncryptedEvents), MacroEntropy, DataProtectionScope.CurrentUser);
                m.Events = JsonSerializer.Deserialize<List<MacroEvent>>(plain, JsonOpts) ?? new();
            }
            catch (Exception ex)
            {
                m.Events = new();
                m.Enabled = false;
                m.Hotkey = "";
                LoadWarnings.Add($"Macro \"{m.Name}\" could not be decrypted (recorded under a different Windows login?) — it is empty and disabled: {ex.Message}");
            }
            m.EncryptedEvents = null;
        }
    }

    /// <summary>S28 Stage F: library action clamps.</summary>
    public static void ClampLibrary(List<LibraryAction> acts)
    {
        if (acts.Count > 200) acts.RemoveRange(200, acts.Count - 200);
        foreach (var a in acts)
        {
            a.Id ??= ""; if (a.Id.Length is 0 or > 40) a.Id = Guid.NewGuid().ToString("N")[..8];
            a.Name ??= ""; if (a.Name.Length > 100) a.Name = a.Name[..100];
            a.Command ??= ""; if (a.Command.Length > 500) a.Command = a.Command[..500];
            a.Process ??= ""; if (a.Process.Length > 100) a.Process = a.Process[..100];
            a.MacroId ??= ""; if (a.MacroId.Length > 40) a.MacroId = "";
            a.PlacementId ??= ""; if (a.PlacementId.Length > 40) a.PlacementId = "";
            a.ProfileName ??= ""; if (a.ProfileName.Length > 60) a.ProfileName = a.ProfileName[..60];
            a.Hotkey ??= ""; if (a.Hotkey.Length > 60) a.Hotkey = "";
            if ((int)a.Kind < 0 || (int)a.Kind > 10) a.Kind = LibraryKind.Peek;
        }
    }

    /// <summary>S28 Stage E: placement rule clamps (shared by Validate + restore).</summary>
    public static void ClampPlacements(List<PlacementRule> rules)
    {
        if (rules.Count > 200) rules.RemoveRange(200, rules.Count - 200);
        foreach (var r in rules)
        {
            r.Id ??= ""; if (r.Id.Length is 0 or > 40) r.Id = Guid.NewGuid().ToString("N")[..8];
            r.Name ??= ""; if (r.Name.Length > 100) r.Name = r.Name[..100];
            r.Process ??= ""; if (r.Process.Length > 100) r.Process = r.Process[..100];
            r.TitleContains ??= ""; if (r.TitleContains.Length > 200) r.TitleContains = r.TitleContains[..200];
            r.LaunchPath ??= ""; if (r.LaunchPath.Length > 500) r.LaunchPath = r.LaunchPath[..500];
            r.Monitor = Math.Clamp(r.Monitor, 0, 15);
            r.X = Math.Clamp(r.X, -20000, 20000); r.Y = Math.Clamp(r.Y, -20000, 20000);
            r.W = Math.Clamp(r.W, 50, 20000); r.H = Math.Clamp(r.H, 30, 20000);
            r.DelaySec = Math.Clamp(r.DelaySec, 0, 120); r.LaunchWaitSec = Math.Clamp(r.LaunchWaitSec, 1, 120);
        }
    }

    /// <summary>S28: https + a parseable absolute URI with a non-empty host.</summary>
    public static bool IsValidUpdateUrl(string url)
        => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
           && Uri.TryCreate(url, UriKind.Absolute, out var u)
           && u.Scheme == Uri.UriSchemeHttps
           && u.Host.Length > 0;

    /// <summary>S28: SHA-256 over every "powerful" field, in a fixed order, so an
    /// external edit to any of them is visible on the next start. Cheap (a few
    /// KB of text at most) and deterministic. NOT an integrity signature — a
    /// local attacker could recompute it (SECURITY.md: no integrity theatre);
    /// it exists only to make SILENT changes visible.</summary>
    public static string ComputePowerfulStamp(AppSettings s)
    {
        var sb = new StringBuilder();
        void Z(Zone z) => sb.Append(z.Id).Append('|').Append((int)z.TriggerAction).Append('|')
                              .Append(z.ActionCommand).Append('|').Append(z.ActionProcess).Append('|')
                              .Append(z.ActionMacroId).Append('\n');
        foreach (var z in s.Zones ?? new()) Z(z);
        foreach (var pr in s.Profiles ?? new()) foreach (var z in pr.Zones ?? new()) Z(z);
        foreach (var m in s.Macros ?? new())
            sb.Append("M|").Append(m.Id).Append('|').Append(m.Hotkey).Append('|').Append(m.Enabled ? 1 : 0).Append('\n');
        sb.Append("U|").Append(s.UpdateManifestUrl ?? "").Append('\n');
        foreach (var a in s.LibraryActions ?? new())
            sb.Append("L|").Append(a.Id).Append('|').Append((int)a.Kind).Append('|').Append(a.Command).Append('|').Append(a.Process).Append('|').Append(a.Hotkey).Append('\n');
        foreach (var r in s.Placements ?? new())
            sb.Append("P|").Append(r.Id).Append('|').Append(r.LaunchPath).Append('|').Append(r.LaunchEnabled ? 1 : 0).Append('\n');
        sb.Append("W|").Append(s.StealthWatchedProcesses ?? "").Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>S28: TRUE when a path lives under OnTop's own config directory —
    /// the provenance test. Own settings/backups are TRUSTED (recovery is never
    /// quarantined); anything else is an outside file and gets quarantined.</summary>
    public bool IsOwnPath(string path)
    {
        try
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string root = Path.GetFullPath(ConfigDir).TrimEnd(Path.DirectorySeparatorChar);
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _log?.Debug($"IsOwnPath({path}) failed: {ex.Message}", "Config");
            return false;   // unknown provenance = outside = quarantine
        }
    }

    // ── Quarantine (SECURITY.md "Shared / imported configs", S28) ───────────
    /// <summary>Neutralize every powerful zone action (RunCommand / KillProcess /
    /// ToggleWindow / RunMacro → Exit) and record each one VERBATIM so the user
    /// can opt it back in from the Import Review window. Safe default: inert.</summary>
    public static void QuarantineZones(IEnumerable<Zone> zones, List<QuarantineItem> items, string origin)
    {
        foreach (var z in zones)
        {
            var act = z.TriggerAction;
            if (act is ZoneAction.Exit or ZoneAction.Stealth) continue;
            string detail = act switch
            {
                ZoneAction.RunCommand => $"Run Command: {z.ActionCommand}",
                ZoneAction.KillProcess => $"Kill Process: {z.ActionProcess}",
                ZoneAction.ToggleWindow => $"Toggle Window: {z.ActionProcess}",
                ZoneAction.RunMacro => $"Run Macro (id {z.ActionMacroId})",
                ZoneAction.ApplyPlacement => $"Apply Placement (id {z.ActionPlacementId})",
                ZoneAction.Library => $"Library Action (id {z.ActionLibraryId})",
                _ => act.ToString(),
            };
            var zone = z;
            items.Add(new QuarantineItem
            {
                Category = $"Zone Action ({origin})",
                Subject = zone.Name.Length > 0 ? zone.Name : "(unnamed zone)",
                Detail = detail,
                Restore = () => zone.TriggerAction = act,
            });
            zone.TriggerAction = ZoneAction.Exit;
        }
    }

    /// <summary>Neutralize every powerful field of a whole settings object that
    /// came from OUTSIDE (restore from a non-OnTop path). Zones (top-level +
    /// every profile), updater URL (blanked), and macros (hotkey blanked +
    /// disabled — the recording itself is kept, it just can't fire).</summary>
    public static void QuarantineSettings(AppSettings s, List<QuarantineItem> items)
    {
        QuarantineZones(s.Zones ?? new(), items, "live");
        foreach (var pr in s.Profiles ?? new())
            QuarantineZones(pr.Zones ?? new(), items, $"profile {pr.Name}");
        if (!string.IsNullOrWhiteSpace(s.UpdateManifestUrl))
        {
            string url = s.UpdateManifestUrl;
            items.Add(new QuarantineItem
            {
                Category = "Updater URL", Subject = "Settings → Updates",
                Detail = url, Restore = () => s.UpdateManifestUrl = url,
            });
            s.UpdateManifestUrl = "";
        }
        QuarantineMacros(s.Macros ?? new(), items);
        // S28 Stage E: launch paths are the powerful half of a placement — blank them.
        foreach (var r in s.Placements ?? new())
        {
            if (string.IsNullOrWhiteSpace(r.LaunchPath)) continue;
            var rule = r; string lp = rule.LaunchPath; bool le = rule.LaunchEnabled;
            items.Add(new QuarantineItem
            {
                Category = "Placement Launch", Subject = rule.Name,
                Detail = $"Launch: {lp}", Restore = () => { rule.LaunchPath = lp; rule.LaunchEnabled = le; },
            });
            rule.LaunchPath = ""; rule.LaunchEnabled = false;
        }
        // S28 Stage F: powerful library actions arrive disabled + unbound.
        foreach (var a in s.LibraryActions ?? new())
        {
            if (!a.IsPowerful && a.Hotkey.Length == 0) continue;
            var act = a; string hk = act.Hotkey; bool en = act.Enabled;
            items.Add(new QuarantineItem { Category = "Library Action", Subject = act.Name, Detail = $"{act.Summary}{(hk.Length > 0 ? $", hotkey {hk}" : "")}", Restore = () => { act.Hotkey = hk; act.Enabled = en; } });
            act.Hotkey = ""; act.Enabled = false;
        }
        s.ActionLibraryEnabled = false;
    }

    /// <summary>Imported macros land DISABLED with their hotkey blanked; the
    /// review restores both together. A macro that is enabled-but-unbound is
    /// still only playable by an explicit click, so this is belt-and-braces.</summary>
    public static void QuarantineMacros(IEnumerable<Macro> macros, List<QuarantineItem> items)
    {
        foreach (var m in macros)
        {
            if (!m.Enabled && string.IsNullOrEmpty(m.Hotkey)) continue;
            var mac = m;
            string hk = mac.Hotkey; bool en = mac.Enabled;
            items.Add(new QuarantineItem
            {
                Category = "Macro",
                Subject = mac.Name,
                Detail = $"{mac.Events.Count} events, {mac.DurationMs / 1000.0:0.0}s{(hk.Length > 0 ? $", hotkey {hk}" : "")} — captures literal keystrokes",
                Restore = () => { mac.Hotkey = hk; mac.Enabled = en; },
            });
            mac.Hotkey = "";
            mac.Enabled = false;
        }
    }

    /// <summary>Forward-compatible migrations for settings written by older builds.</summary>
    private static void Migrate(AppSettings settings)
    {
        if (settings.SchemaVersion < 2)
        {
            // v1 → v2: zones gained a persistent ColorIndex (palette color used to
            // be positional). Assign sequentially so existing zones keep the colors
            // they were already shown with.
            for (int i = 0; i < settings.Zones.Count; i++)
                settings.Zones[i].ColorIndex = i;
        }
        if (settings.SchemaVersion < 3)
        {
            // v2 → v3: zones gained Kind + RelPoints (Drag Box / Draw recording)
            // and settings gained RecordMode plus nine optional toggle hotkeys.
            // Every new field's default (Point kind, empty polygon, unbound
            // hotkeys, Point Click record mode) is exactly right for files
            // written before the fields existed — nothing to rewrite.
        }
        if (settings.SchemaVersion < 4)
        {
            // v3 → v4: added three optional action hotkeys (Quick Record, View
            // Logs, Soft Restart). All default-unbound, so older files load as-is.
        }
        if (settings.SchemaVersion < 5)
        {
            // v4 → v5: added WindowPlacements (saved size/position per window).
            // Defaults to an empty dictionary, so older files load untouched —
            // each window just uses its XAML default until first close.
        }
        if (settings.SchemaVersion < 6)
        {
            // v5 → v6: added the bindable HotkeyRehook (Re-Enable Mouse Hook).
            // No-op: it defaults to "Ctrl+Alt+R" (the previously fixed binding),
            // so older files keep the exact same behavior with nothing to migrate.
        }
        if (settings.SchemaVersion < 18)
        {
            // v17 → v18: MacrosEnabled (default OFF — ships dark) + Macros
            // library; Zone.ActionMacroId; ZoneAction.RunMacro=5. No-op.
        }
        if (settings.SchemaVersion < 17)
        {
            // v16 → v17: HotkeyStealth, StealthWatchedProcesses,
            // HideOverlaysInStealth (default true); Zone gains
            // ActionCommand/ActionProcess + RunCommand/KillProcess/ToggleWindow
            // enum values (int-serialized, append-safe). No-op migration.
        }
        if (settings.SchemaVersion < 16)
        {
            // v15 → v16: added AccentIndex (0 = classic blue). No-op migration.
        }
        if (settings.SchemaVersion < 15)
        {
            // v14 → v15: added TrayMenuStaysOpen (default off) and
            // FirstRunCompleted. Existing configs have clearly been used —
            // mark the tour done so only fresh installs see it.
            settings.FirstRunCompleted = true;
        }
        if (settings.SchemaVersion < 14)
        {
            // v13 → v14: added UpdateManifestUrl (empty = feature dormant),
            // UpdateCheckOnStartup, IgnoredUpdateVersion. No-op migration.
        }
        if (settings.SchemaVersion < 13)
        {
            // v12 → v13: ShowInTaskbar's double duty (taskbar icon + minimize
            // behavior) is split — the window now always shows in the taskbar
            // while open, and MinimizeToTray carries the tray behavior. Users
            // who had the icon OFF clearly wanted tray-centric behavior:
            if (!settings.ShowInTaskbar) settings.MinimizeToTray = true;
        }
        if (settings.SchemaVersion < 12)
        {
            // v11 → v12: added AutoDisableOffscreenZones (bool, defaults false —
            // the display-change monitor awareness is opt-in). No-op migration.
        }
        if (settings.SchemaVersion < 11)
        {
            // v10 → v11: added Profiles (named zone workspaces), ActiveProfile,
            // and HotkeyProfiles (cycle). Empty by default; the VM creates the
            // "Default" profile from the live workspace on first run — no-op here.
        }
        if (settings.SchemaVersion < 30)
        {
            // v29 → v30 (0.45.3): HintPopupsEnabled (true), NoticeSeconds (4, clamped
            // 1–60), PlacementRule.ElevatedNoticeOff (false). Defaults fill in; no-op.
            // The bump is what makes an OLDER build treat this config as newer and
            // not save over it (dropping these fields).
        }
        if (settings.SchemaVersion < 29)
        {
            // v28 → v29 (S28 Stage K): Zone.AfterAction (0), PlacementRule.DelaySec (0) /
            // LaunchWaitSec (10), ClickThroughBorder (true), HotkeyMacroStatus. No-op.
        }
        if (settings.SchemaVersion < 28)
        {
            // v27 → v28 (S28 Stage J): HotkeyRetrySeconds (default 30 = old fixed value). No-op.
        }
        if (settings.SchemaVersion < 27)
        {
            // v26 → v27 (S28 Stage I): PinScenes, PinMemory(+Enabled), HotkeyPinTimed/
            // PinTimedSeconds, HotkeySendToMonitor, ZoneHitFlash, HotkeyMacroPause,
            // Idle rule fields, ProfileSchedules(+Enabled), UiTheme, Zone cooldown/
            // schedule, MacroEvent.L, LibraryKind.ApplyScene. All inert — no-op.
        }
        if (settings.SchemaVersion < 26)
        {
            // v25 → v26 (S28 Stage H): PlacementLaunchEnabled REMOVED (ignored if
            // present); EncryptMacros now defaults ON — existing installed-mode
            // configs are switched on here (design decision: keystrokes are the
            // most sensitive thing stored; DPAPI costs nothing). Portable stays
            // off (the file moves between machines). HoldChordsEnabled/
            // HotkeyHoldPeek/HotkeyHoldPin, Macro.StopKey added (inert).
            settings.EncryptMacros = true;   // Load() flips it back off in portable mode
        }
        if (settings.SchemaVersion < 25)
        {
            // v24 → v25 (S28 Stage G): pin-extra hotkeys, ProfileRules[] +
            // ProfileRulesEnabled, DisplayProfiles + DisplayProfilesEnabled,
            // DoNotDisturb, MacroCountdownSec (3), Zone.Gesture (0 = classic),
            // Macro.NextMacroId/Anchor*/Relative, MacroEvent.S. All inert/no-op.
        }
        if (settings.SchemaVersion < 24)
        {
            // v23 → v24 (S28 Stage F): LibraryActions[], ActionLibraryEnabled (false),
            // Zone.ActionLibraryId, ZoneAction.Library = 7. Ships inert — no-op.
        }
        if (settings.SchemaVersion < 23)
        {
            // v22 → v23 (S28 Stage E): Placements[], PlacementEnabled (false),
            // PlacementLaunchEnabled (false), HotkeyPlacement, Zone.ActionPlacementId.
            // Everything ships inert — no-op migration.
        }
        if (settings.SchemaVersion < 22)
        {
            // v21 → v22 (S28 Stage D): HotkeyMacroRecord (default Ctrl+Alt+M —
            // NEW BINDING for existing users, deliberately: recording must be
            // hotkey-driven; the welcome tour and Help show it), MacroGuardIgnored,
            // ShowMacroStatusBar (true), LowImpactMode (false). No-op migration.
        }
        if (settings.SchemaVersion < 21)
        {
            // v20 → v21 (S28 Stage C): StatsEnabled / ZoneActionsEnabled /
            // StealthWatcherEnabled (all default TRUE = previous behavior),
            // EncryptMacros (default FALSE), Macro.EncryptedEvents (null unless
            // encryption is on). No-op migration.
        }
        if (settings.SchemaVersion < 20)
        {
            // v19 → v20 (S28 Stage B): MacroGuardKnownAntiCheat / MacroKillSwitch /
            // MacroRecordPath (all default TRUE = fail-safe), MacroWarningAcknowledged,
            // HotkeyMacros, Macro.Jitter, Profile.MacroPolicy (0 = Allow). All
            // defaults are the previous behavior — no-op migration.
        }
        if (settings.SchemaVersion < 19)
        {
            // v18 → v19 (S28): added PowerfulFieldsStamp (string). Empty on older
            // files, so the first load never reports a mismatch; the first save
            // writes the real stamp. No-op migration.
        }
        if (settings.SchemaVersion < 10)
        {
            // v9 → v10: added ShowInTaskbar (bool, defaults true). Older configs
            // gain the taskbar icon by default — a deliberate choice; the
            // Settings toggle restores the old hidden behavior. Otherwise no-op.
        }
        if (settings.SchemaVersion < 9)
        {
            // v8 → v9: added HotkeyPeek (flash all zone overlays) and HotkeyRules
            // (open the rules manager). Both default to unbound — no-op migration.
        }
        if (settings.SchemaVersion < 8)
        {
            // v7 → v8: added ColumnWidths (persisted DataGrid column sizes) and
            // WindowRule list / window-rules placement / auto-pin fields. All
            // default to empty — nothing to migrate for older files.
        }
        if (settings.SchemaVersion < 7)
        {
            // v6 → v7: added HotkeyPins (open the Pinned Windows viewer) and the
            // "pins" WindowPlacements key. Both default to empty — no-op migration.
        }
        settings.SchemaVersion = CurrentSchemaVersion;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            RotateBackups();
            settings.SchemaVersion = CurrentSchemaVersion;
            settings.PowerfulFieldsStamp = ComputePowerfulStamp(settings);
            string json = SerializeSettings(settings);
            // Atomic: write to a temp file, then swap — a crash mid-write can
            // never leave a truncated settings.json behind.
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
        }
    }

    /// <summary>Write settings.json atomically WITHOUT rotating the .bak1–3 chain.
    /// For frequent, low-stakes writes (e.g. window size/position on close) that
    /// would otherwise churn the backup history reserved for real config changes.</summary>
    public void SaveNoBackup(AppSettings settings)
    {
        try
        {
            settings.SchemaVersion = CurrentSchemaVersion;
            settings.PowerfulFieldsStamp = ComputePowerfulStamp(settings);
            string json = SerializeSettings(settings);
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveNoBackup error: {ex.Message}");
        }
    }

    private void RotateBackups()
    {
        if (!File.Exists(SettingsPath)) return;
        string bak3 = SettingsPath + ".bak3";
        string bak2 = SettingsPath + ".bak2";
        string bak1 = SettingsPath + ".bak1";

        try { if (File.Exists(bak3)) File.Delete(bak3); } catch (Exception ex) { _log?.Warn($"Backup rotation (delete .bak3) failed: {ex.Message}", "Config"); }
        try { if (File.Exists(bak2)) File.Move(bak2, bak3); } catch (Exception ex) { _log?.Warn($"Backup rotation (.bak2→.bak3) failed: {ex.Message}", "Config"); }
        try { if (File.Exists(bak1)) File.Move(bak1, bak2); } catch (Exception ex) { _log?.Warn($"Backup rotation (.bak1→.bak2) failed: {ex.Message}", "Config"); }
        try { File.Copy(SettingsPath, bak1); } catch (Exception ex) { _log?.Warn($"Backup rotation (copy→.bak1) failed: {ex.Message}", "Config"); }
    }

    public void SaveDraft(AppSettings settings)
    {
        try
        {
            string draft = Path.Combine(ConfigDir, "settings.draft.json");
            File.WriteAllText(draft, SerializeSettings(settings));
        }
        catch (Exception ex) { _log?.Debug($"Draft save failed: {ex.Message}", "Config"); }
    }

    public void DeleteDraft()
    {
        try
        {
            string draft = Path.Combine(ConfigDir, "settings.draft.json");
            if (File.Exists(draft)) File.Delete(draft);
        }
        catch (Exception ex) { _log?.Debug($"Draft delete failed: {ex.Message}", "Config"); }
    }

    public bool HasDraft() => File.Exists(Path.Combine(ConfigDir, "settings.draft.json"));

    // ── Backups (timestamped snapshots) ─────────────────────────────────
    public string BackupsDirectory
    {
        get
        {
            string dir = Path.Combine(ConfigDir, "backups");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Write a timestamped JSON snapshot of the given settings. Returns the path.</summary>
    public string WriteBackup(AppSettings settings)
    {
        settings.SchemaVersion = CurrentSchemaVersion;
        settings.PowerfulFieldsStamp = ComputePowerfulStamp(settings);
        string path = Path.Combine(BackupsDirectory, $"OnTop_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, SerializeSettings(settings));

        // Pruning (roadmap): keep the newest 10 manual/import backups so the
        // folder can't grow forever. Rotation .baks are separate and untouched.
        try
        {
            var old = new DirectoryInfo(BackupsDirectory)
                .GetFiles("OnTop_Backup_*.json")
                .OrderByDescending(f => f.Name)
                .Skip(10);
            foreach (var f in old) f.Delete();
        }
        catch (Exception ex) { _log?.Debug($"Backup pruning failed: {ex.Message}", "Config"); }
        return path;
    }

    /// <summary>Load + migrate a settings/backup file (throws on unreadable input).</summary>
    public AppSettings ReadSettingsFile(string path)
    {
        // S28: this path (Settings → Restore) was skipping the size gate AND
        // Validate — a restore file could smuggle unclamped values straight into
        // the live config. Now identical to Load's treatment.
        var info = new FileInfo(path);
        if (info.Length > MaxConfigBytes)
            throw new InvalidDataException(
                $"Restore refused: file is {info.Length / (1024 * 1024)} MB (limit {MaxConfigBytes / (1024 * 1024)} MB).");
        IsLoading = true;
        try
        {
            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts)
                         ?? throw new InvalidDataException("File did not contain settings JSON");
            if (loaded.SchemaVersion > CurrentSchemaVersion) loaded.SchemaVersion = CurrentSchemaVersion;
            Migrate(loaded);
            Validate(loaded);
            return loaded;
        }
        finally { IsLoading = false; }
    }

    // ── Standalone Import / Export Files ────────────────────────────────
    public void ExportZonesFile(string path, List<Zone> zones)
        => File.WriteAllText(path, JsonSerializer.Serialize(zones, JsonOpts));

    /// <summary>Import zones from a user-picked JSON file. SECURITY (roadmap:
    /// strict validation + size caps on imported JSON): the file is size-gated
    /// before parsing and every zone passes the same clamps the config loader
    /// applies — an oversized or hostile file can't balloon memory or smuggle
    /// unbounded strings in.</summary>
    public List<Zone> ImportZonesFile(string path, List<QuarantineItem> quarantine)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxConfigBytes)
            throw new InvalidDataException(
                $"Import refused: file is {info.Length / (1024 * 1024)} MB (limit {MaxConfigBytes / (1024 * 1024)} MB).");
        List<Zone> zones;
        IsLoading = true;
        try
        {
            zones = JsonSerializer.Deserialize<List<Zone>>(File.ReadAllText(path), JsonOpts) ?? new List<Zone>();
            ClampZones(zones);
            // S28: an explicit import is always OUTSIDE content — powerful actions
            // land inert and are listed verbatim for the user to opt back in.
            QuarantineZones(zones, quarantine, "imported");
        }
        finally { IsLoading = false; }
        return zones;
    }

    /// <summary>S28: import a macro library file (JSON list). Size-gated,
    /// ClampMacros-validated, and QUARANTINED (disabled + hotkey blanked) until
    /// reviewed. Ids are regenerated so imports can never overwrite an existing
    /// macro. Macros are global, so the caller appends to Cfg.Macros.</summary>
    public List<Macro> ImportMacrosFile(string path, List<QuarantineItem> quarantine)
    {
        var info = new FileInfo(path);
        if (info.Length > MaxConfigBytes)
            throw new InvalidDataException(
                $"Import refused: file is {info.Length / (1024 * 1024)} MB (limit {MaxConfigBytes / (1024 * 1024)} MB).");
        List<Macro> macros;
        IsLoading = true;
        try
        {
            macros = JsonSerializer.Deserialize<List<Macro>>(File.ReadAllText(path), JsonOpts) ?? new List<Macro>();
            ClampMacros(macros);
            foreach (var m in macros) m.Id = Guid.NewGuid().ToString("N");
            QuarantineMacros(macros, quarantine);
        }
        finally { IsLoading = false; }
        return macros;
    }

    /// <summary>S28: export macros as a portable JSON list. A SEPARATE deliberate
    /// path from settings (never blocked by at-rest confidentiality — see
    /// SECURITY.md); the caller shows the literal-keystroke warning first.</summary>
    public void ExportMacrosFile(string path, IEnumerable<Macro> macros)
        => File.WriteAllText(path, JsonSerializer.Serialize(macros.ToList(), JsonOpts));

    /// <summary>Shared zone clamps — used by Validate() and by imports.</summary>
    private static void ClampZones(List<Zone> zones)
    {
        if (zones.Count > 100_000) zones.RemoveRange(100_000, zones.Count - 100_000);
        foreach (var z in zones)
        {
            z.Name ??= "";
            if (z.Name.Length > 200) z.Name = z.Name[..200];
            z.Group ??= "";
            if (z.Group.Length > 100) z.Group = z.Group[..100];
            z.RelPoints ??= new();
            if (z.RelPoints.Count > 10_000) z.RelPoints.RemoveRange(10_000, z.RelPoints.Count - 10_000);
            // Per-zone actions (SECURITY.md): cap the user-authored strings and
            // reject out-of-range enum values from newer/hostile files.
            z.ActionCommand ??= "";
            if (z.ActionCommand.Length > 500) z.ActionCommand = z.ActionCommand[..500];
            z.ActionProcess ??= "";
            if (z.ActionProcess.Length > 100) z.ActionProcess = z.ActionProcess[..100];
            z.ActionMacroId ??= "";
            if (z.ActionMacroId.Length > 40) z.ActionMacroId = "";
            z.ActionPlacementId ??= "";
            if (z.ActionPlacementId.Length > 40) z.ActionPlacementId = "";
            z.ActionLibraryId ??= "";
            if (z.ActionLibraryId.Length > 40) z.ActionLibraryId = "";
            if ((int)z.TriggerAction < 0 || (int)z.TriggerAction > 7) z.TriggerAction = ZoneAction.Exit;
            if (z.Gesture < 0 || z.Gesture > 6) z.Gesture = 0;
            z.CooldownSec = Math.Clamp(z.CooldownSec, 0, 3600);
            if (z.AfterAction < 0 || z.AfterAction > 2) z.AfterAction = 0;
            if (z.SchedStart < -1 || z.SchedStart > 1439) z.SchedStart = -1;
            if (z.SchedEnd < -1 || z.SchedEnd > 1439) z.SchedEnd = -1;
            z.SchedDays &= 0x7F; if (z.SchedDays == 0) z.SchedDays = 0x7F;
        }
    }

    /// <summary>Pipe-delimited: name|blockToggle|blockExit (1/0).</summary>
    public void ExportExceptionsFile(string path, IEnumerable<ProcessException> exceptions)
        => File.WriteAllLines(path, exceptions.Select(e =>
            $"{e.Name}|{(e.BlockToggle ? 1 : 0)}|{(e.BlockExit ? 1 : 0)}"));

    public List<ProcessException> ImportExceptionsFile(string path)
    {
        var result = new List<ProcessException>();
        foreach (var line in File.ReadAllLines(path))
        {
            var parts = line.Split('|');
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) continue;
            result.Add(new ProcessException
            {
                Name = CleanProcessName(parts[0]),
                BlockToggle = parts.Length < 2 || ParseBool(parts[1]),
                BlockExit = parts.Length < 3 || ParseBool(parts[2])
            });
        }
        return result;
    }

    /// <summary>One process name per line.</summary>
    public void ExportAutoCloseFile(string path, IEnumerable<string> names)
        => File.WriteAllLines(path, names);

    public List<string> ImportAutoCloseFile(string path)
        => File.ReadAllLines(path)
               .Select(CleanProcessName)
               .Where(n => n.Length > 0)
               .Distinct(StringComparer.OrdinalIgnoreCase)
               .ToList();

    private static bool ParseBool(string raw)
        => raw.Trim() is "1" || raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trim whitespace and a trailing ".exe" so stored names match Process.ProcessName.</summary>
    public static string CleanProcessName(string raw)
    {
        var name = raw.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name;
    }
}
