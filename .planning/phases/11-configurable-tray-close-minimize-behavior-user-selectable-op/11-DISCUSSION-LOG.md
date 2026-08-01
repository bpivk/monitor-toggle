# Phase 11: Configurable Tray Close/Minimize Behavior - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-01
**Phase:** 11-configurable-tray-close-minimize-behavior
**Areas discussed:** Close (X) button behavior + default, Minimize button behavior + default, Settings UI shape, Interaction with existing tray invariants

---

## Close (X) Button Behavior + Default

| Option | Description | Selected |
|--------|-------------|----------|
| Default: hide to tray | Matches current behavior — no change on upgrade | |
| Default: exit the app | Standard Windows convention, bigger change for the existing install | ✓ |

**User's choice:** Default: exit the app (checkbox unchecked).
**Notes:** Flagged that this changes currently-shipped Phase 8 behavior (X always hid to tray) — existing `settings.json` has no such field, so it deserializes as `false` on upgrade, meaning X will exit the app immediately after this ships until the user manually checks the new box. User confirmed explicitly this is intended, not an oversight, when asked a direct follow-up.

| Option | Description | Selected |
|--------|-------------|----------|
| Checkbox: "Closing the window (X) minimizes to tray" | Matches existing chkStartWithWindows pattern | ✓ |
| Two radio buttons | More explicit mutual-exclusivity, new control pattern | |

**User's choice:** Checkbox, matching existing pattern.

---

## Minimize Button Behavior + Default

| Option | Description | Selected |
|--------|-------------|----------|
| Same as Close-to-tray (Hide()) | Reuses existing mechanism, one code path | ✓ |
| Standard minimize + also in tray simultaneously | Two distinct states, more Windows-conventional | |

**User's choice:** Same mechanism as Close-to-tray (Hide()).

| Option | Description | Selected |
|--------|-------------|----------|
| Default: off (standard OS minimize) | Matches Phase 8's original D-03 distinction, no upgrade change | ✓ |
| Default: on | New default, changes today's minimize behavior on upgrade | |

**User's choice:** Default off.

---

## Settings UI Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Same section as "Start with Windows" | Groups tray-related preferences together | ✓ |
| New dedicated section | Keeps autostart visually distinct | |

**User's choice:** Same section as chkStartWithWindows.

---

## Interaction with Existing Tray Invariants

| Option | Description | Selected |
|--------|-------------|----------|
| Always show tray icon regardless of X setting | Simpler, one tray-icon lifecycle | |
| No tray icon at all when X is set to exit | Matches "fully non-tray app" mental model | ✓ |

**User's choice:** No tray icon when X is set to exit.
**Notes:** This raised a follow-up question — what about minimize-to-tray if X is set to exit? Resolved with a second question below.

| Option | Description | Selected |
|--------|-------------|----------|
| Icon shown if either setting uses tray | Tray icon exists whenever there's a live reason for it | ✓ |
| Icon tied only to X-button setting | Simpler rule, but makes minimize-to-tray a dead setting when X=exit | |

**User's choice:** Icon shown if either close-to-tray or minimize-to-tray is active (derived visibility).

| Option | Description | Selected |
|--------|-------------|----------|
| Allow autostart + X-exit combo as-is, no special handling | Both settings stay fully independent | ✓ |
| Warn in Settings if both are set that way | Extra UI surface for an edge case | |

**User's choice:** Allow as-is, no warning.

---

## Toast Notification Dependency (follow-up, surfaced mid-discussion)

| Option | Description | Selected |
|--------|-------------|----------|
| NotifyIcon always instantiated, .Visible toggled by D-08's derived rule | Object always exists; toasts require Visible=true so they go silent when tray is hidden | ✓ |
| Briefly flip Visible=true just to show a toast, then restore | Preserves toast functionality in every config, adds a flicker + explicit handling | |

**User's choice:** Always-instantiated, Visible-gated — accepting that toasts (NOTIF-01, Phase 9's hotkey toast) go silent when both tray settings are off.
**Notes:** Explicitly surfaced as a real consequence (this affects an already-shipped, Complete requirement — NOTIF-01) before the user chose. Not an oversight.

## Claude's Discretion

- Exact `AppSettings` field names (suggested `CloseMinimizesToTray`/`MinimizeToTray`, not locked).
- Exact mechanism for live tray-icon visibility updates on Settings-Save (direct property set vs. helper method).
- Exact WinForms event wiring for minimize interception (`Resize`/`SizeChanged` + `WindowState` check is the expected standard approach).

## Deferred Ideas

None — discussion stayed within phase scope.
