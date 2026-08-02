using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

/// <summary>
/// Hand-written recording fake for IThemeProvider (no mocking framework -- matches the
/// project's no-unnecessary-dependency posture, same convention as FakeControllers.cs).
/// CurrentTheme is settable directly for setup; RaiseThemeChanged mirrors
/// WindowsThemeProvider's real behavior of assigning CurrentTheme before invoking
/// ThemeChanged, so subscribers observe the new value at invocation time.
/// </summary>
public sealed class FakeThemeProvider : IThemeProvider
{
    public AppTheme CurrentTheme { get; set; }

    public event EventHandler? ThemeChanged;

    public void RaiseThemeChanged(AppTheme newTheme)
    {
        CurrentTheme = newTheme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }
}
