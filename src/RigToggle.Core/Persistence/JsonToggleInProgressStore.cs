using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for the toggle-in-progress crash marker (DISPLAY-13).
/// Target path is supplied by the caller (composition root, Plan 04) — expected to
/// be %LocalAppData%\RigToggle\toggle-in-progress.json. Save() uses the same
/// temp-file + File.Move(..., overwrite: true) atomic-write pattern as
/// JsonModeStore. TryLoad() degrades both a malformed/hand-edited
/// file (JsonException) and an interrupted read (IOException) to null rather than
/// throwing. Clear() deletes the file only when it exists, a no-op when it is
/// already absent.
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

    public void Clear()
    {
        // CR-01 (code review): File.Delete can throw (AV lock, sharing violation,
        // permissions) — degrade to a no-op rather than propagate, matching
        // TryLoad()'s own graceful-degradation contract. The caller
        // (ToggleOrchestrator.RunGuarded) also wraps this call defensively, but this
        // guard holds even if some future caller doesn't.
        try
        {
            if (Exists())
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
