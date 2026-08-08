---
phase: 17-manual-monitor-panel-shared-safety-guard
reviewed: 2026-08-08T19:08:29Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - src/RigToggle.App/MainForm.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/MonitorIdentifyOverlay.cs
  - src/RigToggle.App/MonitorPanelForm.cs
  - src/RigToggle.App/MonitorPanelForm.Designer.cs
  - src/RigToggle.App/Program.cs
  - src/RigToggle.Core/ToggleOrchestrator.cs
  - src/RigToggle.Tests/ToggleOrchestratorTests.cs
findings:
  critical: 0
  warning: 6
  info: 3
  total: 9
status: issues_found
---

# Phase 17: Code Review Report

**Reviewed:** 2026-08-08T19:08:29Z
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Reviewed the manual monitor panel (`MonitorPanelForm`), the identify overlay
(`MonitorIdentifyOverlay`), the new `ToggleOrchestrator.BeginExclusiveMonitorAccess()`
lease, and the `MainForm`/`Program.cs` wiring that opens the panel.

The core safety claims from the phase-close audit hold up under direct reading:
`BeginExclusiveMonitorAccess()` shares the same `_busy` field and
`Interlocked.CompareExchange` primitive as `RunGuarded`, is genuinely non-blocking,
never touches the DISPLAY-13 crash marker, and its lease double-dispose is
guarded correctly (`ExclusiveMonitorAccessLease._released`). The panel never routes
through `ToggleService`/`ToggleOrchestrator.ToggleToRigMode/ToNormalMode`, never
persists `MonitorsToDisable`/`MonitorsToEnable`, and never uses `Screen.AllScreens`.
`ToggleOrchestratorTests` covers the new bidirectional-exclusion behavior with
deterministic, event-driven synchronization (no timing guesses).

I also spot-checked `SettingsForm.cs` for precedent before flagging the
un-guarded `DwmTitleBar.ApplyRoundedCornersAndMica`/`ThemeApplier` calls in
`MonitorPanelForm`'s constructor — that pattern is a knowing, documented,
pre-existing codebase convention (D-07: the P/Invoke call is declared to never
throw), not a new defect, so it is not reported below.

That said, several real gaps remain, mostly around the two pieces of genuinely
new lifecycle behavior this phase introduces: a **closable, re-openable,
non-modal form** subscribing to a **process-lifetime static event**
(`SystemEvents.DisplaySettingsChanged`), and a **shared exclusivity primitive**
now used by two conceptually different actions (toggles vs. manual monitor
edits) that still speak with one undifferentiated error message. None of these
rise to data loss or security severity, but several are real crash/robustness
risks worth fixing before shipping this phase.

## Warnings

### WR-01: Hotplug/theme handlers can call BeginInvoke on an already-disposed panel, risking a process-wide crash

**File:** `src/RigToggle.App/MonitorPanelForm.cs:148-186`
**Issue:** `OnDisplaySettingsChanged` and `OnThemeChanged` both start with the
standard `InvokeRequired` → `BeginInvoke` marshal pattern, matching the rest of
the codebase. Unlike `MainForm` (which lives for the entire app lifetime and is
essentially never truly `Dispose()`d until process exit, so this race is
practically unreachable there), `MonitorPanelForm` is explicitly designed to be
opened and closed repeatedly (`OpenMonitorPanel()` in `MainForm.cs:499-514`
recreates it once `IsDisposed` is true). `SystemEvents.DisplaySettingsChanged`
is raised on a dedicated SystemEvents thread, not the UI thread — exactly the
case the existing `InvokeRequired` comment calls out ("SystemEvents is not
guaranteed to raise on the subscriber's thread"). If a hotplug notification (or
a theme flip) is in flight on that background thread at the exact moment the
user closes the panel, `InvokeRequired`/`BeginInvoke` can throw
`ObjectDisposedException` against a Control whose handle has just been torn
down. Because this happens on a non-UI thread, it is not caught by any
WinForms `Application.ThreadException` hook and will terminate the whole
process, not just the panel — for an app whose entire premise is a rig-monitor
setup (i.e., an environment where hotplug events are a realistic, everyday
occurrence), this is a plausible way to crash the app while just closing a
window.
**Fix:** Guard the disposed case before touching `InvokeRequired`, e.g.:
```csharp
private void OnDisplaySettingsChanged(object? sender, EventArgs e)
{
    if (IsDisposed) return;
    if (InvokeRequired)
    {
        try { BeginInvoke(new Action(() => OnDisplaySettingsChanged(sender, e))); }
        catch (ObjectDisposedException) { }
        return;
    }
    ...
}
```
Apply the same guard to `OnThemeChanged` in this file.

