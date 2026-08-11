using System.Drawing;
using RigToggle.Windows;
using Xunit;

namespace RigToggle.Windows.Tests;

// WR-01 (21-REVIEW.md): covers the pure, side-effect-free byte-order extraction helpers
// pulled out of WindowsThemeProvider's registry/DWM accent readers. This was the single
// highest-risk piece of logic phase 21 introduced -- byte-order-critical bit-math that
// was previously verified only via a one-time manual hardware pass (21-03-SUMMARY.md)
// because it could not be unit-tested while entangled with Registry.CurrentUser and
// NativeMethods.DwmGetColorizationColor. These tests pin the exact worked example
// already cited in WindowsThemeProvider.cs's own comments (0xffc77e35 -> #357EC7 on the
// registry/ABGR path, #C77E35 on the DWM/ARGB path) so a future edit that flips a mask
// fails the build instead of waiting for the next manual rig pass. Comparisons use
// ToArgb() rather than Color equality (see WR-02 in the same review) to avoid the exact
// KnownColor/state-flag pitfall this review separately flagged.
public class WindowsThemeProviderTests
{
    [Theory]
    [InlineData(0xffc77e35u, 0x35, 0x7e, 0xc7)] // 21-RESEARCH.md worked example: ABGR dword -> #357EC7 (blue)
    [InlineData(0xff000000u, 0x00, 0x00, 0x00)]
    [InlineData(0xffffffffu, 0xff, 0xff, 0xff)]
    [InlineData(0x00010203u, 0x03, 0x02, 0x01)]
    public void FromAbgrRegistryDword_ExtractsRgbInAbgrOrder(uint dword, byte expectedR, byte expectedG, byte expectedB)
    {
        Color result = WindowsThemeProvider.FromAbgrRegistryDword(dword);

        Assert.Equal(Color.FromArgb(expectedR, expectedG, expectedB).ToArgb(), result.ToArgb());
    }

    [Theory]
    [InlineData(0xffc77e35u, 0xc7, 0x7e, 0x35)] // same dword, ARGB order -> #C77E35 (orange) -- opposite of the registry path
    [InlineData(0xff000000u, 0x00, 0x00, 0x00)]
    [InlineData(0xffffffffu, 0xff, 0xff, 0xff)]
    [InlineData(0x00010203u, 0x01, 0x02, 0x03)]
    public void FromArgbDwmDword_ExtractsRgbInArgbOrder(uint dword, byte expectedR, byte expectedG, byte expectedB)
    {
        Color result = WindowsThemeProvider.FromArgbDwmDword(dword);

        Assert.Equal(Color.FromArgb(expectedR, expectedG, expectedB).ToArgb(), result.ToArgb());
    }
}
