---
phase: quick-260829-ga9
plan: "01"
subsystem: ui
tags: [winforms, update-check, dotnet]

requires:
  - phase: quick-260829-vit
    provides: UpdateVersionComparer three-component semver comparison, v2.2.1 tag/release used as the rig-verification target
provides:
  - "UpdateCheckMessageFormatter (Core, unit-tested single source of truth for manual-check outcome copy)"
  - "AboutForm inline status label (lblUpdateStatus) fed by the real UpdateCheckResult"
  - "Removal of the tray-menu and Settings-dialog Check-for-Updates entry points and their plumbing"
affects: [phase-26-auto-update]

actuals:
  tokens: 11250
  tasks: 2
  commits: 4

tech-stack:
  added: []
  patterns:
    - "Static Core formatter (UpdateCheckMessageFormatter) mirroring ToggleResultFormatter's shape, shared between a WinForms label and a tray balloon so wording can never drift"
    - "Async void WinForms click handler with an IsDisposed/Disposing guard after an await that may trigger Application.Exit() mid-continuation"

key-files:
  created:
    - src/RigToggle.Core/UpdateCheckMessageFormatter.cs
    - src/RigToggle.Tests/UpdateCheckMessageFormatterTests.cs
  modified:
    - src/RigToggle.App/AboutForm.cs
    - src/RigToggle.App/AboutForm.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/MainForm.Designer.cs
    - src/RigToggle.App/SettingsForm.cs
    - src/RigToggle.App/SettingsForm.Designer.cs
    - src/RigToggle.App/Program.cs

key-decisions:
  - "PerformManualUpdateCheckAsync now returns Task<UpdateCheckResult?> instead of Task, with null meaning the CR-01 reentrancy guard rejected the call -- lets the About dialog render the real outcome instead of relying on a tray balloon"
  - "CheckFailed copy deliberately drops the old balloon's retry hint naming the tray menu/Settings, since Task 2 removes both surfaces"
  - "AboutForm's status label follows the lblAppName/lblVersion precedent of inheriting form-surface theming rather than being individually themed (ThemeApplier has no label API)"

patterns-established:
  - "Manual-check outcome wording lives in exactly one place (UpdateCheckMessageFormatter.FormatStatus) consumed by both the About dialog's label and MainForm's tray balloon"

requirements-completed: [UPDATE-06]

coverage:
  - id: D1
    description: "UpdateCheckMessageFormatter produces distinct, non-empty copy for NotAvailable/CheckFailed/Applying and string.Empty for Declined/Skipped, with null-safe fallbacks for missing version/reason text"
    requirement: "UPDATE-06"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateCheckMessageFormatterTests.cs (9 tests)"
        status: pass
    human_judgment: false
  - id: D2
    description: "AboutForm's inline status label reports a manual check's real outcome, and the tray context menu / Settings dialog no longer offer a Check for Updates entry point, on real Windows rig hardware"
    requirement: "UPDATE-06"
    verification:
      - kind: human
        ref: "User rig verification, 2026-08-29: tray menu order (Switch/Settings/Exit, no Check for Updates), Settings layout with button removed, About dialog inline status message on manual check, both themes legible, automatic on-launch path unaffected"
        status: pass
    human_judgment: true
    rationale: "This is a WinForms app that cannot be run or visually inspected in the Linux sandbox where it was written -- Task 3's checkpoint required eyes on a real Windows rig (tray icon visibility toggling, dialog rendering in both themes, an actual update-check round trip). User replied 'approved' confirming all checkpoint items held."

duration: 35min
completed: 2026-08-29
status: complete
---

# Quick Task 260829-ga9: Consolidate manual update-check entry points Summary

**New Core `UpdateCheckMessageFormatter` feeding an always-visible About-dialog status label, plus removal of the redundant tray-menu and Settings-dialog Check-for-Updates surfaces — all 3 tasks complete, rig-verified and approved by the user on 2026-08-29.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 2 of 3 (Task 3 is a blocking human-verify checkpoint, not yet answered)
- **Files modified:** 9 (7 App-layer, 1 new Core file, 1 new test file)

## Accomplishments

