namespace RigToggle.Core.Models;

/// <summary>
/// A single enumerated display, as returned by IMonitorController.GetActiveMonitors()
/// (currently-active displays only) or GetAllMonitors() (active + currently-disabled
/// displays, added Phase 6). DevicePath is the stable identifier persisted in
/// AppSettings.MonitorsToDisable/MonitorsToEnable (and the legacy MonitorDevicePath).
/// IsActive drives the settings grid's "(currently OS-disabled)" suffix and the
/// DISPLAY-06 "leave at least one monitor active" validation predicate.
/// </summary>
public sealed record MonitorInfo(string DevicePath, string FriendlyName, bool IsPrimary, bool IsActive = false);
