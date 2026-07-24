using RigToggle.Core.Models;
using RigToggle.Core.Persistence;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves JsonSettingsStore/JsonSnapshotStore's create-if-missing load, round-trip
/// save/load, and atomic-write (no leftover .tmp) behaviors (SETTINGS-04, T-02-CORRUPT).
/// Each test uses its own unique temp subdirectory, cleaned up on completion.
/// </summary>
public class JsonStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JsonStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RigToggleTests_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void SettingsStore_Load_OnMissingFile_ReturnsAllNullAppSettings()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);

        var settings = store.Load();

        Assert.Null(settings.MonitorDevicePath);
        Assert.Null(settings.MonitorFriendlyName);
        Assert.Null(settings.NormalAudioDeviceId);
        Assert.Null(settings.NormalAudioDeviceName);
        Assert.Null(settings.RigAudioDeviceId);
        Assert.Null(settings.RigAudioDeviceName);
        Assert.Null(settings.CompanionAppPath);
    }

    [Fact]
    public void SettingsStore_Save_ThenLoad_RoundTripsAllFields()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        var original = new AppSettings
        {
            MonitorDevicePath = "\\\\?\\DISPLAY#PRIMARY",
            MonitorFriendlyName = "Primary Monitor",
            NormalAudioDeviceId = "normal-id",
            NormalAudioDeviceName = "Headset",
            RigAudioDeviceId = "rig-id",
            RigAudioDeviceName = "Rig Speakers",
            CompanionAppPath = @"C:\Apps\MozaCompanion.exe",
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.MonitorDevicePath, loaded.MonitorDevicePath);
        Assert.Equal(original.MonitorFriendlyName, loaded.MonitorFriendlyName);
        Assert.Equal(original.NormalAudioDeviceId, loaded.NormalAudioDeviceId);
        Assert.Equal(original.NormalAudioDeviceName, loaded.NormalAudioDeviceName);
        Assert.Equal(original.RigAudioDeviceId, loaded.RigAudioDeviceId);
        Assert.Equal(original.RigAudioDeviceName, loaded.RigAudioDeviceName);
        Assert.Equal(original.CompanionAppPath, loaded.CompanionAppPath);
    }

    [Fact]
    public void SettingsStore_Save_OverExistingFile_LeavesNoTempFileAndUpdatesContent()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        store.Save(new AppSettings { CompanionAppPath = "first.exe" });

        store.Save(new AppSettings { CompanionAppPath = "second.exe" });

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("second.exe", store.Load().CompanionAppPath);
    }

    [Fact]
    public void SnapshotStore_Exists_IsFalseBeforeSave_TrueAfterSave()
    {
        var path = Path.Combine(_tempDir, "state.json");
        var store = new JsonSnapshotStore(path);

        Assert.False(store.Exists());

        store.Save(new StateSnapshot(new MonitorState("\\\\?\\DISPLAY#PRIMARY"), new AudioState(new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device"))));

        Assert.True(store.Exists());
    }

    [Fact]
    public void SnapshotStore_Clear_DeletesFile_SoExistsReturnsFalseAgain()
    {
        var path = Path.Combine(_tempDir, "state.json");
        var store = new JsonSnapshotStore(path);
        store.Save(new StateSnapshot(new MonitorState("\\\\?\\DISPLAY#PRIMARY"), new AudioState(new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device"))));

        store.Clear();

        Assert.False(store.Exists());
    }

    [Fact]
    public void SnapshotStore_Load_ReturnsNullWhenAbsent_AndSavedSnapshotWhenPresent()
    {
        var path = Path.Combine(_tempDir, "state.json");
        var store = new JsonSnapshotStore(path);

        Assert.Null(store.Load());

        var snapshot = new StateSnapshot(new MonitorState("\\\\?\\DISPLAY#PRIMARY"), new AudioState(new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device"), new AudioRoleState("device-id", "Fake Device")));
        store.Save(snapshot);

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(snapshot.Monitor.MonitorDevicePath, loaded!.Monitor.MonitorDevicePath);
        Assert.Equal(snapshot.Audio.Console.DeviceId, loaded.Audio.Console.DeviceId);
        Assert.Equal(snapshot.Audio.Multimedia.DeviceId, loaded.Audio.Multimedia.DeviceId);
        Assert.Equal(snapshot.Audio.Communications.DeviceId, loaded.Audio.Communications.DeviceId);
    }

    [Fact]
    public void SnapshotStore_Load_OnMalformedFile_ReturnsNullInsteadOfThrowing()
    {
        // Simulates a corrupted/truncated or otherwise malformed state.json (e.g. an
        // interrupted write, or a genuinely stale/incompatible shape from a prior
        // schema) — Load must treat this as "no snapshot" = normal mode rather than
        // crashing on startup (Open Question 1, T-03-01).
        var path = Path.Combine(_tempDir, "state.json");
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(path, "{not valid json");
        var store = new JsonSnapshotStore(path);

        var loaded = store.Load();

        Assert.Null(loaded);
    }
}
