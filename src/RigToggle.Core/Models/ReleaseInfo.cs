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
/// </summary>
public sealed record ReleaseInfo(
    string TagName,
    string AssetDownloadUrl,
    string HtmlUrl,
    DateTimeOffset PublishedAt,
    bool Prerelease,
    string? Body);
