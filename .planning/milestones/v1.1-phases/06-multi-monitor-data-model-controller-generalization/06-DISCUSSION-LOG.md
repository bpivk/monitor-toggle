# Phase 6: Multi-Monitor Data Model & Controller Generalization - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-28
**Phase:** 6-Multi-Monitor Data Model & Controller Generalization
**Areas discussed:** Enable-set behavior, Settings selection UI, Validation & confirmation wording, Required-fields & migration UX

---

## Enable-set behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-extend, native resolution | Let Windows/CCD place it via Extend-topology defaults, no manual arrangement UI | ✓ |
| Reuse Windows' last-known position | Reuse remembered mode/position if Windows has one, fallback to auto-extend | |
| User configures explicit position in Settings | Add a position/arrangement control to Settings | |

**User's choice:** Auto-extend, native resolution
**Notes:** Reuses the existing `ApplyTopology(Extend)` crash-recovery mechanism already proven in `WindowsMonitorController.Restore`.

| Option | Description | Selected |
|--------|-------------|----------|
| Always re-disable on restore | Toggle-back always returns enable-set monitors to OS-disabled, regardless of snapshot | ✓ |
| Restore whatever the pre-toggle snapshot says | Let the general snapshot-restore mechanism handle it uniformly | |

**User's choice:** Always re-disable on restore
**Notes:** Deliberately asymmetric from the disable-set's snapshot-based restore — documented as an intentional asymmetry, not an inconsistency to "fix."

---

## Settings selection UI

| Option | Description | Selected |
|--------|-------------|----------|
| Grid: 2 checkbox columns per row | One row per monitor, Disable/Enable checkbox columns (DataGridView) | ✓ |
| Two separate checked-list boxes | "Disable these" / "Enable these" CheckedListBoxes | |

**User's choice:** Grid: 2 checkbox columns per row
**Notes:** Ties both sets visually to the same monitor list.

| Option | Description | Selected |
|--------|-------------|----------|
| No — mutually exclusive, enforced live | Checking one column auto-prevents/unchecks the other for that row | ✓ |
| Allowed, Save blocked with an error | User can check both momentarily, Save stays disabled with a validation message | |

**User's choice:** No — mutually exclusive, enforced live
**Notes:** Prevents an unresolvable config from being expressible at all.

---

## Validation & confirmation wording

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — count enable-set monitors | Check is "will at least one monitor be active after rig-mode topology applies" | ✓ |
| No — only currently-active, non-disabled monitors count | Ignores the enable-set, could false-negative-block a valid all-rig-monitor setup | |

**User's choice:** Yes — count enable-set monitors
**Notes:** Matches actual post-toggle reality, not just the pre-toggle monitor list.

| Option | Description | Selected |
|--------|-------------|----------|
| Full comma-separated list | Always spell out every monitor name, no truncation | ✓ |
| Truncate with "and N more" past a threshold | Guards against a hypothetically long list | |

**User's choice:** Full comma-separated list
**Notes:** A personal rig never has enough monitors for length to matter.

---

## Required-fields & migration UX

| Option | Description | Selected |
|--------|-------------|----------|
| No — require disable-set OR enable-set non-empty | Generalizes past v1.0's "always disable exactly one" assumption | ✓ |
| Yes — disable-set must stay non-empty | Preserves v1.0's implicit assumption | |

**User's choice:** No — require disable-set OR enable-set non-empty
**Notes:** Audio + app path remain required either way; only the monitor-set requirement generalizes.

| Option | Description | Selected |
|--------|-------------|----------|
| Fully silent | Old settings.json loads, legacy field maps automatically, no dialog/toast | ✓ |
| One-time note in Settings | Small inline label the first time Settings opens post-upgrade | |

**User's choice:** Fully silent
**Notes:** Literal reading of DISPLAY-08 — "no re-configuration required."

---

## Claude's Discretion

- Exact migration mechanism (in `JsonSettingsStore.Load()` vs. a separate migration step in the composition root)
- Exact `DataGridView` column/control configuration to achieve the mutual-exclusivity grid
- Whether the enable-set is represented as a `List<string>` of device paths directly or a wrapper type

## Deferred Ideas

None — discussion stayed within phase scope.
