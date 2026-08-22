using System.Text.Json;
using System.Text.Json.Serialization;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.Core.Persistence;

/// <summary>
/// Atomic JSON persistence for the update-applied-but-unconfirmed marker (UPDATE-05,
/// D-09). Mirrors JsonToggleInProgressStore member-for-member (Save/TryLoad/Clear,
/// private Exists, temp-file-then-File.Move(overwrite: true) atomic write, TryLoad
/// degrading every plausible I/O failure to null, Clear as a best-effort
/// no-op-if-absent delete) but is deliberately its own type, not an extension of
/// the toggle-in-progress store -- see IUpdateAppliedMarkerStore's doc comment.
///
/// The Stage enum is serialized as a string (JsonStringEnumConverter), not the
/// default ordinal -- this marker is written by one shipped version's helper
/// process and read by the NEXT version's startup code, so the on-disk value must
/// be self-describing and survive a future UpdateMarkerStage member reorder rather
/// than silently reinterpreting a bare ordinal.
/// </summary>
public sealed class JsonUpdateAppliedMarkerStore : IUpdateAppliedMarkerStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public JsonUpdateAppliedMarkerStore(string path)
    {
        _path = path;
    }

    private bool Exists() => File.Exists(_path);

    public void Save(UpdateAppliedMarker marker)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(marker, Options));
        File.Move(tempPath, _path, overwrite: true);
    }

    public UpdateAppliedMarker? TryLoad()
    {
        if (!Exists())
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UpdateAppliedMarker>(File.ReadAllText(_path), Options);
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
            // Sibling of IOException, not a subtype -- must be caught separately
            // (mirrors JsonToggleInProgressStore's own WR-01 correction), since a
            // restrictive-ACL or AV-quarantine failure here must degrade to null,
            // not crash whatever startup path called TryLoad().
            return null;
        }
    }

    public void Clear()
    {
        // A File.Delete failure (AV lock, sharing violation, permissions) degrades
        // to a no-op rather than propagating, matching TryLoad()'s own graceful-
        // degradation contract.
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
