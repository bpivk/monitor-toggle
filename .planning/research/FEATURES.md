# Feature Research

**Domain:** WinForms desktop-utility UI redesign — status-tile dashboard replacing a grid-based panel, a settings-layout pass, and three theme-backlog capabilities (toggle switch, accent color, manual override)
**Researched:** 2026-08-09
**Confidence:** MEDIUM-HIGH (dashboard/tile conventions verified against Windows' own Settings > Display, DisplayFusion, NirSoft MultiMonitorTool, and hardware-control dashboards — Corsair iCUE, Razer Synapse — via WebSearch cross-referenced with official Microsoft docs; theme-override three-way pattern verified against GitHub Desktop and Windows Terminal's official docs, both named, both independently corroborated; existing-app dependency claims verified by direct inspection of current `RigToggle.App` source, not training data)

This supersedes the stale v2.0-dated `FEATURES.md` (2026-08-04, which covered optional toggle targets, explicit Normal-mode monitor config, and the now-being-retired `MonitorPanelForm`). This file covers the **v2.1 milestone only**: MainForm becomes a monitor-tile dashboard that absorbs `MonitorPanelForm`'s functionality and retires it, SettingsForm gets a pure layout pass (no new fields), and three theme-backlog items (custom toggle switch, accent color, manual light/dark override) close out. It assumes everything through v2.0 (optional targets, explicit per-mode monitor sets, the live monitor panel being retired, live OS theme-following) is already built and working per `.planning/PROJECT.md`.

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these = product feels incomplete, or worse, regresses capability the user already had in v2.0.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| One tile per detected monitor (icon + number), status shown via icon not text | Direct carryover of `MonitorPanelForm`'s already-shipped status-dot convention (PANEL-01) and Windows' own Settings > Display numbered-tile pattern — this is a *relocation and restyling* of an existing capability, not new functionality | LOW-MEDIUM | The status-dot bitmap generation (`CreateStatusDot`, `MonitorPanelForm.cs:77-86`), the `GetAllMonitors()` data path, and the primary/OS-disabled suffix logic are all directly reusable; the new work is layout (tiles vs. grid rows) and hosting them in `MainForm` instead of a separate `Form` |
| Clicking a tile toggles that monitor directly (Disable if active, Enable if not) | Explicit scope requirement; matches MultiMonitorTool's checkbox-per-monitor and DisplayFusion's per-monitor enable list — direct manipulation on the status element itself (not a separate button column) is the natural evolution once the row becomes a tile | LOW | Same underlying call (`IMonitorController.ActivateMonitors`/`DeactivateMonitors`) as today — only the click target changes from a grid action-column cell to the tile itself |
| `SkipMonitorConfirmation` gate still applies when disabling from a tile | Explicit scope requirement (PANEL-04's existing behavior must not regress); users who already opted into "don't ask again" must not get re-prompted just because the trigger UI moved | LOW | Reuse `MonitorConfirmDialog` unchanged — do not invent a second, tile-specific confirmation flow (see Anti-Features) |
| Identify affordance lives near the tiles, still overlays a number on each physical screen | Explicit scope requirement; carried over from PANEL-05, just relocated | LOW | `MonitorIdentifyOverlay` + `CaptureState()` logic is unchanged; only the trigger button's position moves |
| Tiles reflect live hotplug state while MainForm is open | `MonitorPanelForm` already does this (PANEL-03, `SystemEvents.DisplaySettingsChanged`); folding the capability into MainForm without keeping "live" would be a regression, not a redesign | LOW-MEDIUM | MainForm doesn't currently subscribe to `DisplaySettingsChanged` — this is a genuinely new wire-up on MainForm, even though the underlying pattern is copy-paste from `MonitorPanelForm.OnDisplaySettingsChanged` |
| Empty/degenerate state (zero monitors enumerable) doesn't crash or show a blank dashboard | `MonitorPanelForm` already handles this (`lblEmptyState`) — must carry the same defensive posture into MainForm's primary view, which is now unavoidable (no separate window to fail gracefully off to the side) | LOW | Direct port of the existing `PopulateMonitorGrid` empty-state branch |
| Primary monitor and OS-disabled monitors are visually distinguishable on the tile itself, not just in a tooltip | Carryover of the existing `" (Primary)"`/`" (currently OS-disabled)"` suffix logic — a tile view loses grid-row text space, so this must become an icon/badge, not silently drop the information | MEDIUM | Requires a small icon-badge design decision (e.g. a corner marker), not just porting text |
| Mode toggle button positioned after/below the tile row, tiles are the first thing seen | Explicit, literal scope requirement — this is the core "what does the user see first" redesign goal | LOW (layout only) | No functional change to `ToggleOrchestrator` calls, only visual hierarchy |
| Settings entry point moved to a secondary/bottom position, visually de-emphasized | Explicit scope requirement; matches the general pattern in comparable hardware-control dashboards (iCUE, Razer Synapse) where per-device status/action is the primary surface and app-wide configuration is a lower-emphasis secondary entry point, not co-equal with the primary action | LOW | Pure layout/visual-weight change — smaller button, less saturated color, or an icon-only affordance instead of a full-width button |
| `MonitorPanelForm`'s two entry points (MainForm button, tray context-menu item) are both removed, not just one | Explicit scope requirement — a dangling tray-menu item pointing at a form that's supposedly retired would be a real regression a user would notice immediately (tray menu is used constantly per the project's automation-first design) | LOW | `MainForm`'s tray `ContextMenuStrip` "Monitors" item and its handler, `_monitorPanelFormFactory`, and the `MonitorPanelForm`/`.Designer.cs` files themselves are all removal candidates — but see Dependencies below on what to keep vs. delete |
| SettingsForm: no overlapping controls at the form's default size | This is the literal bug being fixed — "cramped," "bolted on," "overlaps at some points" per the user's own description in PROJECT.md/milestone context | LOW-MEDIUM | Anchor/dock and `TableLayoutPanel`/spacing fixes; no new controls, no new logic |
| SettingsForm: controls grouped logically (Rig monitors / Normal monitors / Audio / App / Hotkey read as distinct sections) | Standard convention for any multi-category Windows settings dialog (Windows own Settings, VS Code Settings, every non-trivial preferences window) — the existing `GroupBox`/`Panel` structure already expresses this intent, it's just not spaced/sized well enough to read cleanly today | LOW | Existing `GroupBox` boundaries are very likely the right grouping already (per the milestone context describing them) — the fix is spacing/sizing within and between them, not regrouping |
| Manual theme override: exactly a System / Light / Dark three-way choice, "System" is the default | Near-universal convention in comparable Windows desktop apps (GitHub Desktop, Windows Terminal, Microsoft 365 apps, Docker Desktop, Microsoft Teams) — deviating from this (e.g. a plain on/off toggle with no "follow OS" option) would read as a regression from THEME-01..06's already-shipped live-follow behavior | LOW-MEDIUM | Needs a new persisted setting (Stack research's concern) but the *shape* of the choice (3 options, System first/default) is what users expect — a binary Light/Dark-only toggle is explicitly not sufficient |
| Manual override change takes effect immediately, without restart | Matches the existing live-theme-follow precedent already shipped (THEME-01..06 re-theme on a live OS flip without restart) — a manual override that requires restart would be a worse experience than the OS-follow behavior it's meant to coexist with | LOW-MEDIUM | Reuses the existing `ThemeApplier`/`OnThemeChanged`-style re-theme plumbing already present in `MainForm`, `SettingsForm`, and (currently) `MonitorPanelForm` |
| When set to "System," OS theme changes still live-update the app exactly as today | This is the coexistence contract explicit in the milestone's own framing ("independent of live Windows theme-follow") — System must not become a one-time read, it must remain the existing live-subscribe behavior | LOW | No change to `IThemeProvider`/`WindowsThemeProvider`'s existing `ThemeChanged` event; the override sits as an interposing layer, not a replacement |
| When locked to Light or Dark, a live OS theme flip does NOT silently override the user's explicit choice | The other half of the same coexistence contract — without this, "manual override" wouldn't actually override anything, it would just be a temporary preview that OS changes can clobber | LOW-MEDIUM | Every current `ThemeChanged` subscriber (`MainForm.OnThemeChanged`, `SettingsForm`'s equivalent) reads `_themeProvider.CurrentTheme` directly today — an override layer needs to sit between the raw OS signal and what those consumers treat as "current theme," or all of them need updating in lockstep |
| Custom toggle-switch control reads unambiguously as a two-state control, in both states, without relying on color alone | This control is replacing the app's single most important action (mode toggle) — the project's own established convention (Phase 13's explicit "shape-distinct, colorblind-safe, no color-only differentiation" decision for the tray icons) should extend to this control, since it now carries equivalent weight | LOW-MEDIUM | Needs a track+thumb position difference (not just color) so state is legible without color perception — e.g. thumb-left/thumb-right, not same-position-different-color |

### Differentiators (Competitive Advantage)

Features that set the product apart. Not required, but valuable — and appropriate given this milestone is explicitly about moving from "bolted-on" to "intentional, modern."

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Tile hover/pressed visual feedback (subtle elevation, border highlight) | Signals "this is clickable" before the user commits to a click — standard modern-desktop-app affordance (seen in iCUE/Synapse device cards, Windows 11's own Settings tiles) that a flat, unstyled DataGridView row does not communicate as clearly | LOW-MEDIUM | The project already has `FlatStyle.Flat` + explicit hover/pressed color override precedent from Phase 12 (working around `dotnet/winforms#13897`) — the same technique pattern likely extends to a custom tile control |
| Accent-color highlight on the toggle switch's "on" state and tile hover/selection states | Matches modern Windows 11 visual language (Windows itself uses the user's accent color sparingly, on interactive/selected elements — not as a background wash) — makes the redesigned UI feel native to the current OS rather than hand-picked-palette, which is exactly the "intentional, modern" goal stated for this milestone | MEDIUM | Scope this narrowly: apply accent color to the toggle switch's on-state and interactive-element highlights only, not as a broad recolor of neutral surfaces — over-application is a known anti-pattern (see Anti-Features) |
| Live accent-color follow (re-reads if the user changes their Windows accent color while the app is running) | Consistent with the project's existing "live theme-follow, not read-once-at-startup" philosophy already established for light/dark (THEME-01..06) | LOW-MEDIUM (once the underlying read mechanism exists — a Stack-research concern) | Same event-driven re-theme plumbing (`ThemeChanged`-style) the app already has, extended to a second signal |
| Animated slide transition on the toggle switch (not just an instant redraw) | Purely a polish differentiator — a smooth thumb-slide reads as noticeably more "modern/native" than a static two-state repaint, and this app's core action is exactly the kind of single, high-visibility interaction worth polishing | MEDIUM (WinForms has no built-in easing/animation primitive — needs a `Timer`-driven repaint loop) | Not required for the control to be functionally correct; treat as an enhancement to accept or explicitly defer, not a blocker for shipping THEME-08 |
| Keyboard operability of tiles (Tab focus order, Space/Enter to toggle a focused tile) | Not explicitly scoped, but a genuine accessibility/quality bar for a redesigned primary-interaction surface, and cheap once the tiles are real custom controls (vs. a DataGridView, which already had free keyboard nav that a naive tile redesign could regress) | LOW-MEDIUM | Flag as a candidate the roadmap should explicitly accept or defer — don't let it get silently lost in the grid-to-tile migration |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem good but create problems, or reopen scope this project has already deliberately closed.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|------------------|-------------|
| Drag-to-rearrange tiles to match physical desk layout | Feels natural once monitors are spatial tiles instead of grid rows — "let me put them in the order they sit on my desk" | This is exactly the topology/arrangement-editing scope the project already explicitly rejected for `MonitorPanelForm` ("do not build a full drag-and-drop position/arrangement editor — Windows' own Display settings already owns that job") — a tile layout makes the temptation stronger, not the rejection weaker | Fixed enumeration order (same stable order the grid used, keyed by `DevicePath` as today) — Identify already solves "which physical screen is which" without needing spatial rearrangement |
| A second, tile-specific confirmation dialog or a "quick disable, ask later" fast path that bypasses `SkipMonitorConfirmation` | Direct-manipulation UIs sometimes invite "make the primary action even faster" requests | Would fragment the safety gate into two divergent implementations for the same underlying risk (disabling the wrong/only monitor) — exactly the kind of drift the project's DISPLAY-12 "single shared codepath" decision was designed to prevent | Reuse `MonitorConfirmDialog` and `SkipMonitorConfirmation` exactly as-is; the tile is a new trigger for an unchanged gate, not a reason to add a second gate |
| Reusing the new toggle-switch control (THEME-08) as the per-monitor tile status indicator too, for visual consistency | "We already built a nice switch control, why not use it everywhere there's an on/off state?" | Semantically different actions: the mode toggle is a single global action with binary state; a monitor tile is one of N independently-toggleable hardware items with its own status *and* identity (name, primary/disabled badge, click target) that a bare switch control can't carry — collapsing them would either bloat the switch control with tile-only concerns or strip the tile of information it currently has | Keep the tile as its own control (icon + number + status + click target), and the switch as its own control (single global mode action) — they can share a visual language (same accent color, same corner radius/stroke weight) without being the same component |
| Applying accent color broadly across all interactive elements, panels, and backgrounds | "More accent color = more modern/branded" | Windows 11 itself uses accent color sparingly (selection highlights, toggles, focus rings) — over-applying it fights the OS's own visual language and can clash badly with some user-chosen accent colors (very saturated or very dark choices read poorly as large fill areas) | Scope accent color to the toggle switch's on-state and small interactive highlights only, as already noted under Differentiators |
| A four-option theme setting (System / Light / Dark / "Auto by time of day" or per-schedule) | Feature creep once a theme-settings UI exists — "while we're in there, why not add scheduling" | Out of scope for this milestone (THEME-09 is explicitly "manual override... independent of live Windows theme-follow," not a scheduling feature), and Windows itself doesn't offer time-based scheduling as a first-party feature this app would need to interoperate with — building it would be inventing a feature no comparable app in this survey has | Ship the standard 3-option System/Light/Dark pattern only; if time-based theming is ever wanted, it's a distinct future feature, not a THEME-09 variant |
| Re-tabbing SettingsForm into a multi-tab/wizard flow as part of "the layout pass" | Tempting scope creep once a human is already in the file fixing spacing — "while we're here, let's really redesign this" | Milestone scope is explicitly "layout pass... no new settings fields needed for this — just layout" — restructuring into tabs changes navigation model and discoverability (a category the user could miss entirely on first use), which is a bigger, riskier change than what was asked for | Fix spacing/grouping/overlap within the existing GroupBox/Panel structure; if a tabbed/categorized nav redesign is wanted later, scope it as its own explicit decision, not folded silently into a "spacing fix" |

## Feature Dependencies

```
[MainForm tile dashboard]
    └──requires──> [MonitorPanelForm's existing capability: GetAllMonitors(),
                     ActivateMonitors/DeactivateMonitors, status-dot rendering,
                     SkipMonitorConfirmation gate via MonitorConfirmDialog,
                     ToggleOrchestrator.BeginExclusiveMonitorAccess lease,
                     DISPLAY-12 safety guard (unchanged, lives in
                     WindowsMonitorController)]
    └──requires──> [new: MainForm subscribes to SystemEvents.DisplaySettingsChanged
                     directly -- MonitorPanelForm currently owns this subscription
                     and MainForm does not]
    └──enables───> [retirement of MonitorPanelForm + both its entry points]

[Retirement of MonitorPanelForm]
    └──requires──> [MainForm tile dashboard shipping first / same phase --
                     the capability must land before the standalone window and
                     its two entry points (MainForm button, tray menu item) can
                     be safely removed without a capability gap]

[Identify action relocated near tiles]
    └──requires──> [MonitorIdentifyOverlay, MonitorController.CaptureState() --
                     unchanged, only the trigger's position/host form changes]

[Mode toggle button repositioned below tile row]
    └──independent of the above──> [pure layout change against the existing
                     ToggleOrchestrator/IModeStore-derived mode display]

[THEME-08: custom toggle-switch control]
    └──enhances──> [Mode toggle button repositioning -- the control being
                     replaced and the control being repositioned are the same
                     button, so these should land together or in adjacent
                     phases, not far apart]
    └──enhanced-by──> [THEME-07: accent color, for the switch's "on" state]

[THEME-09: manual light/dark override]
    └──requires──> [interposing layer between IThemeProvider's raw OS signal
                     and what MainForm/SettingsForm/tile-dashboard consumers
                     treat as "current theme" -- every existing ThemeChanged
                     subscriber is a call site that must respect the override,
                     not just read CurrentTheme directly]
    └──conflicts-with-if-done-wrong──> [THEME-01..06's live-OS-follow behavior --
                     the two must be reconciled (System = live-follow as today;
                     Light/Dark = locked, ignore live OS flips) not left to
                     silently race]

[THEME-07: accent color]
    └──independent of THEME-08/09's coexistence question──> [additive visual
                     layer; does not change the theme-resolution logic THEME-09
                     introduces]

[SettingsForm layout pass]
    └──independent of all monitor-tile/theme-backlog work──> [touches only
                     existing controls' Location/Size/Anchor/Dock and
                     GroupBox/Panel structure; zero AppSettings model changes]
```

### Dependency Notes

- **The tile dashboard must land before `MonitorPanelForm` retirement, not alongside it as a single atomic change if it can be avoided:** the milestone explicitly retires the panel and both its entry points, but doing so before the tile dashboard fully covers PANEL-01 through PANEL-05's existing behavior would be a real, user-visible capability regression (no way left to enable/disable a single monitor on demand). Sequence so the replacement is proven working before the original is deleted.
- **MainForm currently has no `SystemEvents.DisplaySettingsChanged` subscription** — this is the one piece of "live" behavior that doesn't already exist somewhere reusable on `MainForm` itself; every other capability being folded in (status dots, click-to-toggle, Identify, the confirmation gate) has a directly portable existing implementation in `MonitorPanelForm.cs`.
- **THEME-09's real complexity is the interposing-layer question, not the settings UI:** the 3-option System/Light/Dark radio/combo itself is a small, well-understood UI (see Table Stakes). The dependency risk is that `IThemeProvider.CurrentTheme`/`ThemeChanged` is read directly today by at least `MainForm`, `SettingsForm`, and `MonitorPanelForm` (soon: the tile dashboard) — a manual override needs a single point of truth for "effective theme" that all of these consult, or the override will apply inconsistently across the app's surfaces (e.g. MainForm honors the override but a dialog doesn't).
- **THEME-08 and the mode-toggle repositioning are the same button** — building the custom switch control against the button's *current* position and then moving it, or vice versa, is redundant churn; sequence them in the same phase or with the switch-control work immediately preceding the layout move.
- **SettingsForm's layout pass has zero dependency on the theme-backlog or dashboard work** and could ship in an earlier or later phase independently — it's a self-contained visual change against existing controls, matching this milestone's own framing ("no new settings fields needed for this — just layout").

## MVP Definition

This is a fixed-scope milestone (all six target-feature groups below are already committed per PROJECT.md), so this section reframes as **sequencing risk**, not a trim decision — same convention the superseded v2.0 FEATURES.md used ("MVP-equivalent Scoping").

### Ship This Milestone (all committed)

- [ ] Monitor-tile dashboard on MainForm (icon + number + live status, click-to-toggle, Identify relocated, `SkipMonitorConfirmation` preserved) — this is the milestone's headline capability and the highest-complexity item; land it first among the UI-facing work so the panel-retirement decision has something proven to retire onto
- [ ] `MonitorPanelForm` + both entry points removed — only after the dashboard above is confirmed to cover PANEL-01 through PANEL-05
- [ ] Mode toggle button relocated below/after the tile row — low complexity, sequence with THEME-08 to avoid redundant churn on the same control
- [ ] Settings entry point de-emphasized/relocated to secondary position — low complexity, part of the same MainForm layout work
- [ ] SettingsForm layout pass (no new fields) — independent, can slot anywhere in the phase sequence
- [ ] THEME-08 custom toggle-switch control — depends on landing near the mode-toggle relocation
- [ ] THEME-07 accent-color highlighting — additive, can follow THEME-08 to have a concrete "on" state to color
- [ ] THEME-09 manual light/dark override — the riskiest of the three theme items due to the interposing-layer dependency noted above; do not treat as "just another settings checkbox," scope its own verification pass against every existing theme-consuming surface

### Explicitly Not This Milestone

- [ ] Toggle-switch slide animation — nice-to-have polish, defer if time-constrained without blocking THEME-08's functional shipment
- [ ] Tile keyboard operability (Tab/Space/Enter) — flag for roadmap to explicitly accept or defer; don't let it silently vanish in the grid-to-tile migration, but it's not blocking
- [ ] Any accent-color application beyond the toggle switch + small interactive highlights — explicitly scoped narrow per Anti-Features
- [ ] Time-of-day/scheduled theme auto-switching — not part of THEME-09's scope, a distinct future feature if ever wanted
- [ ] SettingsForm tab/wizard restructuring — explicitly out of scope for "layout pass"; a future, separately-decided redesign if ever wanted

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Monitor-tile dashboard (replaces panel) | HIGH | MEDIUM | P1 |
| `MonitorPanelForm` + entry-point retirement | HIGH (removes confusion of two places to do the same thing) | LOW | P1 |
| Mode toggle relocated below tiles | MEDIUM | LOW | P1 |
| Settings entry point de-emphasized | LOW-MEDIUM | LOW | P1 |
| SettingsForm layout/spacing pass | HIGH (directly fixes a named user complaint) | LOW-MEDIUM | P1 |
| THEME-08 custom toggle switch | MEDIUM-HIGH (this is now the app's single most-used control) | MEDIUM | P1 |
| THEME-09 manual light/dark override | MEDIUM | MEDIUM (mostly in the interposing-layer risk, not the UI) | P1 |
| THEME-07 accent-color highlighting | MEDIUM | MEDIUM | P1 |
| Tile hover/pressed feedback | MEDIUM | LOW-MEDIUM | P2 |
| Live accent-color follow (re-read on OS change) | LOW-MEDIUM | LOW-MEDIUM | P2 |
| Toggle-switch slide animation | LOW | MEDIUM | P3 |
| Tile keyboard operability | LOW-MEDIUM | LOW-MEDIUM | P2 |

**Priority key:**
- P1: Committed this milestone per PROJECT.md's target-feature list
- P2: Worth doing well since it's cheap relative to value, but not explicitly scoped — candidate for the roadmap to accept or defer
- P3: Pure polish, defer without concern if time-constrained

## Competitor Feature Analysis

| Feature | Windows Settings > Display | DisplayFusion / MultiMonitorTool | Corsair iCUE / Razer Synapse | Our Approach |
|---------|------------------------------|-----------------------------------|-------------------------------|--------------|
| Per-item status representation | Numbered rectangle tiles, spatial layout matching physical arrangement | Checkbox/grid row per monitor (MultiMonitorTool); named profile list (DisplayFusion) | Device card per connected peripheral, clickable into a deeper per-device page | Icon+number tile, fixed stable order (not spatial/draggable), click toggles directly rather than opening a deeper page — flatter than iCUE's card-to-subpage pattern since there's only one action (on/off) per monitor, no deeper config needed |
| Identify affordance | Native `Identify` button, large numbered overlay per screen | Not standard (relies on OS numbering) | N/A (peripherals, not displays) | Direct native-Windows-pattern match — keep the same large-number-overlay convention already built in Phase 17, just relocated |
| Primary action vs. per-item status | No single "primary action" concept — Display settings is pure configuration, no toggle-a-mode action | DisplayFusion profiles: switching a whole profile *is* the primary action, monitor list is secondary/read-only in the switcher UI | Dashboard-first (cards), configuration is one click deeper per card — no single global primary action either | This app is the outlier in a good way: it has both N independently-toggleable items (monitors) *and* one global primary action (mode toggle) in the same window — closest real analog is DisplayFusion's profile-switch-as-primary-action, but we also expose the per-monitor granularity DisplayFusion hides behind profiles |
| Theme setting shape | N/A (OS-level, not an app setting) | N/A | N/A (both apps have their own dark-only or custom-skinned chrome, not an OS-theme-follow model) | Follow the broader Windows-app-ecosystem convention instead (GitHub Desktop, Windows Terminal, Docker Desktop, Teams — all ship a System/Light/Dark three-way), since none of the monitor/hardware-dashboard comparables in this survey have a relevant theme-override pattern to draw from |

## Sources

- https://support.microsoft.com/en-us/windows/hardware/display-graphics/how-to-use-multiple-monitors-in-windows — Windows Settings > Display's numbered-tile + Identify pattern — HIGH confidence (official Microsoft support doc, re-confirmed this session)
- DisplayFusion Monitor Profiles (help/discussions, re-surveyed this session; also cited in the superseded v2.0 FEATURES.md) — explicit named target-set + confirmation-prompt-suppression pattern — MEDIUM confidence (vendor docs/forum, internally consistent)
- NirSoft MultiMonitorTool product/help pages (re-surveyed this session; also cited in v2.0 FEATURES.md) — per-monitor checkbox/hotkey/tray/CLI toggle pattern — MEDIUM-HIGH confidence
- Corsair iCUE / Razer Synapse dashboard descriptions (WebSearch, multiple independent write-ups) — device-card-as-primary-dashboard-element, card-click-to-deeper-config pattern — MEDIUM confidence (secondary write-ups, not vendor UI documentation, but consistent across sources)
- https://learn.microsoft.com/en-us/windows/terminal/customize-settings/themes — Windows Terminal's "system" theme option — HIGH confidence (official Microsoft Learn doc)
- GitHub Desktop theme documentation (docs.github.com, WebSearch) — System/Light/Dark three-way pattern, "System" as the always-match-computer option — HIGH confidence (official GitHub docs)
- Docker Desktop, Microsoft 365 apps, Microsoft Teams theme settings — corroborating instances of the same System/Light/Dark convention — MEDIUM confidence (WebSearch-surfaced, not all independently doc-verified this session, but consistent with well-established, widely-observed training-data knowledge of these specific products' settings UIs)
- Direct source inspection (this session): `/home/bpivk/moza/src/RigToggle.App/MonitorPanelForm.cs`, `/home/bpivk/moza/src/RigToggle.App/MainForm.cs`, `/home/bpivk/moza/src/RigToggle.Core/Models/AppTheme.cs`, `/home/bpivk/moza/src/RigToggle.Core/Abstractions/IThemeProvider.cs` — HIGH confidence (primary source, current repo state)
- `/home/bpivk/moza/.planning/PROJECT.md` — milestone framing, existing requirement IDs (PANEL-01..05, DISPLAY-12, THEME-01..09), Key Decisions history — HIGH confidence (primary project source)

---
*Feature research for: Rig Toggle v2.1 (Modern UI Redesign & Theme Backlog)*
*Researched: 2026-08-09*
