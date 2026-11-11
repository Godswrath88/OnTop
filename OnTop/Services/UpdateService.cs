using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// STAGE-ONE update checker (roadmap: online updater — check &amp; notify only).
/// Fetches a tiny JSON manifest over HTTPS, compares versions, and offers to
/// open the release page IN THE BROWSER. Nothing is downloaded by the app and
/// nothing is executed — stage two (in-app download + verify + run) stays
/// gated on code signing per SECURITY.md.
///
/// SECURITY (the amended no-network exception, see SECURITY.md):
///  • Dormant by default: no request unless the user has set a manifest URL.
///  • HTTPS is enforced on the manifest URL AND on the release URL before the
///    browser is opened; anything else is refused and logged.
///  • Response is size-capped (64 KB) and parsed with strict JSON; a 10 s
///    timeout bounds the whole call. Failures never nag and never block.
///  • Downgrade/equal versions are never offered (rollback refusal).
///  • Every event logs under "Updates": check start/result, prompt choice,
///    ignored-version set/cleared, and every error.
/// Manifest format (see docs/UPDATES.md):
///   { "version": "0.28.0", "url": "https://.../releases/tag/v0.28.0", "notes": "..." }
/// </summary>
public static class UpdateService
{
    private const int MaxManifestBytes = 64 * 1024;

    private sealed class Manifest
    {
        public string? Version { get; set; }
        public string? Url { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>Run a check. <paramref name="manual"/> = user pressed the
    /// Settings button (report "up to date" and clear an ignored version);
    /// otherwise it's the quiet startup check (respects IgnoredUpdateVersion).</summary>
    public static async Task CheckAsync(MainViewModel vm, bool manual)
    {
        string url = (vm.Cfg.UpdateManifestUrl ?? "").Trim();
        if (url.Length == 0)
        {
            if (manual)
                ToastService.Show("No Update URL Configured (Settings → Updates)", ToastType.Info);
            return;
        }
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            vm.Log.Warn($"Update check refused — manifest URL is not https: {url}", "Updates");
            if (manual) ToastService.Show("Update URL Must Be HTTPS", ToastType.Warning);
            return;
        }

        vm.Log.Info($"Update check ({(manual ? "manual" : "startup")}) — {url}", "Updates");
        Manifest? m;
        try
        {
            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10),
                MaxResponseContentBufferSize = MaxManifestBytes,
            };
            string json = await http.GetStringAsync(url).ConfigureAwait(false);
            m = JsonSerializer.Deserialize<Manifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            vm.Log.Info($"Update check failed (never blocks): {ex.Message}", "Updates");
            if (manual)
                Application.Current.Dispatcher.Invoke(() =>
                    ToastService.Show("Update Check Failed — See Log", ToastType.Warning));
            return;
        }

        if (m?.Version is null || !Version.TryParse(m.Version.Trim(), out var remote))
        {
            vm.Log.Warn("Update manifest unreadable (no parsable version)", "Updates");
            return;
        }
        var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

        // Rollback refusal: equal or older is never offered.
        if (remote <= current)
        {
            vm.Log.Info($"Up to date (current {current}, remote {remote})", "Updates");
            if (manual)
            {
                vm.Cfg.IgnoredUpdateVersion = "";
                Application.Current.Dispatcher.Invoke(() =>
                    ToastService.Show($"You're Up To Date ({current})", ToastType.Success));
            }
            return;
        }

        if (!manual && string.Equals(vm.Cfg.IgnoredUpdateVersion, m.Version.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            vm.Log.Info($"Update {remote} available but ignored by user preference", "Updates");
            return;
        }

        string releaseUrl = (m.Url ?? "").Trim();
        bool canOpen = releaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (!canOpen)
            vm.Log.Warn("Update manifest's release URL is not https — browser open disabled", "Updates");

        Application.Current.Dispatcher.Invoke(() =>
        {
            vm.Log.Action($"Update prompt shown — {current} → {remote}", "Updates");
            var choice = Views.UpdatePromptDialog.Show(remote.ToString(), m.Notes ?? "", canOpen);
            switch (choice)
            {
                case Views.UpdateChoice.Open:
                    vm.Log.Action($"Update prompt: user chose Open Download Page ({releaseUrl})", "Updates");
                    try
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(releaseUrl) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        vm.Log.Error($"Opening release page failed: {ex.Message}", "Updates");
                    }
                    break;
                case Views.UpdateChoice.IgnoreVersion:
                    vm.Cfg.IgnoredUpdateVersion = m.Version!.Trim();
                    vm.Settings.SaveNoBackup(vm.Cfg);
                    vm.Log.Action($"Update {remote} ignored (silenced until the next release)", "Updates");
                    break;
                default:
                    vm.Log.Action("Update prompt: user chose Cancel (will re-prompt next startup)", "Updates");
                    break;
            }
        });
    }
}
