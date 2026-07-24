namespace RigToggle.Core.Models;

/// <summary>
/// A single enumerated display, as returned by IMonitorController.GetActiveMonitors().
/// DevicePath is the stable identifier persisted in AppSettings.MonitorDevicePath.
/// </summary>
public sealed record MonitorInfo(string DevicePath, string FriendlyName, bool IsPrimary);
