---
created: 2026-08-22T18:42:36.619Z
title: Monitor position resets to default on disable/re-enable
area: monitor-control
severity: major
files:
  - src/RigToggle.Windows/WindowsMonitorController.cs (likely — GetAllMonitors/enable-disable path)
---

## Problem

A monitor's position, as configured in Windows Display Settings, resets to its default position when the monitor is disabled and then re-enabled through Rig Toggle (the dashboard tile toggle or the Rig/Normal mode toggle). The user has to manually re-arrange the monitor's position in Windows Display Settings every time it wakes/re-enables. Reported by the user as "always needs setting up after the monitor wakes" — confirmed as major severity (wrong behavior with no acceptable workaround, happens every time).

## Solution

TBD. Likely needs investigation into whether the CCD API's `SetDisplayConfig` call path (used for enable/disable) is dropping the monitor's `DISPLAYCONFIG_SOURCE_MODE`/position info rather than preserving it across the disable→enable round-trip — compare against `PathInfo.GetActivePaths()`/`ApplyPathInfos()` usage in the monitor toggle code. Route through `/gsd-debug` for systematic investigation.
