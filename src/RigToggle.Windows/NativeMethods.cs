using System.Runtime.InteropServices;

namespace RigToggle.Windows;

/// <summary>
/// Hand-rolled user32.dll P/Invoke signatures for window focus/minimize control
/// (APP-02/APP-03). No analog elsewhere in this repo — this is the project's only
/// P/Invoke surface. Deliberately does NOT include FindWindow/FindWindowEx (title-based
/// lookup is forbidden — CLAUDE.md "What NOT to Use") and does NOT take a dependency on
/// the PInvoke.User32 NuGet package (CLAUDE.md Alternatives Considered: hand-rolling is
/// preferred for a surface this small). Stable, decades-unchanged Win32 API
/// (learn.microsoft.com/windows/win32/api/winuser).
/// </summary>
internal static class NativeMethods
{
    public const int SW_MINIMIZE = 6;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
}
