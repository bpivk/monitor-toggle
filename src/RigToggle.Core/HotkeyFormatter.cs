using System.Text;

namespace RigToggle.Core;

/// <summary>
/// Renders a HotkeyCombo (modifier bit-mask + virtual-key code) as the friendly combo
/// string shown in the Settings capture textbox (09-UI-SPEC.md "Friendly combo string
/// format", D-01). The fixed modifier order (Ctrl, Alt, Shift, Win) matches Windows' own
/// "Customize Shortcut Key" UI so it reads as familiar rather than app-invented. The
/// exact virtual-key-to-name mapping for uncommon keys is intentionally best-effort per
/// UI-SPEC — anything not explicitly mapped falls back to a stable, non-empty
/// "Key"+value token rather than throwing or returning an empty string (T-09-01: must
/// never throw on any int input, including out-of-range/garbage values from a
/// hand-edited settings.json).
/// </summary>
public static class HotkeyFormatter
{
    /// <summary>
    /// Renders modifiers+virtualKey as e.g. "Ctrl+Alt+R" — modifier tokens in fixed
    /// Ctrl/Alt/Shift/Win order (only the ones actually set), then the key's display
    /// name, joined with '+' and no spaces. Never throws.
    /// </summary>
    public static string ToDisplayString(int modifiers, int virtualKey)
    {
        var builder = new StringBuilder();

        AppendToken(builder, (modifiers & HotkeyCombo.ModControl) != 0, "Ctrl");
        AppendToken(builder, (modifiers & HotkeyCombo.ModAlt) != 0, "Alt");
        AppendToken(builder, (modifiers & HotkeyCombo.ModShift) != 0, "Shift");
        AppendToken(builder, (modifiers & HotkeyCombo.ModWin) != 0, "Win");
        AppendToken(builder, condition: true, KeyDisplayName(virtualKey));

        return builder.ToString();
    }

    private static void AppendToken(StringBuilder builder, bool condition, string token)
    {
        if (!condition)
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('+');
        }

        builder.Append(token);
    }

    private static string KeyDisplayName(int virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A) // A-Z
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39) // 0-9
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87) // F1-F24
        {
            return "F" + (virtualKey - 0x70 + 1);
        }

        return virtualKey switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x09 => "Tab",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x24 => "Home",
            0x23 => "End",
            0x2D => "Insert",
            0x2E => "Delete",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            _ => "Key" + virtualKey,
        };
    }
}
