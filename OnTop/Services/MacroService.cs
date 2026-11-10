using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OnTop.Helpers;
using OnTop.Models;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// The macro module (roadmap End Goal, core). Self-contained by design: with
/// MacrosEnabled off (the default) this class installs NOTHING and does
/// NOTHING — the pinning core stays hook-free of it.
///
/// SECURITY.md "Macro Module" governs everything here:
///  • The keyboard+mouse LL hooks exist ONLY between StartRecording and
///    StopRecording — installed at start, removed at stop, verifiably below.
///  • Recorded keystrokes live ONLY in the user's local config; the log gets
///    event COUNTS, never content.
///  • SendInput synthesis happens ONLY inside user-initiated playback.
///  • Anti-cheat guard: playback REFUSES to start while any Stealth watched
///    process is running, aborts mid-run if one appears, and always has a
///    panic stop (Ctrl+Alt+X while playing, the macro's own hotkey, the Stop
///    button, or the kill switch).
/// </summary>
public class MacroService : IDisposable
{
    private readonly MainViewModel _vm;

    // Recording state
    private IntPtr _mouseHook = IntPtr.Zero, _keyHook = IntPtr.Zero;
    private Win32.HookProc? _mouseProc, _keyProc;   // kept alive vs GC (classic hook bug)
    private Macro? _recording;
    private Stopwatch? _clock;
    private const int MaxEventsPerMacro = 20_000;

    // Playback state
    private volatile bool _stopFlag;
    private Task? _playTask;
    /// <summary>Id of the macro currently playing, or null.</summary>
    public string? PlayingId { get; private set; }
    public bool IsRecording => _recording is not null;
    public event Action? StateChanged;
    /// <summary>S28: live recording telemetry for the status bar (UI polls).</summary>
    public int RecordingEventCount => _recording?.Events.Count ?? 0;
    public long RecordingElapsedMs => _clock?.ElapsedMilliseconds ?? 0;
    /// <summary>Name + event count of the most recently SAVED recording (for the
    /// "✔ Saved" state); null until something is saved this session.</summary>
    public (string Name, int Events)? LastSaved { get; private set; }
    /// <summary>S28 Stage G: seconds left in the pre-play countdown (0 = not counting).</summary>
    public int CountdownRemaining { get; private set; }
    /// <summary>S28 Stage G: title being waited for during a K=10 step (null otherwise).</summary>
    public string? WaitingFor { get; private set; }
    /// <summary>S28 Stage I: DRY RUN — narrate every step on the status strip, send NOTHING. Session-only.</summary>
    public bool DryRun { get; set; }
    public string? DryStep { get; private set; }
    /// <summary>S28 Stage I: paused mid-playback (panic still stops).</summary>
    public bool IsPaused => _pauseFlag;
    private volatile bool _pauseFlag;
    public void TogglePause()
    {
        if (PlayingId is null) return;
        _pauseFlag = !_pauseFlag;
        _vm.Log.Action(_pauseFlag ? "Macro playback PAUSED" : "Macro playback RESUMED", "Recording");
        Toast(_pauseFlag ? "Macro Paused — Same Hotkey Resumes" : "Macro Resumed");
        StateChanged?.Invoke();
    }
    public Macro? Playing => PlayingId is null ? null : _vm.Cfg.Macros.Find(m => m.Id == PlayingId);

    /// <summary>S28: the record hotkey — press to start (auto-named), press
    /// again to stop &amp; save. The stop press is trimmed from the tail.</summary>
    public void ToggleRecording()
    {
        if (IsRecording) { StopRecording(save: true); return; }
        StartRecording($"Macro {_vm.Cfg.Macros.Count + 1}");
    }

    public MacroService(MainViewModel vm) => _vm = vm;

    /// <summary>Known kernel-level / invasive anti-cheat services (roadmap End
    /// Goal E "Known-risky-process list"). Process names WITHOUT .exe. Checked
    /// by the guard when Cfg.MacroGuardKnownAntiCheat is on (default) in
    /// addition to the user's Stealth watched list. Maintained here; extend
    /// freely — a false positive only refuses a macro, never breaks the app.</summary>
    public static readonly string[] KnownAntiCheat =
    {
        "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService", "BattlEye",
        "vgc", "vgtray",                       // Riot Vanguard
        "FACEITService", "FACEIT",
        "PnkBstrA", "PnkBstrB",                // PunkBuster
        "GameGuard", "GameMon", "nProtect",
        "xigncode3", "XIGNCODE", "ACE-Tray", "ACE-BASE", "ACE-Guard",
        "mhyprot2", "HoYoProtect",
        "RicochetAC", "TenProtect", "TP3Helper",
    };

    /// <summary>S28: the active profile's stance. DENY → no macro hotkeys bind,
    /// playback/recording refuse. Read live so a switch takes effect instantly.</summary>
    public bool ActiveProfileDenies()
    {
        var p = _vm.Cfg.Profiles.Find(x => string.Equals(x.Name, _vm.Cfg.ActiveProfile, StringComparison.OrdinalIgnoreCase));
        return p is not null && p.MacroPolicy == Profile.PolicyDeny;
    }

    // ── Recording ───────────────────────────────────────────────────────
    public bool StartRecording(string name)
    {
        if (RefuseIfLoading("StartRecording")) return false;
        if (!_vm.Cfg.MacrosEnabled) { Toast("Macros Are Disabled (Master Toggle)"); return false; }
        if (ActiveProfileDenies()) { Toast($"Profile \"{_vm.Cfg.ActiveProfile}\" Denies Macros"); return false; }
        if (IsRecording || PlayingId is not null) { Toast("Already Recording Or Playing"); return false; }
        if (_vm.Cfg.Macros.Count >= 50) { Toast("Macro Limit Reached (50)"); return false; }

        _recording = new Macro { Name = string.IsNullOrWhiteSpace(name) ? $"Macro {_vm.Cfg.Macros.Count + 1}" : name.Trim() };
        // S28 Stage G: remember the front window so Relative replay can follow it later.
        try
        {
            var fg = Win32.GetForegroundWindow();
            if (fg != IntPtr.Zero && !Win32.IsOwnWindow(fg) && Win32.GetWindowRect(fg, out var rc))
            {
                _recording.AnchorTitle = Win32.GetWindowTitle(fg);
                _recording.AnchorLeft = rc.Left; _recording.AnchorTop = rc.Top;
            }
        }
        catch (Exception ex) { _vm.Log.Debug($"Anchor capture: {ex.Message}", "Recording"); }
        _clock = Stopwatch.StartNew();

        var mod = Win32.GetModuleHandle(null);
        _mouseProc = MouseHookProc;
        _keyProc = KeyHookProc;
        _mouseHook = Win32.SetHook(Win32.WH_MOUSE_LL2, _mouseProc, mod, 0);
        _keyHook = Win32.SetHook(Win32.WH_KEYBOARD_LL, _keyProc, mod, 0);
        if (_mouseHook == IntPtr.Zero || _keyHook == IntPtr.Zero)
        {
            _vm.Log.Error("Macro recording hooks failed to install", "Recording");
            AbortRecordingHooks();
            _recording = null;
            Toast("Recording Failed — Hooks Unavailable");
            return false;
        }
        _vm.Log.Action($"MACRO RECORDING STARTED: {_recording.Name} (kbd+mouse hooks installed for the duration)", "Recording");
        Toast($"Recording '{_recording.Name}' — Stop From The Macros Window");
        StateChanged?.Invoke();
        return true;
    }