### WR-02: "A toggle is already in progress" is shown even when the busy holder is a manual monitor action

**File:** `src/RigToggle.Core/ToggleOrchestrator.cs:92-93, 135-136`
**Issue:** `BeginExclusiveMonitorAccess()` and `RunGuarded()` both throw
`ToggleInProgressException` with the identical literal message `"A toggle is
already in progress. Wait for it to finish, then try again."`. This message is
accurate when a real toggle is holding `_busy` and a panel action is rejected
(`MonitorPanelForm.TryAcquireMonitorAccess`, lines 213-227). It is misleading
in the reverse case: if the manual monitor panel is mid-Disable/Enable (holding
the lease via `BeginExclusiveMonitorAccess()`) and the user fires the global
hotkey or the tray "Switch mode" item, `PerformBackgroundToggle()`
(`MainForm.cs:681-729`) surfaces this same "a toggle is already in progress"
text via a balloon tip — even though no toggle is actually running, a manual
monitor edit is. A user chasing this message (e.g. via a bug report) will be
looking for a stuck toggle that doesn't exist.
**Fix:** Give the two call sites distinct messages, e.g. `"A monitor action is
already in progress. Wait for it to finish, then try again."` for
`BeginExclusiveMonitorAccess()`, keeping the existing text for `RunGuarded()`.

### WR-03: Confirm dialog's nested message loop can let a hotplug refresh invalidate the captured devicePath before the mutation runs

**File:** `src/RigToggle.App/MonitorPanelForm.cs:229-279`
**Issue:** `DisableMonitor` captures `devicePath`/`friendlyName` from the
row that was clicked, then opens `MonitorConfirmDialog.ShowDialog(this)`
(line 245), which — per this file's own comment on `TryAcquireMonitorAccess`
(lines 208-212) — runs a nested message loop that dispatches other queued
messages, including `SystemEvents.DisplaySettingsChanged` marshaled via
`BeginInvoke`. That means `OnDisplaySettingsChanged` → `PopulateMonitorGrid()`
(lines 148-164, 91-140) can legitimately run *while the confirm dialog for a
specific monitor is still open*, clearing and rebuilding `dgvMonitorPanel.Rows`
and `_allMonitors` out from under the dialog the user is looking at. If the
monitor named in the now-stale dialog was unplugged during that window and the
user still clicks OK, `_monitorController.DeactivateMonitors(new HashSet<string>
{ devicePath })` (line 259) is called with a devicePath that is no longer in
`_allMonitors` at all — the code never re-validates the captured devicePath
against the freshly repopulated list before acting on it.
**Fix:** After `ShowDialog` returns `OK`, re-resolve `devicePath` against a
fresh `_monitorController.GetAllMonitors()` (or the already-refreshed
`_allMonitors`) and abort with a message if it's no longer present, rather than
trusting the pre-dialog snapshot unconditionally.

### WR-04: Identify overlay creation loop has no defensive try/catch, unlike every other cosmetic/diagnostic code path in this codebase

