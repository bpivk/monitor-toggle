---
status: partial
phase: 05-orchestration-full-toggle-packaging
source: [05-VERIFICATION.md]
started: 2026-07-25T19:30:00Z
updated: 2026-07-25T19:30:00Z
---

## Current Test

[awaiting human testing]

## Tests

### 1. CORE-04 checklist dialog on-screen rendering
expected: A MessageBox titled "Rig Toggle" appears reading "The toggle did not fully complete:" followed by per-step lines (e.g. "Monitor: OK", "Audio: FAILED (‹reason›)", "App: not attempted") — not the generic exception dialog — and the mode/status labels reflect the partial state change. Induce it by temporarily pointing the configured rig audio device at an unplugged/renamed endpoint, or otherwise making one mutation step fail, then clicking "Switch to Rig Mode".
result: [pending]

### 2. CR-01 fix rig confirmation (low priority, defensive-only)
expected: MainForm shows "Mode: Normal" (not "Mode: Rig") immediately after WindowsMonitorController.Disable fails at one of its pre-mutation guards (target monitor not currently active, or target is the only active display), with the checklist reporting "Monitor: FAILED (...)".
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps
