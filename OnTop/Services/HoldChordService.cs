using System;
using System.Runtime.InteropServices;
using OnTop.Helpers;
using OnTop.ViewModels;

namespace OnTop.Services;

/// <summary>
/// S28 Stage H — "hold" chords: Hold-To-Peek (overlays visible while held) and
/// Hold-To-Pin (focused window topmost while held). These need to see KEY-UP,
/// which RegisterHotKey cannot deliver, so this installs a WH_KEYBOARD_LL hook —
/// under the SAME lifecycle rule the macro recorder already follows
/// (SECURITY.md "Input hook", rewritten S28): the hook exists ONLY while the
/// feature switch Cfg.HoldChordsEnabled is on AND at least one chord is set;
/// it is removed the instant either stops being true, on stealth, and on exit.
/// It never stores or forwards a keystroke — it compares against two chords
/// and returns. Everything else about it is observation-only.
/// </summary>
public class HoldChordService : IDisposable
{
    private readonly MainViewModel _vm;
    private IntPtr _hook = IntPtr.Zero;
    private Win32.HookProc? _proc;
    private (uint Mod, uint Vk) _peek, _pin;
    private bool _peekHeld, _pinHeld;
    private IntPtr _pinnedByHold = IntPtr.Zero;

    public HoldChordService(MainViewModel vm) => _vm = vm;
    public bool IsHooked => _hook != IntPtr.Zero;

    /// <summary>Re-evaluate: install or remove the hook to match config.</summary>
    public void Sync()
    {
        bool peekOk = HotkeyService.ParseHotkey(_vm.Cfg.HotkeyHoldPeek, out var pm, out var pv);
        bool pinOk = HotkeyService.ParseHotkey(_vm.Cfg.HotkeyHoldPin, out var qm, out var qv);
        _peek = peekOk ? (pm, pv) : (0, 0);
        _pin = pinOk ? (qm, qv) : (0, 0);
        bool want = _vm.Cfg.HoldChordsEnabled && !_vm.Cfg.StealthMode && !_vm.HotkeysSuspended && (peekOk || pinOk);
        if (want && _hook == IntPtr.Zero)
        {
            _proc = Proc;
            _hook = Win32.SetHook(Win32.WH_KEYBOARD_LL, _proc, Win32.GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) { _vm.Log.Error("Hold-chord keyboard hook failed to install", "Hotkey"); _proc = null; }
            else _vm.Log.Action("Hold-chord keyboard hook INSTALLED (hold-to-peek/pin armed)", "Hotkey");
        }
        else if (!want && _hook != IntPtr.Zero) Remove("disarmed");
    }

    private void Remove(string why)
    {
        if (_hook != IntPtr.Zero) { Win32.UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; _proc = null; _vm.Log.Action($"Hold-chord keyboard hook REMOVED ({why})", "Hotkey"); }
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        if (_peekHeld) { _peekHeld = false; _vm.RequestPeekHold(false); }
        if (_pinHeld) { _pinHeld = false; UnpinHeld(); }
    }

    private static bool ModsDown(uint mod)
    {
        bool Down(int vk) => (Win32.GetAsyncKeyState(vk) & 0x8000) != 0;
        if ((mod & Win32.MOD_CONTROL) != 0 && !Down(0x11)) return false;
        if ((mod & Win32.MOD_ALT) != 0 && !Down(0x12)) return false;
        if ((mod & Win32.MOD_SHIFT) != 0 && !Down(0x10)) return false;
        if ((mod & Win32.MOD_WIN) != 0 && !(Down(0x5B) || Down(0x5C))) return false;
        return true;
    }

    private IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;
                bool down = msg is 0x0100 or 0x0104, up = msg is 0x0101 or 0x0105;
                uint vk = info.vkCode;
                if (down)
                {
                    if (_peek.Vk != 0 && vk == _peek.Vk && !_peekHeld && ModsDown(_peek.Mod))
                    { _peekHeld = true; System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _vm.RequestPeekHold(true)); }
                    if (_pin.Vk != 0 && vk == _pin.Vk && !_pinHeld && ModsDown(_pin.Mod))
                    { _pinHeld = true; System.Windows.Application.Current.Dispatcher.BeginInvoke(PinHeld); }
                }
                else if (up)
                {
                    // Releasing the key OR any of its modifiers ends the hold.
                    bool isMod = vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
                    if (_peekHeld && (vk == _peek.Vk || isMod)) { _peekHeld = false; System.Windows.Application.Current.Dispatcher.BeginInvoke(() => _vm.RequestPeekHold(false)); }
                    if (_pinHeld && (vk == _pin.Vk || isMod)) { _pinHeld = false; System.Windows.Application.Current.Dispatcher.BeginInvoke(UnpinHeld); }
                }
            }
        }
        catch (Exception ex) { _vm.Log.Debug($"Hold-chord hook: {ex.Message}", "Hotkey"); }
        return Win32.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void PinHeld()
    {
        var fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero || Win32.IsOwnWindow(fg) || Win32.IsTopmost(fg)) return;   // already topmost: nothing to do, nothing to undo
        Win32.SetTopmost(fg, true);
        _pinnedByHold = fg;
        _vm.Log.Action($"Hold-to-pin ON — {Win32.GetProcessName(fg) ?? "window"}", "Pins");
    }

    private void UnpinHeld()
    {
        if (_pinnedByHold == IntPtr.Zero) return;
        if (Win32.IsWindow(_pinnedByHold)) Win32.SetTopmost(_pinnedByHold, false);
        _vm.Log.Action("Hold-to-pin released", "Pins");
        _pinnedByHold = IntPtr.Zero;
    }

    public void Dispose() => Remove("dispose");
}
