namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device at toggle-time, used to restore it later.
/// DefaultDeviceId is nullable to represent "no default device could be determined"
/// at capture time.
/// </summary>
public sealed record AudioState(string? DefaultDeviceId);
