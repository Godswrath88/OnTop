using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using OnTop.Helpers;

namespace OnTop.Services;

/// <summary>
/// S28 Stage L — the EMERGENCY STOP. Exit and Kill are the two hotkeys that
/// must work even when the rest of OnTop is frozen, so they do NOT live on the
/// WPF window like every other hotkey: WM_HOTKEY is delivered to the message
/// queue of the THREAD that called RegisterHotKey, and a hung UI thread never
/// pumps its queue. This service owns a dedicated thread ("OnTop E-Stop") whose
/// only job is to pump its own queue, so its two hotkeys are delivered no matter
/// what the UI thread is doing.
///
///  • KILL  → one log line, then <c>TerminateProcess</c> from this thread.
///    Nothing is waited on, nothing is asked. The OS removes low-level hooks
///    and hotkeys when the process dies; pinned windows stay pinned (as today).
///  • EXIT  → posted to the UI thread (normal clean shutdown: exception-process
///    check, confirm-before-exit, settings save). If the UI thread does not pick
///    it up within <see cref="ExitGraceMs"/>, the stop escalates to the KILL path.
///  • WATCHDOG → a 1 s ping of the dispatcher. If the UI thread stops answering
///    for <see cref="HangReportSec"/> s, ONE warning is logged (with the wall-clock
///    stall length) and another line when it recovers — so a freeze leaves a
///    trace in the log next to whatever happened just before it.
///
/// SECURITY: this adds no input hooks and no new capability — two RegisterHotKey
/// calls moved to another thread, plus a process self-terminate. It cannot be
/// triggered by config or imports; only by the physical key combos.
/// </summary>
public sealed class EmergencyStopService : IDisposable
{
    public const int ExitGraceMs = 3000;
    public const int HangReportSec = 4;

    private const int IdExit = 1, IdKill = 2;
    private const uint WM_APP_REBIND = 0x8000 + 1, WM_APP_QUIT = 0x8000 + 2;

