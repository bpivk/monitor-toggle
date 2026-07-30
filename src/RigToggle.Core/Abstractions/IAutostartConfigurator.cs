namespace RigToggle.Core.Abstractions;

/// <summary>
/// HKCU "start with Windows" registration contract (TRAY-02). Implemented by
/// RigToggle.Windows.WindowsAutostartConfigurator this phase. The current-user Run
/// registry key's existence is the single source of truth for whether autostart is
/// enabled -- there is deliberately no mirrored AppSettings boolean to drift out of
/// sync with the actual registry state.
/// </summary>
public interface IAutostartConfigurator
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
