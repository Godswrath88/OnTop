using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// Polls running processes and exits OnTop when any configured trigger process
/// is found. Honors the master enable, on-start-only mode, and poll interval
/// from settings, reconfiguring live as they change in the Auto-Close GUI.
/// </summary>
public sealed class AutoCloseService : IDisposable
{
    private readonly MainViewModel _vm;
    private readonly DispatcherTimer _timer = new();

    public AutoCloseService(MainViewModel vm)
    {
        _vm = vm;
        _timer.Tick += (_, _) => CheckNow();
        _vm.Cfg.PropertyChanged += OnCfgChanged;
        Reconfigure();

        // The initial scan runs after startup finishes, not during it.
        if (_vm.Cfg.AutoCloseEnabled)
            Application.Current?.Dispatcher.BeginInvoke(
                new Action(CheckNow), DispatcherPriority.ApplicationIdle);
    }

    private void OnCfgChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.AutoCloseEnabled)
            or nameof(AppSettings.AutoCloseInterval)
            or nameof(AppSettings.AutoCloseOnStartOnly))
            Reconfigure();
    }

    private void Reconfigure()
    {
        _timer.Stop();
        if (_vm.Cfg.AutoCloseEnabled && !_vm.Cfg.AutoCloseOnStartOnly)
        {
            _timer.Interval = TimeSpan.FromSeconds(Math.Max(2, _vm.Cfg.AutoCloseInterval));
            _timer.Start();
        }
    }

    /// <summary>Scan running processes once; shut down on the first trigger match.</summary>
    public void CheckNow()
    {
        if (!_vm.Cfg.AutoCloseEnabled || _vm.AutoCloseTriggers.Count == 0) return;

        string? hit = null;
        var processes = System.Diagnostics.Process.GetProcesses();
        try
        {
            foreach (var proc in processes)
            {
                try
                {
                    if (_vm.AutoCloseTriggers.Any(t =>
                            string.Equals(t, proc.ProcessName, StringComparison.OrdinalIgnoreCase)))
                    {
                        hit = proc.ProcessName;
                        break;
                    }
                }
                catch { /* process may have exited mid-scan */ }
            }
        }
        finally
        {
            foreach (var proc in processes) proc.Dispose();
        }

        if (hit == null) return;
        _vm.Log.Action($"Auto-close trigger matched: {hit} — exiting OnTop");
        Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    public void Dispose()
    {
        _timer.Stop();
        _vm.Cfg.PropertyChanged -= OnCfgChanged;
    }
}
