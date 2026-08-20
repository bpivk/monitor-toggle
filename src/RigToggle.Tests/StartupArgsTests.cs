using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves StartupArgs.ShouldStartHidden is a pure, exact-token `--tray` predicate that
/// never throws on empty/garbage args (Security Domain V5).
/// </summary>
public class StartupArgsTests
{
    [Theory]
    [InlineData(new[] { "--tray" }, true)]
    [InlineData(new[] { "--TRAY" }, true)]
    [InlineData(new[] { "--rig", "--tray" }, true)]
    [InlineData(new string[] { }, false)]
    [InlineData(new[] { "foo", "--bar" }, false)]
    [InlineData(new[] { "tray", "-tray", "--tray-x" }, false)]
    public void ShouldStartHidden_MatchesBehaviorContract(string[] args, bool expected)
    {
        Assert.Equal(expected, StartupArgs.ShouldStartHidden(args));
    }

    [Fact]
    public void ShouldStartHidden_NullArgs_ReturnsFalseWithoutThrowing()
    {
        // WR-04 (code review): the documented "never throws on null" contract was
        // previously unfulfilled and untested — Enumerable.Contains throws on a null
        // source. Guard added in StartupArgs.cs; this proves it holds.
        Assert.False(StartupArgs.ShouldStartHidden(null));
    }
}
