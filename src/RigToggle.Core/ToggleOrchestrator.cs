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
    /// Phase 17 (PANEL-02, T-17-01/T-17-02/T-17-03): claims exclusive access to the
    /// shared monitor-controller state for a manual monitor panel action.
    ///
    /// (a) This shares ONE flag — the same <see cref="_busy"/> field <see cref="RunGuarded"/>
    /// uses via the identical <c>Interlocked.CompareExchange</c> claim primitive — so a
    /// manual monitor mutation and a Rig/Normal toggle are mutually exclusive in both
    /// directions: acquiring this lease while a toggle is in flight throws, and a
    /// toggle requested while this lease is held also throws.
    ///
    /// (b) It exists because the manual monitor panel (Phase 17, PANEL-02) deliberately
    /// bypasses <see cref="ToggleService"/> yet mutates the same stateful
    /// <c>WindowsMonitorController</c> singleton, and <c>MonitorConfirmDialog.ShowDialog()</c>'s
    /// nested message loop can dispatch <c>WM_HOTKEY</c> mid-action, making a panel-action /
    /// hotkey-toggle race reachable without this guard.
    ///
    /// (c) It deliberately never calls <see cref="_markerStore"/>.Save/Clear — a manual
    /// monitor action is not a toggle, and writing the DISPLAY-13 crash marker here would
    /// make a crash during a manual monitor action surface a spurious "interrupted toggle"
    /// recovery dialog at next launch, naming a <see cref="ToggleMode"/> the user never
    /// selected. This asymmetry with <see cref="RunGuarded"/> is deliberate.
    ///
    /// (d) It is non-blocking and rejects immediately, never queues — same D-01 discipline
    /// as <see cref="RunGuarded"/>.
    /// </summary>
    public IDisposable BeginExclusiveMonitorAccess()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            throw new ToggleInProgressException(
                "A toggle is already in progress. Wait for it to finish, then try again.");
        }

        return new ExclusiveMonitorAccessLease(this);
    }

    /// <summary>
    /// T-17-02: releases <see cref="_busy"/> exactly once. Holds an <c>int _released</c>
    /// guard so a double-dispose (or a `using` plus a manual extra <c>Dispose()</c> call)
    /// can never clear a different holder's claim — the flag is only written when this
    /// lease transitions from "held" to "released" for the first time.
    /// </summary>
    private sealed class ExclusiveMonitorAccessLease : IDisposable
    {
        private readonly ToggleOrchestrator _owner;
        private int _released;

        public ExclusiveMonitorAccessLease(ToggleOrchestrator owner) => _owner = owner;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                Volatile.Write(ref _owner._busy, 0);
            }
        }
    }

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
            //
            // CR-01 (code review): wrapped in its own try/catch so a marker-cleanup
            // I/O failure (AV lock, sharing violation, permissions) can never prevent
            // the busy-flag release below — best-effort, matching
            // JsonToggleInProgressStore.TryLoad()'s own degrade-rather-than-throw
            // philosophy for this file.
            try
            {
                _markerStore.Clear();
            }
            catch
            {
                // Best-effort marker cleanup — must never block the busy-flag release.
            }

            // Must run even when ToggleService or the marker cleanup above throws —
            // otherwise a single failed toggle would permanently wedge the app in
            // "busy" and every future request (including a well-formed one) would be
            // rejected forever.
            Volatile.Write(ref _busy, 0);
        }
    }
}
