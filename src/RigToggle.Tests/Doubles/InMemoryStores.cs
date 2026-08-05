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

/// <summary>
/// Hand-written in-memory IModeStore double. Records "mode.Save" to the shared call-log
/// (Exists/TryLoad are read-only queries and intentionally not logged, matching
/// InMemorySnapshotStore's convention). Unlike JsonModeStore, this double does not simulate
/// corruption — a dedicated corrupt-file test, if needed, exercises JsonModeStore directly.
/// </summary>
public sealed class InMemoryModeStore : IModeStore
{
    private readonly List<string> _callLog;
    private ToggleMode? _mode;

    public InMemoryModeStore(List<string> callLog) => _callLog = callLog;

    public bool Exists() => _mode is not null;

    public ToggleMode? TryLoad() => _mode;

    public void Save(ToggleMode mode)
    {
        _mode = mode;
        _callLog.Add("mode.Save");
    }
}

/// <summary>
/// Hand-written in-memory IToggleInProgressStore double. Records "marker.Save" /
/// "marker.Clear" to the shared call-log, matching InMemorySnapshotStore's convention so
/// downstream ToggleService/ToggleOrchestrator tests can assert interaction ordering.
/// </summary>
public sealed class InMemoryToggleInProgressStore : IToggleInProgressStore
{
    private readonly List<string> _callLog;
    private ToggleInProgressMarker? _marker;

    public InMemoryToggleInProgressStore(List<string> callLog) => _callLog = callLog;

    public ToggleInProgressMarker? TryLoad() => _marker;

    public void Save(ToggleInProgressMarker marker)
    {
        _marker = marker;
        _callLog.Add("marker.Save");
    }

    public void Clear()
    {
        _marker = null;
        _callLog.Add("marker.Clear");
    }
}
