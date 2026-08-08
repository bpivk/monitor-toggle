---
status: partial
phase: 16-normal-mode-explicit-monitor-config-mode-store-redesign
source: [16-VERIFICATION.md]
started: 2026-08-08T12:03:44Z
updated: 2026-08-08T12:03:44Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. Real crash-mid-toggle detection (DISPLAY-13)
expected: Kill the RigToggle process (Task Manager "End Task") mid-toggle on the real rig, then relaunch. A blocking startup dialog states the last toggle to {Rig/Normal} Mode didn't finish cleanly and that no automatic retry was attempted; after clicking OK, `%LocalAppData%\RigToggle\toggle-in-progress.json` is gone (marker cleared).
result: [pending]

### 2. Re-confirm re-flowed Settings layout on the rig (DISPLAY-09)
expected: Open Settings (current side-by-side grid layout, post gap-closure commit `098d10c`). Rig Mode and Normal Mode grids sit side by side with plain "Off"/"On" column headers, all downstream controls (audio, app path, hotkey, checkboxes, Save/Discard) are visible and unclipped, and the Normal grid's dark-mode theming matches the Rig grid.
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
