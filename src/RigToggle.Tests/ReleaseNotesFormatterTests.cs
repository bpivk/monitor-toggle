using System.Linq;
using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves ReleaseNotesFormatter.Format's supported Markdown-lite subset (headers,
/// bullets, inline bold), the empty-body fallback, and unsupported-construct
/// degradation -- HotkeyFormatterTests.cs's [Theory]/[InlineData] style where a case
/// is a simple input/output pair, [Fact] where a run sequence must be asserted.
///
/// The "unsupported constructs degrade" group below is the automated half of the
/// 26-UI-SPEC.md overflow row's backstop coverage; the remaining evidence -- how a
/// real GitHub release body with rich formatting actually looks rendered in the
/// dialog -- comes from the operator checkpoint in plan 26-05.
/// </summary>
public class ReleaseNotesFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \n")]
    public void Format_NullOrWhitespaceBody_ReturnsSinglePlainRunWithFallbackText(string? body)
    {
        var runs = ReleaseNotesFormatter.Format(body);

        var run = Assert.Single(runs);
        Assert.Equal(ReleaseNotesFormatter.FallbackText, run.Text);
        Assert.Equal(ReleaseNoteStyle.Body, run.Style);
        Assert.False(run.Bullet);
        Assert.True(run.EndsLine);
    }

    [Fact]
    public void Format_TopLevelHeader_YieldsOneHeadingRun_NoBullet()
    {
        var runs = ReleaseNotesFormatter.Format("# Title");

        var run = Assert.Single(runs);
        Assert.Equal("Title", run.Text);
        Assert.Equal(ReleaseNoteStyle.Heading, run.Style);
        Assert.False(run.Bullet);
    }

    [Theory]
    [InlineData("## Section", "Section")]
    [InlineData("### Sub", "Sub")]
    public void Format_SubHeader_YieldsOneLabelRun(string body, string expectedText)
    {
        var runs = ReleaseNotesFormatter.Format(body);

        var run = Assert.Single(runs);
        Assert.Equal(expectedText, run.Text);
        Assert.Equal(ReleaseNoteStyle.Label, run.Style);
        Assert.False(run.Bullet);
    }

    [Theory]
    [InlineData("- First item", "First item")]
    [InlineData("* First item", "First item")]
    public void Format_BulletLine_YieldsBulletTrueWithMarkerStripped(string body, string expectedText)
    {
        var runs = ReleaseNotesFormatter.Format(body);

        var run = Assert.Single(runs);
        Assert.Equal(expectedText, run.Text);
        Assert.True(run.Bullet);
    }

    [Fact]
    public void Format_LineContainingBold_YieldsThreeRuns_PlainBoldPlain_AsterisksStripped()
    {
        var runs = ReleaseNotesFormatter.Format("This is **bold** text");

        Assert.Equal(3, runs.Count);
        Assert.Equal("This is ", runs[0].Text);
        Assert.Equal(ReleaseNoteStyle.Body, runs[0].Style);
        Assert.False(runs[0].EndsLine);

        Assert.Equal("bold", runs[1].Text);
        Assert.Equal(ReleaseNoteStyle.Label, runs[1].Style);
        Assert.False(runs[1].EndsLine);

        Assert.Equal(" text", runs[2].Text);
        Assert.Equal(ReleaseNoteStyle.Body, runs[2].Style);
        Assert.True(runs[2].EndsLine);

        Assert.DoesNotContain("*", string.Concat(runs.Select(r => r.Text)));
    }

    [Theory]
    [InlineData("Text with ** an unmatched marker")]
    [InlineData("** leading unmatched")]
    [InlineData("trailing unmatched **")]
    public void Format_UnmatchedBoldDelimiter_YieldsSinglePlainRun_CharactersPreservedVerbatim(string body)
    {
        var runs = ReleaseNotesFormatter.Format(body);

        var run = Assert.Single(runs);
        Assert.Equal(body, run.Text);
        Assert.Equal(ReleaseNoteStyle.Body, run.Style);
    }

    [Theory]
    [InlineData("| Col A | Col B |")]
    [InlineData("![alt text](https://example.com/image.png)")]
    [InlineData("[link text](https://example.com)")]
    [InlineData("```csharp\nvar x = 1;\n```")]
    [InlineData("  - nested list item")]
    public void Format_UnsupportedConstruct_DegradesToPlainText_NeverBreaksOrDropsContent(string body)
    {
        var runs = ReleaseNotesFormatter.Format(body);

        Assert.NotEmpty(runs);
        Assert.Equal(body, string.Join("\n", runs.Select(r => r.Text)).TrimEnd('\n'));
        Assert.All(runs, r => Assert.Equal(ReleaseNoteStyle.Body, r.Style));
    }

    [Fact]
    public void Format_ArbitrarilyLongBody_NeverThrows_NeverTruncates()
    {
        string longLine = new string('x', 50_000);
        string body = string.Join("\n", Enumerable.Repeat(longLine, 20));

        var exception = Record.Exception(() => ReleaseNotesFormatter.Format(body));

        Assert.Null(exception);

        var runs = ReleaseNotesFormatter.Format(body);
        int totalLength = runs.Sum(r => r.Text.Length);

        // 20 lines of 50,000 chars each, none stripped/truncated.
        Assert.Equal(20 * 50_000, totalLength);
    }

    [Fact]
    public void Format_MultiLineBody_MarksLastRunOfEachLineWithEndsLine()
    {
        var runs = ReleaseNotesFormatter.Format("# Heading\n- Bullet one\n- Bullet two");

        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.True(r.EndsLine));
        Assert.Equal("Heading", runs[0].Text);
        Assert.Equal("Bullet one", runs[1].Text);
        Assert.Equal("Bullet two", runs[2].Text);
    }
}
