using System.Threading;
using RigToggle.Core.Models;

namespace RigToggle.Core;

/// <summary>
/// Wraps ToggleService with a non-blocking, reentrancy-safe single-flight guard
/// (CORE-06). This is the single entry point every toggle trigger (today's GUI
/// button; tray menu / hotkey / CLI in Phases 8-10) must call through — ToggleService
/// itself remains untouched, a pure, already-unit-tested step sequencer with no
/// concurrency concerns (D-03).
///
/// D-01: the guard is a non-blocking busy-flag (Interlocked.CompareExchange), never a
/// blocking lock/Monitor.Enter and never a queue. A second call arriving while one is
/// in flight is rejected immediately — it never waits for the first to finish and is
/// never silently serialized behind it. A plain `lock` would block the second caller
/// until the first finishes and then let it proceed, which is exactly the rejected
/// "queue" behavior.
///
/// D-02: ONE shared flag guards BOTH ToggleToRigMode and ToggleToNormalMode — not
/// independent per-direction flags. Both directions mutate the same underlying
/// monitor/audio/app state, so a rig-mode toggle in flight must also reject a
/// normal-mode request (and vice versa), not just a same-direction repeat.
///
/// The flag is reset in a `finally` clause — not placed after the call inside `try` —
/// so it is always released even when ToggleService throws (including its own existing
/// preflight InvalidOperationExceptions for unconfigured settings or a missing
/// companion app path) — otherwise a single failed toggle would permanently wedge the
/// orchestrator in "busy" for the rest of the app's lifetime.
/// </summary>
public sealed class ToggleOrchestrator
{
    private readonly ToggleService _toggleService;
    private readonly Abstractions.IToggleInProgressStore _markerStore;

    // 0 = idle, 1 = a toggle is in flight. Interlocked.CompareExchange makes the
    // "is anyone in flight?" check and "claim it" set a single atomic operation —
    // this is what D-01 means by "non-blocking busy-flag": a second caller is
    // rejected immediately, it never waits and is never queued.
    private int _busy;

    public ToggleOrchestrator(ToggleService toggleService, Abstractions.IToggleInProgressStore markerStore)
    {
        _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
        _markerStore = markerStore ?? throw new ArgumentNullException(nameof(markerStore));
    }

    public ToggleResult ToggleToRigMode() => RunGuarded(ToggleMode.Rig, _toggleService.ToggleToRigMode);

    public ToggleResult ToggleToNormalMode() => RunGuarded(ToggleMode.Normal, _toggleService.ToggleToNormalMode);

    // D-04 pass-throughs — pure reads, no guard. Safe to call at any time, including
    // while a toggle is in flight (mirrors how MainForm.RefreshUi() already calls
    // IsInRigMode() immediately after every toggle today).
    public bool IsInRigMode() => _toggleService.IsInRigMode();
    public bool IsSettingsConfigured() => _toggleService.IsSettingsConfigured();

    // DISPLAY-11 pass-through — lets MainForm's toggle-trigger guards check whether
    // the mode is unambiguously known (mode file present and parsed successfully)
    // before branching on IsInRigMode()'s value at all.
    public bool IsModeKnown() => _toggleService.IsModeKnown();

    /// <summary>
    /// DISPLAY-13 crash-detection marker lifecycle: Save() at the start of every
    /// guarded toggle, Clear() in the finally on clean completion. If the marker is
    /// still present on the next launch, the previous toggle did not finish cleanly
    /// (a real process kill/crash) — this is distinct from and unrelated to
    /// <see cref="ToggleInProgressException"/> below, which is the existing
    /// in-memory, same-process reentrancy guard (CORE-06). Do not conflate the two:
    /// the marker survives a crash by design; the exception exists entirely in
    /// memory and can never survive one.
    /// </summary>
    private ToggleResult RunGuarded(ToggleMode targetMode, Func<ToggleResult> pipeline)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new ToggleInProgressException(
                "A toggle is already in progress. Wait for it to finish, then try again.");
        }

        try
        {
            _markerStore.Save(new ToggleInProgressMarker(targetMode, DateTimeOffset.UtcNow));
            return pipeline();
        }
        finally
        {
            // Clears on any managed exception path (including ToggleService's own
            // preflight InvalidOperationExceptions) — mirrors the busy-flag's own
            // finally discipline. Deliberately does NOT clear on a real process
            // kill/crash — that is exactly the condition DISPLAY-13 exists to detect
            // at next launch.
            _markerStore.Clear();

            // Must run even when ToggleService throws (its own preflight
            // InvalidOperationExceptions, or anything unexpected) — otherwise a
            // single failed toggle would permanently wedge the app in "busy" and
            // every future request (including a well-formed one) would be
            // rejected forever.
            Volatile.Write(ref _busy, 0);
        }
    }
}