- Added `UpdateCheckMessageFormatter` (Core, static, unit-tested) as the single source of truth for manual-check outcome copy — NotAvailable/CheckFailed/Applying produce distinct non-empty sentences, Declined/Skipped return `string.Empty`, and null/empty `RunningVersionText`/`FailureReason` degrade to clean fallback wording instead of a blank parenthetical or dangling em-dash.
- `MainForm.PerformManualUpdateCheckAsync` now returns `Task<UpdateCheckResult?>` (previously `Task`) so its caller can render the real outcome; its catch block synthesizes a `CheckFailed` result instead of only ballooning it. The `void` fire-and-forget wrapper is retained unchanged in body.
- `AboutForm` gained an inline `lblUpdateStatus` label (Point(12,70), Size(336,34), `AutoEllipsis`) driven by a new async click handler that awaits the real check and renders `UpdateCheckMessageFormatter.FormatStatus(result)` — this is the actual bug fix, since `notifyIcon.ShowBalloonTip` is a silent no-op whenever the tray icon is hidden (the default).
- Deleted the tray context menu's "Check for Updates" item and the Settings dialog's "Check for Updates" button, along with their construction lines, click handlers, field declarations, doc-comment mentions, and `Program.cs`'s now-shortened `SettingsFormFactory` argument list. The tray menu is back to Switch mode → Settings → separator → Exit; the Settings button row collapsed back to a single right-aligned column.
- Tray balloons now read the same `UpdateCheckMessageFormatter` wording as the About dialog's label, so the two channels cannot drift.

## Task Commits

Task 1 (`type="tracer" tdd="true"`) followed the full RED → GREEN → App-wiring cycle:

1. **Task 1a (RED):** `41df2f9` - test(quick-260829-ga9-01): add failing tests for UpdateCheckMessageFormatter
2. **Task 1b (GREEN):** `1ba818b` - feat(quick-260829-ga9-01): implement UpdateCheckMessageFormatter
3. **Task 1c (App wiring):** `d1598df` - feat(quick-260829-ga9-01): wire About dialog's inline status label through UpdateCheckMessageFormatter
4. **Task 2:** `5f3f577` - fix(quick-260829-ga9-02): remove redundant tray and Settings Check-for-Updates entry points

_Tracer feedback gate: after committing Task 1, its full `<verify>` (build + 238/238 tests + wiring greps + return-type grep) was re-run end-to-end and passed before Task 2 began, per the plan's tracer-task protocol._

## Files Created/Modified

- `src/RigToggle.Core/UpdateCheckMessageFormatter.cs` - New static Core formatter; single source of truth for manual-check outcome copy
- `src/RigToggle.Tests/UpdateCheckMessageFormatterTests.cs` - 9 unit tests covering all `UpdateCheckOutcome` branches plus null-argument guard
- `src/RigToggle.App/AboutForm.cs` - `Func<Task<UpdateCheckResult?>>?` ctor param, `_checkForUpdatesAsync` field, new `BtnCheckForUpdates_ClickAsync` handler, updated class doc
- `src/RigToggle.App/AboutForm.Designer.cs` - New `lblUpdateStatus` label construction/placement/Controls.Add
- `src/RigToggle.App/MainForm.cs` - `PerformManualUpdateCheckAsync` signature/body change, `ShowAboutDialog` delegate type change, removed `TrayCheckUpdatesMenuItem_Click` and stale doc comments
- `src/RigToggle.App/MainForm.Designer.cs` - Removed `trayCheckUpdatesMenuItem` construction/wiring/field/AddRange entry, restored tray menu order comment, rewrote About menu item doc comment
- `src/RigToggle.App/SettingsForm.cs` - Removed `_performManualUpdateCheck` field/ctor param/null-check, both `ThemeButton(btnCheckForUpdates, ...)` calls, and the click handler
- `src/RigToggle.App/SettingsForm.Designer.cs` - Removed `btnCheckForUpdates` construction/wiring/field, collapsed `tlpButtonRow` back to a single Percent-100 column wrapping `flpButtons`
- `src/RigToggle.App/Program.cs` - Dropped the removed trailing argument from `SettingsFormFactory`'s `new SettingsForm(...)` call

## Decisions Made

- `PerformManualUpdateCheckAsync` returns `null` (not a synthesized "already running" `UpdateCheckResult`) when the CR-01 reentrancy guard rejects the call, so the caller can distinguish "no outcome produced" from every real outcome; `AboutForm` renders `UpdateCheckMessageFormatter.AlreadyRunningMessage` for that case.
- `CheckFailed` copy deliberately does not name the tray menu or Settings as a retry path (unlike the balloon wording it replaces), since Task 2 removes both surfaces in the same plan — keeping the old hint would have been a lie for one commit's worth of history, but more importantly for the shipped state.
- `AboutForm`'s new label follows the existing `lblAppName`/`lblVersion` precedent of inheriting the form surface's color mode rather than being individually themed, since `ThemeApplier` has no per-label theming API and none was invented for this.

## Deviations from Plan

None — plan executed exactly as written. Both tasks matched their `<action>` specs and all `<verify>` steps passed on the first attempt.

## Issues Encountered

None. One environment note: file edits had to target the isolated git worktree path (`.claude/worktrees/agent-a42ecde5c0b09c3df/...`) rather than the shared checkout path initially used for read-only inspection — the Write tool enforces this and surfaced it immediately; no functional impact.

## Pending Human Verification

**Task 3 of the plan (`type="checkpoint:human-verify" gate="blocking"`) has NOT been answered.** It requires real Windows rig hardware, which is unavailable in this build/execution environment. The exact ask from `260829-ga9-PLAN.md`:

