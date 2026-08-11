using System.Runtime.InteropServices;
using System.Text;

namespace RigToggle.Windows;

/// <summary>
/// Hand-rolled user32.dll P/Invoke signatures used by FindBestMainWindow/MinimizeIfRunning
/// (APP-02/APP-03), a dwmapi.dll signature used by DwmTitleBar for Mica/rounded-corner
/// theming (THEME-06), and a second dwmapi.dll signature used by WindowsThemeProvider as
/// THEME-07's accent-color fallback read. Deliberately does NOT include FindWindow/
/// FindWindowEx (title-based lookup is forbidden — CLAUDE.md "What NOT to Use") and does
/// NOT take a dependency on the PInvoke.User32 NuGet package (CLAUDE.md Alternatives
/// Considered: hand-rolling is preferred for a surface this small). Stable,
/// decades-unchanged Win32 API (learn.microsoft.com/windows/win32/api/winuser).
/// LaunchOrFocus no longer touches any window at all (it unconditionally relaunches via
/// ShellExecute instead) -- most of the user32.dll block below exists solely to support
/// MinimizeIfRunning's window lookup and minimize call.
/// </summary>
internal static class NativeMethods
{
    public const int SW_MINIMIZE = 6;

    // GW_OWNER for GetWindow -- used to filter EnumWindows results down to top-level
    // (owner-less) windows only, excluding tooltips/owned popups.
    public const uint GW_OWNER = 4;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Read-only diagnostic queries re-added for the toggle-back investigation into
    // MinimizeIfRunning (quick task 260726-ixu). Not used for any control-flow decision --
    // purely to log the target window's visible/iconic state before and after the
    // ShowWindow(SW_MINIMIZE) call.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    // .NET's own Process.MainWindowHandle only returns a non-zero handle for a
    // top-level, owner-less window where IsWindowVisible == true (documented .NET
    // internal heuristic). Companion apps that use the common WinForms "minimize/close
    // to tray" pattern set their main form's Visible = false (not merely
    // WindowState.Minimized) when hidden to the tray -- MainWindowHandle returns
    // IntPtr.Zero for those even though a live top-level HWND exists and can be shown.
    // EnumWindows + GetWindowThreadProcessId (matched by process ID) finds that window
    // regardless of visibility -- this is PID-based matching, NOT title/class matching
    // (FindWindow/FindWindowEx), which CLAUDE.md forbids.
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    // GetWindowPlacement's rcNormalPosition reports a window's RESTORED size/position
    // regardless of its current minimized/maximized/normal state -- unlike
    // GetWindowRect, which for a minimized (iconic) window reports a degenerate/
    // off-screen rect (historically positioned around x=-32000,y=-32000 with a small
    // nominal size), not the window's real dimensions. FindBestMainWindow must compare
    // window sizes to pick the real main window over a smaller utility window even when
    // the real one is currently iconic (confirmed rig-test repro state:
    // .planning/debug/resolved/moza-foreground-focus.md 2026-07-25T05:00:00Z,
    // IsIconic=True on the main dashboard at comparison time) -- rcNormalPosition stays a
    // stable, correct signal for area comparison across all window states.
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    // TRIG-01 global-hotkey P/Invoke surface: registers/unregisters a system-wide hotkey
    // on a caller-owned message-pump window handle (MainForm's, in this project). Kept
    // internal like the rest of this class -- RigToggle.App has no InternalsVisibleTo
    // grant to this assembly (see AssemblyInfo.cs), so it cannot call these directly.
    // Exposed to RigToggle.App only through the public GlobalHotkey wrapper (GlobalHotkey.cs
    // in this same namespace), never through a new InternalsVisibleTo grant.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // THEME-06 DWM P/Invoke surface: requests title-bar Mica backdrop + rounded window
    // corners on a caller-owned window handle. Kept internal like the rest of this class
    // -- exposed to RigToggle.App only through the public DwmTitleBar façade
    // (DwmTitleBar.cs in this same namespace), never through a new InternalsVisibleTo
    // grant. DWMWA_USE_IMMERSIVE_DARK_MODE is now called explicitly by DwmTitleBar
    // (12-REVIEW.md CR-01) -- the Windows 11 rig test in 12-04 proved the app's white/light
    // title bar in dark mode, falsifying the earlier assumption that Application.SetColorMode
    // alone would flip this attribute. DwmSetWindowAttribute is declared to return int
    // (HRESULT) rather than throwing on failure, so an unsupported OS/attribute is a
    // silently-ignorable non-zero return (D-07) -- no try/catch needed at the call site.
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    // THEME-07 accent-color fallback read, used by WindowsThemeProvider only when the
    // primary HKCU\...\DWM\AccentColor registry value is absent or unreadable (D-01).
    // Returns an HRESULT (0 == S_OK) rather than throwing, matching this class's existing
    // DWM convention -- the caller treats a non-zero return as a failed read, not an
    // exception.
    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetColorizationColor(out uint pcrColorization, [MarshalAs(UnmanagedType.Bool)] out bool pfOpaqueBlend);
}
