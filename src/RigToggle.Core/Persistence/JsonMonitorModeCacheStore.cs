using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for the monitor mode/position cache (debug session
/// monitor-pos-no-persist). Target path is supplied by the caller (composition root,
/// Program.cs) -- expected to be %LocalAppData%\RigToggle\monitor-mode-cache.json,
/// a sibling of settings.json/mode.json/toggle-in-progress.json. Save() uses the
/// same temp-file + File.Move(..., overwrite: true) atomic-write pattern as
/// JsonModeStore/JsonSettingsStore/JsonToggleInProgressStore, so an interrupted
/// write cannot corrupt the prior good file. Load() degrades a
/// malformed/hand-edited file (JsonException), an interrupted read (IOException),
/// and a permissions failure (UnauthorizedAccessException) all to an empty list
/// rather than throwing, matching JsonSettingsStore.Load()'s "never throw" contract
/// -- a corrupt or unreadable cache file must fall back to today's "no cache entry"
/// behavior (driver picks the position), never block the monitor toggle.
/// </summary>
public sealed class JsonMonitorModeCacheStore : IMonitorModeCacheStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonMonitorModeCacheStore(string path)
    {
        _path = path;
    }

    public IReadOnlyList<CachedMonitorMode> Load()
    {
        if (!File.Exists(_path))
        {
            return Array.Empty<CachedMonitorMode>();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<CachedMonitorMode>>(json, Options)
                ?? new List<CachedMonitorMode>();
        }
        catch (JsonException)
        {
            return Array.Empty<CachedMonitorMode>();
        }
        catch (IOException)
        {
            return Array.Empty<CachedMonitorMode>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<CachedMonitorMode>();
        }
    }

    public void Save(IReadOnlyList<CachedMonitorMode> modes)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(modes, Options));
        File.Move(tempPath, _path, overwrite: true);
    }
}