    public void StopRecording(bool save = true)
    {
        if (_recording is null) return;
        AbortRecordingHooks();                      // hooks come OFF first, always
        var m = _recording;
        _recording = null;
        _clock = null;

        // The stop click/hotkey itself lands in the tail — drop the final 300 ms.
        if (m.Events.Count > 0)
        {
            uint cutoff = m.Events[^1].T > 300 ? m.Events[^1].T - 300 : 0;
            m.Events.RemoveAll(e => e.T > cutoff && e.K >= 1);   // keep trailing moves
        }
        // S28 K BUG FIX (from testing: the recording still contains the Ctrl+Alt+M start
        // chord): the hotkey fires on the LAST key-down, so the hooks then see the
        // chord's key-UPS (orphan ups at the start); at stop the chord's key-DOWNS
        // arrive before the hook is removed (orphan downs at the end). Strip both.
        TrimChordEdges(m.Events);
        // Actions-only capture (S28, roadmap "lighter capture modes"): drop the
        // pure mouse path. Buttons re-anchor absolutely at replay, so clicks
        // still land where recorded — the macro just doesn't "draw" the path.
        if (!_vm.Cfg.MacroRecordPath)
        {
            int before = m.Events.Count;
            m.Events.RemoveAll(e => e.K == 0);
            _vm.Log.Debug($"Actions-only capture: dropped {before - m.Events.Count} move events", "Recording");
        }

        if (save && m.Events.Count > 0)
        {
            _vm.Cfg.Macros.Add(m);
            _vm.Settings.SaveNoBackup(_vm.Cfg);
            LastSaved = (m.Name, m.Events.Count);
            _vm.Log.Action($"MACRO SAVED: {m.Name} — {m.Events.Count} events, {m.DurationMs / 1000.0:0.0}s (content stored in config only)", "Recording");
            Toast($"Saved '{m.Name}' — {m.Events.Count} Events, {m.DurationMs / 1000.0:0.0}s");
        }
        else
        {
            _vm.Log.Action("Macro recording discarded", "Recording");
            Toast("Recording Discarded");
        }
        StateChanged?.Invoke();
    }

    /// <summary>Remove key-ups whose key was never pressed inside the recording
    /// (held before it started) and key-downs never released inside it (still held
    /// when it stopped). Auto-repeat (down,down,down,up) is preserved.</summary>
    private static void TrimChordEdges(List<MacroEvent> ev)
    {
        var downSeen = new HashSet<int>();
        ev.RemoveAll(e =>
        {
            if (e.K == 8) { downSeen.Add(e.D); return false; }
            return e.K == 9 && !downSeen.Contains(e.D);
        });
        var upAfter = new HashSet<int>();
        for (int i = ev.Count - 1; i >= 0; i--)
        {
            var e = ev[i];
            if (e.K == 9) upAfter.Add(e.D);
            else if (e.K == 8 && !upAfter.Contains(e.D)) ev.RemoveAt(i);
        }
    }

