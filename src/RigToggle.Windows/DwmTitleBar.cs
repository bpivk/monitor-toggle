namespace RigToggle.Windows;

/// <summary>
/// Public cross-assembly façade over the internal DwmSetWindowAttribute P/Invoke in
/// NativeMethods (THEME-06). Exists because NativeMethods is `internal` to
/// RigToggle.Windows and RigToggle.App has no InternalsVisibleTo grant to this assembly,
/// so the App layer would otherwise get CS0122 trying to call the P/Invoke directly.
/// This mirrors the existing public-adapter convention every other cross-assembly
/// Windows surface in this project follows (GlobalHotkey, WindowsAutostartConfigurator,
/// WindowsThemeProvider are all `public` for exactly this reason). Callers use
/// DwmTitleBar.*, never NativeMethods.* -- do not "simplify" this away by re-exposing
/// NativeMethods or adding an InternalsVisibleTo grant to RigToggle.App; the public
/// wrapper is the deliberate encapsulation boundary. Every call here is silently
/// best-effort: DwmSetWindowAttribute returns an HRESULT (int) rather than throwing, so
/// an unsupported OS/attribute (e.g. Windows 10, or a pre-Mica Windows 11 build) simply
/// leaves the window's chrome unchanged instead of crashing the caller (D-07).
/// </summary>
public static class DwmTitleBar
{
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_MAINWINDOW = 2; // D-02: standard Mica, NOT Mica Alt/Acrylic

    public static void ApplyRoundedCornersAndMica(IntPtr handle)
    {
        int corner = DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int backdrop = DWMSBT_MAINWINDOW;
        NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }
}
