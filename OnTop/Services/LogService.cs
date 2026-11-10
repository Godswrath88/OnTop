using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace OnTop.Services;

/// <summary>One line of the live log tail (bound by the Log Viewer).</summary>
public sealed class LogEntry
{
    public DateTime Time { get; init; }
    public string Level { get; init; } = "INFO";
    public string Category { get; init; } = "General";
    public string Message { get; init; } = "";
}

/// <summary>
/// Session file logging + a live in-memory tail, rebuilt to v9.5 spec:
/// category tags, a DEBUG level gated by the Verbose setting, millisecond
/// timestamps, and per-category tracking toggles (errors always log).
/// All existing call sites keep working — category defaults to "General".
/// </summary>
public class LogService
{
    /// <summary>Categories offered as tracking checkboxes in the Log Viewer.</summary>
    public static readonly string[] KnownCategories =
        { "General", "System", "Hotkey", "Detection", "Recording", "Settings",
          "Overlay", "Zones", "Sliders", "Pins", "Rules", "Config", "Profiles", "Updates" };

    private const int MaxEntries = 2000;

    private readonly string _logPath;
    private readonly string _logDir;
    /// <summary>Lock for the in-memory tail snapshot (HOTFIX v0.36.1: TailText
    /// locked on a field that was never declared — from S24).</summary>
    private readonly object _sync = new();

    /// <summary>Where session logs live (Open Logs Folder button).</summary>
    public string LogDirectory => _logDir;
    public string CurrentLogFile => _logPath;

    /// <summary>Snapshot of the newest N in-memory entries (diagnostics tail).
    /// Safe to call from any thread — copies under the same lock as writes.</summary>
    public string TailText(int n)
    {
        lock (_sync)
        {
            var sb = new System.Text.StringBuilder();
            int start = Math.Max(0, Entries.Count - n);
            for (int i = start; i < Entries.Count; i++)
            {
                var e = Entries[i];
                sb.AppendLine($"{e.Time:HH:mm:ss.fff}  {e.Level,-6} [{e.Category}] {e.Message}");
            }
            return sb.ToString();
        }
    }

    /// <summary>Live tail for the Log Viewer. Mutated on the UI thread only.</summary>
    public ObservableCollection<LogEntry> Entries { get; } = new();

    /// <summary>Wired by the VM to settings — categories returning false are
    /// not tracked (skipped in file and tail). Errors always log.</summary>
    public Func<string, bool>? IsCategoryEnabled;

    /// <summary>Mirrored from AppSettings.VerboseLogging by the VM; gates Debug().</summary>
    public bool Verbose;

    public LogService(string logDir)
    {
        _logDir = logDir;
        Directory.CreateDirectory(logDir);
        string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        _logPath = Path.Combine(logDir, $"OnTop_{stamp}.log");

        Write("INFO", "System", $"OnTop started — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Write("INFO", "System", $"OS: {Environment.OSVersion}  .NET: {Environment.Version}");
    }

    public void Info(string msg, string category = "General") => Write("INFO", category, msg);
    public void Warn(string msg, string category = "General") => Write("WARN", category, msg);
    public void Action(string msg, string category = "General") => Write("ACTION", category, msg);

    /// <summary>Errors bypass the category-tracking filter.</summary>
    public void Error(string msg, string category = "General") => Write("ERROR", category, msg);

    /// <summary>Only written when Verbose logging is enabled.</summary>
    public void Debug(string msg, string category = "General")
    {
        if (Verbose) Write("DEBUG", category, msg);
    }

    private void Write(string level, string category, string msg)
    {
        if (level != "ERROR" && IsCategoryEnabled?.Invoke(category) == false) return;

        var now = DateTime.Now;
        try
        {
            File.AppendAllText(_logPath, $"{now:yyyy-MM-dd HH:mm:ss.fff}  [{level,-6}] [{category}] {msg}\r\n");
        }
        catch { /* justified-silent: the logger cannot log its own failure */ }

        // Live tail — marshal to the UI thread; crash handlers may log from anywhere.
        var app = Application.Current;
        if (app?.Dispatcher == null) return;

        void AddCore()
        {
            Entries.Add(new LogEntry { Time = now, Level = level, Category = category, Message = msg });
            while (Entries.Count > MaxEntries) Entries.RemoveAt(0);
        }

        if (app.Dispatcher.CheckAccess()) AddCore();
        else app.Dispatcher.BeginInvoke(AddCore);
    }

    public void PurgeOld(int retentionDays)
    {
        if (retentionDays <= 0) return;
        try
        {
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.GetFiles(_logDir, "OnTop_*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* justified-silent: the logger cannot log its own failure */ }
    }
}
