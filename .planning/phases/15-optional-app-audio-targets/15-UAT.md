---
status: complete
phase: 15-optional-app-audio-targets
source: [15-01-SUMMARY.md, 15-02-SUMMARY.md, 15-03-SUMMARY.md, 15-04-SUMMARY.md]
started: 2026-08-04T19:15:54Z
updated: 2026-08-04T20:10:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Unset companion app path skips cleanly
expected: In Settings, click Clear next to the app path (box shows "No app shortcut or .exe selected", Clear becomes disabled). Save. Toggle to Rig, then back to Normal. The toggle-result checklist shows "App: Skipped (not configured)" both directions — no error dialog, no "did not fully complete" warning.
result: pass

### 2. Unset Rig-mode audio device skips cleanly
expected: Set the Rig audio dropdown to "(None — don't switch audio)", Save, toggle to Rig. The checklist shows "Audio: Skipped (not configured)"; your current default audio device is unchanged.
result: pass

### 3. Configured Normal-mode audio device actually applies
expected: Set a specific Normal-mode audio device (different from the current default), Save, toggle to Rig then back to Normal. On toggle-to-Normal, the Windows default playback device actually switches to the configured device (visible in the Windows sound flyout), and the checklist shows "Audio: OK".
result: pass

### 4. Broken (moved) app path fails loudly, not skipped
expected: Configure a valid .exe, Save, then move/rename that .exe on disk. Toggle to Rig. The checklist shows "App: FAILED (...)" with a friendly "could not be found ... Open Settings and reselect..." message — never "Skipped".
result: pass

### 5. Removed audio device fails loudly, not skipped
expected: Configure a USB/removable audio device as the Rig (or Normal) device, Save, unplug it, then toggle in that direction. The checklist shows "Audio: FAILED (...)" with a "configured ... audio device could not be found. Open Settings and reselect it." message — never "Skipped".
result: pass

### 6. Settings affordances: Clear button, "(None)" audio option, and broken-still-blocks-Save
expected: The app-path row has a Clear button next to it, and each audio dropdown offers a "(None — don't switch audio)" entry. With a moved/broken .exe still configured (not cleared), reopening Settings shows Save disabled with an app-path warning; clicking Clear then re-Saving succeeds. Save no longer requires audio/app to be set — only a valid monitor grid.
result: pass

## Summary

total: 6
passed: 6
issues: 0
pending: 0
skipped: 0

## Gaps

[none yet]