**File:** `src/RigToggle.App/MonitorPanelForm.cs:307-346`
**Issue:** `BtnIdentify_Click` wraps only `_monitorController.CaptureState()`
in a try/catch (lines 310-318). The loop that follows
(`new MonitorIdentifyOverlay(snapshot, number).Show(); number++;`, lines
343-344) is unguarded. Every other "cosmetic-only" or diagnostic code path in
this codebase (`MainForm.OnThemeChanged`, `MainForm.ApplyDwmChrome`,
`MonitorPanelForm.OnThemeChanged`, `MonitorPanelForm.OnDisplaySettingsChanged`)
is explicitly wrapped with a comment along the lines of "a theming/hotplug
failure must never crash the panel." Identify is exactly this class of
diagnostic, non-critical convenience feature, yet a failure constructing any
one `MonitorIdentifyOverlay` (e.g. `Font`/`Bitmap`/GDI resource exhaustion, or
a future edit that adds fallible logic to the overlay's constructor) aborts the
remaining overlays and propagates an unhandled exception out of a `Button.Click`
handler.
**Fix:** Wrap the per-row overlay creation in its own try/catch (continuing the
loop on failure), matching the "diagnostic feature must never crash" posture
used everywhere else:
```csharp
try
{
    new MonitorIdentifyOverlay(snapshot, number).Show();
}
catch (Exception ex)
{
    System.Diagnostics.Trace.WriteLine($"BtnIdentify_Click: overlay for {devicePath} failed: {ex}");
}
number++;
```

### WR-05: "Don't ask again" from a single-monitor panel Disable now silently suppresses the higher-stakes Rig/Normal toggle confirmation too

**File:** `src/RigToggle.App/MonitorPanelForm.cs:236-251`
**Issue:** Prior to this phase, `AppSettings.SkipMonitorConfirmation` could
only be set from the DISPLAY-07 confirmation shown before a Rig/Normal mode
toggle (`MainForm.cs:352-392`) — checking "don't ask again" there is
self-consistent with what it suppresses (future toggle confirmations). Phase
17 adds a second, structurally different write path: `DisableMonitor` (lines
236-251) shows a `MonitorConfirmDialog` scoped to a single monitor from the
panel, and its own "don't ask again" checkbox writes the exact same
`SkipMonitorConfirmation` flag (line 248). A user who disables one monitor via
the panel as a quick one-off action and checks "don't ask again" — reasonably
believing this only applies to that one panel action — will unknowingly also
suppress the full, multi-monitor DISPLAY-07 confirmation the next time they
switch to Rig Mode, without any indication that checking the box here has that
broader, higher-stakes scope.
**Fix:** Either use a separate settings flag for panel-originated confirmations
vs. toggle-originated ones, or make the panel's confirm dialog copy explicit
that "don't ask again" applies to all future monitor confirmations including
mode toggles.

### WR-06: `MonitorIdentifyOverlay` instances are never given an `Owner`, so they outlive an already-closed panel

**File:** `src/RigToggle.App/MonitorPanelForm.cs:343`
**Issue:** `new MonitorIdentifyOverlay(snapshot, number).Show()` never sets
`Owner`. If the user closes `MonitorPanelForm` while identify overlays are
still on screen (within their 2.5s auto-close window), the overlays are left
dangling with no parent relationship — they'll still auto-close on their own
timer, but in the interim they are borderless, `TopMost` windows with no owner
to tie their lifetime to, which is inconsistent with the rest of this
codebase's dialog-ownership discipline (`MonitorConfirmDialog.ShowDialog(this)`,
`SettingsForm.ShowDialog(this)`).
**Fix:** Pass `this` (the panel) as `Owner` in the overlay constructor, or set
`overlay.Owner = this;` before `Show()`.

## Info

### IN-01: `BeginExclusiveMonitorAccess` vs. itself has no reentrancy test

**File:** `src/RigToggle.Tests/ToggleOrchestratorTests.cs`
**Issue:** The test suite proves toggle-vs-lease exclusion in both directions
(`BeginExclusiveMonitorAccess_WhileToggleInFlight_ThrowsToggleInProgress`,
`ToggleToRigMode_WhileExclusiveMonitorAccessHeld_ThrowsToggleInProgress`), but
there is no test proving a second `BeginExclusiveMonitorAccess()` call is
rejected while a first lease is already held (i.e., two concurrent manual
monitor actions, panel-to-panel exclusion). The implementation appears correct
by inspection (same `_busy`/`CompareExchange` path), but this symmetric case
is untested.
**Fix:** Add a test acquiring one lease, then asserting a second
`BeginExclusiveMonitorAccess()` call throws `ToggleInProgressException` while
the first is still held.

### IN-02: `CreateStatusDot`'s bitmap/ellipse dimensions are inconsistent magic numbers

**File:** `src/RigToggle.App/MonitorPanelForm.cs:77-86`
**Issue:** The bitmap is allocated as `12x12` but the ellipse is drawn at
`FillEllipse(brush, 0, 0, 11, 11)` — an off-by-one between the canvas size and
the drawn shape (leaving a 1px unused margin on two edges rather than a
centered dot). Purely cosmetic, not a functional bug.
**Fix:** Use consistent dimensions, e.g. `FillEllipse(brush, 1, 1, 10, 10)` for
a centered dot, or `FillEllipse(brush, 0, 0, 12, 12)` to fill the canvas.

