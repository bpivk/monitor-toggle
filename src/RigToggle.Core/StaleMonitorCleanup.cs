using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Automatically ages out monitor device paths that AppSettings' four monitor sets
/// (MonitorsToDisable/MonitorsToEnable/NormalMonitorsToDisable/NormalMonitorsToEnable)
/// reference but GetAllMonitors() can no longer detect at all -- e.g. a monitor renamed
/// or re-enumerated by Windows, never coming back under its old device path. Settings
/// itself always preserves an undetected path indefinitely on every Save (so opening
/// Settings for something unrelated never wipes a merely-unplugged monitor's config) --
/// this is the mechanism that eventually cleans up a genuinely-gone one, with zero user
/// interaction and zero Settings-warning UI, since the app has no way to ask "is this
/// temporary?" and a fixed wait is the simplest resolution that doesn't require asking.
/// </summary>
public static class StaleMonitorCleanup
{
    /// <summary>
    /// How long a device path may go undetected before it is dropped for good. Chosen to
    /// comfortably outlast an extended real-world disconnection (travel, a monitor sent
    /// for repair) while still eventually clearing a permanently-gone one without any
    /// user action.
    /// </summary>
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(30);

    /// <summary>
    /// Reconciles <paramref name="settings"/> against <paramref name="currentlyDetectedDevicePaths"/>:
    /// clears tracking for any path that is visible again, starts tracking any path seen
    /// missing for the first time, and drops any path that has been missing for at least
    /// <paramref name="expiry"/> from every monitor set and from tracking. Mutates
    /// <paramref name="settings"/> in place; returns true if anything changed (caller
    /// should persist).
    /// </summary>
    public static bool Reconcile(
        AppSettings settings,
        IReadOnlySet<string> currentlyDetectedDevicePaths,
        DateTime nowUtc,
        TimeSpan expiry)
    {
        var tracking = settings.StaleMonitorFirstMissingUtc is null
            ? new Dictionary<string, DateTime>()
            : new Dictionary<string, DateTime>(settings.StaleMonitorFirstMissingUtc);

        var referenced = new HashSet<string>();
        referenced.UnionWith(settings.MonitorsToDisable ?? new List<string>());
        referenced.UnionWith(settings.MonitorsToEnable ?? new List<string>());
        referenced.UnionWith(settings.NormalMonitorsToDisable ?? new List<string>());
        referenced.UnionWith(settings.NormalMonitorsToEnable ?? new List<string>());

        var toExpire = new HashSet<string>();
        bool changed = false;

        foreach (string devicePath in referenced)
        {
            if (currentlyDetectedDevicePaths.Contains(devicePath))
            {
                if (tracking.Remove(devicePath))
                {
                    changed = true;
                }

                continue;
            }

            if (!tracking.TryGetValue(devicePath, out DateTime firstMissingUtc))
            {
                tracking[devicePath] = nowUtc;
                changed = true;
                continue;
            }

            if (nowUtc - firstMissingUtc >= expiry)
            {
                toExpire.Add(devicePath);
            }
        }

        // Drop tracking for anything no longer referenced at all (e.g. a prior expiry
        // already removed it from the monitor sets, or the user hand-edited settings.json).
        foreach (string tracked in tracking.Keys.Where(p => !referenced.Contains(p)).ToList())
        {
            tracking.Remove(tracked);
            changed = true;
        }

        if (toExpire.Count > 0)
        {
            settings.MonitorsToDisable = settings.MonitorsToDisable?.Where(p => !toExpire.Contains(p)).ToList();
            settings.MonitorsToEnable = settings.MonitorsToEnable?.Where(p => !toExpire.Contains(p)).ToList();
            settings.NormalMonitorsToDisable = settings.NormalMonitorsToDisable?.Where(p => !toExpire.Contains(p)).ToList();
            settings.NormalMonitorsToEnable = settings.NormalMonitorsToEnable?.Where(p => !toExpire.Contains(p)).ToList();

            foreach (string expired in toExpire)
            {
                tracking.Remove(expired);
            }

            changed = true;
        }

        settings.StaleMonitorFirstMissingUtc = tracking.Count > 0 ? tracking : null;

        return changed;
    }
}
