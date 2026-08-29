using RigToggle.Core;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Quick-260829-ga9/UPDATE-06: proves UpdateCheckMessageFormatter.FormatStatus's
/// per-outcome wording, matching UpdateOrchestratorTests.cs's plain-record,
/// no-mocking-library convention. Declined/Skipped assert string.Empty (D-05: a
/// dialog the user is already looking at shouldn't re-announce a choice they just
/// made); NotAvailable/CheckFailed assert distinguishable, non-empty copy (D-06/
/// D-07); Applying asserts a non-empty in-progress message; a null result argument
/// asserts ArgumentNullException, matching this codebase's existing null-guard
/// convention (e.g. AboutForm's own constructor).
/// </summary>
public class UpdateCheckMessageFormatterTests
{
    [Fact]
    public void FormatStatus_NotAvailable_WithVersion_ContainsLatestVersionAndVersionText()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.NotAvailable, "2.2.1", FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.Contains("latest version", message);
        Assert.Contains("2.2.1", message);
    }

    [Fact]
    public void FormatStatus_NotAvailable_WithNullVersion_DropsVersionClauseEntirely()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.NotAvailable, RunningVersionText: null, FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.False(string.IsNullOrEmpty(message));
        Assert.Contains("latest version", message);
        Assert.DoesNotContain("v)", message);
        Assert.DoesNotContain("()", message);
    }

    [Fact]
    public void FormatStatus_CheckFailed_WithReason_ContainsReasonAndIsDistinctFromNotAvailable()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.CheckFailed, RunningVersionText: "2.2.1", FailureReason: "the network is unreachable");

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.Contains("the network is unreachable", message);
        Assert.DoesNotContain("latest version", message);
    }

    [Fact]
    public void FormatStatus_CheckFailed_WithNullReason_UsesGenericFailureMessage()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.CheckFailed, RunningVersionText: "2.2.1", FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.False(string.IsNullOrEmpty(message));
        Assert.DoesNotContain("latest version", message);
    }

    [Fact]
    public void FormatStatus_CheckFailed_WithEmptyReason_MatchesNullReasonMessage()
    {
        var nullReasonResult = new UpdateCheckResult(UpdateCheckOutcome.CheckFailed, RunningVersionText: "2.2.1", FailureReason: null);
        var emptyReasonResult = new UpdateCheckResult(UpdateCheckOutcome.CheckFailed, RunningVersionText: "2.2.1", FailureReason: "");

        string nullMessage = UpdateCheckMessageFormatter.FormatStatus(nullReasonResult);
        string emptyMessage = UpdateCheckMessageFormatter.FormatStatus(emptyReasonResult);

        Assert.Equal(nullMessage, emptyMessage);
    }

    [Fact]
    public void FormatStatus_Applying_DescribesDownloadingOrInstalling()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.Applying, RunningVersionText: "2.2.1", FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.False(string.IsNullOrEmpty(message));
        Assert.Matches("(?i)download|install", message);
    }

    [Fact]
    public void FormatStatus_Declined_ReturnsEmptyString()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.Declined, RunningVersionText: "2.2.1", FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void FormatStatus_Skipped_ReturnsEmptyString()
    {
        var result = new UpdateCheckResult(UpdateCheckOutcome.Skipped, RunningVersionText: "2.2.1", FailureReason: null);

        string message = UpdateCheckMessageFormatter.FormatStatus(result);

        Assert.Equal(string.Empty, message);
    }

    [Fact]
    public void FormatStatus_NullResult_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => UpdateCheckMessageFormatter.FormatStatus(null!));
    }
}
