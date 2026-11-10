using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace OnTop.Helpers;

/// <summary>
/// Creates/removes the "Start with Windows" shortcut in the user's Startup
/// folder via the IShellLink COM interface — no extra package references.
/// The shortcut carries the exe's own icon.
/// </summary>
public static class StartupShortcut
{
    private static string LinkPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "OnTop.lnk");

    public static bool Exists() => File.Exists(LinkPath);

    /// <summary>Create or remove the shortcut to match the desired state.</summary>
    public static void Apply(bool startWithWindows)
    {
        if (startWithWindows) Create();
        else Remove();
    }

    public static void Create()
    {
        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve the executable path");

        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(exe);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe) ?? "");
            link.SetIconLocation(exe, 0);
            link.SetDescription("OnTop — Always On Top Utility");
            ((IPersistFile)link).Save(LinkPath, false);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    public static void Remove()
    {
        if (File.Exists(LinkPath)) File.Delete(LinkPath);
    }

    // ── COM interop (vtable order must not change) ──────────────────────
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