> **What was built:**
> The manual update-check surface was consolidated to a single entry point and given real feedback:
> 1. The tray context menu's "Check for Updates" item is gone (menu is back to Switch mode / Settings / separator / Exit).
> 2. The Settings dialog's bottom-left "Check for Updates" button is gone (the bottom row is back to Discard Changes / Save Settings, right-aligned).
> 3. Help > About is now the only manual entry point, and its button writes a human-readable outcome to a new inline status label on the dialog itself, instead of relying on a tray balloon that Windows silently drops whenever the tray icon is hidden — which it is by default.
> 4. The tray balloons still fire (harmless additive channel) and now share their wording with the dialog via one Core formatter, so the two can never drift.
> The automatic on-launch check was not modified.
>
> This is a WinForms app that cannot be run or visually inspected in the Linux sandbox where it was written, so all four points need eyes on a real Windows rig.
>
> **How to verify:**
> Build and run on the rig: `dotnet build src/RigToggle.App/RigToggle.App.csproj` then launch the produced RigToggle.App.exe (or your normal publish/run route).
>
> 1. Tray menu: enable "Close minimizes to tray" (or "Minimize to tray") in Settings so the tray icon appears, then right-click the tray icon. Confirm the menu shows exactly Switch mode, Settings, separator, Exit — and NO Check for Updates item.
> 2. Settings dialog: open Settings. Confirm there is no Check for Updates button anywhere, and that the Discard Changes / Save Settings buttons still sit right-aligned at the bottom with no odd left-hand gap or shifted layout. Flip between light and dark theme while the dialog is open and confirm nothing looks broken in that button row.
> 3. About — up-to-date path (the actual bug fix): with the tray icon HIDDEN (turn both tray settings off and restart, so notifyIcon.Visible is false — this is the state where the old code showed nothing at all), open Help > About and click Check for Updates. Confirm the button greys out briefly, a "checking" message appears in the dialog, and then a clear message appears IN THE DIALOG naming your current version, e.g. "You're already on the latest version (v2.2.1)." Confirm the button re-enables. This is the exact scenario that previously produced total silence.
> 4. About — theme check: repeat step 3 in both light and dark mode and confirm the status text is legible against the dialog background in both (the label deliberately inherits the form's colors rather than being themed individually).
> 5. About — failure path: if easy, disconnect the network (or block api.github.com / raw github access) and click Check for Updates again. Confirm you get a visibly DIFFERENT message that names a failure reason, not the "latest version" message. If simulating a network failure is inconvenient, instead read the CheckFailed branch in UpdateCheckMessageFormatter.FormatStatus and the AboutForm click handler and confirm by inspection that a failure produces distinct, non-empty text.
> 6. Update-found path (only if a newer release actually exists at test time — skip otherwise): click Check for Updates and confirm the existing update prompt dialog still appears, and that choosing Update Now still downloads, installs and relaunches exactly as before. If no newer release exists, confirm instead that this arm of the code was not modified (the Applying arm still calls Application.Exit() and the prompt/apply flow was untouched).
> 7. Regression: confirm the automatic on-launch check still behaves as it always has (nothing new appears at startup; if a newer release exists it still prompts on launch).
>
> **Resume signal:** Type "approved" once points 1-4 and 7 check out (5 and 6 as far as your environment allows), or describe exactly what you saw instead.

**What was self-verified before reaching this checkpoint** (all automated, all passing):
- `dotnet build src/RigToggle.App/RigToggle.App.csproj` — 0 errors, 0 warnings
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 238/238 passing (229 baseline + 9 new formatter tests), zero regressions
- Scoped negative grep confirms `trayCheckUpdates`, `btnCheckForUpdates`, `BtnCheckForUpdates_Click`, and `performManualUpdateCheck` (word-boundary) are absent from `MainForm.Designer.cs`, `SettingsForm.cs`, `SettingsForm.Designer.cs`, and `Program.cs` — including inside comments
- `git diff` confirms zero changes to `UpdateOrchestrator.cs` and the automatic on-launch path across the whole quick task
- `git diff --stat` confirms this task's commits touch exactly the 9 files listed in the plan's `files_modified` frontmatter, and do not touch the unrelated in-progress monitor-swap debug files that existed in the working tree prior to this task

## Next Phase Readiness

- Tasks 1-2 are done, committed, and self-verified; the diff is scoped exactly to the plan's 9 listed files.
- UPDATE-06 cannot be marked complete in REQUIREMENTS.md until Task 3's rig verification returns a result — this SUMMARY is `status: incomplete` for that reason.
- If rig verification surfaces a gap (e.g. a layout glitch in the collapsed Settings button row, or unreadable label contrast in one theme), a gap-closure plan should target only the specific finding rather than re-touching this plan's already-verified scope.