    private void AbortRecordingHooks()
    {
        if (_mouseHook != IntPtr.Zero) { Win32.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        if (_keyHook != IntPtr.Zero) { Win32.UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
        _mouseProc = null;
        _keyProc = null;
    }

    private void Record(byte kind, int x, int y, int data)
    {
        var m = _recording;
        if (m is null || _clock is null) return;
        // Normalize to the 0..65535 virtual-desktop space AT RECORD TIME so
        // playback can re-project onto whatever layout exists then.
        int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        int vw = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN));
        int vh = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
        m.Events.Add(new MacroEvent
        {
            T = (uint)_clock.ElapsedMilliseconds,
            K = kind,
            X = (int)(((long)(x - vx)) * 65535 / vw),
            Y = (int)(((long)(y - vy)) * 65535 / vh),
            D = data,
        });
        if (m.Events.Count >= MaxEventsPerMacro)
        {
            _vm.Log.Warn($"Macro hit the {MaxEventsPerMacro}-event cap — auto-stopping", "Recording");
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => StopRecording(save: true));
        }
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recording is not null)
        {
            try
            {
                var info = Marshal.PtrToStructure<MSLL>(lParam);
                switch ((int)wParam)
                {
                    case 0x0200: Record(0, info.pt.X, info.pt.Y, 0); break;                       // MOVE
                    case 0x0201: Record(1, info.pt.X, info.pt.Y, 0); break;                       // LDOWN
                    case 0x0202: Record(2, info.pt.X, info.pt.Y, 0); break;                       // LUP
                    case 0x0204: Record(3, info.pt.X, info.pt.Y, 0); break;                       // RDOWN
                    case 0x0205: Record(4, info.pt.X, info.pt.Y, 0); break;                       // RUP
                    case 0x0207: Record(5, info.pt.X, info.pt.Y, 0); break;                       // MDOWN
                    case 0x0208: Record(6, info.pt.X, info.pt.Y, 0); break;                       // MUP
                    case 0x020A: Record(7, info.pt.X, info.pt.Y, (short)(info.mouseData >> 16)); break; // WHEEL
                }
            }
            catch (Exception ex) { _vm.Log.Debug($"Macro mouse capture: {ex.Message}", "Recording"); }
        }
        return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _recording is not null)
        {
            try
            {
                var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;
                if (msg is 0x0100 or 0x0104) Record(8, 0, 0, (int)info.vkCode);   // KEYDOWN/SYS
                else if (msg is 0x0101 or 0x0105) Record(9, 0, 0, (int)info.vkCode); // KEYUP/SYS
            }
            catch (Exception ex) { _vm.Log.Debug($"Macro key capture: {ex.Message}", "Recording"); }
        }
        return Win32.CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLL { public P pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct P { public int X, Y; }

    // ── Playback ────────────────────────────────────────────────────────
    public void Play(Macro m) => Play(m, 0);

    /// <summary>depth = chain depth (0 = user-started). Chains stop at 5.</summary>
    private void Play(Macro m, int depth)
    {
        if (RefuseIfLoading("Play")) return;
        if (!_vm.Cfg.MacrosEnabled) { Toast("Macros Are Disabled (Master Toggle)"); return; }
        if (ActiveProfileDenies())
        {
            _vm.Log.Warn($"Macro playback REFUSED — profile \"{_vm.Cfg.ActiveProfile}\" policy is DENY", "Recording");
            Toast($"Profile \"{_vm.Cfg.ActiveProfile}\" Denies Macros");
            return;
        }
        if (IsRecording) { Toast("Stop Recording First"); return; }
        if (PlayingId is not null) { Toast("A Macro Is Already Playing (Ctrl+Alt+X Stops It)"); return; }
        if (m.Events.Count == 0) { Toast("Macro Is Empty"); return; }

        // ANTI-CHEAT GUARD: refuse while any watched process is running.
        string? watched = FindRunningWatched();
        if (watched is not null)
        {
            // S28 Stage D: a refusal at START is already safe — the kill switch is
            // reserved for a process that APPEARS mid-run. (From testing: a
            // persistent service like PnkBstrA tripped it before playback even
            // began; use Known Anti-Cheat... to exclude such a service.)
            _vm.Log.Warn($"Macro playback REFUSED — '{watched}' is running (anti-cheat guard). Exclude it in Macros → Known Anti-Cheat... if it is harmless on your setup.", "Recording");
            Toast($"Playback Refused — '{watched}' Is Running. (Macros → Known Anti-Cheat… To Exclude It)");
            return;
        }

        _stopFlag = false; _pauseFlag = false; DryStep = null;
        PlayingId = m.Id;
        _vm.Log.Action($"MACRO {(DryRun ? "DRY RUN" : "PLAYBACK")}: {m.Name} — {m.Events.Count} events, speed {m.Speed:0.#}x, repeat {(m.Repeat == 0 ? "∞" : m.Repeat.ToString())} (panic: Ctrl+Alt+X)", "Recording");
        Toast($"Playing '{m.Name}' — Ctrl+Alt+X Stops");
        StateChanged?.Invoke();

        int panicId = _vm.Hotkeys.Register("Ctrl+Alt+X", Stop);   // panic ONLY while playing
        int countdown = Math.Clamp(_vm.Cfg.MacroCountdownSec, 0, 5);
        // S28 Stage G: relative replay — find the anchor window NOW and compute
        // how far it moved since recording, in normalized units.
        int ox = 0, oy = 0;
        if (m.Relative && m.AnchorTitle.Length > 0)
        {
            var hit = Win32.GetVisibleWindows().FirstOrDefault(w => w.Title.IndexOf(m.AnchorTitle, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hit.Hwnd != IntPtr.Zero && Win32.GetWindowRect(hit.Hwnd, out var rc))
            {
                int vw = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN));
                int vh = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
                ox = (int)((long)(rc.Left - m.AnchorLeft) * 65535 / vw);
                oy = (int)((long)(rc.Top - m.AnchorTop) * 65535 / vh);
                _vm.Log.Info($"Relative replay: '{m.AnchorTitle}' moved {rc.Left - m.AnchorLeft},{rc.Top - m.AnchorTop} px since recording", "Recording");
            }
            else { _vm.Log.Warn($"Relative replay: window '{m.AnchorTitle}' not found — playing at recorded positions", "Recording"); }
        }

        _playTask = Task.Run(() =>
        {
            try
            {
                // S28 Stage G: 3-2-1 countdown (hands off the keyboard).
                for (int c = countdown; c > 0 && !_stopFlag; c--)
                {
                    CountdownRemaining = c; StateChanged?.Invoke();
                    Thread.Sleep(1000);
                }
                CountdownRemaining = 0; StateChanged?.Invoke();
                if (_stopFlag) return;
                double speed = Math.Clamp(m.Speed, 0.1, 10.0);
                int repeat = Math.Clamp(m.Repeat, 0, 1000);
                int guardTick = 0;
                // Humanizing jitter (S28): ±N px converted to normalized units
                // ONCE per run against the current virtual desktop.
                int jitterPx = Math.Clamp(m.Jitter, 0, 20);
                int jx = 0, jy = 0;
                if (jitterPx > 0)
                {
                    int vw = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN));
                    int vh = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
                    jx = jitterPx * 65535 / vw;
                    jy = jitterPx * 65535 / vh;
                }
                var rng = new Random();
                for (int r = 0; (repeat == 0 || r < repeat) && !_stopFlag; r++)
                {
                    long start = Stopwatch.GetTimestamp();
                    foreach (var ev in m.Events)
                    {
                        if (_stopFlag) break;
                        // Pace to the recorded timeline (scaled), absolute-anchored
                        // so drift can't accumulate.
                        double targetMs = ev.T / speed;
                        while (!_stopFlag)
                        {
                            double elapsed = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                            double remain = targetMs - elapsed;
                            if (remain <= 0) break;
                            if (remain > 20) Thread.Sleep(10); else Thread.Sleep(0);
                        }
                        if (_stopFlag) break;
                        // S28 Stage I: pause — hold here, then re-base the timeline.
                        if (_pauseFlag)
                        {
                            long p0 = Stopwatch.GetTimestamp();
                            while (_pauseFlag && !_stopFlag) Thread.Sleep(50);
                            start += Stopwatch.GetTimestamp() - p0;
                            if (_stopFlag) break;
                        }
                        // S28 Stage H: repeat-until-key (polled — no hook).
                        if (m.StopKey > 0 && (Win32.GetAsyncKeyState(m.StopKey) & 0x8000) != 0)
                        {
                            _stopFlag = true;
                            _vm.Log.Action($"Macro '{m.Name}' stopped by its stop key", "Recording");
                            break;
                        }
                        if (ev.K == 10)
                        {
                            // WAIT FOR WINDOW (a wait, not a verb): poll titles until match or timeout.
                            string want = ev.S ?? "";
                            WaitingFor = want; StateChanged?.Invoke();
                            long w0 = Stopwatch.GetTimestamp(); bool found = false;
                            while (!_stopFlag && (Stopwatch.GetTimestamp() - w0) * 1000.0 / Stopwatch.Frequency < ev.D)
                            {
                                if (Win32.GetVisibleWindows().Any(w => w.Title.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)) { found = true; break; }
                                Thread.Sleep(100);
                            }
                            WaitingFor = null; StateChanged?.Invoke();
                            if (!found && !_stopFlag)
                            {
                                _stopFlag = true;
                                _vm.Log.Warn($"Macro '{m.Name}' stopped — window '{want}' did not appear within {ev.D} ms", "Recording");
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => Toast($"Macro Stopped — '{want}' Never Appeared"));
                            }
                            // Re-base the timeline so the wait doesn't make later steps fire late.
                            start = Stopwatch.GetTimestamp() - (long)(ev.T / speed * Stopwatch.Frequency / 1000.0);
                            continue;
                        }
                        if (DryRun)
                        {
                            DryStep = $"{Views.MacroEditorWindow.KindName(ev.K)} · {Views.MacroEditorWindow.DetailText(ev)}";
                            if ((guardTick & 3) == 0) StateChanged?.Invoke();
                        }
                        else Emit(ev, jx, jy, rng, ox, oy);

                        // Mid-run anti-cheat check (~every 64 events).
                        if ((++guardTick & 63) == 0 && FindRunningWatched() is string w)
                        {
                            _stopFlag = true;
                            _vm.Log.Warn($"Macro playback ABORTED — watched process appeared: {w}", "Recording");
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                Toast($"Playback Stopped — '{w}' Appeared (Anti-Cheat Guard)");
                                TripKillSwitch(w, "aborted mid-run");
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { _vm.Log.Error($"Macro playback failed: {ex.Message}", "Recording"); }
            finally
            {
                bool stopped = _stopFlag;
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    _vm.Hotkeys.Unregister(panicId);
                    PlayingId = null; _pauseFlag = false; DryStep = null;
                    _vm.Log.Action(DryRun ? "Macro DRY RUN ended (nothing was sent)" : "Macro playback ended", "Recording");
                    StateChanged?.Invoke();
                    // S28 Stage G: chaining — only on a NATURAL finish, depth-capped, no self-loop.
                    if (!stopped && m.NextMacroId.Length > 0 && depth < 5)
                    {
                        var next = _vm.Cfg.Macros.Find(x => x.Id == m.NextMacroId && x.Id != m.Id);
                        if (next is null) Toast($"'{m.Name}': Next Macro Not Found");
                        else if (!next.Enabled) Toast($"'{m.Name}': Next Macro '{next.Name}' Is Disabled");
                        else { _vm.Log.Action($"Macro chain: '{m.Name}' → '{next.Name}' (depth {depth + 1})", "Recording"); Play(next, depth + 1); }
                    }
                    else if (!stopped && m.NextMacroId.Length > 0) Toast("Macro Chain Stopped — Depth Limit (5)");
                });
            }
        });
    }

    public void Stop()
    {
        if (PlayingId is null) return;
        _stopFlag = true;
        Toast("Macro Playback Stopped");
    }

    /// <summary>jx/jy = jitter half-range (normalized units); ox/oy = relative-replay
    /// offset (normalized). Both apply only to mouse coordinates.</summary>
    private static void Emit(MacroEvent ev, int jx, int jy, Random rng, int ox, int oy)
    {
        var inputs = new Win32.INPUT[1];
        if (ev.K <= 7)
        {
            uint flags = ev.K switch
            {
                0 => Win32.MOUSEEVENTF_MOVE,
                1 => Win32.MOUSEEVENTF_LEFTDOWN, 2 => Win32.MOUSEEVENTF_LEFTUP,
                3 => Win32.MOUSEEVENTF_RIGHTDOWN, 4 => Win32.MOUSEEVENTF_RIGHTUP,
                5 => Win32.MOUSEEVENTF_MIDDLEDOWN, 6 => Win32.MOUSEEVENTF_MIDDLEUP,
                _ => Win32.MOUSEEVENTF_WHEEL,
            };
            bool isWheel = ev.K == 7;
            // Button events (1–6) also re-anchor to their recorded position so the
            // click lands where it was recorded (absolute, re-projected onto the
            // CURRENT virtual desktop). Wheel scrolls at the current position — no
            // move rider. Pure move (0) is already MOVE.
            if (!isWheel)
                flags |= Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK
                         | (ev.K != 0 ? Win32.MOUSEEVENTF_MOVE : 0);
            inputs[0] = new Win32.INPUT
            {
                type = Win32.INPUT_MOUSE,
                U = new Win32.InputUnion
                {
                    mi = new Win32.MOUSEINPUT
                    {
                        dx = isWheel ? 0 : Math.Clamp(ev.X + ox + (jx > 0 ? rng.Next(-jx, jx + 1) : 0), 0, 65535),
                        dy = isWheel ? 0 : Math.Clamp(ev.Y + oy + (jy > 0 ? rng.Next(-jy, jy + 1) : 0), 0, 65535),
                        mouseData = isWheel ? unchecked((uint)(ev.D << 16)) : 0,
                        dwFlags = flags,
                    },
                },
            };
        }
        else
        {
            inputs[0] = new Win32.INPUT
            {
                type = Win32.INPUT_KEYBOARD,
                U = new Win32.InputUnion
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = (ushort)ev.D,
                        dwFlags = ev.K == 9 ? Win32.KEYEVENTF_KEYUP : 0,
                    },
                },
            };
        }
        Win32.SendInput(1, inputs, Win32.INPUT.Size);
    }

    /// <summary>Guard layer 1 lookup: the user's Stealth watched list PLUS (when
    /// MacroGuardKnownAntiCheat is on) the built-in KnownAntiCheat list. Returns
    /// the first running name, or null. Public so the UI can show "guard active".</summary>
    public string? FindRunningWatched()
    {
        string list = _vm.Cfg.StealthWatchedProcesses;
        if (!string.IsNullOrWhiteSpace(list))
            foreach (var raw in list.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = raw.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                if (name.Length == 0) continue;
                if (IsRunning(name)) return name;
            }
        if (_vm.Cfg.MacroGuardKnownAntiCheat)
            foreach (var name in KnownAntiCheat)
            {
                if (_vm.Cfg.MacroGuardIgnored.Exists(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))) continue;   // user-excluded
                if (IsRunning(name)) return name;
            }
        return null;
    }

    private static bool IsRunning(string name)
    {
        var procs = Process.GetProcessesByName(name);
        bool any = procs.Length > 0;
        foreach (var pr in procs) pr.Dispose();
        return any;
    }

    /// <summary>Guard layer 3 — the KILL SWITCH (roadmap End Goal D). A guard hit
    /// doesn't merely refuse/abort: it turns the master toggle OFF, which
    /// unbinds every macro hotkey, and stays off until the user re-enables it
    /// by hand. Off-able via Cfg.MacroKillSwitch (default ON). Always logged as
    /// an ACTION and toasted at screen center (critical zone). UI-thread only.</summary>
    private void TripKillSwitch(string process, string context)
    {
        if (!_vm.Cfg.MacroKillSwitch || !_vm.Cfg.MacrosEnabled) return;
        _vm.Cfg.MacrosEnabled = false;
        _vm.BindHotkeys();
        _vm.Settings.SaveNoBackup(_vm.Cfg);
        _vm.Log.Action($"MACRO KILL SWITCH: '{process}' detected ({context}) — macros DISABLED (master toggle off, hotkeys unbound) until re-enabled by hand", "Recording");
        ToastService.Show($"Macros Turned OFF — '{process}' Appeared (Kill Switch). Tick 'Macros Enabled' In The Macros Window To Re-Arm.", ToastType.Error, 9000);
        StateChanged?.Invoke();
    }

    private static void Toast(string msg)
        => System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            ToastService.Show(msg, ToastType.Info));

    /// <summary>S28 inert-load guard (SECURITY.md): synthesis must never start
    /// while a config is being read. Should never fire; if it does, it's a bug.</summary>
    private bool RefuseIfLoading(string what)
    {
        if (!SettingsService.IsLoading) return false;
        _vm.Log.Error($"INERT-LOAD GUARD: MacroService.{what} invoked during a config load — refused (report this)", "Recording");
        Toast("Blocked: Macro Action Attempted During Config Load (See Log)");
        return true;
    }

    public void Dispose()
    {
        _stopFlag = true;
        StopRecording(save: false);
    }
}
