using System;

namespace RigToggle.Core.Models;

/// <summary>
/// Immutable snapshot of a single GitHub release, as returned by
/// GET /repos/{owner}/{repo}/releases/latest (RigToggle.Core.GitHubReleaseFeed).
/// Follows ToggleInProgressMarker.cs's exact shape: a plain sealed record with
/// primary-constructor properties, no behavior.
///
/// <see cref="Body"/> carries the release's Markdown notes -- D-01 requires showing
/// them in the confirm dialog, which is why this record's field list goes one
/// property beyond ARCHITECTURE.md's original component sketch (which omitted
/// Body); UpdatePromptDialog is the sole consumer of this field.
///
/// <see cref="ChecksumDownloadUrl"/> (D-10/D-11) is nullable because a release
/// published before this phase (every tag through v2.1) has no ".sha256" asset --
/// GitHubReleaseFeed resolves it independently of AssetDownloadUrl and returns null
/// rather than discarding the whole release when it is absent. Fail-closed
/// consequence: an update offered from a pre-checksum release cannot be verified
/// and therefore cannot be applied -- WindowsUpdateApplier.DownloadAndStageAsync
/// treats a null ChecksumDownloadUrl as an apply failure, not an implicit pass.
/// </summary>
public sealed record ReleaseInfo(
    string TagName,
    string AssetDownloadUrl,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    bool Prerelease,
    string? Body,
    string? ChecksumDownloadUrl);
