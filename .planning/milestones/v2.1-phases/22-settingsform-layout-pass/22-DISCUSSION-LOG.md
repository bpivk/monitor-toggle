# Phase 22: SettingsForm Layout Pass - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-11
**Phase:** 22-SettingsForm-Layout-Pass
**Areas discussed:** Reflow width vs. minimal spacing fix, TableLayoutPanel migration vs. keep absolute positioning, Room for Phase 23's upcoming radio group, Target window size / resizability

---

## Reflow width vs. minimal spacing fix

| Option | Description | Selected |
|--------|-------------|----------|
| Real 2-column reflow (recommended) | Move audio + app path panels beside the monitor grids' column, using full 828px width | |
| Minimal spacing fix only | Keep tall single-column stack, just fix overlap/spacing | |

**User's choice:** Neither preset option — free text: "Maybe put it side by side. One side is for normal mode and normal audio and the second for rig mode and audio. That would mean a 2 column reflow that would actually make sense"
**Notes:** User's own structure: group by MODE (Normal column, Rig column), not by category (all monitors, then all audio). This splits the current shared `pnlAudioDevices` panel apart, moving each mode's audio picker into that mode's own column.

**Follow-up: where do global (non-mode-specific) controls go?**

| Option | Description | Selected |
|--------|-------------|----------|
| Shared section below the two columns (recommended) | One full-width area for App path/Hotkey/debug logging/tray checkboxes | ✓ |
| Split further — sub-grouped within the shared area | Separate small boxes (e.g. "App & Hotkey", "Startup & Tray") | |

**User's choice:** Shared section below the two columns.

---

## TableLayoutPanel migration vs. keep absolute positioning

| Option | Description | Selected |
|--------|-------------|----------|
| Keep absolute positioning, new numbers (recommended) | Lower risk, matches every other form in the codebase | |
| Migrate to TableLayoutPanel/FlowLayoutPanel | More robust across DPI scales but new territory, flagged as a DPI pitfall in this milestone's research | ✓ |

**User's choice:** Migrate to TableLayoutPanel/FlowLayoutPanel.
**Notes:** Explicitly against the recommended (lower-risk) option — a deliberate call accepting the DPI-verification burden.

**Follow-up: scope of migration**

| Option | Description | Selected |
|--------|-------------|----------|
| Whole form (recommended for consistency) | One coherent layout system top to bottom | ✓ |
| Just the two-column area | Shared global section stays plain-Panel, closer to today's pattern | |

**User's choice:** Whole form.

---

## Room for Phase 23's upcoming radio group

| Option | Description | Selected |
|--------|-------------|----------|
| Reserve a slot now (recommended) | Add a row/cell in the TableLayoutPanel for the future radio group | ✓ |
| Stay fully independent | Phase 23 figures out placement whenever it starts | |

**User's choice:** Reserve a slot now.

**Follow-up: where should the reserved slot go?**

| Option | Description | Selected |
|--------|-------------|----------|
| In the shared global section (recommended) | Alongside App path/Hotkey/debug logging/tray checkboxes | ✓ |
| Its own small row near the top | Dedicated, more visible spot | |

**User's choice:** In the shared global section.

---

## Target window size / resizability

| Option | Description | Selected |
|--------|-------------|----------|
| Size to content, no fixed target (recommended) | Let TableLayoutPanel determine natural size | ✓ |
| Target a specific max size | Pin an explicit ceiling (e.g. 1366×768) | |

**User's choice:** Size to content, no fixed target.

**Follow-up: resizability**

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it non-resizable / fixed (recommended) | Matches today's FixedDialog behavior | |
| Allow resizing | Let the user drag-resize | ✓ |

**User's choice:** Allow resizing — explicitly against the recommended (matches-today) option.

**Follow-up: maximize button?**

| Option | Description | Selected |
|--------|-------------|----------|
| Sizable border only, no maximize (recommended) | Draggable edges, no maximize button | ✓ |
| Sizable + maximize button | Full standard resizable-window behavior | |

**User's choice:** Sizable border only, no maximize.

---

## Claude's Discretion

- Exact TableLayoutPanel row/column structure and cell sizing (Percent/AutoSize/Absolute).
- Whether MinimizeBox stays false or becomes true given the FormBorderStyle change to Sizable.
- Exact column width split between Normal and Rig columns (default: even split).
- How THEME-05's flat-bordered-Panel-as-GroupBox visual treatment carries into the new grouping.
- Exact DPI/rig-verification checklist steps for the 125%/150% scale checks.

## Deferred Ideas

None — discussion stayed within phase scope. Phase 23's radio group was discussed only as a layout-reservation question; building it remains Phase 23's own scope.
