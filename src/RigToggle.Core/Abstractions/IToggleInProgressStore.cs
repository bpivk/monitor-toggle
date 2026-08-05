using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for the disk-persisted toggle-in-progress crash marker
/// (DISPLAY-13). Lifecycle: Save() is called at toggle start with the target mode
/// and start time; Clear() is called in the orchestrator's finally block on clean
/// completion. If the marker is still present on the next launch (TryLoad() returns
/// non-null), the previous toggle did not finish cleanly. Implemented by
/// RigToggle.Core.Persistence.JsonToggleInProgressStore.
/// </summary>
public interface IToggleInProgressStore
{
    ToggleInProgressMarker? TryLoad();
    void Save(ToggleInProgressMarker marker);
    void Clear();
}
