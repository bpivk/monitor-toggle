namespace RigToggle.Core.Models;

/// <summary>
/// Captured default audio playback device state at toggle-time, used to restore it later.
/// Holds one AudioRoleState snapshot per Windows audio role (eConsole, eMultimedia,
/// eCommunications) per D-02 / AUDIO-02, since Windows tracks a separate default render
/// endpoint for each role and restore must be exact across all three.
/// </summary>
public sealed record AudioState(AudioRoleState Console, AudioRoleState Multimedia, AudioRoleState Communications);
