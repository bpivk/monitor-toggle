# Phase 19: Monitor-Tile Dashboard & MonitorPanelForm Retirement - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-09
**Phase:** 19-Monitor-Tile-Dashboard-MonitorPanelForm-Retirement
**Areas discussed:** Tile visual design, MainForm sizing, Settings & Identify placement, Tile layout for multiple monitors

---

## Tile Visual Design

| Option | Description | Selected |
|--------|-------------|----------|
| Reuse the existing monitor motif | Same silhouette style as the normal-mode tray icon, IconGen's drawing code directly reusable | ✓ |
| Simpler glyph just for tiles | A plainer rectangle/monitor shape distinct from the tray icon | |
| Let me explain | — | |

**User's choice:** Reuse the existing monitor motif.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| Outline = off, filled = on | Hollow/outline for inactive, solid-filled for active | |
| Dimmed/grayed = off, full color = on | Same shape, dimmed/lower-opacity when off | |
| Let me explain | — | ✓ (freeform) |

**User's choice (freeform):** "Something that will quickly and at a glance show if it's on or off without any doubt."
**Notes:** Neither preset option was selected directly — user wanted stronger, unambiguous clarity. Followed up with a combined-signal question.

| Option | Description | Selected |
|--------|-------------|----------|
| Combined outline+color shift | Off = hollow gray outline, on = solid filled color — two independent signals | ✓ |
| Icon plus a separate status dot/badge | Keep icon shape constant, add a colored dot/badge like the old panel | |
| Let me explain | — | |

**User's choice:** Combined outline+color shift.
**Notes:** Directly addresses the "no doubt at a glance" requirement from the prior freeform answer.

| Option | Description | Selected |
|--------|-------------|----------|
| Small badge/marker | Subtle corner badge/label distinguishes the primary monitor | ✓ |
| No special marking needed | All tiles look identical regardless of primary/secondary | |
| Let me explain | — | |

**User's choice:** Small badge/marker.
**Notes:** None.

---

## MainForm Sizing

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed larger size | One bigger fixed window, no resize logic | |
| Auto-sizes to fit monitor count | Window width/tile area grows/shrinks based on detected monitor count | ✓ |
| Let me explain | — | |

**User's choice:** Auto-sizes to fit monitor count.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| Cap width, wrap extra tiles | Window grows to a max width, then extra tiles wrap to a second row | ✓ |
| Always one row, no cap | Window always grows wide enough to fit every monitor in one row | |
| Let me explain | — | |

**User's choice:** Cap width, wrap extra tiles.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| Window resizes live | Window resizes automatically on hotplug, matching auto-size behavior | ✓ |
| Fixed at launch size, tiles reflow inside | Window size set once at open, doesn't change afterward | |

**User's choice:** Window resizes live.
**Notes:** None.

---

## Settings & Identify Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Small icon-only gear button | Compact gear icon in a bottom corner | ✓ |
| Small labeled text button | Keeps "Settings" text, shrunk and repositioned | |
| Let me explain | — | |

**User's choice:** Small icon-only gear button.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| One shared button near the tiles | Single Identify button numbers every screen at once, matches old panel | ✓ |
| Per-tile identify action | Each tile gets its own identify affordance | |
| Let me explain | — | |

**User's choice:** One shared button near the tiles.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| Directly below the tile row | Tiles first, then Identify, then the toggle further below | ✓ |
| Directly above the tile row | Identify sits above the tiles as a utility action before monitor states are shown | |
| Let me explain | — | |

**User's choice:** Directly below the tile row.
**Notes:** Establishes the final top-to-bottom order: tiles → Identify → toggle → Settings (bottom).

---

## Tile Layout for Multiple Monitors

| Option | Description | Selected |
|--------|-------------|----------|
| Sorted by monitor number | Simple, stable ordering matching Windows' own numbering | ✓ |
| Left-to-right by physical position | Mirrors physical desk arrangement | |
| Let me explain | — | |

**User's choice:** Sorted by monitor number.
**Notes:** None.

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed tile size | Same size regardless of monitor count; more tiles = more wrapping | ✓ |
| Tiles shrink to fit more per row | Tile size scales down as monitor count grows | |
| Let me explain | — | |

**User's choice:** Fixed tile size.
**Notes:** None.

---

## Claude's Discretion

- Exact tile pixel dimensions, spacing, and the precise tile-count/pixel threshold that triggers wrapping to a second row.
- Exact visual treatment of the primary-monitor badge (icon/glyph, corner placement, size).
- Exact gear-icon geometry for the Settings button.
- Whether the live window-resize uses an animated transition or an instant resize (default: instant).
- Exact mechanism for porting `MonitorPanelForm`'s mutation logic, exclusive-access lease, hotplug subscription, and confirmation gating into `MainForm` — scoped by research (ARCHITECTURE.md, PITFALLS.md), not re-litigated in discussion.
- Whether the shared Identify/toggle/Settings controls shift position as the tile area grows to a second row — natural consequence of auto-sizing, left to planning.

## Deferred Ideas

None — discussion stayed within phase scope. THEME-08 (toggle-switch visual redesign) was explicitly acknowledged as Phase 20's scope, not this phase's.
