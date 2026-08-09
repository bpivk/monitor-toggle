using RigToggle.Core;
using RigToggle.Core.Models;
using RigToggle.Core.Persistence;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves JsonSettingsStore's create-if-missing load, round-trip
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
        Assert.Null(settings.HotkeyModifiers);
        Assert.Null(settings.HotkeyKey);
    }

    [Fact]
    public void SettingsStore_Save_ThenLoad_RoundTripsHotkeyFields()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        var original = new AppSettings
        {
            HotkeyModifiers = HotkeyCombo.ModControl | HotkeyCombo.ModAlt,
            HotkeyKey = 0x52,
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.HotkeyModifiers, loaded.HotkeyModifiers);
        Assert.Equal(original.HotkeyKey, loaded.HotkeyKey);
    }

    [Fact]
    public void SettingsStore_Save_WithNullHotkeyFields_LoadsBackAsNull()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        var original = new AppSettings
        {
            HotkeyModifiers = null,
            HotkeyKey = null,
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Null(loaded.HotkeyModifiers);
        Assert.Null(loaded.HotkeyKey);
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
    public void SettingsStore_Save_ThenLoad_RoundTripsTrayBehaviorFlags()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        var original = new AppSettings
        {
            CloseMinimizesToTray = true,
            MinimizeToTray = true,
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.True(loaded.CloseMinimizesToTray);
        Assert.True(loaded.MinimizeToTray);
    }

    [Fact]
    public void SettingsStore_Save_WithDefaultTrayBehaviorFlags_LoadsBackFalse()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new JsonSettingsStore(path);
        var original = new AppSettings();

        store.Save(original);
        var loaded = store.Load();

        Assert.False(loaded.CloseMinimizesToTray);
        Assert.False(loaded.MinimizeToTray);
    }

    [Fact]
    public void SettingsStore_Load_LegacyFileWithoutTrayFlags_DefaultsBothFalse()
    {
        // Genuine pre-Phase-11-shape JSON: only pre-existing keys, NO tray-flag keys at
        // all (proves this is a real legacy file, not a hand-seeded false value) — the
        // executable proof of the D-02/D-05 upgrade default.
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, """
            {
              "MonitorsToDisable": ["\\\\?\\DISPLAY#PRIMARY"],
              "MonitorsToEnable": [],
              "CompanionAppPath": "C:\\Apps\\MozaCompanion.exe"
            }
            """);
        var store = new JsonSettingsStore(path);

        var loaded = store.Load();

        Assert.False(loaded.CloseMinimizesToTray);
        Assert.False(loaded.MinimizeToTray);
    }

    [Fact]
    public void SettingsStore_Load_MigratesLegacyMonitorDevicePath_IntoDisableSet()
    {
        // Genuine v1.0-shape JSON: only legacy singular fields, NO plural fields at all
        // (proves this is a real pre-Phase-6 file, not a hand-crafted null-plural stand-in).
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, """
            {
              "MonitorDevicePath": "\\\\?\\DISPLAY#PRIMARY",
              "MonitorFriendlyName": "Primary Monitor",
              "NormalAudioDeviceId": "normal-id",
              "NormalAudioDeviceName": "Headset",
              "RigAudioDeviceId": "rig-id",
              "RigAudioDeviceName": "Rig Speakers",
              "CompanionAppPath": "C:\\Apps\\MozaCompanion.exe"
            }
            """);
        var store = new JsonSettingsStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded.MonitorsToDisable);
        Assert.Single(loaded.MonitorsToDisable!);
        Assert.Equal(@"\\?\DISPLAY#PRIMARY", loaded.MonitorsToDisable![0]);
        Assert.True(loaded.MonitorsToEnable is null || loaded.MonitorsToEnable.Count == 0);
    }

    [Fact]
    public void SettingsStore_Load_DoesNotRemigrate_WhenDisableSetAlreadyPopulated()
    {
        // v1.1-shape file: legacy MonitorDevicePath still present (never scrubbed) AND
        // a non-empty MonitorsToDisable already persisted — migration must not overwrite it.
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, """
            {
              "MonitorDevicePath": "\\\\?\\DISPLAY#PRIMARY",
              "MonitorFriendlyName": "Primary Monitor",
              "MonitorsToDisable": ["\\\\?\\DISPLAY#RIG"],
              "MonitorsToEnable": []
            }
            """);
        var store = new JsonSettingsStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded.MonitorsToDisable);
        Assert.Single(loaded.MonitorsToDisable!);
        Assert.Equal(@"\\?\DISPLAY#RIG", loaded.MonitorsToDisable![0]);
    }

    [Fact]
    public void SettingsStore_Load_DoesNotRemigrate_WhenDisableSetDeliberatelyEmptied()
    {
        // CR-01 (code review): a v1.1 user who has moved to an enable-only configuration
        // saves MonitorsToDisable as an explicit empty list. MonitorDevicePath is never
        // scrubbed post-migration (by design), so the migration guard must key off
        // "MonitorsToDisable is null" only — an empty-but-non-null list must NOT be
        // treated as "never migrated" and re-populated from the stale legacy field.
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(path, """
            {
              "MonitorDevicePath": "\\\\?\\DISPLAY#PRIMARY",
              "MonitorFriendlyName": "Primary Monitor",
              "MonitorsToDisable": [],
              "MonitorsToEnable": ["\\\\?\\DISPLAY#RIG"]
            }
            """);
        var store = new JsonSettingsStore(path);

        var loaded = store.Load();

        Assert.NotNull(loaded.MonitorsToDisable);
        Assert.Empty(loaded.MonitorsToDisable!);
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
}
