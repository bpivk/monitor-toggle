using System.Globalization;

namespace RigToggle.Core;

/// <summary>
/// Pure, unit-testable Major.Minor version comparison for this project's own tag
/// scheme (v1.0 ... v2.1, never three-component semver). No I/O, never throws --
/// the same defensive-utility discipline as ToggleResultFormatter.TruncateForBalloon.
///
/// Deliberately never uses System.Version.CompareTo on the parsed tag/running values
/// (ARCHITECTURE.md Anti-Pattern 4): `new Version("2.2")` yields Major=2, Minor=2,
/// Build=-1, Revision=-1, while a `&lt;Version&gt;2.2&lt;/Version&gt;`-stamped
/// assembly's AssemblyVersion normalizes to 2.2.0.0 (Build=0, Revision=0).
/// Version.CompareTo treats an unset (-1) component as less than an explicit 0, so
/// comparing the SAME logical version across mismatched component counts can
/// incorrectly read as "the tag is older" purely from that mismatch. Comparing ONLY
/// the parsed Major/Minor components from both sides -- the actual granularity this
/// project's versioning scheme uses -- sidesteps that trap entirely.
///
/// Numeric (not lexical/string) ordering specifically because this project's tags
/// will eventually reach a double-digit minor (PITFALLS.md Pitfall 3):
/// "v2.10".CompareTo("v2.9") as raw strings would incorrectly conclude v2.10 is
/// older than v2.9 -- not yet reachable from today's real tag history (v1.0...v2.1),
/// which is exactly why the unit tests must include a synthetic v2.9/v2.10 case
/// rather than relying on today's data to ever exercise it.
/// </summary>
public static class UpdateVersionComparer
{
    /// <summary>
    /// Strips an optional leading v/V, splits on '.', and parses the first two
    /// segments as non-negative ints via int.TryParse with
    /// CultureInfo.InvariantCulture. Additional segments beyond the first two are
    /// ignored; fewer than two segments is a parse failure. Never throws for any
    /// input, including null/empty/garbage.
    /// </summary>
    public static bool TryParseTag(string? tag, out int major, out int minor)
    {
        major = 0;
        minor = 0;

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

        major = parsedMajor;
        minor = parsedMinor;
        return true;
    }

    /// <summary>
    /// True iff <paramref name="tagName"/>'s parsed Major.Minor is strictly greater
    /// than <paramref name="runningVersion"/>'s Major.Minor, comparing ONLY those two
    /// components from both sides -- never System.Version.CompareTo (see class doc
    /// comment for why). False (never throws) for a null runningVersion or an
    /// unparseable tagName -- a garbage/unreachable tag is never treated as "newer."
    /// </summary>
    public static bool IsNewer(Version? runningVersion, string? tagName)
    {
        if (runningVersion is null)
        {
            return false;
        }

        if (!TryParseTag(tagName, out int tagMajor, out int tagMinor))
        {
            return false;
        }

        if (tagMajor != runningVersion.Major)
        {
            return tagMajor > runningVersion.Major;
        }

        return tagMinor > runningVersion.Minor;
    }
}
