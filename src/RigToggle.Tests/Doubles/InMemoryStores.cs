using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

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
/// (Exists/TryLoad are read-only queries and intentionally not logged; only mutating
/// operations are logged). Unlike JsonModeStore, this double does not simulate
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
/// "marker.Clear" to the shared call-log; read-only queries are intentionally not
/// logged, so downstream ToggleService/ToggleOrchestrator tests can assert interaction ordering.
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

/// <summary>
/// CR-01 (code review) regression double: IToggleInProgressStore whose Clear() always
/// throws, simulating a marker-cleanup I/O failure (AV lock, sharing violation,
/// permissions). Save()/TryLoad() behave normally so the rest of the guarded-toggle
/// pipeline is unaffected — only marker cleanup is broken.
/// </summary>
public sealed class ThrowingClearToggleInProgressStore : IToggleInProgressStore
{
    private ToggleInProgressMarker? _marker;

    public ToggleInProgressMarker? TryLoad() => _marker;

    public void Save(ToggleInProgressMarker marker) => _marker = marker;

    public void Clear() => throw new IOException("Simulated marker cleanup failure.");
}

/// <summary>
/// Hand-written ISettingsStore double whose Load() always throws, simulating a
/// settings-file I/O failure surfaced as an exception (AV lock, permissions) --
/// distinct from JsonSettingsStore's own degrade-to-fresh-AppSettings path, which
/// never throws. Used by OverridableThemeProviderTests to prove the resolver's
/// fail-silent read helper degrades to System (null) rather than propagating.
/// Save() also throws if invoked, since Plan 23-01 never calls ISettingsStore.Save
/// (hard constraint 7) -- an accidental call should fail loudly, not silently
/// succeed.
/// </summary>
public sealed class ThrowingSettingsStore : ISettingsStore
{
    public AppSettings Load() => throw new IOException("Simulated settings load failure.");

    public void Save(AppSettings settings) => throw new IOException("Simulated settings save failure.");
}
