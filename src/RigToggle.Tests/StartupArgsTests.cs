using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves StartupArgs.ShouldStartHidden is a pure, exact-token `--tray` predicate that
/// never throws on empty/garbage args (Security Domain V5), and that
/// StartupArgs.TryGetApplyUpdateArgs is the equivalent exact-token, never-throwing
/// parser for the `--apply-update` startup-gate bypass flag (D-03/D-04), including the
/// opaque trailing payload it extracts.
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

    // --- TryGetApplyUpdateArgs (D-03/D-04, UPDATE-07 bypass flag) ---

    [Theory]
    [InlineData(new[] { "--apply-update" }, true)]
    [InlineData(new[] { "--APPLY-UPDATE" }, true)]
    [InlineData(new[] { "--tray", "--apply-update" }, true)]
    [InlineData(new string[] { }, false)]
    [InlineData(new[] { "foo", "--bar" }, false)]
    [InlineData(new[] { "apply-update", "-apply-update", "--apply-update-x" }, false)]
    public void TryGetApplyUpdateArgs_DetectsExactToken_D04(string[] args, bool expected)
    {
        // D-04: TryGetApplyUpdateArgs matches the exact --apply-update token,
        // case-insensitively, exactly like ShouldStartHidden matches --tray — not a
        // substring, not a near-miss variant.
        bool detected = StartupArgs.TryGetApplyUpdateArgs(args, out _);
        Assert.Equal(expected, detected);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_FlagAlone_PayloadIsEmptyNotNull()
    {
        // D-04/option-a (opaque trailing payload): a bare flag with no trailing
        // tokens is a legitimate call shape — Phase 26's relaunch may forward an
        // empty payload — and must yield an empty, non-null payload rather than
        // throwing or returning null.
        bool detected = StartupArgs.TryGetApplyUpdateArgs(new[] { StartupArgs.ApplyUpdateFlag }, out var payload);

        Assert.True(detected);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_FlagFollowedByTokens_PayloadIsExactTokensInOrder()
    {
        // D-04/option-a: every token after the flag is returned unmodified, in
        // original order, uninterpreted — Phase 26 owns the positional meaning, not
        // this parser.
        var args = new[] { StartupArgs.ApplyUpdateFlag, "staged.exe", "1234", "--tray" };

        bool detected = StartupArgs.TryGetApplyUpdateArgs(args, out var payload);

        Assert.True(detected);
        Assert.Equal(new[] { "staged.exe", "1234", "--tray" }, payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_FlagPrecededAndFollowedByTokens_PayloadExcludesPrecedingTokens()
    {
        // D-04: only tokens after the flag belong to the payload — tokens that
        // preceded it on the command line are not part of what Phase 26 consumes.
        var args = new[] { "--tray", StartupArgs.ApplyUpdateFlag, "staged.exe", "1234" };

        bool detected = StartupArgs.TryGetApplyUpdateArgs(args, out var payload);

        Assert.True(detected);
        Assert.Equal(new[] { "staged.exe", "1234" }, payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_FlagAbsent_PayloadIsEmptyNotNull()
    {
        // D-03: this is the ordinary (non-bypass) startup path — unrelated args must
        // never falsely detect the flag, and the out payload must still be a safe,
        // non-null empty array for callers that inspect it without checking the bool
        // first.
        bool detected = StartupArgs.TryGetApplyUpdateArgs(new[] { "--tray", "foo" }, out var payload);

        Assert.False(detected);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_EmptyArgs_ReturnsFalseWithoutThrowing()
    {
        // D-04's never-crash-on-startup contract extends to the empty-array input.
        bool detected = StartupArgs.TryGetApplyUpdateArgs(Array.Empty<string>(), out var payload);

        Assert.False(detected);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_NullArgs_ReturnsFalseWithoutThrowing()
    {
        // D-04: mirrors ShouldStartHidden_NullArgs_ReturnsFalseWithoutThrowing — this
        // parser runs before any UI exists, so it must never throw on a null array.
        bool detected = StartupArgs.TryGetApplyUpdateArgs(null, out var payload);

        Assert.False(detected);
        Assert.NotNull(payload);
        Assert.Empty(payload);
    }

    [Fact]
    public void TryGetApplyUpdateArgs_CoexistsWithTrayFlag_NeitherHelperStealsTheOthersToken()
    {
        // D-03: the two flags are parsed independently by two dedicated helpers —
        // neither generalises into a shared "any flag" mechanism, and combining them
        // on one command line does not confuse either parser.
        var args = new[] { StartupArgs.ApplyUpdateFlag, "staged.exe", "--tray" };

        Assert.True(StartupArgs.ShouldStartHidden(args));

        bool detected = StartupArgs.TryGetApplyUpdateArgs(args, out var payload);
        Assert.True(detected);
        Assert.Equal(new[] { "staged.exe", "--tray" }, payload);
    }
}
