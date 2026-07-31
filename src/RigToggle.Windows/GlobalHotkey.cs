namespace RigToggle.Windows;

/// <summary>
/// Public cross-assembly façade over the internal RegisterHotKey/UnregisterHotKey
/// P/Invoke in NativeMethods (TRIG-01). Exists because NativeMethods is `internal` to
/// RigToggle.Windows and RigToggle.App has no InternalsVisibleTo grant to this assembly
/// (see AssemblyInfo.cs -- only RigToggle.Windows.Tests is granted that), so the App
/// layer would otherwise get CS0122 trying to call the P/Invoke directly. This mirrors
/// the existing public-adapter convention every other cross-assembly Windows surface in
/// this project follows (WindowsAutostartConfigurator, WindowsAppController,
/// WindowsAudioController, WindowsMonitorController are all `public` for exactly this
/// reason). MainForm calls GlobalHotkey.*, never NativeMethods.* -- do not "simplify"
/// this away by re-exposing NativeMethods or adding an InternalsVisibleTo grant to
/// RigToggle.App; the public wrapper is the deliberate encapsulation boundary.
/// </summary>
public static class GlobalHotkey
{
    /// <summary>WM_HOTKEY -- the window message RegisterHotKey delivers to the owning
    /// window's message pump when the registered combination is pressed.</summary>
    public const int WmHotkey = 0x0312;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;

    public const uint ModWin = 0x0008;

    /// <summary>OR this into fsModifiers so a held-down hotkey fires exactly once per
    /// press instead of autorepeating WM_HOTKEY messages for as long as the key is
    /// held.</summary>
    public const uint ModNoRepeat = 0x4000;

    public static bool Register(IntPtr hWnd, int id, uint fsModifiers, uint vk) =>
        NativeMethods.RegisterHotKey(hWnd, id, fsModifiers, vk);

    public static bool Unregister(IntPtr hWnd, int id) =>
        NativeMethods.UnregisterHotKey(hWnd, id);
}
