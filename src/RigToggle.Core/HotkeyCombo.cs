namespace RigToggle.Core;

/// <summary>
/// Pure, Windows-free representation of a global hotkey combination (TRIG-01):
/// a Win32-style modifier bit-mask plus a virtual-key code. Lives in RigToggle.Core
/// (net10.0, zero Windows API references — see RigToggle.Core.csproj) so both the
/// Settings capture textbox and the eventual RegisterHotKey P/Invoke (RigToggle.Windows,
/// plan 09-02) consume one tested contract instead of re-deriving combo semantics.
/// </summary>
public readonly record struct HotkeyCombo(int Modifiers, int VirtualKey)
{
    /// <summary>
    /// Mirrors the Win32 MOD_ALT value that RegisterHotKey's fsModifiers parameter
    /// expects (canonical wire format; the actual DllImport lives in RigToggle.Windows,
    /// not here).
    /// </summary>
    public const int ModAlt = 0x0001;

    /// <summary>
    /// Mirrors the Win32 MOD_CONTROL value that RegisterHotKey's fsModifiers parameter
    /// expects (canonical wire format; the actual DllImport lives in RigToggle.Windows,
    /// not here).
    /// </summary>
    public const int ModControl = 0x0002;

    /// <summary>
    /// Mirrors the Win32 MOD_SHIFT value that RegisterHotKey's fsModifiers parameter
    /// expects (canonical wire format; the actual DllImport lives in RigToggle.Windows,
    /// not here).
    /// </summary>
    public const int ModShift = 0x0004;

    /// <summary>
    /// Mirrors the Win32 MOD_WIN value that RegisterHotKey's fsModifiers parameter
    /// expects (canonical wire format; the actual DllImport lives in RigToggle.Windows,
    /// not here).
    /// </summary>
    public const int ModWin = 0x0008;

    /// <summary>
    /// True iff virtualKey is itself a modifier key (Shift/Control/Alt/Win, generic or
    /// left/right-specific variant), so the App capture layer can reject a bare-modifier
    /// press per D-01 against Core-tested truth rather than an ad-hoc App check. Never
    /// throws on any int input, including out-of-range/garbage values.
    /// </summary>
    public static bool IsModifierVirtualKey(int virtualKey) => virtualKey switch
    {
        0x10 => true, // Shift (generic)
        0x11 => true, // Control (generic)
        0x12 => true, // Alt/Menu (generic)
        0x5B => true, // LWin
        0x5C => true, // RWin
        0xA0 => true, // LShift
        0xA1 => true, // RShift
        0xA2 => true, // LControl
        0xA3 => true, // RControl
        0xA4 => true, // LMenu
        0xA5 => true, // RMenu
        _ => false,
    };
}
