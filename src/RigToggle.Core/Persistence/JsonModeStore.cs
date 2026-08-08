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
            var mode = JsonSerializer.Deserialize<ToggleMode>(File.ReadAllText(_path));

            // WR-02 (code review): System.Text.Json deserializes any JSON integer into
            // an enum-typed property without validating it against defined members —
            // e.g. a hand-edited/corrupted "2" would silently become an
            // out-of-range ToggleMode instead of failing. Explicitly reject anything
            // that isn't a real ToggleMode member so corruption fails loudly (D-06/
            // D-07) rather than defaulting to whatever IsInRigMode()'s equality check
            // happens to evaluate an undefined value as.
            if (!Enum.IsDefined(typeof(ToggleMode), mode))
            {
                return null;
            }

            return mode;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // WR-01 (code review): a permissions problem (restrictive ACLs, AV
            // quarantine) is a sibling of IOException, not a subtype — must be caught
            // separately so StartupRecoveryChecker.Run() (deliberately unguarded)
            // degrades to its own dialog instead of crashing with an unhandled
            // exception at the first line of Main()'s recovery check.
            return null;
        }
    }
}
