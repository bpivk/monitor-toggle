namespace RigToggle.Windows;

/// <summary>
/// Public cross-assembly façade over the internal registered-window-message P/Invoke
/// pair in NativeMethods (INSTANCE-01/INSTANCE-02), mirroring GlobalHotkey.cs's exact
/// public-facade-over-internal-NativeMethods convention: NativeMethods is `internal` to
/// RigToggle.Windows with no InternalsVisibleTo grant to RigToggle.App, so the App layer
/// reaches this signal only through this wrapper, never NativeMethods directly.
///
/// The message name is embedded with this application's fixed instance GUID because a
/// registered-window-message id is a machine-wide reservation keyed off the exact string
/// passed in — embedding the GUID removes any chance of accidentally colliding with an
/// unrelated application that happens to register a similarly-generic name.
///
/// A zero <see cref="MessageId"/> means registration failed, and callers must never
/// compare a received message against an unguarded zero id, because WM_NULL is also
/// zero — treating every WM_NULL as an activation signal would restore the window on
/// essentially every idle message pump tick.
///
/// This signal is deliberately payload-free: <see cref="BroadcastActivation"/> always
/// posts with wParam and lParam both zero, and callers must never start passing data
/// through them. The whole effect on receipt is "make this app's own window visible and
/// focused" — nothing is delivered, read, or trusted from the broadcast itself.
/// </summary>
public static class ActivationSignal
{
    /// <summary>
    /// Machine-wide-unique message name, scoped by this application's fixed instance
    /// GUID (see the class doc comment for why the GUID is embedded here).
    /// </summary>
    public const string MessageName = "RigToggle_ShowExisting_8f3a1c42-7b5e-4d19-9a06-2e5c1f8b7d34";

    private static uint? _messageId;

    /// <summary>
    /// The registered message id for <see cref="MessageName"/>, resolved and cached
    /// (including a zero/failed result — a failed registration does not retry on every
    /// access) on first use.
    /// </summary>
    public static uint MessageId => _messageId ??= NativeMethods.RegisterWindowMessage(MessageName);

    /// <summary>
    /// Broadcasts the activation signal to every top-level window on the desktop.
    /// Returns immediately without posting when <see cref="MessageId"/> is zero
    /// (registration failed) — there is nothing meaningful to broadcast in that case.
    /// </summary>
    public static void BroadcastActivation()
    {
        uint messageId = MessageId;
        if (messageId == 0)
        {
            return;
        }

        NativeMethods.PostMessage((IntPtr)NativeMethods.HWND_BROADCAST, messageId, IntPtr.Zero, IntPtr.Zero);
    }
}
