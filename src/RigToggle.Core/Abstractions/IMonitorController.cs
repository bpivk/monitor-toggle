using RigToggle.Core.Models;

namespace RigToggle.Core.Abstractions;

/// <summary>
/// Monitor enumeration and CCD-level activate/deactivate contract. Implemented
/// by RigToggle.Windows.WindowsMonitorController. Read methods (GetActiveMonitors,
/// GetAllMonitors, CaptureState) are real starting Phase 2/6; mutating methods
/// (ActivateMonitors, DeactivateMonitors) are real starting Phase 4/6
/// (02-RESEARCH.md Pattern 1; 04-RESEARCH.md Patterns 1/2/3/4; 06-RESEARCH.md
/// Patterns 1/2/3 for the N-monitor generalization).
/// </summary>
public interface IMonitorController
{
    /// <summary>Currently-active displays only. Used by CaptureState's existing snapshot logic.</summary>
    IReadOnlyList<MonitorInfo> GetActiveMonitors();

    /// <summary>
    /// Active AND currently OS-disabled-but-available displays (06-RESEARCH.md Pattern 3).
    /// Required to resolve an enable-set monitor's friendly name/identity at confirm-time,
    /// since it is inactive by definition before ActivateMonitors runs and therefore cannot
    /// resolve via GetActiveMonitors().
    /// </summary>
    IReadOnlyList<MonitorInfo> GetAllMonitors();

    MonitorState CaptureState();

    /// <summary>
    /// Activates the given OS-disabled monitors via CCD Extend topology (06-RESEARCH.md
    /// Pattern 2) — the N-monitor generalization of the enable-set half of the rig-mode
    /// Monitor step. Must run BEFORE DeactivateMonitors on rig-mode entry (Pitfall 2
    /// ordering constraint).
    /// </summary>
    /// <param name="monitorDevicePaths">The OS-disabled device paths to activate.</param>
    /// <param name="monitorSwapDisableSet">
    /// Debug session monitor-position-resets-to-de (Symptom 2, round 3): the device paths a
    /// DeactivateMonitors call — running immediately after, within the same logical
    /// Monitor-step operation — is about to remove, or an empty set when no such call
    /// follows (e.g. a single dashboard-tile enable action). Round 2's rig log showed
    /// WindowsMonitorController's scoped-activation path is unreliable when it transiently
    /// keeps every already-active survivor active alongside the newly-activated target(s)
    /// during a full swap (silently reverted by the OS/driver seconds after an apparently-
    /// successful apply, or an outright PathChangeException on the reverse direction).
    /// Round 3's rig log then showed the "skip scoped activation, fall back to whole-
    /// topology Extend" mitigation that shape led to is ALSO unreliable — Extend both
    /// failed to activate the actually-requested target AND reactivated an unrelated,
    /// independently-disabled monitor in the same call — because ApplyTopology(Extend)
    /// passes no explicit path array at all (confirmed by decompiling WindowsDisplayAPI
    /// 1.3.0.13: it calls SetDisplayConfig(0, null, 0, null, ...) — zero paths/modes
    /// supplied), so it structurally cannot express "activate exactly this target, and
    /// nothing this caller didn't ask for." The fix: the implementation excludes
    /// monitorSwapDisableSet from the survivors it preserves when building its scoped,
    /// explicit-path plan, so a single ApplyPathInfos call goes directly from the pre-
    /// toggle topology to the exact desired post-toggle topology — never transiently
    /// holding more monitors active than either state, and freeing the excluded survivors'
    /// PathDisplaySource for the new target(s) to claim. Callers whose accompanying
    /// DeactivateMonitors call targets a different set must pass that set here; callers
    /// with no accompanying deactivation must pass an empty set.
    /// </param>
    void ActivateMonitors(IReadOnlySet<string> monitorDevicePaths, IReadOnlySet<string> monitorSwapDisableSet);

    /// <summary>
    /// Deactivates (true CCD-level detach, not power-off) the given monitors
    /// (06-RESEARCH.md Pattern 1) — the N-monitor generalization of the former
    /// single-target Disable(string). Reused for both the rig-mode disable-set removal
    /// and the toggle-back enable-set teardown (D-02).
    /// </summary>
    void DeactivateMonitors(IReadOnlySet<string> monitorDevicePaths);
}
