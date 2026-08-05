using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for the explicit ToggleMode value. Target path is supplied
/// by the caller (composition root, Plan 04) — expected to be
/// %LocalAppData%\RigToggle\mode.json. Save() uses the same temp-file +
/// File.Move(..., overwrite: true) atomic-write pattern as JsonSnapshotStore/
/// JsonSettingsStore, so an interrupted write cannot corrupt the prior good file.
/// TryLoad() degrades BOTH a malformed/hand-edited file (JsonException) AND an
/// interrupted read (IOException, e.g. antivirus lock, mid-write 0-byte file) to
/// null rather than throwing — matching JsonSettingsStore's more complete two-
/// exception coverage rather than JsonSnapshotStore's single-exception coverage
/// (T-16-01).
/// </summary>
public sealed class JsonModeStore : IModeStore
{
    private readonly string _path;

    public JsonModeStore(string path)
    {
        _path = path;
    }

    public bool Exists() => File.Exists(_path);

    public void Save(ToggleMode mode)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(mode));
        File.Move(tempPath, _path, overwrite: true);
    }

    public ToggleMode? TryLoad()
    {
        if (!Exists())
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ToggleMode>(File.ReadAllText(_path));
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
}
