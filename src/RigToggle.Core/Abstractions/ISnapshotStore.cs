using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Persistence contract for StateSnapshot. Snapshot-file presence itself is the
/// mode indicator (D-14): Mode == RigMode iff Exists() is true. Implemented by
/// RigToggle.Core.Persistence.JsonSnapshotStore (plain net10.0, no Windows API refs).
/// </summary>
public interface ISnapshotStore
{
    bool Exists();
    void Save(StateSnapshot snapshot);
    StateSnapshot? Load();
    void Clear();
}