    private readonly LogService _log;
    private Thread? _thread;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);

    // Rebind request (written by the UI thread, applied on the e-stop thread).
    private readonly object _sync = new();
    private string _exitCombo = "", _killCombo = "";
    private bool _exitOk, _killOk;
    private int _exitErr, _killErr;
    private readonly AutoResetEvent _rebound = new(false);

    /// <summary>What EXIT runs on the UI thread (the VM's normal exit-hotkey handler).</summary>
    public Action? OnExitRequested;

    // Watchdog
    private System.Threading.Timer? _watchdog;
    private long _lastPong = Environment.TickCount64;
    private bool _hangReported;
    private long _hangSince;

    public bool ExitBound { get { lock (_sync) return _exitOk; } }
    public bool KillBound { get { lock (_sync) return _killOk; } }
    public int ExitError { get { lock (_sync) return _exitErr; } }
    public int KillError { get { lock (_sync) return _killErr; } }

    public EmergencyStopService(LogService log) { _log = log; }

    /// <summary>Start the e-stop thread (idempotent). Returns once its message
    /// queue exists, so <see cref="Rebind"/> can be called immediately after.</summary>
    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(ThreadMain) { Name = "OnTop E-Stop", IsBackground = true };
        _thread.Start();
        _ready.Wait(2000);
        _watchdog = new System.Threading.Timer(_ => WatchdogTick(), null, 1000, 1000);
    }

    /// <summary>(Re)register the two combos on the e-stop thread. Empty exit =
    /// unbound (capture UI open, or the user cleared it). Kill is never empty —
    /// the Hotkeys editor falls back to Ctrl+Alt+Q. Returns after the thread has
    /// applied it (it is never busy), so the bound flags are current.</summary>
    public void Rebind(string exitCombo, string killCombo)
    {
        lock (_sync) { _exitCombo = exitCombo ?? ""; _killCombo = killCombo ?? ""; }
        if (_threadId == 0) return;
        _rebound.Reset();
        if (Win32.PostThreadMessage(_threadId, WM_APP_REBIND, IntPtr.Zero, IntPtr.Zero))
            _rebound.WaitOne(1000);
    }

    // ── The e-stop thread ───────────────────────────────────────────────
    private void ThreadMain()
    {
        _threadId = Win32.GetCurrentThreadId();
        // Force the thread's message queue into existence BEFORE signalling ready,
        // or a PostThreadMessage racing us would fail with ERROR_INVALID_THREAD_ID.
        Win32.PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        _ready.Set();

        while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            try
            {
                if (msg.message == Win32.WM_HOTKEY)
                {
                    int id = (int)msg.wParam;
                    if (id == IdKill) KillNow("Kill hotkey");
                    else if (id == IdExit) ExitWithEscalation();
                }
                else if (msg.message == WM_APP_REBIND) ApplyRebind();
                else if (msg.message == WM_APP_QUIT) break;
                else { Win32.TranslateMessage(ref msg); Win32.DispatchMessage(ref msg); }
            }
            catch (Exception ex)
            {
                // The one thread that must never die: log and keep pumping.
                try { _log.Error($"E-stop thread: {ex.GetType().Name}: {ex.Message}", "Hotkey"); } catch { /* logger down */ }
            }
        }
        Win32.UnregisterHotKey(IntPtr.Zero, IdExit);
        Win32.UnregisterHotKey(IntPtr.Zero, IdKill);
    }

    private void ApplyRebind()
    {
        string exit, kill;
        lock (_sync) { exit = _exitCombo; kill = _killCombo; }
        Win32.UnregisterHotKey(IntPtr.Zero, IdExit);
        Win32.UnregisterHotKey(IntPtr.Zero, IdKill);
        bool eOk = false, kOk = false; int eErr = 0, kErr = 0;
        if (HotkeyService.ParseHotkey(exit, out uint em, out uint ev))
        {
            eOk = Win32.RegisterHotKey(IntPtr.Zero, IdExit, em | Win32.MOD_NOREPEAT, ev);
            if (!eOk) eErr = Marshal.GetLastWin32Error();
        }
        if (HotkeyService.ParseHotkey(kill, out uint km, out uint kv))
        {
            kOk = Win32.RegisterHotKey(IntPtr.Zero, IdKill, km | Win32.MOD_NOREPEAT, kv);
            if (!kOk) kErr = Marshal.GetLastWin32Error();
        }
        lock (_sync) { _exitOk = eOk; _killOk = kOk; _exitErr = eErr; _killErr = kErr; }
        _rebound.Set();
    }

    /// <summary>EXIT: hand the clean shutdown to the UI thread and give it
    /// <see cref="ExitGraceMs"/> to at least START; if it never does, the UI
    /// thread is hung and the only honest thing left is the hard stop.</summary>
    private void ExitWithEscalation()
    {
        var started = new ManualResetEventSlim(false);
        var app = Application.Current;
        var disp = app?.Dispatcher;
        if (disp is null) { KillNow("Exit hotkey (no dispatcher)"); return; }
        _log.Action("Exit hotkey pressed (e-stop thread) — handing to the UI thread", "Hotkey");
        disp.InvokeAsync(() =>
        {
            started.Set();
            OnExitRequested?.Invoke();
        }, DispatcherPriority.Send);
        if (!started.Wait(ExitGraceMs))
        {
            _log.Error($"UI thread did not respond to Exit within {ExitGraceMs / 1000} s — escalating to hard stop", "System");
            KillNow("Exit hotkey, UI thread hung");
        }
    }

    /// <summary>KILL: the e-stop. One synchronous log line (File.AppendAllText —
    /// on disk before we return), then TerminateProcess from THIS thread. Not
    /// Environment.Exit, which runs process-exit handlers and can itself block
    /// behind a hung thread.</summary>
    public void KillNow(string why)
    {
        try { _log.Action($"KILL — {why}: terminating OnTop now (no cleanup; hooks and hotkeys are released by the OS)", "Hotkey"); } catch { /* logger down */ }
        Win32.TerminateProcess(Win32.GetCurrentProcess(), 0);
    }

    // ── Watchdog ────────────────────────────────────────────────────────
    private void WatchdogTick()
    {
        try
        {
            var disp = Application.Current?.Dispatcher;
            if (disp is null) return;
            long now = Environment.TickCount64;
            long silent = (now - Interlocked.Read(ref _lastPong)) / 1000;
            if (silent >= HangReportSec && !_hangReported)
            {
                _hangReported = true; _hangSince = now - silent * 1000;
                _log.Warn($"UI thread unresponsive for {silent} s — Exit/Kill hotkeys remain armed on the e-stop thread", "System");
            }
            else if (silent < 2 && _hangReported)
            {
                _hangReported = false;
                _log.Warn($"UI thread responsive again after ~{(now - _hangSince) / 1000} s", "System");
            }
            // The ping: queued at Send priority; a healthy loop answers within a frame.
            disp.InvokeAsync(() => { Interlocked.Exchange(ref _lastPong, Environment.TickCount64); }, DispatcherPriority.Send);
        }
        catch { /* justified-silent: the watchdog must never throw on its timer thread */ }
    }

    public void Dispose()
    {
        _watchdog?.Dispose();
        if (_threadId != 0) Win32.PostThreadMessage(_threadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(500);
        _ready.Dispose();
        _rebound.Dispose();
    }
}
