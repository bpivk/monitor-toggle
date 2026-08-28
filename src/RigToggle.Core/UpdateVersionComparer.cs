using System.Globalization;

namespace RigToggle.Core;

/// <summary>
/// Pure, unit-testable three-component (Major.Minor.Patch) version comparison for
/// this project's own tag scheme: vX.Y.Z going forward, with every two-segment
/// historical tag (v1.0 ... v2.2) parsed as patch 0 for backward compatibility. No
/// I/O, never throws -- the same defensive-utility discipline as
/// ToggleResultFormatter.TruncateForBalloon.
///
/// Deliberately never uses System.Version.CompareTo on the parsed tag/running values
/// (ARCHITECTURE.md Anti-Pattern 4): `new Version("2.2")` yields Major=2, Minor=2,
/// Build=-1, Revision=-1, while a `&lt;Version&gt;2.2&lt;/Version&gt;`-stamped
/// assembly's AssemblyVersion normalizes to 2.2.0.0 (Build=0, Revision=0).
/// Version.CompareTo treats an unset (-1) component as less than an explicit 0, so
/// comparing the SAME logical version across mismatched component counts can
/// incorrectly read as "the tag is older" purely from that mismatch -- and the same
/// trap applies at the patch level: a raw two-component running <see cref="Version"/>
/// reports Build == -1, which would make a tag's explicit patch 0 look greater than
/// it purely from the unset component, reporting a phantom update. Comparing ONLY
/// the parsed Major/Minor/Patch components from both sides -- normalizing the
/// running side's Build via <c>Math.Max(runningVersion.Build, 0)</c> -- sidesteps
/// both traps entirely.
///
/// Numeric (not lexical/string) ordering specifically because this project's tags
/// will eventually reach a double-digit minor or patch (PITFALLS.md Pitfall 3):
/// "v2.10".CompareTo("v2.9") as raw strings would incorrectly conclude v2.10 is
/// older than v2.9 -- not yet reachable from today's real tag history at the time
/// this guard was written, which is exactly why the unit tests must include
/// synthetic double-digit minor AND patch cases rather than relying on today's data
/// to ever exercise it. The same numeric-not-lexical requirement applies at the
/// patch level: "v2.2.10" must not be misread as older than "v2.2.9".
/// </summary>
public static class UpdateVersionComparer
{
    /// <summary>
    /// Strips an optional leading v/V, splits on '.', and parses the first three
    /// segments as non-negative ints via int.TryParse with
    /// CultureInfo.InvariantCulture. A missing third segment (a two-segment
    /// historical tag) reads as patch 0. A present-but-unparseable third segment is
    /// a parse failure, not a silent 0. Segments beyond the third are ignored;
    /// fewer than two segments is a parse failure. Never throws for any input,
    /// including null/empty/garbage.
    /// </summary>
    public static bool TryParseTag(string? tag, out int major, out int minor, out int patch)
    {
        major = 0;
        minor = 0;
        patch = 0;

        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string trimmed = tag[0] is 'v' or 'V' ? tag[1..] : tag;
        string[] segments = trimmed.Split('.');

        if (segments.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedMajor)
            || !int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int parsedMinor))
        {
            return false;
        }

        int parsedPatch = 0;
        if (segments.Length >= 3
            && !int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out parsedPatch))
        {
            return false;
        }

        major = parsedMajor;
        minor = parsedMinor;
        patch = parsedPatch;
        return true;
    }

    /// <summary>
    /// True iff <paramref name="tagName"/>'s parsed Major.Minor.Patch is strictly
    /// greater than <paramref name="runningVersion"/>'s, comparing raw integer
    /// components at the FIRST differing level (major, then minor, then patch) from
    /// both sides -- never System.Version.CompareTo (see class doc comment for why).
    /// <paramref name="runningVersion"/>'s Build component is normalized via
    /// <c>Math.Max(runningVersion.Build, 0)</c> before comparison, since a
    /// two-component running <see cref="Version"/> reports Build == -1 (see class
    /// doc comment); Revision is never consulted. False (never throws) for a null
    /// runningVersion or an unparseable tagName -- a garbage/unreachable tag is
    /// never treated as "newer."
    /// </summary>
    public static bool IsNewer(Version? runningVersion, string? tagName)
    {
        if (runningVersion is null)
        {
            return false;
        }

        if (!TryParseTag(tagName, out int tagMajor, out int tagMinor, out int tagPatch))
        {
            return false;
        }

        if (tagMajor != runningVersion.Major)
        {
            return tagMajor > runningVersion.Major;
        }

        if (tagMinor != runningVersion.Minor)
        {
            return tagMinor > runningVersion.Minor;
        }

        int runningPatch = Math.Max(runningVersion.Build, 0);
        return tagPatch > runningPatch;
    }
}
