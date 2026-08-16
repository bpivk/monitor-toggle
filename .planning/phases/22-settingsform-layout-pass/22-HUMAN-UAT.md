---
status: resolved
phase: 22-settingsform-layout-pass
source: [22-VERIFICATION.md]
started: 2026-08-16T10:30:00Z
updated: 2026-08-16T11:00:00Z
---

## Current Test

[complete]

## Tests

### 1. CR-01/CR-02 working-area clamp under real overshoot conditions
expected: Open Settings while the owning window/tray icon sits on a non-primary monitor in the actual two-monitor rig setup, and separately with Windows display scale at 150% on a smaller or lower-resolution display where the dialog's preferred content size exceeds the working area. The Settings window's full outer bounds (including title bar and border) should fit within the working area of the monitor it actually appears on — no part hidden behind the taskbar or off-screen — and the size should be computed against the correct monitor, not always the primary display.
result: PASS — user confirmed both sub-tests (opened Settings with the owner on the rig/non-primary monitor, and at 150% display scale after relaunch) fit fully on-screen with no clipping or mispositioning, no issues reported.

## Summary

total: 1
passed: 1
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
