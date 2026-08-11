using System.Drawing;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves the IThemeProvider contract's three documented behaviors via FakeThemeProvider
/// (THEME-01/THEME-02/THEME-07): ThemeChanged fires exactly once per RaiseThemeChanged
/// call with CurrentTheme reflecting the new value at invocation time; AccentColorChanged
/// fires exactly once per RaiseAccentColorChanged call with AccentColor reflecting the new
/// value at invocation time. RED-first (TDD): written before FakeThemeProvider.cs's
/// behavior is exercised anywhere else.
/// </summary>
public class ThemeProviderContractTests
{
    [Fact]
    public void RaiseThemeChanged_InvokesSubscriberExactlyOnce_WithUpdatedCurrentTheme()
    {
        var provider = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        int invocationCount = 0;
        AppTheme? observedTheme = null;

        provider.ThemeChanged += (_, _) =>
        {
            invocationCount++;
            observedTheme = provider.CurrentTheme;
        };

        provider.RaiseThemeChanged(AppTheme.Dark);

        Assert.Equal(1, invocationCount);
        Assert.Equal(AppTheme.Dark, observedTheme);
        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void AppTheme_HasExactlyLightAndDarkMembers()
    {
        var members = Enum.GetValues<AppTheme>();

        Assert.Equal(2, members.Length);
        Assert.Contains(AppTheme.Light, members);
        Assert.Contains(AppTheme.Dark, members);
    }

    [Fact]
    public void RaiseAccentColorChanged_InvokesSubscriberExactlyOnce_WithUpdatedAccentColor()
    {
        var provider = new FakeThemeProvider { AccentColor = Color.FromArgb(0, 90, 158) };
        int invocationCount = 0;
        Color observedAccent = default;

        provider.AccentColorChanged += (_, _) =>
        {
            invocationCount++;
            observedAccent = provider.AccentColor;
        };

        provider.RaiseAccentColorChanged(Color.FromArgb(255, 0, 0));

        Assert.Equal(1, invocationCount);
        Assert.Equal(Color.FromArgb(255, 0, 0), observedAccent);
        Assert.Equal(Color.FromArgb(255, 0, 0), provider.AccentColor);
    }
}
