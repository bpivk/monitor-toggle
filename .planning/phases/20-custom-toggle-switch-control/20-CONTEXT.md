# Phase 20: Custom Toggle-Switch Control - Context

**Gathered:** 2026-08-10
**Status:** Ready for planning

<domain>
## Phase Boundary

The plain `btnToggle` `Button` ("Switch to Rig Mode" / "Switch to Normal Mode") is replaced by a custom-drawn, compact track+thumb toggle switch control paired with a static "Rig Mode" label in a Settings-row layout. The switch's position/fill communicates on/off state (Rig = on, Normal = off); it stays keyboard-operable (Tab focus, Space/Enter) and renders correctly themed in light/dark, including on tray-hidden startup. Does not touch accent-color live-following (Phase 21/THEME-07 — this phase reuses the existing fixed `AccentColor` placeholder), manual theme override (Phase 22), SettingsForm layout (Phase 23), or slide animation (explicitly out of scope per `REQUIREMENTS.md`). Requirement: THEME-08.

</domain>

<decisions>
## Implementation Decisions

### Size, Shape & Layout
- **D-01:** The switch is a **compact conventional toggle** (~50-60px), not a full-width pill matching today's 288px button — it reads as a standard Settings-style on/off control rather than retaining the plain button's full-width primary-action footprint.
- **D-02:** Track shape is a **rounded-rect pill** (fully rounded ends) with a circular thumb sliding left/right — the universally recognized toggle-switch silhouette, built via the stroke-then-fill GDI+ compositing this codebase already validated in Phase 13.
- **D-03:** Layout is a **label-left, switch-right row** ("Rig Mode" text, then the switch), matching the near-universal Settings-row convention (Windows Settings, iOS, GitHub Desktop).
- **D-04:** The **entire row is clickable** (label + switch both toggle on click), not just the small switch control itself — keeps the click target reasonably sized despite the compact switch.

### Label & lblMode
- **D-05:** Label text is **static "Rig Mode"** (names what the row controls, like a Settings toggle), not dynamic verb-based phrasing ("Switch to X") — the switch's own position/fill conveys current on/off state, so the label never needs to re-render on toggle.
- **D-06:** The existing `lblMode` ("Mode: Rig" / "Mode: Normal") above the tile row is **removed** — the switch row becomes MainForm's single source of truth for current-mode display, avoiding showing the same state twice on one small window.
- **D-07 (DISPLAY-11 interaction):** The existing "Unknown" mode state (mode file missing/corrupted — `!_orchestrator.IsModeKnown()`, currently `lblMode.Text = "Mode: Unknown"` + `btnToggle.Text = "Toggle"`) is represented as an **indeterminate switch position**: thumb centered/mid-track, track in a neutral/gray color, visually distinct from both Rig-on and Normal-on so it can never be mistaken for a real state. This preserves DISPLAY-11's "never guess Rig or Normal when mode is unknown" rule now that `lblMode` (D-06) is gone.

### On/Off Mapping & Colors
- **D-08:** **Rig mode = "on"** (thumb right, track filled); **Normal mode = "off"** (thumb left, track neutral) — Normal is the app's baseline/default state everywhere else in the project, so it maps naturally to the switch's neutral/off/left position.
- **D-09:** The ON-state track fill reuses the **exact same `AccentColor` placeholder** the tile dashboard's ON-state icon fill already uses (`Color.FromArgb(0, 90, 158)` dark / `SystemColors.Highlight` light, from `MainForm.AccentColor` / `ThemeApplier.ThemeMonitorTile`) — visually consistent with Phase 19's tiles today, and this same property becomes the live Windows accent color for free once Phase 21 (THEME-07) lands, requiring no rework here.
- **D-10:** The OFF-state track is a **neutral gray/outline**, matching Phase 19's D-02 tile convention (hollow/gray outline = off) — same "off" visual language app-wide.
- **D-11:** The thumb is a **white/contrasting solid circle** on top of the track, per standard toggle-switch convention (Windows, iOS, Android) — stays visible and high-contrast regardless of on/off state or light/dark theme.

### Hover/Press Feedback
- **D-12:** Hover/press interaction feedback **copies the existing owner-draw pattern exactly** — the same hover+press color-shift and accent focus ring already established in Phase 19 for the Identify/Settings buttons (`DrawButtonFocusRing`) — keeping visual language consistent across every interactive control on MainForm.

