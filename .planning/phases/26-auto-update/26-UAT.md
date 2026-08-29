---
status: testing
phase: 26-auto-update
source: [26-VERIFICATION.md]
started: 2026-08-23T00:00:00Z
updated: 2026-08-29T00:00:00Z
---

## Current Test

number: 2
name: Interrupted-update recovery (SC4 / UPDATE-05)
expected: |
  Deliberately interrupt an update (kill the helper mid-swap, or simulate
  disk-full/locked-file). A launchable exe still exists at the original path
  afterward; a Warning balloon explains the failure.
awaiting: user response

## Tests

### 1. Real exe swap and relaunch (SC3 / UPDATE-04)
expected: The exe at the original install path is now the new version; no SmartScreen interstitial; autostart still works without re-toggling.
result: pass — user tested live against real v2.2.1 -> v2.2.2 release (tagged/pushed 2026-08-29), triggered via the new About dialog's Check for Updates, confirmed "It works great." User explicitly confirmed no SmartScreen interstitial appeared and no autostart regression occurred.

### 2. Interrupted-update recovery (SC4 / UPDATE-05)
expected: Deliberately interrupt an update (kill the helper mid-swap, or simulate disk-full/locked-file). A launchable exe still exists at the original path afterward; a Warning balloon explains the failure.
result: [pending]

### 3. Auto-rollback timing correctness, including CR-02's fix (D-09 / UPDATE-05)
expected: Apply an update, then (a) exit gracefully within 10 seconds via tray Exit/window close/shutdown and confirm NO false revert on next launch; (b) kill the process within 10 seconds and confirm a correct, single (non-looping) revert with the right balloon text.
result: [pending]

### 4. On-launch check reliability under both startup modes (SC1)
expected: Launch normally and via --tray; confirm the automatic update check fires in both cases with no silently-dropped BeginInvoke.
result: [pending]

### 5. Real release-notes rendering (UI-SPEC backstop)
expected: View the actual next release's notes in the update dialog; unsupported Markdown constructs (tables, links, images, nested lists) render as readable plain text, not garbled output.
result: [pending]

## Summary

total: 5
passed: 1
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps
