using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves HotkeyFormatter.ToDisplayString renders the fixed
/// Ctrl+Alt+Shift+Win modifier order followed by the key's display name
/// (09-UI-SPEC.md "Friendly combo string format"), and that HotkeyCombo's
/// modifier-key detection matches the documented virtual-key ranges.
/// RED-first (TDD): written before HotkeyFormatter.cs exists.
/// </summary>
public class HotkeyFormatterTests
{
    [Theory]
    [InlineData(HotkeyCombo.ModControl | HotkeyCombo.ModAlt, 0x52, "Ctrl+Alt+R")]
    [InlineData(HotkeyCombo.ModControl | HotkeyCombo.ModShift, 0x74, "Ctrl+Shift+F5")]
    [InlineData(
        HotkeyCombo.ModWin | HotkeyCombo.ModControl | HotkeyCombo.ModAlt | HotkeyCombo.ModShift,
        0x41,
        "Ctrl+Alt+Shift+Win+A")]
    [InlineData(HotkeyCombo.ModAlt, 0x31, "Alt+1")]
    public void ToDisplayString_RendersExpectedFriendlyCombo(int modifiers, int virtualKey, string expected)
    {
        Assert.Equal(expected, HotkeyFormatter.ToDisplayString(modifiers, virtualKey));
    }

    [Fact]
    public void ToDisplayString_NoModifiers_OmitsModifierTokens()
    {
        Assert.Equal("R", HotkeyFormatter.ToDisplayString(0, 0x52));
    }

    [Fact]
    public void ToDisplayString_UnmappedKey_FallsBackToStableNonEmptyToken()
    {
        string result = HotkeyFormatter.ToDisplayString(HotkeyCombo.ModControl, unchecked((int)0xFFFF0000));

        Assert.False(string.IsNullOrEmpty(result));
        Assert.StartsWith("Ctrl+", result);
    }

    [Theory]
    [InlineData(0x11, true)]  // Control (generic)
    [InlineData(0x12, true)]  // Alt/Menu (generic)
    [InlineData(0x10, true)]  // Shift (generic)
    [InlineData(0x5B, true)]  // LWin
    [InlineData(0xA2, true)]  // LControl
    [InlineData(0x52, false)] // 'R'
    [InlineData(0x74, false)] // F5
    public void IsModifierVirtualKey_MatchesDocumentedRanges(int virtualKey, bool expected)
    {
        Assert.Equal(expected, HotkeyCombo.IsModifierVirtualKey(virtualKey));
    }
}
