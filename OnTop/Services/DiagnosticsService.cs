using System;
using System.IO;
using System.IO.Compression;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// Builds the diagnostics bundle (settings.json, stats.json, current log,
/// diag.txt, log-tail.txt) used by BOTH the Log Viewer's "Export Diagnostics..."
/// button and the crash-report prompt. One writer, so the two paths can't drift.
/// SECURITY: purely local — the caller picks (or owns) the destination path,
/// contents are the user's own config, and nothing is ever transmitted.
/// </summary>
public static class DiagnosticsService
{
    /// <summary>Write a bundle zip to <paramref name="zipPath"/>. An optional
    /// user comment ("what were you doing?") is embedded in diag.txt.</summary>
    public static void WriteBundle(MainViewModel vm, string zipPath, string? comment = null)
    {
        // FileMode.Create: Export's save dialog already confirmed "Replace";
        // ZipFile.Open(Create) used CreateNew and threw on an existing file.
        using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        void AddIfExists(string path, string entryName)
        {
            if (!File.Exists(path)) return;
            // ReadWrite|Delete sharing: the live log is still open for appending
            // (CreateEntryFromFile's FileShare.Read would throw mid-write).
            using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = zip.CreateEntry(entryName).Open();
            src.CopyTo(dst);
        }
        AddIfExists(Path.Combine(vm.Settings.ConfigDir, "settings.json"), "settings.json");
        AddIfExists(Path.Combine(vm.Settings.ConfigDir, "stats.json"), "stats.json");
        AddIfExists(vm.Log.CurrentLogFile, Path.GetFileName(vm.Log.CurrentLogFile));

        // A ZipArchive in Create mode allows only one open entry at a time, so
        // each entry's writer is disposed before the next CreateEntry.
        using (var w = new StreamWriter(zip.CreateEntry("diag.txt").Open()))
        {
            w.WriteLine($"OnTop version : {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            w.WriteLine($"Schema        : {SettingsService.CurrentSchemaVersion}");
            w.WriteLine($"OS            : {Environment.OSVersion}");
            w.WriteLine($".NET          : {Environment.Version}");
            w.WriteLine($"Generated     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            w.WriteLine($"Zones         : {vm.Zones.Count}   Profiles: {vm.Cfg.Profiles.Count}   Active: {vm.ActiveProfileName}");
            w.WriteLine();
            w.WriteLine("Monitors:");
            foreach (var scr in System.Windows.Forms.Screen.AllScreens)
                w.WriteLine($"  {(scr.Primary ? "*" : " ")} {scr.DeviceName}  {scr.Bounds.Width}x{scr.Bounds.Height} @ ({scr.Bounds.X},{scr.Bounds.Y})");
            if (!string.IsNullOrWhiteSpace(comment))
            {
                w.WriteLine();
                w.WriteLine("User comment (what were you doing?):");
                w.WriteLine(comment.Trim());
            }
        }

        // Roadmap: the last ~50 log lines ride along even if the log file on
        // disk is locked, rotated, or huge — the freshest context, guaranteed.
        using (var tw = new StreamWriter(zip.CreateEntry("log-tail.txt").Open()))
            tw.Write(vm.Log.TailText(50));
    }

    /// <summary>Bundle into %APPDATA%\OnTop\crashes with a timestamped name.
    /// Returns the written path.</summary>
    public static string WriteCrashBundle(MainViewModel vm, string? comment)
    {
        string dir = Path.Combine(vm.Settings.ConfigDir, "crashes");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"OnTop_Crash_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip");
        WriteBundle(vm, path, comment);
        return path;
    }
}