### Claude's Discretion
- Exact pixel dimensions of the compact switch (within the ~50-60px range) and the row's total height/spacing — informed by D-01 but not pinned to an exact number; should look proportionate next to the tile row above it.
- Exact indeterminate-state geometry for D-07 (how visually "centered" the thumb sits, precise gray shade) — must remain unmistakably distinct from both on and off per DISPLAY-11's "never guess" rule, exact values left to planning.
- Exact hover/press color deltas for D-12 — reuse the established pattern's mechanism, adapt the specific shift for the switch's track/thumb shape rather than a button's flat rectangle.
- Whether the row's vertical position shifts to fill the vertical space `lblMode`'s removal (D-06) frees up, or whether that space is preserved as breathing room — natural layout consequence, left to planning.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (THEME-08 — this phase's sole requirement; Out of Scope section explicitly excludes toggle-switch slide animation)
- `.planning/ROADMAP.md` (Phase 20 section — goal, 3 success criteria, "UI hint: yes", depends on Phase 19)
- `.planning/PROJECT.md` (Current Milestone: v2.1 section — full milestone framing)

### Research (this milestone — read before planning)
- `.planning/research/SUMMARY.md` §"Phase B: Custom Toggle-Switch Control (THEME-08)" — build sequencing, rationale for landing after the tile-dashboard work is stable
- `.planning/research/ARCHITECTURE.md` — `ToggleSwitch : Control` component definition, `ThemeApplier.ThemeToggleSwitch` extension pattern, Anti-Pattern 2 (do not add a second `SystemEvents` subscription — accent-color reading belongs to Phase 21, not this phase)
- `.planning/research/PITFALLS.md` — Pitfall 1 (theming pipeline miss — new control must be added to both `OnThemeChanged` AND `InitializeTrayState()`), Pitfall 2 (GraphicsPath seam artifacts — reuse validated stroke-then-fill compositing), Pitfall 9 (DPI/`AutoScaleMode.Font` pixel-math breakage — verify at 125%/150% scale on real hardware)
- `.planning/research/FEATURES.md` — "Toggle switch reads as two-state without relying on color alone" (table stakes), "Toggle-switch slide animation" (explicitly deferred/out of scope)

### Prior phases (precedent this phase must follow, not regress)
- `.planning/phases/19-monitor-tile-dashboard-monitorpanelform-retirement/19-CONTEXT.md` — D-02 (tile outline+fill on/off convention this phase's D-09/D-10 mirror), D-09 (current MainForm vertical ordering: tile row → Identify → toggle → Settings, this phase's switch replaces the toggle slot in that order)
- `.planning/milestones/v1.2-phases/13-tray-app-icon-redesign/13-CONTEXT.md` — stroke-then-fill GDI+ compositing precedent (D-02 above)
- `.planning/milestones/v1.2-phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — theme application must fire via both `OnHandleCreated`/`InitializeTrayState()` and `OnThemeChanged`, not `Form_Load`/`OnShown` alone

### Existing code (this phase's actual surface area)
- `src/RigToggle.App/MainForm.cs` / `MainForm.Designer.cs` — current `btnToggle` (`Button`, `FlatStyle.Flat`, 288x40, positioned via `Scaled(TogglePx)` below `btnIdentify`), `lblMode` (to be removed per D-06), `AccentColor` property (line ~182, reused per D-09), `RefreshUi()` (mode-dependent text/icon updates — the "Unknown" branch at lines ~360-367 is directly relevant to D-07), `BtnToggle_Paint`/`BtnToggle_Enter`/`BtnToggle_Leave`/`DrawButtonFocusRing` (hover/focus pattern reused per D-12)
- `src/RigToggle.App/ThemeApplier.cs` — `ThemeButton` (current toggle theming, to be replaced by a new `ThemeToggleSwitch` method), `ThemeMonitorTile` (AccentColor source precedent for D-09/D-10)
- `src/RigToggle.App/Controls/MonitorTile.cs` (Phase 19) — closest existing precedent for a custom `Control` with owner-draw `OnPaint`, GDI+ `using`-scoped resource discipline, and try/catch-wrapped paint body — the pattern this phase's `ToggleSwitch` control should follow structurally

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainForm.AccentColor` property (`IsDark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight`) — reused directly for the switch's ON-state track fill per D-09, zero new color literal introduced.
- `DrawButtonFocusRing` — existing accent-color focus-ring helper already used by `btnToggle`/`btnIdentify`/`btnSettings`; reused for the switch's keyboard focus indicator per D-12.
- `MonitorTile`'s owner-draw `OnPaint` structure (Phase 19) — GDI+ `using`-scoped shapes, try/catch-wrapped paint body with `Trace.WriteLine` — the established shape this new `ToggleSwitch` control should follow.

### Established Patterns
- Stroke-then-fill GDI+ compositing (Phase 13, reused Phase 19) — avoids the `GraphicsPath` seam-artifact bug; mandatory for the switch's track+thumb rendering (D-02).
- `ThemeApplier`'s explicit per-control theming, not a recursive Controls-tree walk — the new `ToggleSwitch` must be added to both `OnThemeChanged` and `InitializeTrayState()` call sites (Pitfall 1).
- Fail-loud, never-silently-guess for mode state (Phase 16 precedent, DISPLAY-11) — directly drives D-07's indeterminate-state requirement now that `lblMode` no longer exists as the "Unknown" signal.

### Integration Points
- `MainForm.RefreshUi()` — currently sets `lblMode.Text` and `btnToggle.Text` per mode; after this phase, the Unknown/Rig/Normal branches instead set the new switch control's state property (per D-07/D-08), and the `lblMode.Text` lines are deleted.
- `MainForm`'s layout method (`LayoutDashboard()`/similar, Phase 19) — the toggle's `Location`/`Size` calculation swaps from `btnToggle.Size = new Size(contentWidth, Scaled(TogglePx))` (full-width) to the new compact row's dimensions (D-01), freeing horizontal space that D-03's label occupies.

</code_context>

<specifics>
## Specific Ideas

- User confirmed the Settings-style toggle-row metaphor explicitly (compact switch + label, over a full-width pill) — the mental model is "a Windows Settings toggle row," not "a restyled primary button."
- User's framing for on/off mapping: Normal is the baseline/default state described everywhere else in the project, so it should map to the switch's neutral/off/left reading rather than "on."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Toggle-switch slide animation was already explicitly out of scope per `REQUIREMENTS.md` before this discussion started and was not re-litigated. Accent-color live-following (THEME-07) was explicitly deferred to Phase 21 per D-09's placeholder-reuse decision.

</deferred>

---

*Phase: 20-Custom-Toggle-Switch-Control*
*Context gathered: 2026-08-10*