### IN-03: `MonitorPanelForm`/`MonitorIdentifyOverlay` have no automated test coverage

**File:** `src/RigToggle.App/MonitorPanelForm.cs`, `src/RigToggle.App/MonitorIdentifyOverlay.cs`
**Issue:** All of the new grid-population logic (`PopulateMonitorGrid`'s
suffix computation, row keying via `Tag`), the Disable/Enable dispatch in
`DgvMonitorPanel_CellClick`, and the Identify numbering logic in
`BtnIdentify_Click` ship with zero automated tests — consistent with this
codebase's existing convention of not unit-testing WinForms UI classes
(`MainForm`, `SettingsForm` are likewise untested), so this is not a
phase-17-specific regression, but it's worth noting the panel's non-trivial
new logic (e.g. WR-03's stale-devicePath path, the display-order numbering in
`BtnIdentify_Click`) is currently verified only by manual/rig testing.
**Fix:** Consider extracting the pure logic (row-suffix computation, snapshot
matching for Identify) into testable helper methods, mirroring how
`ToggleResultFormatter` was already extracted for testability elsewhere in
this codebase.

---

## Resolution (2026-08-08)

7 of 9 findings addressed; 2 deliberately not fixed.

- **WR-01** (disposed-panel crash risk) — fixed. `IsDisposed` guard added before `InvokeRequired`/`BeginInvoke` in both `OnDisplaySettingsChanged` and `OnThemeChanged`, plus a `catch (ObjectDisposedException)` around the `BeginInvoke` call itself for the race window between the check and the call.
- **WR-02** (shared "toggle in progress" message can be misleading when a monitor-panel lease holds `_busy`) — **not fixed, deliberate.** This is an explicit, documented design decision from 17-01's plan: "reuse this exact existing string verbatim... do not introduce a new message or a new exception type," directly consistent with this phase's core DISPLAY-12 philosophy (one shared codepath, not per-caller variants). The reviewer's own suggested fix (change only `BeginExclusiveMonitorAccess()`'s message) doesn't actually address the misleading direction described in the same finding (a toggle rejected while a monitor lease is held) — a fully correct fix would require holder-identity tracking that doesn't exist and wasn't asked for. Given the edge case only reaches a live user via `MonitorConfirmDialog`'s nested message loop plus a same-instant hotkey press (the plan's own "Bonus, optional" scenario), left as-is rather than overriding a locked planning decision for a narrow, low-stakes wording imprecision.
- **WR-03** (stale devicePath after confirm dialog's nested message loop) — fixed. Re-validates `devicePath` against `_allMonitors` after `ShowDialog()` returns, before calling `DeactivateMonitors`; aborts with an informational message and refreshes the grid if the monitor is no longer present.
- **WR-04** (unguarded Identify overlay creation loop) — fixed. Wrapped in try/catch per-overlay, matching the codebase's existing "diagnostic code must never crash" convention; failure of one overlay no longer aborts the rest.
- **WR-05** (shared `SkipMonitorConfirmation` checkbox scope not indicated to the user) — fixed via copy clarification ("Don't ask again for any monitor change") rather than a second settings flag, avoiding a schema/migration change for a UX-clarity fix.
- **WR-06** (Identify overlays have no `Owner`) — fixed. `Owner = this` set before `Show()`.
- **IN-01** (no panel-to-panel lease exclusion test) — fixed. Added `BeginExclusiveMonitorAccess_WhileAnotherLeaseHeld_ThrowsToggleInProgress`.
- **IN-02** (status-dot bitmap/ellipse off-by-one) — fixed. `FillEllipse` now draws at the full `12x12` canvas size.
- **IN-03** (no automated test coverage for `MonitorPanelForm`/`MonitorIdentifyOverlay`) — **not fixed, deliberate.** Matches this codebase's existing, established convention of not unit-testing WinForms UI classes (`MainForm`, `SettingsForm` are likewise untested) — not a phase-17-specific regression. Verified via rig testing instead (17-04-SUMMARY.md).

`dotnet build RigToggle.sln -p:EnableWindowsTargeting=true` — 0 errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 85/85 pass (84 + 1 new IN-01 regression test).

---

_Reviewed: 2026-08-08T19:08:29Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
