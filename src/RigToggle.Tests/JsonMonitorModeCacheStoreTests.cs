using RigToggle.Core.Models;
using RigToggle.Core.Persistence;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves JsonMonitorModeCacheStore's create-if-missing load, round-trip save/load,
/// atomic-write (no leftover .tmp), and corrupt-file-degrades-to-empty behaviors --
/// debug session monitor-pos-no-persist. Mirrors JsonStoreTests' exact structure
/// (own unique temp subdirectory per test, cleaned up on completion). This is also
/// the closest thing to a direct regression test for the reported bug: two
/// independently-constructed store instances pointed at the same path, the second
/// reading what the first wrote, is exactly what happens across the two OS
/// processes (pid=13292 disabling, pid=22056 re-enabling) in the original repro.
/// </summary>
public class JsonMonitorModeCacheStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JsonMonitorModeCacheStoreTests()
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

    private static CachedMonitorMode SampleEntry(string devicePath = "DELA0BC", int x = -1920, int y = 0) =>
        new(
            DevicePath: devicePath,
            AdapterIdLowPart: 123456,
            AdapterIdHighPart: 7,
            SourceId: 1,
            PositionX: x,
            PositionY: y,
            ResolutionWidth: 1920,
            ResolutionHeight: 1200,
            PixelFormat: 2,
            LastCachedUtc: new DateTimeOffset(2026, 9, 15, 18, 43, 38, TimeSpan.Zero));

    [Fact]
    public void Load_OnMissingFile_ReturnsEmpty()
    {
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        var store = new JsonMonitorModeCacheStore(path);

        var loaded = store.Load();

        Assert.Empty(loaded);
    }

    [Fact]
    public void Save_ThenLoad_FromANewStoreInstance_RoundTripsAllFields()
    {
        // "A new store instance" mirrors the actual repro: a second OS process
        // constructs its own store object pointed at the same file -- there is no
        // shared in-memory state between Save and Load here, only the file itself.
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        var writer = new JsonMonitorModeCacheStore(path);
        var original = SampleEntry();

        writer.Save(new[] { original });

        var reader = new JsonMonitorModeCacheStore(path);
        var loaded = reader.Load();

        var entry = Assert.Single(loaded);
        Assert.Equal(original.DevicePath, entry.DevicePath);
        Assert.Equal(original.AdapterIdLowPart, entry.AdapterIdLowPart);
        Assert.Equal(original.AdapterIdHighPart, entry.AdapterIdHighPart);
        Assert.Equal(original.SourceId, entry.SourceId);
        Assert.Equal(original.PositionX, entry.PositionX);
        Assert.Equal(original.PositionY, entry.PositionY);
        Assert.Equal(original.ResolutionWidth, entry.ResolutionWidth);
        Assert.Equal(original.ResolutionHeight, entry.ResolutionHeight);
        Assert.Equal(original.PixelFormat, entry.PixelFormat);
        Assert.Equal(original.LastCachedUtc, entry.LastCachedUtc);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsNegativeAndZeroPositions()
    {
        // Boundary neighbors around the reported defect's exact shape: a monitor to
        // the LEFT of the primary has a negative X (the DELL in the original repro,
        // arrangement 2-1-3, sits left of the primary), and (0,0) is the origin
        // PromoteToOriginIfNeeded treats specially -- both must survive the round
        // trip unchanged, not get clamped/defaulted to 0.
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        var store = new JsonMonitorModeCacheStore(path);
        var entries = new[]
        {
            SampleEntry("DEV-NEGATIVE", x: -1920, y: -200),
            SampleEntry("DEV-ORIGIN", x: 0, y: 0),
        };

        store.Save(entries);
        var loaded = store.Load();

        Assert.Equal(-1920, loaded.Single(e => e.DevicePath == "DEV-NEGATIVE").PositionX);
        Assert.Equal(-200, loaded.Single(e => e.DevicePath == "DEV-NEGATIVE").PositionY);
        Assert.Equal(0, loaded.Single(e => e.DevicePath == "DEV-ORIGIN").PositionX);
        Assert.Equal(0, loaded.Single(e => e.DevicePath == "DEV-ORIGIN").PositionY);
    }

    [Fact]
    public void Save_OverExistingFile_LeavesNoTempFileAndUpdatesContent()
    {
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        var store = new JsonMonitorModeCacheStore(path);
        store.Save(new[] { SampleEntry("FIRST") });

        store.Save(new[] { SampleEntry("SECOND") });

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("SECOND", Assert.Single(store.Load()).DevicePath);
    }

    [Fact]
    public void Load_MalformedJson_DegradesToEmpty_NeverThrows()
    {
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        File.WriteAllText(path, "{ not valid json at all");
        var store = new JsonMonitorModeCacheStore(path);

        var loaded = store.Load();

        Assert.Empty(loaded);
    }

    [Fact]
    public void Save_WithEmptyList_RoundTripsToEmpty()
    {
        var path = Path.Combine(_tempDir, "monitor-mode-cache.json");
        var store = new JsonMonitorModeCacheStore(path);

        store.Save(Array.Empty<CachedMonitorMode>());
        var loaded = store.Load();

        Assert.Empty(loaded);
    }
}
