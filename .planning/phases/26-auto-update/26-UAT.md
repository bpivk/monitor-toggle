---
status: closed_by_signoff
phase: 26-auto-update
source: [26-VERIFICATION.md]
started: 2026-08-23T00:00:00Z
updated: 2026-08-29T00:00:00Z
---

## Current Test

None — closed. Tests 2-5 were not independently run; see each test's result and the "Operator sign-off" note below.

## Tests

### 1. Real exe swap and relaunch (SC3 / UPDATE-04)
expected: The exe at the original install path is now the new version; no SmartScreen interstitial; autostart still works without re-toggling.
result: pass — user tested live against real v2.2.1 -> v2.2.2 release (tagged/pushed 2026-08-29), triggered via the new About dialog's Check for Updates, confirmed "It works great." User explicitly confirmed no SmartScreen interstitial appeared and no autostart regression occurred.

### 2. Interrupted-update recovery (SC4 / UPDATE-05)
expected: Deliberately interrupt an update (kill the helper mid-swap, or simulate disk-full/locked-file). A launchable exe still exists at the original path afterward; a Warning balloon explains the failure.
result: closed by operator sign-off, 2026-08-29 — not independently verified. This scenario requires deliberately killing the update helper mid-swap or simulating a disk-full/locked-file condition; the user's day-to-day use of the app "seeming fine" does not exercise this path. Not a fabricated pass — remains an open follow-up item, should be run if the opportunity arises.

### 3. Auto-rollback timing correctness, including CR-02's fix (D-09 / UPDATE-05)
expected: Apply an update, then (a) exit gracefully within 10 seconds via tray Exit/window close/shutdown and confirm NO false revert on next launch; (b) kill the process within 10 seconds and confirm a correct, single (non-looping) revert with the right balloon text.
result: closed by operator sign-off, 2026-08-29 — not independently verified. Requires deliberately killing the process within a 10-second timing window; not something normal use would exercise. Not a fabricated pass — remains an open follow-up item.

### 4. On-launch check reliability under both startup modes (SC1)
expected: Launch normally and via --tray; confirm the automatic update check fires in both cases with no silently-dropped BeginInvoke.
result: closed by operator sign-off, 2026-08-29 — not independently verified. The user has exercised the normal-launch on-demand path (via the About dialog) but the `--tray` hidden-startup path specifically was not confirmed. Not a fabricated pass — remains an open follow-up item.

### 5. Real release-notes rendering (UI-SPEC backstop)
expected: View the actual next release's notes in the update dialog; unsupported Markdown constructs (tables, links, images, nested lists) render as readable plain text, not garbled output.
result: closed by operator sign-off, 2026-08-29 — not independently verified. The v2.2.2 test release had no complex Markdown release notes to exercise this rendering path. Not a fabricated pass — remains an open follow-up item.

## Summary

total: 5
passed: 1
issues: 0
pending: 0
skipped: 0
blocked: 0
signed_off_unverified: 4

## Operator sign-off (2026-08-29)

Tests 2-5 were closed by the user's explicit authorization rather than by completing their original ask, matching how Phase 25 Plan 04 Task 3's rig-verification checkpoint was previously closed in this project. The user's rationale, verbatim intent: "everything seems fine so just confirm it." The orchestrator flagged before recording this that tests 2-4 specifically require deliberate failure-injection scenarios (killing the update helper mid-swap, simulating disk-full, killing the process within a 10-second rollback window) that normal use would not exercise, and asked the user to choose between (a) signing off without testing or (b) actually running each scenario. The user chose (a). These four items are recorded as open follow-up items, not fabricated passes, and should be run for real if the opportunity arises -- particularly test 2/3's failure-recovery paths, since those protect against leaving the app unlaunchable after a bad update.

## Gaps
