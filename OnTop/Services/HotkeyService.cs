using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OnTop.Helpers;

namespace OnTop.Services;

public class HotkeyService : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _handlers = new();
    /// <summary>Ids climb from FirstId and wrap back at LastId (RegisterHotKey's
    /// documented range ends at 0xBFFF), skipping ids still registered. They are
    /// NOT handed out lowest-first: a just-released id must not be reused right
    /// away, or a WM_HOTKEY still queued for its old owner (or a stale id held by
    /// a caller) would reach the new one. A full lap is ~40,000 registrations.</summary>
    private const int FirstId = 9000, LastId = 0xBFFF;
    private int _nextId = FirstId;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    /// <summary>S28 K: Win32 error from the last failed Register (1409 = hotkey already registered elsewhere).</summary>
    public int LastError { get; private set; }

    public int Register(string hotkeyStr, Action callback)
    {
        // -3 = not a valid combo (a config problem, not a conflict: never retried).
        if (!ParseHotkey(hotkeyStr, out uint mod, out uint vk))
            return -3;

        int id;
        do { id = _nextId; _nextId = _nextId >= LastId ? FirstId : _nextId + 1; }
        while (_handlers.ContainsKey(id));
        if (Win32.RegisterHotKey(_hwnd, id, mod | Win32.MOD_NOREPEAT, vk))
        {
            _handlers[id] = callback;
            return id;
        }
        LastError = System.Runtime.InteropServices.Marshal.GetLastWin32Error();   // 1409 = already registered by another program
        return -1;
    }

    public void Unregister(int id)
    {
        if (id < 0) return;
        Win32.UnregisterHotKey(_hwnd, id);
        _handlers.Remove(id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
            Win32.UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses "Ctrl+Alt+R", "F1", "Ctrl+F5", "Alt+F10", "Ctrl+Alt+Q", etc.
    /// </summary>
    public static bool ParseHotkey(string s, out uint modifiers, out uint vk)
    {
        modifiers = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;

        var parts = s.Replace(" ", "").Split('+');
        string keyPart = "";

        foreach (var part in parts)
        {
            switch (part.ToLower())
            {
                case "ctrl": case "control": modifiers |= Win32.MOD_CONTROL; break;
                case "alt": modifiers |= Win32.MOD_ALT; break;
                case "shift": modifiers |= Win32.MOD_SHIFT; break;
                case "win": modifiers |= Win32.MOD_WIN; break;
                default: keyPart = part; break;
            }
        }

        if (string.IsNullOrEmpty(keyPart)) return false;

        // Try parsing as Key enum
        if (Enum.TryParse<Key>(keyPart, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }

        return false;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
