namespace RigToggle.Core.Models;

/// <summary>
/// Explicit, file-backed current-mode value (Normal or Rig). Replaces the retired
/// D-14 snapshot-presence inference (IsInRigMode() == _snapshotStore.Exists()) — the
/// snapshot-restore mechanism no longer exists for Normal mode, so mode must be its
/// own persisted fact rather than derived from another file's presence. Normal is
/// listed first: a fresh install with no snapshot/mode file seeds to Normal (see
/// Plan 04 bootstrap).
/// </summary>
public enum ToggleMode
{
    Normal,
    Rig,
}
