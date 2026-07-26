---
phase: quick-260726-k3u
plan: 01
subsystem: settings-ui
tags: [winforms, layout, checkbox, bugfix]

requires:
  - phase: quick-260726-jti
    provides: chkEnableDebugLogging checkbox (EnableDebugLogging setting)
provides:
  - Correct height for chkEnableDebugLogging so its wrapped two-line text is fully visible
affects: [settings-ui]

tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - src/RigToggle.App/SettingsForm.Designer.cs

key-decisions:
  - "Grew checkbox height (24px -> 40px) rather than shortening the label text -- user explicitly wanted the full %LOCALAPPDATA%\\RigToggle\\debug.log path spelled out, not abbreviated"
  - "Shifted btnSaveSettings/btnDiscardChanges and ClientSize down by the same 16px delta to preserve original margins with no overlap"

requirements-completed: []

duration: ~5min
completed: 2026-07-26
status: complete
---

# Quick Task 260726-k3u: Fix SettingsForm Checkbox Height Summary

**Grew the "Enable debug logging" checkbox from 24px to 40px tall so its wrapped two-line text (previously clipped after "(writes to") is fully visible, shifting the Save/Discard buttons and form ClientSize down 16px to match.**

## Root cause

Quick task 260726-jti added a checkbox with `AutoSize = false` and a long label ("Enable debug logging (writes to %LOCALAPPDATA%\RigToggle\debug.log)"). WinForms wraps CheckBox text automatically at the control's width when it doesn't fit on one line, but the control was only 24px tall (single-line height) — so the wrapped second line rendered outside the control's visible bounds and was clipped. Confirmed by the user's rig report: "the text ... cuts at (writes to".

## Fix

Four numeric literal changes in `SettingsForm.Designer.cs`:
- `chkEnableDebugLogging.Size`: `(396, 24)` -> `(396, 40)`
- `btnSaveSettings.Location`: `(180, 360)` -> `(180, 376)`
- `btnDiscardChanges.Location`: `(298, 360)` -> `(298, 376)`
- `ClientSize`: `(420, 408)` -> `(420, 424)`

No C# behavior changes — pure layout fix.

## Verification

No .NET SDK in this Linux sandbox, so verified via grep confirming all four new values are present and the old values are gone. **User must confirm visually on the rig** that both lines of checkbox text now display fully and the Save/Discard buttons don't overlap it.

## Commits

- `640b11a` — docs(260726-k3u): pre-dispatch plan for checkbox height fix
- `984814d` — fix(260726-k3u): grow debug-logging checkbox height so wrapped text isn't clipped
