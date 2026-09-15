namespace RigToggle.Core.Models;

/// <summary>
/// On-disk-persistable snapshot of one device path's last-known live CCD mode
/// (PathDisplaySource identity + Position/Resolution/PixelFormat), keyed by
/// DevicePath by the caller (WindowsMonitorController). Mirrors MonitorPathSnapshot's
/// existing "cast CCD enums to plain int, zero WindowsDisplayAPI reference" convention
/// (CORE-05) so RigToggle.Core stays free of WindowsDisplayAPI references and this
/// stays trivially JSON-serializable.
///
/// Debug session monitor-pos-no-persist: closes the exact gap
/// WindowsMonitorController's own class remarks documented as a known, accepted
/// scope boundary (see that class's _lastKnownActiveModeByDevicePath field doc) --
/// that in-memory cache does not survive an app restart, so a monitor disabled by
/// one process and re-enabled by a newly-started one (confirmed via a user debug.log
/// showing two different pid= STARTUP lines bracketing the disable/enable pair) lost
/// its position, collapsing a 2-1-3 monitor arrangement to 1-2. AdapterIdLowPart/
/// AdapterIdHighPart/SourceId (the source's LUID + source id) are captured alongside
/// Position/Resolution/PixelFormat -- not just position -- so
/// TryBuildScopedActivationPlan's existing SelectSourceForActivation source-reclaim
/// preference (round 14 fix B) also survives a restart, not just position. A
/// stale/no-longer-present adapter identity (e.g. after a reboot changes LUIDs)
/// simply fails SelectSourceForActivation's Contains() check and falls back to the
/// existing greedy first-unclaimed pick -- byte-for-byte the same degrade-gracefully
/// behavior as a missing cache entry today, so this can only improve on today's
/// restart behavior, never regress the already-verified same-session behavior.
///
/// LastCachedUtc supports a simple, recency-ordered bound on the persisted set
/// (see WindowsMonitorController.PersistMonitorModeCache) so a permanently-unplugged
/// monitor's stale entry does not accumulate on disk forever -- deliberately not a
/// timed expiry (a monitor could legitimately be unplugged for months and still be
/// worth restoring the position of when reconnected), just a bounded count.
/// </summary>
public sealed record CachedMonitorMode(
    string DevicePath,
    uint AdapterIdLowPart,
    int AdapterIdHighPart,
    uint SourceId,
    int PositionX,
    int PositionY,
    int ResolutionWidth,
    int ResolutionHeight,
    int PixelFormat,
    DateTimeOffset LastCachedUtc);
