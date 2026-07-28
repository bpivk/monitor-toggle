using System.Text.Json;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for AppSettings. Target path is supplied by the caller
/// (composition root, Plan 05) — expected to be
/// %LocalAppData%\RigToggle\settings.json (02-RESEARCH.md Pattern 3 / Open Question #1,
/// resolved in favor of LocalApplicationData over roaming AppData).
/// Load() on a missing file returns a fresh, all-null AppSettings ("never configured"),
/// never throws. Save() writes to a ".tmp" sibling then File.Move(..., overwrite: true)
/// so an interrupted write cannot corrupt the prior good file (SETTINGS-04, T-02-CORRUPT).
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public JsonSettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();

            // D-08: silent v1.0->v1.1 migration. A genuine legacy file has no plural
            // fields at all (they deserialize to null); an already-migrated v1.1 file
            // has a non-empty MonitorsToDisable, which must NOT be overwritten. Lives
            // inside this try block on purpose — a corrupted legacy file must still
            // hit the JsonException/IOException degrade-to-fresh-AppSettings() path
            // below, not a second/divergent failure mode.
            if (!string.IsNullOrEmpty(loaded.MonitorDevicePath)
                && (loaded.MonitorsToDisable is null || loaded.MonitorsToDisable.Count == 0))
            {
                loaded.MonitorsToDisable = new List<string> { loaded.MonitorDevicePath };
            }

            return loaded;
        }
        catch (JsonException)
        {
            // Malformed/truncated/hand-edited settings.json (SETTINGS-04, T-02-CORRUPT) —
            // degrade to a fresh "never configured" AppSettings rather than crash the app.
            return new AppSettings();
        }
        catch (IOException)
        {
            // Read interrupted (e.g. antivirus lock, 0-byte file mid-write) — same
            // degrade-to-fresh-settings behavior as the malformed-JSON case.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
        File.Move(tempPath, _path, overwrite: true);
    }
}
