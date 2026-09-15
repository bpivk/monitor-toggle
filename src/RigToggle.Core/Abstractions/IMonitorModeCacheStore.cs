using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// On-disk persistence for WindowsMonitorController's per-device-path live-mode cache
/// (debug session monitor-pos-no-persist). Implemented by
/// RigToggle.Core.Persistence.JsonMonitorModeCacheStore. Load() never throws --
/// matching JsonSettingsStore/JsonModeStore's existing "degrade to empty on a
/// missing/corrupt file" convention -- since a cache-load failure must never block
/// the monitor toggle. Save() follows the sibling stores' convention instead (may
/// throw on a genuine I/O failure); WindowsMonitorController is responsible for
/// wrapping its Save() call defensively, since it runs on the CCD-activation hot
/// path where a disk failure must never abort an in-progress monitor toggle.
/// </summary>
public interface IMonitorModeCacheStore
{
    /// <summary>All persisted entries, or an empty list if the file is missing, corrupt, or unreadable.</summary>
    IReadOnlyList<CachedMonitorMode> Load();

    /// <summary>Overwrites the persisted set with exactly the given entries.</summary>
    void Save(IReadOnlyList<CachedMonitorMode> modes);
}
