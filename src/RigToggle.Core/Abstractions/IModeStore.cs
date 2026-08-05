using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for the explicit current-mode value (ToggleMode). Replaces
/// ISnapshotStore's role as the mode indicator (D-14) now that Normal mode no longer
/// restores from a snapshot. TryLoad() returns null when the file is missing OR fails
/// to parse — callers distinguish "never bootstrapped" (Exists() == false) from
/// "exists but corrupted" (Exists() == true, TryLoad() == null) at startup, so a
/// corrupted mode file can fail loudly instead of silently defaulting to a mode
/// (D-06/D-07). Implemented by RigToggle.Core.Persistence.JsonModeStore.
/// </summary>
public interface IModeStore
{
    bool Exists();
    ToggleMode? TryLoad();
    void Save(ToggleMode mode);
}
