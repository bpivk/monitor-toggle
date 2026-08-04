# Phase 12: Theme Infrastructure & Live Theme-Following - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-02
**Phase:** 12-theme-infrastructure-live-theme-following
**Areas discussed:** MessageBox dialogs, Rounded corners/Mica style, Live-flip stale-color edge case

---

## MessageBox Dialogs

| Option | Description | Selected |
|--------|-------------|----------|
| Leave native | Accept the light MessageBox as-is even in dark mode. Zero extra work/risk. | ✓ |
| Dark title bar only | CBT-hook the MessageBox window handle, apply DWM dark-title-bar attribute, body stays native. | |
| Replace with custom themed dialog | Build a small custom Form to replace all 4 call sites, fully theme-matched. | |

**User's choice:** Leave native (Recommended)
**Notes:** These are small, infrequent, informational popups (toggle-result checklist, warnings) — not the main UI surface. Accepted as a deliberate tradeoff, not a gap to close later.

---

## Rounded Corners / Mica Style

| Option | Description | Selected |
|--------|-------------|----------|
| Mica | Standard Windows 11 app backdrop (File Explorer, Settings, most native Win11 apps). | ✓ |
| Mica Alt | Slightly stronger/darker tint variant, typically used for apps with a tab strip/denser chrome. | |
| No backdrop, just rounded corners | Skip the Mica blur, keep only DWM rounded-corner window shape. | |

**User's choice:** Mica (Recommended)
**Notes:** Applies to both MainForm and SettingsForm via `DWMWA_SYSTEMBACKDROP_TYPE`, alongside `DWMWA_WINDOW_CORNER_PREFERENCE` for rounded corners. Windows 10 / unsupported builds: attribute call is a no-op, not an error.

---

## Live-Flip Stale-Color Edge Case

| Option | Description | Selected |
|--------|-------------|----------|
| Accept as known limitation | Leave the stale ToolStrip/ContextMenuStrip separator/dropdown-arrow color after a live theme flip undocumented-fixed; no first-party fix exists (`dotnet/winforms#12027`). | ✓ |
| Rebuild ContextMenuStrip on theme change | Recreate the tray menu's ToolStripMenuItems from scratch on each live theme flip to force fresh brushes. | |

**User's choice:** Accept as known limitation (Recommended)
**Notes:** Affects only the tray right-click menu's separator line — a glance-and-dismiss surface. Rebuilding the menu on every theme flip was judged disproportionate effort for a cosmetic nit. Documented in code comments per this codebase's established rationale-comment convention.

---

## Claude's Discretion

- Exact shape of the theme-provider abstraction (`IThemeProvider`/`WindowsThemeProvider` split vs. a simpler static helper) — research recommends the interface split mirroring `IAutostartConfigurator`, left to planner.
- Where the per-control recolor pass (`ThemeApplier`) lives — research recommends `RigToggle.App`, left to planner.
- `UserPreferenceCategory` filter and UI-thread marshaling for `SystemEvents.UserPreferenceChanged` — left to planner/executor to verify against actual runtime behavior.
- `FlatStyle.System` vs `FlatStyle.Flat` per control, routing around the known `dotnet/winforms#13897` dark-mode button-coloring bug — left to planner.
- Whether `SettingsForm` is instantiated fresh per open or reused — needs a quick codebase check before planning the live-update subscription lifecycle.

## Deferred Ideas

None — discussion stayed within phase scope. THEME-07 (accent-color highlight), THEME-08 (custom toggle-switch control), and THEME-09 (manual theme override) remain correctly deferred to the v2 backlog per REQUIREMENTS.md; none were raised as in-scope asks during this discussion.
