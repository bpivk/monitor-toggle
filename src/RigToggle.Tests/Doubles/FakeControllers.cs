using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Tests.Doubles;

/// <summary>
/// Hand-written recording fakes for the three mutation-adapter interfaces (no mocking
/// framework — matches the project's no-unnecessary-dependency posture). Each call
/// appends a label to a shared call-log so tests can assert both call order (e.g.
/// snapshot Save must precede any mutation) and the values passed through from settings.
/// </summary>
public sealed class FakeMonitorController : IMonitorController
{
    private readonly List<string> _callLog;

    public FakeMonitorController(List<string> callLog) => _callLog = callLog;

    public IReadOnlyList<MonitorInfo> GetActiveMonitors()
    {
        _callLog.Add("monitor.GetActiveMonitors");
        return new List<MonitorInfo> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", true) };
    }

    public MonitorState CaptureState(string monitorDevicePath)
    {
        _callLog.Add($"monitor.CaptureState:{monitorDevicePath}");
        return new MonitorState(monitorDevicePath);
    }

    public void Disable(string monitorDevicePath)
    {
        _callLog.Add($"monitor.Disable:{monitorDevicePath}");
    }

    public void Restore(MonitorState previousState)
    {
        _callLog.Add($"monitor.Restore:{previousState.MonitorDevicePath}");
    }
}

public sealed class FakeAudioController : IAudioController
{
    private readonly List<string> _callLog;
    private readonly string? _capturedDefaultDeviceId;

    public FakeAudioController(List<string> callLog, string? capturedDefaultDeviceId = "fake-normal-device")
    {
        _callLog = callLog;
        _capturedDefaultDeviceId = capturedDefaultDeviceId;
    }

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
    {
        _callLog.Add("audio.GetPlaybackDevices");
        return new List<AudioDeviceInfo> { new("fake-normal-device", "Fake Speakers") };
    }

    public AudioState CaptureState()
    {
        _callLog.Add("audio.CaptureState");
        var roleState = new AudioRoleState(_capturedDefaultDeviceId, null);
        return new AudioState(roleState, roleState, roleState);
    }

    public void SetDefault(string deviceId)
    {
        _callLog.Add($"audio.SetDefault:{deviceId}");
    }

    public void Restore(AudioState previousState)
    {
        _callLog.Add($"audio.Restore:{previousState.Multimedia.DeviceId}");
    }
}

public sealed class FakeAppController : IAppController
{
    private readonly List<string> _callLog;
    private readonly bool _isRunning;

    public FakeAppController(List<string> callLog, bool isRunning = false)
    {
        _callLog = callLog;
        _isRunning = isRunning;
    }

    public bool IsRunning(string companionAppPath)
    {
        _callLog.Add($"app.IsRunning:{companionAppPath}");
        return _isRunning;
    }

    public void LaunchOrFocus(string companionAppPath)
    {
        _callLog.Add($"app.LaunchOrFocus:{companionAppPath}");
    }

    public void MinimizeIfRunning(string companionAppPath)
    {
        _callLog.Add($"app.MinimizeIfRunning:{companionAppPath}");
    }
}
