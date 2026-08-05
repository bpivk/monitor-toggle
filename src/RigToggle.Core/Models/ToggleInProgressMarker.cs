using System;

namespace RigToggle.Core.Models;

/// <summary>
/// Disk-persisted crash-detection marker (DISPLAY-13): written when a toggle starts
/// (target mode + start time), cleared on clean completion. If this marker is found
/// on next launch, the previous toggle did not finish cleanly — the app was killed or
/// crashed mid-toggle.
///
/// This is a DIFFERENT concept from RigToggle.Core.ToggleInProgressException, an
/// unrelated in-memory reentrancy-guard exception (CORE-06) thrown by
/// ToggleOrchestrator when a second toggle is requested while one is already running.
/// That exception never touches disk and has nothing to do with crash recovery — do
/// not conflate the two.
/// </summary>
public sealed record ToggleInProgressMarker(ToggleMode TargetMode, DateTimeOffset StartedAtUtc);
