using RigToggle.Core;
using RigToggle.Core.Models;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves ToggleResultFormatter reproduces MainForm's exact checklist wording
/// byte-for-byte, plus the mode-title and balloon-truncation helpers new for this
/// phase (D-09).
/// </summary>
public class ToggleResultFormatterTests
{
    [Fact]
    public void FormatChecklist_AllSucceeded_RendersOkPerStep()
    {
        var result = new ToggleResult(new List<ToggleStepResult>
        {
            new("Monitor", ToggleStepOutcome.Succeeded, null),
            new("Audio", ToggleStepOutcome.Succeeded, null),
            new("App", ToggleStepOutcome.Succeeded, null),
        });

        string expected = string.Join(
            Environment.NewLine,
            "Monitor: OK",
            "Audio: OK",
            "App: OK");

        Assert.Equal(expected, ToggleResultFormatter.FormatChecklist(result));
    }

    [Fact]
    public void FormatChecklist_FailedStep_RendersFailedWithReason()
    {
        var result = new ToggleResult(new List<ToggleStepResult>
        {
            new("Audio", ToggleStepOutcome.Failed, "device not found"),
        });

        Assert.Equal("Audio: FAILED (device not found)", ToggleResultFormatter.FormatChecklist(result));
    }

    [Fact]
    public void FormatChecklist_NotAttemptedStep_RendersNotAttempted()
    {
        var result = new ToggleResult(new List<ToggleStepResult>
        {
            new("App", ToggleStepOutcome.NotAttempted, null),
        });

        Assert.Equal("App: not attempted", ToggleResultFormatter.FormatChecklist(result));
    }

    [Fact]
    public void FormatChecklist_SkippedStep_RendersLowercaseSkipped()
    {
        var result = new ToggleResult(new List<ToggleStepResult>
        {
            new("Audio", ToggleStepOutcome.Skipped, null),
        });

        Assert.Equal("Audio: skipped (not configured)", ToggleResultFormatter.FormatChecklist(result));
    }

    [Theory]
    [InlineData(true, "Switched to Rig Mode")]
    [InlineData(false, "Switched to Normal Mode")]
    public void FormatModeTitle_ReturnsExpectedTitle(bool isInRigMode, string expected)
    {
        Assert.Equal(expected, ToggleResultFormatter.FormatModeTitle(isInRigMode));
    }

    [Fact]
    public void TruncateForBalloon_ShortText_ReturnsUnchanged()
    {
        string text = "short reason";

        Assert.Equal(text, ToggleResultFormatter.TruncateForBalloon(text, 250));
    }

    [Fact]
    public void TruncateForBalloon_LongText_TruncatesWithEllipsisMarker()
    {
        string text = new string('x', 300);

        string result = ToggleResultFormatter.TruncateForBalloon(text, 250);

        Assert.True(result.Length <= 250);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void TruncateForBalloon_EmptyString_NeverThrows()
    {
        string result = ToggleResultFormatter.TruncateForBalloon(string.Empty, 250);

        Assert.Equal(string.Empty, result);
    }
}
