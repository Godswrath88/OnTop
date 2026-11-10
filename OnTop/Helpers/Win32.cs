using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OnTop.Helpers;

public static class Win32
{
    // ── Global Hotkeys ──────────────────────────────────────────────────
    // SetLastError so HotkeyService.LastError / the e-stop bind errors are real (1409 = taken by another program).
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── E-stop thread plumbing (Services/EmergencyStopService) ──────────
    // A hotkey registered with hWnd = NULL is delivered as WM_HOTKEY to the
    // queue of the REGISTERING THREAD — independent of the WPF dispatcher.
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; }
    [DllImport("user32.dll")] public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] public static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
    /// <summary>The hammer: no handlers, no finalizers, no waiting on any other thread.</summary>
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    // ── Window Management ───────────────────────────────────────────────
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [StructLayout(LayoutKind.Sequential)] public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    /// <summary>S28 Stage I: milliseconds since the last keyboard/mouse input (session-wide, a read).</summary>
    public static uint IdleMilliseconds()
    {
        var li = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return GetLastInputInfo(ref li) ? unchecked((uint)Environment.TickCount - li.dwTime) : 0;
    }
    // S28 Stage E/G additions — position/size + layering (foreign windows: geometry + z-order + alpha only).
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr hWnd, out uint crKey, out byte bAlpha, out uint dwFlags);
    [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
    public const uint GA_ROOT = 2;
    public const uint LWA_ALPHA = 0x2;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int SW_MAXIMIZE = 3, SW_MINIMIZE_ = 6;
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOZORDER = 0x0004;
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_TRANSPARENT = 0x00000020;   // click-through (overlay lock)
    public const int WS_EX_TOOLWINDOW = 0x00000080;    // hidden from Alt+Tab
    public const int WS_EX_NOACTIVATE = 0x08000000;    // never steals focus

    // ── DWM Dark Mode ───────────────────────────────────────────────────
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    public const int DWMWA_CLOAKED = 14;
    /// <summary>S28 K: DWM-cloaked windows are "visible" to IsWindowVisible but not on screen (UWP/virtual-desktop ghosts).</summary>
    public static bool IsCloaked(IntPtr hwnd) => DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int v, sizeof(int)) == 0 && v != 0;

    /// <summary>S28 K: the top-level window actually under a point, walking the
    /// z-order from the top and SKIPPING any window the caller excludes (our
    /// zone overlays). Unlike WindowFromPoint, this looks THROUGH the overlays
    /// to whatever is really beneath — so Desktop Only can still tell a desktop
    /// click from a click on another program under the overlay.</summary>
    public static IntPtr TopWindowAtPoint(POINT pt, Func<IntPtr, bool>? skip)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            if (skip is not null && skip(h)) return true;
            if (IsCloaked(h)) return true;
            if (!GetWindowRect(h, out var rc) || rc.Right <= rc.Left || rc.Bottom <= rc.Top) return true;
            if (pt.X < rc.Left || pt.X >= rc.Right || pt.Y < rc.Top || pt.Y >= rc.Bottom) return true;
            found = h; return false;
        }, IntPtr.Zero);
        return found;
    }

    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;
    public const int DWMWA_TEXT_COLOR = 36;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public static void ApplyDarkMode(IntPtr hwnd)
    {
        int val = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref val, sizeof(int));
        ApplyBackdrop(hwnd);
        // Round corners
        int round = 2;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        // Dark caption
        int captionColor = 0x00281A1A; // BGR
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
    }

    /// <summary>Mica (2) when enabled in settings, plain (1) otherwise.
    /// Safe to re-call on live windows when the Settings toggle flips.</summary>
    public static void ApplyBackdrop(IntPtr hwnd)
    {
        // Windows 11 22H2+ has the real Mica attribute; Windows 10 gets the
        // acrylic-blur AccentPolicy fallback (roadmap: Win10 acrylic fallback).
        if (Environment.OSVersion.Version.Build >= 22621)
        {
            int backdrop = ThemeOptions.MicaEnabled ? 2 : 1;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        else if (ThemeOptions.MicaEnabled)
        {
            var accent = new AccentPolicy
            {
                AccentState = 4,                    // ACCENT_ENABLE_ACRYLICBLURBEHIND
                GradientColor = unchecked((int)0xCC281A1A), // AABBGGRR — dark navy tint
            };
            var size = System.Runtime.InteropServices.Marshal.SizeOf(accent);
            IntPtr ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = 19,                 // WCA_ACCENT_POLICY
                    SizeOfData = size,
                    Data = ptr,
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    // ── Input synthesis + capture (macro module; see SECURITY.md "Macros") ──
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;                 // 0 = mouse, 1 = keyboard
        public InputUnion U;
        public static int Size => System.Runtime.InteropServices.Marshal.SizeOf<INPUT>();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    public struct InputUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)] public MOUSEINPUT mi;
        [System.Runtime.InteropServices.FieldOffset(0)] public KEYBDINPUT ki;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    public const uint MOUSEEVENTF_MOVE = 0x0001, MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004,
        MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010, MOUSEEVENTF_MIDDLEDOWN = 0x0020,
        MOUSEEVENTF_MIDDLEUP = 0x0040, MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_ABSOLUTE = 0x8000,
        MOUSEEVENTF_VIRTUALDESK = 0x4000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowsHookExW")]
    public static extern IntPtr SetHook(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
    public const int WH_MOUSE_LL2 = 14;

    // Low-level KEYBOARD hook — installed ONLY while a macro is recording.
    public const int WH_KEYBOARD_LL = 13;
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    // ── Window show-state (per-zone ToggleWindow action) ────────────────
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    // ── Process Info ────────────────────────────────────────────────────
    public static string? GetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;
            // S28 L: QueryFullProcessImageName via a limited-rights handle — one
            // cheap call per window. Process.GetProcessById(...).ProcessName
            // enumerates EVERY process on the machine for EVERY window listed,
            // which made the Pinned Windows scan crawl on busy systems.
            var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h != IntPtr.Zero)
            {
                try
                {
                    var sb = new System.Text.StringBuilder(1024);
                    int len = sb.Capacity;
                    if (QueryFullProcessImageName(h, 0, sb, ref len) && len > 0)
                        return System.IO.Path.GetFileNameWithoutExtension(sb.ToString(0, len));
                }
                finally { CloseHandle(h); }
            }
            // Protected / elevated processes refuse even limited query: fall back.
            return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return null; }
    }
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr hObject);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, System.Text.StringBuilder lpExeName, ref int lpdwSize);

    public static string GetWindowClass(IntPtr hwnd)
    {
        var sb = new System.Text.StringBuilder(256);
        GetClassName(hwnd, sb, 256);
        return sb.ToString();
    }

    public static bool IsDesktopWindow(IntPtr hwnd)
    {
        var cls = GetWindowClass(hwnd);
        return cls is "Progman" or "WorkerW" or "SysListView32";
    }

    // ── Toggle Always On Top ────────────────────────────────────────────
    public static bool IsTopmost(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        return (exStyle & WS_EX_TOPMOST) != 0;
    }

    public static void ToggleTopmost(IntPtr hwnd)
    {
        var insert = IsTopmost(hwnd) ? HWND_NOTOPMOST : HWND_TOPMOST;
        SetWindowPos(hwnd, insert, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public static void SetTopmost(IntPtr hwnd, bool topmost)
        => SetWindowPos(hwnd, topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
                        0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    private static readonly uint OwnPid = (uint)System.Environment.ProcessId;

    /// <summary>True if the window belongs to THIS process (OnTop's own GUI).
    /// SECURITY/QOL: we never let foreign-window paths (toggle, unpin, rules)
    /// touch our own windows — that's how "Unpin All" once buried the pins GUI.</summary>
    public static bool IsOwnWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == OwnPid;
    }

    // ── Window enumeration (Pinned Windows viewer) ──────────────────────
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    public static string GetWindowTitle(IntPtr hwnd)
    {
        int len = GetWindowTextLength(hwnd);
        if (len <= 0) return "";
        // SECURITY: cap length — a hostile window could report an enormous title.
        len = Math.Min(len, 512);
        var sb = new System.Text.StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        // SECURITY: titles are attacker-controlled text that ends up in our logs
        // and UI — strip control chars (incl. newlines) to block log forging.
        var raw = sb.ToString();
        var clean = new System.Text.StringBuilder(raw.Length);
        foreach (char ch in raw)
            clean.Append(char.IsControl(ch) ? ' ' : ch);
        return clean.ToString();
    }

    /// <summary>Every visible, titled, topmost top-level window — i.e. what's
    /// currently pinned above others (by us or anything else). Excludes tool
    /// windows without titles and the desktop shell.</summary>
    public static List<(IntPtr Hwnd, string Title, string Process)> GetTopmostWindows()
    {
        var result = new List<(IntPtr, string, string)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || !IsTopmost(hwnd) || IsDesktopWindow(hwnd))
                return true;
            if (IsOwnWindow(hwnd)) return true;   // never list/manage our own GUI
            string title = GetWindowTitle(hwnd);
            if (title.Length == 0) return true;
            result.Add((hwnd, title, GetProcessName(hwnd) ?? "?"));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Every visible, titled top-level window regardless of topmost state
    /// (Window Rules scans these). Own windows and the desktop shell excluded.</summary>
    public static List<(IntPtr Hwnd, string Title, string Process)> GetVisibleWindows()
    {
        var result = new List<(IntPtr, string, string)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || IsDesktopWindow(hwnd)) return true;
            if (IsOwnWindow(hwnd)) return true;
            string title = GetWindowTitle(hwnd);
            if (title.Length == 0) return true;
            result.Add((hwnd, title, GetProcessName(hwnd) ?? "?"));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }
}
