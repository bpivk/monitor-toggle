using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves UpdateVersionComparer's numeric (not lexical) Major.Minor-only ordering,
/// seeded with this project's real tag history (v1.0 -> v2.1) plus the synthetic
/// v2.9/v2.10 double-digit-minor case PITFALLS.md Pitfall 3 explicitly demands --
/// no test against today's real data would ever catch a lexical-comparison bug,
/// since no double-digit minor has been cut yet. Also proves the component-count
/// false-negative guard ARCHITECTURE.md Anti-Pattern 4 flags (a stamped
/// four-component running version against a two-component tag) and TryParseTag's
/// v-prefix-strip / extra-segments-ignored / fewer-than-two-segments-fails
/// contract. [Theory]/[InlineData] shape follows HotkeyFormatterTests.cs.
/// </summary>
public class UpdateVersionComparerTests
{
    [Theory]
    // This project's real tag history: each version reads as newer than its
    // immediate predecessor, and not newer than itself.
    [InlineData(1, 0, "v1.1", true)]
    [InlineData(1, 1, "v1.2", true)]
    [InlineData(1, 2, "v2.0", true)]
    [InlineData(2, 0, "v2.1", true)]
    [InlineData(1, 0, "v1.0", false)]
    [InlineData(2, 1, "v2.1", false)]
    // The synthetic double-digit case (PITFALLS.md Pitfall 3): numeric, not
    // lexical, ordering -- "v2.10" must NOT be misread as older than "v2.9".
    [InlineData(2, 9, "v2.10", true)]
    [InlineData(2, 10, "v2.9", false)]
    public void IsNewer_RunningVersusTag_UsesNumericMajorMinorOrdering(int runningMajor, int runningMinor, string tag, bool expected)
    {
        var running = new Version(runningMajor, runningMinor, 0, 0);

        Assert.Equal(expected, UpdateVersionComparer.IsNewer(running, tag));
    }

    /// <summary>
    /// ARCHITECTURE.md Anti-Pattern 4: a stamped four-component running version
    /// (2.2.0.0, what &lt;Version&gt;2.2&lt;/Version&gt; normalizes to) against the
    /// two-component tag "v2.2" must read as NOT newer -- raw System.Version.CompareTo
    /// would incorrectly read this as "the tag is older" purely from the unset
    /// Build/Revision components on a `new Version("2.2")` parse, which is exactly
    /// the false-negative this comparer exists to avoid by comparing only Major/Minor.
    /// </summary>
    [Fact]
    public void IsNewer_SameVersionMismatchedComponentCount_IsNotNewer()
    {
        var running = new Version(2, 2, 0, 0);

        Assert.False(UpdateVersionComparer.IsNewer(running, "v2.2"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("vX.Y")]
    [InlineData("2")]
    [InlineData("garbage")]
    public void IsNewer_DegenerateTag_ReturnsFalseAndDoesNotThrow(string? tag)
    {
        var running = new Version(1, 0, 0, 0);

        Assert.False(UpdateVersionComparer.IsNewer(running, tag));
    }

    [Fact]
    public void IsNewer_NullRunningVersion_ReturnsFalseAndDoesNotThrow()
    {
        Assert.False(UpdateVersionComparer.IsNewer(null, "v99.0"));
    }

    [Theory]
    [InlineData("v1.2", true, 1, 2)]
    [InlineData("V1.2", true, 1, 2)] // v-prefix strip is case-insensitive
    [InlineData("1.2", true, 1, 2)] // no leading v at all
    [InlineData("v2.10.5", true, 2, 10)] // extra segments beyond the first two are ignored
    public void TryParseTag_ValidTag_ParsesMajorMinorAndIgnoresExtraSegments(string tag, bool expectedResult, int expectedMajor, int expectedMinor)
    {
        bool result = UpdateVersionComparer.TryParseTag(tag, out int major, out int minor);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedMajor, major);
        Assert.Equal(expectedMinor, minor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("2")] // fewer than two segments
    [InlineData("vX.Y")] // non-numeric segments
    public void TryParseTag_InvalidTag_ReturnsFalse(string? tag)
    {
        Assert.False(UpdateVersionComparer.TryParseTag(tag, out _, out _));
    }
}
