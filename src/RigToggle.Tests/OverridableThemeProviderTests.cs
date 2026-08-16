using System.Drawing;
using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Tests.Doubles;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Unit coverage for OverridableThemeProvider, the single shared effective-theme
/// resolver (THEME-09, D-04, Pitfall 6). Hand-written doubles only
/// (FakeThemeProvider, InMemorySettingsStore, ThrowingSettingsStore) -- no mocking
/// framework, matching ThemeProviderContractTests.cs's convention. Covers override
/// precedence, preview precedence, live-flip suppression while overridden,
/// fail-silent degradation to System on a throwing store or an out-of-range
/// persisted value, and ThemeChanged raise counts for SetPreviewOverride /
/// RefreshOverride.
/// </summary>
public class OverridableThemeProviderTests
{
    [Fact]
    public void CurrentTheme_PersistedDarkOverride_WinsOverLiveLightSignal()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Dark });
        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_PersistedLightOverride_WinsOverLiveDarkSignal()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Dark };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Light });
        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Light, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_NullOverride_FallsThroughToLiveLightSignal()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = null });
        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Light, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_NullOverride_FallsThroughToLiveDarkSignal()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Dark };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = null });
        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_LiveFlip_DoesNotChangeWhileOverrideIsSet()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Dark });
        var provider = new OverridableThemeProvider(inner, store);

        inner.RaiseThemeChanged(AppTheme.Dark);
        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);

        inner.RaiseThemeChanged(AppTheme.Light);
        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_LiveFlip_ChangesWhenOverrideIsNull()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = null });
        var provider = new OverridableThemeProvider(inner, store);

        inner.RaiseThemeChanged(AppTheme.Dark);

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void SetPreviewOverride_TakesPrecedenceOverPersistedValue()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Dark });
        var provider = new OverridableThemeProvider(inner, store);

        provider.SetPreviewOverride(AppTheme.Light);

        Assert.Equal(AppTheme.Light, provider.CurrentTheme);
    }

    [Fact]
    public void SetPreviewOverride_Null_PreviewsSystemWhileDarkIsPersisted()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Dark });
        var provider = new OverridableThemeProvider(inner, store);

        provider.SetPreviewOverride(null);

        // Previewing null (System) falls through to the live signal, not the
        // still-persisted Dark override.
        Assert.Equal(AppTheme.Light, provider.CurrentTheme);
    }

    [Fact]
    public void RefreshOverride_DropsActivePreview_ReturnsToPersistedValue()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = AppTheme.Dark });
        var provider = new OverridableThemeProvider(inner, store);

        provider.SetPreviewOverride(AppTheme.Light);
        Assert.Equal(AppTheme.Light, provider.CurrentTheme);

        provider.RefreshOverride();

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void SetPreviewOverride_RaisesThemeChangedExactlyOnce()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings());
        var provider = new OverridableThemeProvider(inner, store);
        int count = 0;
        provider.ThemeChanged += (_, _) => count++;

        provider.SetPreviewOverride(AppTheme.Dark);

        Assert.Equal(1, count);
    }

    [Fact]
    public void RefreshOverride_RaisesThemeChangedExactlyOnce()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Light };
        var store = new InMemorySettingsStore(new AppSettings());
        var provider = new OverridableThemeProvider(inner, store);
        int count = 0;
        provider.ThemeChanged += (_, _) => count++;

        provider.RefreshOverride();

        Assert.Equal(1, count);
    }

    [Fact]
    public void CurrentTheme_ThrowingStore_ResolvesToLiveSignalInsteadOfThrowing()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Dark };
        var store = new ThrowingSettingsStore();

        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void CurrentTheme_OutOfRangeOverride_ResolvesToLiveSignal()
    {
        var inner = new FakeThemeProvider { CurrentTheme = AppTheme.Dark };
        var store = new InMemorySettingsStore(new AppSettings { ThemeOverride = (AppTheme)99 });

        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(AppTheme.Dark, provider.CurrentTheme);
    }

    [Fact]
    public void AccentColor_PassesThroughInnerProviderUnchanged()
    {
        var expected = Color.FromArgb(255, 0, 128);
        var inner = new FakeThemeProvider { AccentColor = expected };
        var store = new InMemorySettingsStore(new AppSettings());
        var provider = new OverridableThemeProvider(inner, store);

        Assert.Equal(expected, provider.AccentColor);
    }

    [Fact]
    public void AccentColorChanged_PassesThroughInnerProviderUnchanged()
    {
        var inner = new FakeThemeProvider { AccentColor = Color.FromArgb(1, 2, 3) };
        var store = new InMemorySettingsStore(new AppSettings());
        var provider = new OverridableThemeProvider(inner, store);
        int count = 0;
        Color observed = default;
        provider.AccentColorChanged += (_, _) =>
        {
            count++;
            observed = provider.AccentColor;
        };

        inner.RaiseAccentColorChanged(Color.FromArgb(10, 20, 30));

        Assert.Equal(1, count);
        Assert.Equal(Color.FromArgb(10, 20, 30), observed);
    }
}
