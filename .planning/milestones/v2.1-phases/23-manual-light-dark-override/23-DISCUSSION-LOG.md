# Phase 23: Manual Light/Dark Override - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-16
**Phase:** 23-Manual-Light-Dark-Override
**Areas discussed:** Apply timing, Radio labels & default framing, Scope of the override's reach

---

## Apply Timing

| Option | Description | Selected |
|--------|-------------|----------|
| Immediate (live preview) | Selecting Light/Dark repaints the app right away, before Save; Discard would need to revert it | ✓ |
| Save-gated (matches every other field) | Nothing takes effect until Save is clicked, consistent with the rest of the form | |

**User's choice:** Immediate (live preview)
**Notes:** Follow-up question asked what Discard should do to a previewed-but-unsaved override.

| Option | Description | Selected |
|--------|-------------|----------|
| Revert to last-saved override | Discard reverts the radio selection AND the live theme back to whatever was persisted before the dialog opened | ✓ |
| Leave the live preview applied | Discard reverts other fields but leaves the theme as previewed; resets on next app restart only | |

**User's choice:** Revert to last-saved override (recommended)
**Notes:** Matches Discard's existing meaning for every other field in SettingsForm.

---

## Radio Labels & Default Framing

| Option | Description | Selected |
|--------|-------------|----------|
| System / Light / Dark | Matches roadmap wording verbatim and Windows Settings > Colors terminology | ✓ |
| Follow Windows / Light / Dark | More explicit for unfamiliar users, deviates from roadmap/OS terminology | |

**User's choice:** System / Light / Dark

| Option | Description | Selected |
|--------|-------------|----------|
| Just pre-selected, no extra text | System checked by default, no additional label | |
| Pre-selected plus "(default)" suffix | e.g. "System (default)" — stays legible as default even after user changes selection | ✓ |

**User's choice:** Pre-selected plus "(default)" suffix

---

## Scope of the Override's Reach

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — all three, no exceptions | MainForm, SettingsForm, and MonitorConfirmDialog all resolve through one shared effective-theme resolver | ✓ |
| Something should stay live-only | User names a surface that should keep following live OS theme | |

**User's choice:** Yes — all three, no exceptions (recommended)
**Notes:** Directly implements Pitfall 6's prescribed fix; zero live-Windows-flip leakage anywhere once an override is set.

| Option | Description | Selected |
|--------|-------------|----------|
| No — out of scope, use judgment | The narrow MonitorConfirmDialog-open-during-override-change edge case doesn't need special handling; resolver reads fresh on each dialog open | ✓ |
| Yes — should be considered | Wanted in the rig-verification checklist | |

**User's choice:** No — out of scope, use judgment

---

## Claude's Discretion

- Exact shared "effective theme" resolver shape and placement (`OverridableThemeProvider` per research ARCHITECTURE.md)
- `AppSettings.ThemeOverride` type (`AppTheme?` nullable, per research recommendation, unless planning finds a reason otherwise)
- Exact radio group control layout inside the `pnlThemeReserved` slot (RadioButton stack vs. labeled row)
- Exact rig-verification checklist steps for the three-surface override-honoring scenario
- Implementation mechanics of Discard's revert-to-last-saved behavior (re-read from `ISettingsStore` vs. cached pre-open value)

## Deferred Ideas

None — discussion stayed within phase scope.
