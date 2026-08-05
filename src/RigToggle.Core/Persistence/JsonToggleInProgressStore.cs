using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for the toggle-in-progress crash marker (DISPLAY-13).
/// Target path is supplied by the caller (composition root, Plan 04) — expected to
/// be %LocalAppData%\RigToggle\toggle-in-progress.json. Save() uses the same
/// temp-file + File.Move(..., overwrite: true) atomic-write pattern as
/// JsonSnapshotStore/JsonModeStore. TryLoad() degrades both a malformed/hand-edited
/// file (JsonException) and an interrupted read (IOException) to null rather than
/// throwing. Clear() deletes the file only when it exists, mirroring
/// JsonSnapshotStore.Clear().
/// </summary>
public sealed class JsonToggleInProgressStore : IToggleInProgressStore
{
    private readonly string _path;

    public JsonToggleInProgressStore(string path)
    {
        _path = path;
    }

    private bool Exists() => File.Exists(_path);

    public void Save(ToggleInProgressMarker marker)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(marker));
        File.Move(tempPath, _path, overwrite: true);
    }

    public ToggleInProgressMarker? TryLoad()
    {
        if (!Exists())
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ToggleInProgressMarker>(File.ReadAllText(_path));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Clear()
    {
        if (Exists())
        {
            File.Delete(_path);
        }
    }
}
