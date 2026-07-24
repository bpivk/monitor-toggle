namespace RigToggle.Core.Models;

/// <summary>
/// Combined monitor + audio state captured immediately before a toggle mutation,
/// persisted via ISnapshotStore so toggle-back can restore the exact prior configuration.
/// Snapshot-file presence itself is what determines current mode (D-14): Mode == RigMode
/// iff ISnapshotStore.Exists() is true.
/// </summary>
public sealed record StateSnapshot(MonitorState Monitor, AudioState Audio);
