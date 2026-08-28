using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves UpdateVersionComparer's numeric (not lexical) three-component
/// Major.Minor.Patch ordering, seeded with this project's real tag history
/// (v1.0 -> v2.2) plus the synthetic v2.9/v2.10 double-digit-minor and
/// v2.2.9/v2.2.10 double-digit-patch cases PITFALLS.md Pitfall 3 explicitly
/// demands -- no test against today's real data would ever catch a
/// lexical-comparison bug, since no double-digit minor/patch has been cut yet.
/// Also proves the component-count false-negative guard ARCHITECTURE.md
/// Anti-Pattern 4 flags: both a stamped four-component running version against
/// a two-component tag, AND a raw two-component running version (Build == -1)
/// against a two-component tag, which must not misread the unset Build as less
/// than the tag's implied patch 0. Also proves TryParseTag's v-prefix-strip /
/// two-segments-means-patch-zero / segments-beyond-third-ignored /
/// fewer-than-two-segments-fails / unparseable-third-segment-fails contract.
/// [Theory]/[InlineData] shape follows HotkeyFormatterTests.cs.
/// </summary>
public class UpdateVersionComparerTests
{
    [Theory]
    // This project's real tag history: each version reads as newer than its
    // immediate predecessor, and not newer than itself.
    [InlineData(1, 0, 0, "v1.1", true)]
    [InlineData(1, 1, 0, "v1.2", true)]
    [InlineData(1, 2, 0, "v2.0", true)]
    [InlineData(2, 0, 0, "v2.1", true)]
    [InlineData(1, 0, 0, "v1.0", false)]
    [InlineData(2, 1, 0, "v2.1", false)]
    // The synthetic double-digit case (PITFALLS.md Pitfall 3): numeric, not
    // lexical, ordering -- "v2.10" must NOT be misread as older than "v2.9".
    [InlineData(2, 9, 0, "v2.10", true)]
    [InlineData(2, 10, 0, "v2.9", false)]
    // Patch-level ordering (three-component semver): compared at the FIRST
    // differing level, after major and minor both match.
    [InlineData(2, 2, 0, "v2.2.1", true)]
    [InlineData(2, 2, 1, "v2.2.1", false)]
    [InlineData(2, 2, 1, "v2.2", false)] // tag patch 0 is not greater than running patch 1
    [InlineData(2, 2, 9, "v2.2.10", true)] // numeric, not lexical, at the patch level too
    [InlineData(2, 2, 10, "v2.2.9", false)]
    [InlineData(2, 2, 5, "v2.3", true)] // a higher minor wins over a higher patch
    [InlineData(2, 3, 0, "v2.2.9", false)]
    public void IsNewer_RunningVersusTag_UsesNumericMajorMinorPatchOrdering(int runningMajor, int runningMinor, int runningPatch, string tag, bool expected)
    {
        var running = new Version(runningMajor, runningMinor, runningPatch, 0);

        Assert.Equal(expected, UpdateVersionComparer.IsNewer(running, tag));
    }

    /// <summary>
    /// ARCHITECTURE.md Anti-Pattern 4: a stamped four-component running version
    /// (2.2.0.0, what &lt;Version&gt;2.2&lt;/Version&gt; normalizes to) against the
    /// two-component tag "v2.2" must read as NOT newer -- raw System.Version.CompareTo
    /// would incorrectly read this as "the tag is older" purely from the unset
    /// Build/Revision components on a `new Version("2.2")` parse, which is exactly
    /// the false-negative this comparer exists to avoid by comparing only raw
    /// parsed integer components.
    /// </summary>
    [Fact]
    public void IsNewer_SameVersionMismatchedComponentCount_IsNotNewer()
    {
        var running = new Version(2, 2, 0, 0);

        Assert.False(UpdateVersionComparer.IsNewer(running, "v2.2"));
    }

    /// <summary>
    /// CRITICAL guard: a two-component <see cref="Version"/> (what
    /// <c>Program.cs:299</c> can really produce, e.g. via
    /// <c>?? new Version(0, 0)</c>) reports <c>Build == -1</c>. Reading that raw
    /// would make a tag's patch 0 look greater than -1 and report a phantom
    /// update. <see cref="UpdateVersionComparer.IsNewer"/> must normalize with
    /// <c>Math.Max(runningVersion.Build, 0)</c> before comparing.
    /// </summary>
    [Theory]
    [InlineData(2, 2, "v2.2")]
    [InlineData(0, 0, "v0.0.0")]
    public void IsNewer_TwoComponentRunningVersion_BuildMinusOneNormalizedToZero_IsNotNewer(int runningMajor, int runningMinor, string tag)
    {
        var running = new Version(runningMajor, runningMinor); // Build == -1, Revision == -1

        Assert.False(UpdateVersionComparer.IsNewer(running, tag));
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
    [InlineData("v1.2", true, 1, 2, 0)] // two segments means patch 0
    [InlineData("V1.2", true, 1, 2, 0)] // v-prefix strip is case-insensitive
    [InlineData("1.2", true, 1, 2, 0)] // no leading v at all
    [InlineData("v2.2.1", true, 2, 2, 1)]
    [InlineData("v2.10.5", true, 2, 10, 5)] // third segment is now meaningful, not discarded
    [InlineData("1.2.3.4", true, 1, 2, 3)] // segments beyond the third are still ignored
    public void TryParseTag_ValidTag_ParsesMajorMinorPatch(string tag, bool expectedResult, int expectedMajor, int expectedMinor, int expectedPatch)
    {
        bool result = UpdateVersionComparer.TryParseTag(tag, out int major, out int minor, out int patch);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedMajor, major);
        Assert.Equal(expectedMinor, minor);
        Assert.Equal(expectedPatch, patch);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("2")] // fewer than two segments
    [InlineData("vX.Y")] // non-numeric segments
    [InlineData("v2.2.x")] // present-but-unparseable patch segment is a parse failure, not a silent 0
    public void TryParseTag_InvalidTag_ReturnsFalse(string? tag)
    {
        Assert.False(UpdateVersionComparer.TryParseTag(tag, out _, out _, out _));
    }
}
