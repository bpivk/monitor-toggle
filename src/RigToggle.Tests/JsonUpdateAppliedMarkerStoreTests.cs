using RigToggle.Core.Models;
using RigToggle.Core.Persistence;
using Xunit;

namespace RigToggle.Tests;

/// <summary>
/// Proves JsonUpdateAppliedMarkerStore's round-trip, corruption-tolerance, and
/// string-enum persistence (UPDATE-05, D-09) -- the property that protects the
/// cross-version on-disk format from a future UpdateMarkerStage member reorder.
/// Each test uses its own unique temp subdirectory, cleaned up on completion,
/// mirroring JsonStoreTests' fixture pattern.
/// </summary>
public class JsonUpdateAppliedMarkerStoreTests : IDisposable
{
    private readonly string _tempDir;

    public JsonUpdateAppliedMarkerStoreTests()
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
    public void Save_ThenTryLoad_RoundTripsAllFourMembers()
    {
        var path = Path.Combine(_tempDir, "update-applied.json");
        var store = new JsonUpdateAppliedMarkerStore(path);
        var original = new UpdateAppliedMarker("2.3", "2.2", DateTimeOffset.Parse("2026-08-22T12:00:00Z"), UpdateMarkerStage.FirstLaunchAttempted);

        store.Save(original);
        var loaded = store.TryLoad();

        Assert.NotNull(loaded);
        Assert.Equal(original.NewVersion, loaded!.NewVersion);
        Assert.Equal(original.PreviousVersion, loaded.PreviousVersion);
        Assert.Equal(original.AppliedAtUtc, loaded.AppliedAtUtc);
        Assert.Equal(original.Stage, loaded.Stage);
    }

    [Fact]
    public void TryLoad_OnMissingFile_ReturnsNull()
    {
        var path = Path.Combine(_tempDir, "update-applied.json");
        var store = new JsonUpdateAppliedMarkerStore(path);

        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void TryLoad_OverMalformedJson_ReturnsNullWithoutThrowing()
    {
        Directory.CreateDirectory(_tempDir);
        var path = Path.Combine(_tempDir, "update-applied.json");
        File.WriteAllText(path, "{ this is not valid json ][");
        var store = new JsonUpdateAppliedMarkerStore(path);

        var result = store.TryLoad();

        Assert.Null(result);
    }

    [Fact]
    public void Clear_OnMissingFile_IsSilentNoOp()
    {
        var path = Path.Combine(_tempDir, "update-applied.json");
        var store = new JsonUpdateAppliedMarkerStore(path);

        var exception = Record.Exception(() => store.Clear());

        Assert.Null(exception);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenItDoesNotExist()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "subdir", "update-applied.json");
        var store = new JsonUpdateAppliedMarkerStore(nestedPath);

        store.Save(new UpdateAppliedMarker("2.3", "2.2", DateTimeOffset.UtcNow, UpdateMarkerStage.Applied));

        Assert.True(File.Exists(nestedPath));
    }

    /// <summary>
    /// The property protecting the cross-version format from a future
    /// UpdateMarkerStage member reorder: the persisted Stage is a quoted string,
    /// never a bare ordinal that a reorder would silently reinterpret.
    /// </summary>
    [Fact]
    public void Save_PersistsStage_AsQuotedStringNotOrdinal()
    {
        var path = Path.Combine(_tempDir, "update-applied.json");
        var store = new JsonUpdateAppliedMarkerStore(path);

        store.Save(new UpdateAppliedMarker("2.3", "2.2", DateTimeOffset.UtcNow, UpdateMarkerStage.Reverted));

        string rawJson = File.ReadAllText(path);
        Assert.Contains("\"Stage\":\"Reverted\"", rawJson);
        Assert.DoesNotContain("\"Stage\":2", rawJson);
    }
}
