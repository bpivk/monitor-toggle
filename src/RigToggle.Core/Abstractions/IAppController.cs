namespace RigToggle.Core.Abstractions;

/// <summary>
/// Companion-app running-detection and launch/focus/minimize contract. Implemented
/// by RigToggle.Windows.WindowsAppController. IsRunning is real starting Phase 2
/// (D-07); LaunchOrFocus/MinimizeIfRunning are no-op stubs until Phase 3
/// (02-RESEARCH.md Pattern 1).
/// </summary>
public interface IAppController
{
    bool IsRunning(string companionAppPath);
    void LaunchOrFocus(string companionAppPath);
    void MinimizeIfRunning(string companionAppPath);
}
