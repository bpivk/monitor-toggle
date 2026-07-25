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
    private readonly bool _throwOnDisable;
    private readonly bool _mutatesBeforeThrowingOnDisable;
    private bool _disableWasCalled;

    public FakeMonitorController(
        List<string> callLog,
        bool throwOnDisable = false,
        bool mutatesBeforeThrowingOnDisable = false)
    {
        _callLog = callLog;
        _throwOnDisable = throwOnDisable;
        _mutatesBeforeThrowingOnDisable = mutatesBeforeThrowingOnDisable;
    }

    public IReadOnlyList<MonitorInfo> GetActiveMonitors()
    {
        _callLog.Add("monitor.GetActiveMonitors");
        return new List<MonitorInfo> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", true) };
    }

    public MonitorState CaptureState()
    {
        _callLog.Add("monitor.CaptureState");

        // CR-01 test support: when _mutatesBeforeThrowingOnDisable is set, simulate a
        // partial CCD mutation that happened before Disable's own verify-and-throw
        // fired (as opposed to _throwOnDisable's pre-mutation guard case, which never
        // touches live state at all) — the second CaptureState() call (post-Disable)
        // must report a genuinely different position, proving ToggleService.
        // MonitorStateUnchanged correctly detects "something changed" and keeps the
        // snapshot rather than clearing it.
        int positionX = _mutatesBeforeThrowingOnDisable && _disableWasCalled ? 999 : 0;

        return new MonitorState(
            new List<MonitorPathSnapshot> { new("\\\\?\\DISPLAY#FAKE", "Fake Monitor", positionX, 0, 1920, 1080, 0, 0, 0, 0, 60000UL, 0, true) },
            "\\\\?\\DISPLAY#FAKE");
    }

    public void Disable(string monitorDevicePath)
    {
        _callLog.Add($"monitor.Disable:{monitorDevicePath}");
        _disableWasCalled = true;

        if (_throwOnDisable || _mutatesBeforeThrowingOnDisable)
        {
            // D-04: simulates a CCD/topology failure, proving ToggleToRigMode records
            // Monitor=Failed and marks Audio/App NotAttempted without calling them.
            throw new InvalidOperationException("Fake monitor disable failure (simulated CCD/topology failure).");
        }
    }

    public void Restore(MonitorState previousState)
    {
        _callLog.Add($"monitor.Restore:{previousState.TargetDevicePath}");
    }
}

public sealed class FakeAudioController : IAudioController
{
    private readonly List<string> _callLog;
    private readonly string? _capturedDefaultDeviceId;
    private readonly bool _throwOnRestore;

    public FakeAudioController(
        List<string> callLog,
        string? capturedDefaultDeviceId = "fake-normal-device",
        bool throwOnRestore = false)
    {
        _callLog = callLog;
        _capturedDefaultDeviceId = capturedDefaultDeviceId;
        _throwOnRestore = throwOnRestore;
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

        if (_throwOnRestore)
        {
            // Gap-closure 03-04: simulates a role whose captured device is gone,
            // surfacing as an uncaught InvalidOperationException from Restore — proves
            // ToggleToNormalMode still minimizes + clears even when this throws.
            throw new InvalidOperationException("Fake audio restore failure (simulated stale/missing device).");
        }
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
