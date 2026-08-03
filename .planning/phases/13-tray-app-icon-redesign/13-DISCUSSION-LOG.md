# Phase 13: Tray & App Icon Redesign - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-03
**Phase:** 13-tray-app-icon-redesign
**Areas discussed:** Icon Motif & Style, Color accents & contrast

---

## Icon Motif & Style

| Option | Description | Selected |
|--------|-------------|----------|
| Monitor on/off (evolve current idea) | Keep the monitor-based metaphor but make states genuinely different shapes (filled vs. X'd/dimmed) | |
| Steering wheel vs. desktop monitor | Rig mode = steering wheel silhouette; normal mode = desktop monitor silhouette | ✓ |
| Something else | User describes their own motif | |

**User's choice:** Steering wheel vs. desktop monitor
**Notes:** Chosen for strong shape contrast at 16px and direct mapping to the user's actual sim-racing setup, rather than an abstract variation on the same glyph.

| Option | Description | Selected |
|--------|-------------|----------|
| Flat filled, Fluent-style | Solid shapes, matches Windows 11 native icons and Phase 12's flat-modern app theming | ✓ |
| Outline/line-art | Stroke-only silhouette | |
| You decide | Claude picks based on legibility at 16px | |

**User's choice:** Flat filled, Fluent-style

| Option | Description | Selected |
|--------|-------------|----------|
| Procedurally drawn in code | GDI+/System.Drawing geometric shapes, same approach as current icons | ✓ |
| I'll supply the icon files myself | User provides finished artwork | |
| Something else | e.g. AI image generation, icon font/library | |

**User's choice:** Procedurally drawn in code

| Option | Description | Selected |
|--------|-------------|----------|
| Normal-mode monitor icon | .exe/taskbar icon matches the app's default-launch state | ✓ |
| A combined/neutral motif | New third design blending both | |
| Rig-mode wheel icon | Leads with the app's differentiating purpose | |

**User's choice:** Normal-mode monitor icon

---

## Color accents & contrast

| Option | Description | Selected |
|--------|-------------|----------|
| Monochrome silhouette | White/light-gray fill + dark outline, matches native Windows tray icon convention | ✓ |
| Keep an accent color per mode | Distinct fill color per mode (e.g. carry forward current orange) as secondary cue | |

**User's choice:** Monochrome silhouette

| Option | Description | Selected |
|--------|-------------|----------|
| Outlined/stroked silhouette | Self-contained contrast via opposite-tone outline, works on any taskbar | ✓ |
| Solid fill only, no outline | Simpler but risks disappearing on same-tone taskbar | |

**User's choice:** Outlined/stroked silhouette

| Option | Description | Selected |
|--------|-------------|----------|
| Give the exe icon color | Dark-gray body + subtle accent, following the taskbar-vs-tray color convention (e.g. VS Code) | ✓ |
| Keep it monochrome too | Same treatment as tray icons, just larger | |

**User's choice:** Give the exe icon color

---

## Claude's Discretion

- Exact stroke width, corner radius, and geometric proportions of both silhouettes — to be validated against legibility at 16px and the 16/20/24/32px multi-resolution requirement.
- Exact color values for the `.exe`/taskbar icon's color treatment.
- Whether icon generation happens via a one-time script producing checked-in `.ico` files, or a build-time step.
- File/resource naming and embedding mechanism — reuse or evolve the existing `LogicalName` embedded-resource pattern.

## Deferred Ideas

None — discussion stayed within phase scope.
