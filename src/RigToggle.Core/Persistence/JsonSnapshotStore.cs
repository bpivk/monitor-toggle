using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for StateSnapshot. Target path is supplied by the caller
/// (composition root, Plan 05) — expected to be %LocalAppData%\RigToggle\state.json.
/// File presence itself is the mode indicator (D-14): Exists() == true means Rig mode.
/// Save() uses the same temp-file + File.Move(..., overwrite: true) atomic-write pattern
/// as JsonSettingsStore, so ToggleService's snapshot-before-mutate guarantee (D-08/CORE-03)
/// cannot be undermined by an interrupted write (T-02-CORRUPT).
/// </summary>
public sealed class JsonSnapshotStore : ISnapshotStore
{
    private readonly string _path;

    public JsonSnapshotStore(string path)
    {
        _path = path;
    }

    public bool Exists() => File.Exists(_path);

    public void Save(StateSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot));
        File.Move(tempPath, _path, overwrite: true);
    }

    public StateSnapshot? Load()
        => Exists() ? JsonSerializer.Deserialize<StateSnapshot>(File.ReadAllText(_path)) : null;

    public void Clear()
    {
        if (Exists())
        {
            File.Delete(_path);
        }
    }
}
