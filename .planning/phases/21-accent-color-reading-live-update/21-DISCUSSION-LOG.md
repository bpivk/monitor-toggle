# Phase 21: Accent-Color Reading & Live Update - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-10
**Phase:** 21-Accent-Color-Reading-Live-Update
**Areas discussed:** Ground-truth your accent color, Risk-hedging on the registry-key bet, Rig-verification workflow, Scope of accent-tinted elements

---

## Ground-truth your accent color

| Option | Description | Selected |
|--------|-------------|----------|
| I'll check and tell you now | Look up the exact swatch/hex and title-bar toggle state and share it | ✓ |
| I'll verify later on the rig | Proceed on research's best-guess, verify after the build | |
| Doesn't matter, use whatever's technically correct | Not fussed about the exact swatch right now | |

**User's choice:** "I'll check and tell you now" → provided free text: **"manual but blue"** (accent mode is manually set, not automatic-from-background; color family is blue, not the Windows default). Follow-up: "Show accent color on title bars and window borders" is **On**.
**Notes:** Confirms the rig's actual accent is a custom (non-default) value, matching the roadmap's Success Criterion 3 requirement to work "for a custom (non-default) accent color." Title-bar-tinting being ON reduces (but doesn't eliminate, per PITFALLS.md) the risk of ColorizationColor/AccentColor divergence.

---

## Risk-hedging on the registry-key bet

| Option | Description | Selected |
|--------|-------------|----------|
| One primary source, fix later if wrong (recommended) | AccentColor registry key primary, DwmGetColorizationColor fallback; treat as hypothesis, fix-forward if disproven | ✓ |
| Build both paths with comparison/diagnostic logging | Read both sources up front, log both, pick via explicit rule | |

**User's choice:** One primary source, fix later if wrong.
**Notes:** Matches this codebase's existing convention (one clear source + graceful fallback) over defensive multi-path logic.

---

## Rig-verification workflow

| Option | Description | Selected |
|--------|-------------|----------|
| I'll rig-verify myself, report back (recommended) | User runs the rig checklist, reports PASS/FAIL, gap-closure round if needed before phase marked done | ✓ |
| Mark done pending my verification, move on | Implementation marked complete without blocking on rig verification | |

**User's choice:** I'll rig-verify myself, report back.
**Notes:** Matches how Phase 12/13 closed historically — implement + write checklist, user runs it live, gap-closure if something fails.

---

## Scope of accent-tinted elements

| Option | Description | Selected |
|--------|-------------|----------|
| Keep it to today's existing set (recommended) | Tiles, toggle switch, Identify/Settings focus rings go live-accent; title bar stays light/dark-only | ✓ |
| Also accent-tint the title bar/window border | Extend DwmTitleBar.cs with DWMWA_CAPTION_COLOR using live accent | |

**User's choice:** Keep it to today's existing set.
**Notes:** Title-bar accent-tinting was raised as an option and explicitly declined as new scope beyond the roadmap's framing — logged as a deferred idea, not folded into this phase.

---

## Claude's Discretion

- Exact registry-value parsing/masking (0xAARRGGBB unpacking, alpha stripping).
- Whether `AccentColorChanged` needs defensive periodic re-check alongside the event subscription (only if event-only fails rig verification).
- Exact diff/no-op logic for same-color re-selection.

## Deferred Ideas

- Accent-tinted DWM title bar/window border (`DWMWA_CAPTION_COLOR`) — considered and explicitly declined as out of this phase's scope; not assigned to any future phase, noted for a possible future backlog item.
