using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

/// <summary>
/// Hand-written in-memory ISnapshotStore double. Records "snapshot.Save" / "snapshot.Clear"
/// to the shared call-log (Exists/Load are read-only queries and intentionally not logged,
/// so assertions like "Clear is the last snapshot-store interaction" stay unambiguous).
/// </summary>
public sealed class InMemorySnapshotStore : ISnapshotStore
{
    private readonly List<string> _callLog;
    private StateSnapshot? _snapshot;

    public InMemorySnapshotStore(List<string> callLog) => _callLog = callLog;

    public bool Exists() => _snapshot is not null;

    public void Save(StateSnapshot snapshot)
    {
        _snapshot = snapshot;
        _callLog.Add("snapshot.Save");
    }

    public StateSnapshot? Load() => _snapshot;

    public void Clear()
    {
        _snapshot = null;
        _callLog.Add("snapshot.Clear");
    }
}

/// <summary>
/// Hand-written in-memory ISettingsStore double. Load() returns whatever was supplied at
/// construction (or via the Current property), matching the "already configured" precondition
/// ToggleService assumes when orchestrating a toggle.
/// </summary>
public sealed class InMemorySettingsStore : ISettingsStore
{
    public InMemorySettingsStore(AppSettings? initial = null)
    {
        Current = initial ?? new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public AppSettings Load() => Current;

    public void Save(AppSettings settings) => Current = settings;
}
