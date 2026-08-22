using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for the disk-persisted update-applied-but-unconfirmed
/// marker (UPDATE-05, D-09). Lifecycle: Save() is called by the outgoing version's
/// helper process immediately after a successful swap (Stage = Applied); the
/// incoming version's UpdateRollbackChecker.Run() advances Stage on every launch
/// until the marker is cleared -- either because the new exe reached
/// confirmed-healthy (ConfirmHealthy), or because a restore-and-revert completed.
/// Deliberately a separate interface/type from IToggleInProgressStore -- these are
/// two distinct crash-detection concerns this codebase keeps in separate types.
/// Implemented by RigToggle.Core.Persistence.JsonUpdateAppliedMarkerStore.
/// </summary>
public interface IUpdateAppliedMarkerStore
{
    UpdateAppliedMarker? TryLoad();
    void Save(UpdateAppliedMarker marker);
    void Clear();
}
