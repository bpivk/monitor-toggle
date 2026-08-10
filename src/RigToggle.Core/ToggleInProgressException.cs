namespace RigToggle.Core;

/// <summary>
/// Thrown by ToggleOrchestrator when a toggle is requested while another is already
/// in flight (CORE-06). Subclasses InvalidOperationException (not a bare Exception)
/// so it is caught by MainForm.ToggleSwitch_ActionRequested's existing `catch (Exception ex)` block
/// with zero UI changes — the same way ToggleService's own preflight guards
/// (unconfigured settings, missing companion app path) are already surfaced today
/// (D-05).
/// </summary>
public sealed class ToggleInProgressException : InvalidOperationException
{
    public ToggleInProgressException() { }
    public ToggleInProgressException(string message) : base(message) { }
    public ToggleInProgressException(string message, Exception innerException) : base(message, innerException) { }
}
