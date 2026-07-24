namespace RigToggle.Core.Models;

/// <summary>
/// Captured per-path monitor state for a single active display target at snapshot-time,
/// used to restore the exact prior CCD topology (DISPLAY-02). Mirrors AudioRoleState's
/// per-unit-primitive shape, one record per active PathTargetInfo.
///
/// The five CCD-enum fields (PixelFormat, Rotation, Scaling, OutputTechnology,
/// ScanLineOrdering) are stored as their underlying numeric value (plain int), not the
/// WindowsDisplayAPI enum types themselves — RigToggle.Core targets net10.0 (not
/// -windows) and must carry zero WindowsDisplayAPI references; the Windows adapter casts
/// to/from these enums at the boundary. This also keeps the shape trivially
/// JSON-serializable and stable across a process restart (CORE-05).
/// </summary>
public sealed record MonitorPathSnapshot(
    string DevicePath,
    string FriendlyName,
    int PositionX,
    int PositionY,
    int ResolutionWidth,
    int ResolutionHeight,
    int PixelFormat,
    int Rotation,
    int Scaling,
    int OutputTechnology,
    ulong FrequencyInMillihertz,
    int ScanLineOrdering,
    bool IsPrimary);
