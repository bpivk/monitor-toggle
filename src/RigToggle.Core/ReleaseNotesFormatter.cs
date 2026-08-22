namespace RigToggle.Core;

/// <summary>
/// The three visual roles a ReleaseNoteRun can carry (26-UI-SPEC.md Typography
/// table): Body is plain 9pt regular text, Label is 9pt bold (used for both
/// "##"/"###" headers and inline "**bold**" spans), Heading is 12pt bold (used for
/// a top-level "#" header only).
/// </summary>
public enum ReleaseNoteStyle
{
    Body,
    Label,
    Heading,
}

/// <summary>
/// One styled, optionally bulleted span of release-notes text. <see cref="EndsLine"/>
/// marks the last run produced from a given source line, so ReleaseNotesRenderer
/// knows where to append a newline without the formatter needing to know anything
/// about a WinForms control.
/// </summary>
public sealed record ReleaseNoteRun(string Text, ReleaseNoteStyle Style, bool Bullet, bool EndsLine);

/// <summary>
/// Pure, hand-rolled Markdown-lite parser for a GitHub release body (D-01) --
/// follows ToggleResultFormatter.cs's "pure function, defensive against degenerate
/// input, never throws" discipline. No Markdown library exists in this codebase and
/// none is added here (D-01 is explicit this is a small hand-rolled formatter).
///
/// Supported subset, one line at a time: a leading "# " becomes a top-level
/// <see cref="ReleaseNoteStyle.Heading"/> run; a leading "## " or "### " becomes a
/// <see cref="ReleaseNoteStyle.Label"/> run; a leading "- " or "* " sets
/// <see cref="ReleaseNoteRun.Bullet"/> true with the marker (and one following
/// space) stripped; within any line, paired "**...**" delimiters split the line
/// into alternating plain (the line's own style) and Label-styled bold runs, with
/// the delimiters themselves removed. Everything else -- tables, images, links,
/// fenced code blocks, nested lists, and any unpaired "**" -- is intentionally
/// passed through as plain text with its original characters preserved: this
/// formatter degrades unsupported constructs rather than attempting to parse them,
/// so the release-notes view is never worse than a readable raw dump.
///
/// Never throws for any input, including null/empty/whitespace/arbitrarily long
/// bodies: <see cref="Format"/> wraps its own parse in a try/catch that, on any
/// unexpected failure, returns a single plain run carrying the original text
/// unchanged (T-26-13's DoS/pathological-input mitigation).
/// </summary>
public static class ReleaseNotesFormatter
{
    /// <summary>
    /// UI-SPEC Copywriting Contract's verbatim empty-state copy -- shown as the
    /// release notes area's sole content when a release has no body.
    /// </summary>
    public const string FallbackText = "This release doesn't include notes. See the full release on GitHub for details.";

    /// <summary>
    /// Parses <paramref name="body"/> into a flat, ordered list of styled runs. A
    /// null, empty, or all-whitespace body yields exactly one plain run carrying
    /// <see cref="FallbackText"/> verbatim -- never a blank/empty result.
    /// </summary>
    public static IReadOnlyList<ReleaseNoteRun> Format(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new[] { new ReleaseNoteRun(FallbackText, ReleaseNoteStyle.Body, false, true) };
        }

        try
        {
            var runs = new List<ReleaseNoteRun>();
            string[] lines = body.Replace("\r\n", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                FormatLine(rawLine, runs);
            }

            return runs;
        }
        catch
        {
            // T-26-13: a single linear pass with no backtracking regex should never
            // fail, but if it somehow does, degrade to a readable raw dump rather
            // than an empty dialog or a propagated exception.
            return new[] { new ReleaseNoteRun(body, ReleaseNoteStyle.Body, false, true) };
        }
    }

    private static void FormatLine(string rawLine, List<ReleaseNoteRun> runs)
    {
        string line = rawLine;
        ReleaseNoteStyle lineStyle = ReleaseNoteStyle.Body;
        bool bullet = false;

        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            lineStyle = ReleaseNoteStyle.Label;
            line = line[4..];
        }
        else if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            lineStyle = ReleaseNoteStyle.Label;
            line = line[3..];
        }
        else if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            lineStyle = ReleaseNoteStyle.Heading;
            line = line[2..];
        }
        else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
        {
            bullet = true;
            line = line[2..];
        }

        List<(string Text, bool Bold)> segments = SplitBoldSegments(line);

        for (int i = 0; i < segments.Count; i++)
        {
            bool isLast = i == segments.Count - 1;
            ReleaseNoteStyle segmentStyle = segments[i].Bold ? ReleaseNoteStyle.Label : lineStyle;
            runs.Add(new ReleaseNoteRun(segments[i].Text, segmentStyle, bullet, isLast));
        }
    }

    /// <summary>
    /// Splits a single line on paired "**" delimiters into alternating plain/bold
    /// segments, delimiters removed. An ODD number of "**" occurrences (an
    /// unpaired/dangling delimiter) is detected via Split producing an EVEN element
    /// count, and is deliberately treated as "no bold in this line at all" -- the
    /// whole line is returned as one plain segment with its original characters,
    /// asterisks included, preserved verbatim rather than guessed-at.
    /// </summary>
    private static List<(string Text, bool Bold)> SplitBoldSegments(string line)
    {
        string[] parts = line.Split("**", StringSplitOptions.None);

        if (parts.Length % 2 == 0)
        {
            return new List<(string, bool)> { (line, false) };
        }

        var result = new List<(string, bool)>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                // An empty plain segment is expected at the boundaries of e.g.
                // "**bold**" (empty text before/after the delimiters) -- dropping it
                // avoids producing a stray blank run.
                continue;
            }

            result.Add((parts[i], i % 2 == 1));
        }

        if (result.Count == 0)
        {
            result.Add((string.Empty, false));
        }

        return result;
    }
}
