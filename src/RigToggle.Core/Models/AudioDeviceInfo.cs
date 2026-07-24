namespace RigToggle.Core.Models;

/// <summary>
/// A single enumerated audio render (playback) endpoint, as returned by
/// IAudioController.GetPlaybackDevices(). Id is the stable identifier persisted
/// in AppSettings.NormalAudioDeviceId / RigAudioDeviceId.
/// </summary>
public sealed record AudioDeviceInfo(string Id, string FriendlyName);
