using System;

namespace RigToggle.Core.Models;

/// <summary>
/// Disk-persisted cross-version state (D-09, UPDATE-05): written by the OUTGOING
/// version's helper process (UpdateApplyEntryPoint, immediately after a successful
/// swap) and read by the INCOMING version's startup code (UpdateRollbackChecker).
/// Because the writer and reader are literally different shipped binaries, the
/// member names below are part of the on-disk format and must not be renamed --
/// doing so would make a v2.3+ that changed them unable to recognise a marker
/// written by v2.2's helper, silently breaking auto-rollback exactly in the
/// unattended-update scenario UPDATE-05 exists to protect (there is no migration
/// path for this file, unlike settings.json).
///
/// Stage is serialized as a string (JsonStringEnumConverter, see
/// JsonUpdateAppliedMarkerStore) rather than the default ordinal, so a future
/// member reorder can never silently reinterpret an on-disk value.
///
/// Deliberately a sibling of, not a reuse/extension of, ToggleInProgressMarker --
/// these are two distinct crash-detection concerns this codebase keeps in separate
/// types (mirrors PITFALLS.md Pitfall 7's "don't reuse _busy for a different
/// concern" lesson).
/// </summary>
public sealed record UpdateAppliedMarker(
    string NewVersion,
    string PreviousVersion,
    DateTimeOffset AppliedAtUtc,
    UpdateMarkerStage Stage);

/// <summary>
/// The three-stage, strictly one-way state machine UpdateRollbackChecker.Run walks
/// through: Applied (swap just completed, the new exe's first launch not yet
/// observed) -> FirstLaunchAttempted (the new exe's first launch started but has
/// not yet confirmed healthy) -> Reverted (the previous exe was restored and this
/// is its first launch back). Every branch of Run either advances this value
/// forward by exactly one stage or clears the marker entirely -- never loops
/// backward, so an update that crashes on every boot can restore-and-relaunch at
/// most once, never retry forever (UPDATE-05).
/// </summary>
public enum UpdateMarkerStage
{
    Applied,
    FirstLaunchAttempted,
    Reverted,
}
