namespace RigToggle.Core.Models;

/// <summary>
/// A full-topology capture of every active display path at capture time, used by
/// ToggleService's CR-01 unchanged-versus-mutated comparison after a failed monitor
/// step: a second CaptureState() taken post-failure is compared against this one to
/// detect whether the CCD mutation actually touched live state before it threw.
/// TargetDevicePath names which entry was the configured monitor being acted on at
/// capture time.
///
/// History: originally captured one MonitorPathSnapshot per active display path (not
/// just the target) because Phase 4's repositioning-aware disable shifted every
/// surviving path's coordinates, and a since-retired snapshot-restore mechanism needed
/// to undo that shift for every display, not just reactivate the disabled one
/// (DISPLAY-02). That restore path was removed in Phase 18 once Phase 16 replaced it
/// with explicit per-mode target application; this record's shape was kept as-is
/// because CR-01's comparison still needs the same full-topology snapshot.
/// </summary>
public sealed record MonitorState(IReadOnlyList<MonitorPathSnapshot> Paths, string TargetDevicePath);
