# Phase 16: Normal-Mode Explicit Monitor Config & Mode-Store Redesign - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-04
**Phase:** 16-normal-mode-explicit-monitor-config-mode-store-redesign
**Areas discussed:** Un-mentioned monitors in Normal set, Crash-recovery UX (DISPLAY-13), Settings UI layout for Normal-mode grid, Mode-marker corruption fallback

---

## Un-mentioned monitors in Normal set

| Option | Description | Selected |
|--------|-------------|----------|
| Left untouched | Matches Rig mode's existing convention exactly — true symmetry, no new mental model | ✓ |
| Explicitly enabled by default | Any monitor not mentioned gets turned on — diverges from how Rig mode already works | |

**User's choice:** Left untouched
**Notes:** Locked as D-01. Research (FEATURES.md) explicitly flagged this as needing a documented default rather than snapshot-fallback ambiguity.

---

## Crash-recovery UX (DISPLAY-13)

**Q1: How should the app tell you about a detected crash mid-toggle?**

| Option | Description | Selected |
|--------|-------------|----------|
| Dialog on startup | MessageBox on launch, impossible to miss | ✓ |
| Status banner in main window | Less interruptive, but easy to miss if starting minimized/tray-only | |
| Silent log only | Lowest friction, contradicts app's habit of surfacing failures | |

**User's choice:** Dialog on startup — locked as D-02.

**Q2: Should the dialog offer a recovery action, or just inform?**

| Option | Description | Selected |
|--------|-------------|----------|
| Inform only | User manually verifies and re-toggles | ✓ |
| Offer to retry the toggle | Convenient but risks repeating whatever caused the crash | |

**User's choice:** Inform only — locked as D-03.

---

## Settings UI layout for Normal-mode grid

**Q1: Where should the new grid live relative to the existing Rig-mode grid?**

| Option | Description | Selected |
|--------|-------------|----------|
| Stacked below | Both configs visible at once, no new navigation | ✓ |
| Side-by-side columns | Compact but cramps monitor-name text |  |
| Second tab | Full-width each, but hides one config while editing the other |  |

**User's choice:** Stacked below — locked as D-04.

**Q2: Should the new grid mirror the Rig grid's column-header + explanation-label convention exactly?**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, mirror exactly | "Off (Normal)/On (Normal)" headers + own explanation label | ✓ |
| Shared single explanation | Less repetition, breaks established one-label-per-grid convention |  |

**User's choice:** Yes, mirror exactly — locked as D-05.

---

## Mode-marker corruption fallback

**Q1: What should the app do if the new mode flag is missing/corrupted on launch?**

| Option | Description | Selected |
|--------|-------------|----------|
| Fail loudly | Matches existing corrupted-snapshot precedent, "never silently guess state" discipline | ✓ |
| Default silently to Normal mode | Simpler UX but risks confidently showing the wrong mode (Pitfall 4/5 territory) |  |

**User's choice:** Fail loudly — locked as D-06.

**Q2: When should this check fire, and what's the UX?**

| Option | Description | Selected |
|--------|-------------|----------|
| On app startup, blocking dialog | Catches the problem before any toggle risks compounding it | ✓ |
| Only when a toggle is attempted | Matches today's exact precedent, but UI silently shows a possibly-wrong mode until then |  |

**User's choice:** On app startup, blocking dialog — locked as D-07.

---

## Claude's Discretion

- Exact shape of the `IModeStore` abstraction (interface, file format, whether it's a new file or repurposes the existing snapshot location) — must be file-backed per Pitfall 4.
- Whether the "toggle in progress" marker is a separate file from the mode flag or folded into the same store.
- Exact code shape for preserving CR-01's recapture-and-compare safety net against the new mode store (the requirement to preserve it is locked, not the implementation).
- Exact wording of the two new dialogs (crash-recovery, mode-corruption) — tone/shape locked to match existing error messages, precise phrasing not locked.
- Whether `AppSettings` gains flat `NormalMonitorsToDisable`/`NormalMonitorsToEnable` fields or a nested `MonitorTarget`-shaped structure reused for both modes.

## Deferred Ideas

None — discussion stayed within phase scope.
