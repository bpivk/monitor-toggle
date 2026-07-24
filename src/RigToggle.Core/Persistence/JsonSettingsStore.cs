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

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
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
