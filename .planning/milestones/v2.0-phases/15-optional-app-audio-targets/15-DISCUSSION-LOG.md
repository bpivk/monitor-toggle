# Phase 15: Optional App & Audio Targets - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-04
**Phase:** 15-optional-app-audio-targets
**Areas discussed:** Unset affordance, Skipped-step display, Toggle-readiness gate, Broken-target UX

---

## Unset Affordance

### App path clear mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Add a Clear button | A small 'Clear' button/link next to the field, enabled only when a path is set. | ✓ |
| Right-click context menu | A 'Clear' item on a right-click context menu over the textbox. | |

**User's choice:** Add a Clear button
**Notes:** `txtAppPath` is read-only (SettingsForm.Designer.cs:295), so an explicit affordance is required — text can't be manually deleted.

### Audio "unset" representation

| Option | Description | Selected |
|--------|-------------|----------|
| Add a 'None' list item | Prepend an explicit '(None — don't switch audio)' entry to each dropdown. | ✓ |
| Allow blank/no selection | Let the combo box sit with nothing selected (SelectedIndex = -1). | |

**User's choice:** Add a 'None' list item
**Notes:** A blank dropdown risks reading as an unfinished form rather than a deliberate choice.

---

## Skipped-Step Display

### New outcome type

| Option | Description | Selected |
|--------|-------------|----------|
| New 'Skipped' outcome | Add a 4th ToggleStepOutcome distinct from NotAttempted. | ✓ |
| Reuse NotAttempted with distinct text | Keep 3 outcomes, give the skipped case its own message string. | |

**User's choice:** New 'Skipped' outcome
**Notes:** Matches the codebase's existing "never collapse two different states into one" convention.

### Row visibility

| Option | Description | Selected |
|--------|-------------|----------|
| Always show the row | All 3 rows (Monitor/Audio/App) always appear; unset ones read 'Skipped (not configured)'. | ✓ |
| Omit unset rows entirely | Only show rows for steps actually attempted. | |

**User's choice:** Always show the row
**Notes:** Keeps the checklist shape consistent toggle-to-toggle.

---

## Toggle-Readiness Gate

### Toggle button enablement

| Option | Description | Selected |
|--------|-------------|----------|
| Monitor set only | Only monitor-set configuration gates the toggle button; audio/app never block it. | ✓ |
| Keep requiring all fields | Toggle stays disabled until monitor + both audio + app are all configured. | |

**User's choice:** Monitor set only

### Settings Save button gating

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, monitor-gated only | Save enabled once monitor set is valid; blank audio/app never block it; broken-but-configured still blocks. | ✓ |
| Keep current all-fields gating | Save stays disabled unless every field validates, same as today. | |

**User's choice:** Yes, monitor-gated only

---

## Broken-Target UX

### Audio error message treatment

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, matching friendly message | Give audio the same friendly, actionable toggle-time failure message pattern the app path already has. | ✓ |
| Leave the raw exception message | Audio failures keep surfacing whatever NAudio/IPolicyConfig's exception message says. | |

**User's choice:** Yes, matching friendly message

---

## Claude's Discretion

- Exact enum/property shape for the new `Skipped` outcome
- Exact wording of the new audio-device-not-found message (must match app-path message's tone/shape)
- Exact placement/styling of the app-path Clear button and audio dropdowns' "(None...)" list item
- Whether audio-device-not-found detection happens via a pre-flight existence check or by catching/re-wrapping `SetDefault`'s exception

## Deferred Ideas

None — discussion stayed within phase scope.
