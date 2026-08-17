# Phase 21: Accent-Color Reading & Live Update - Context

**Gathered:** 2026-08-10
**Status:** Ready for planning

<domain>
## Phase Boundary

Key interactive elements that today read a fixed light/dark placeholder color (`MainForm.AccentColor` / `ThemeApplier`'s duplicated literal) instead pick up the user's actual live Windows accent color, and update live if the user changes their accent color while the app is running — matching what Settings > Personalization > Colors shows, including for a custom (manually-set, non-default) accent. Scoped strictly to what already consumes the placeholder today (see D-04 below); does not add accent-tinting to any control that isn't already accent-colored. Does not touch manual light/dark override (Phase 22) or SettingsForm layout (Phase 23). Requirement: THEME-07.

</domain>

<decisions>
## Implementation Decisions

### Source of Truth & Risk-Hedging
- **D-01:** Implement **one primary read path**: `HKCU\Software\Microsoft\Windows\DWM\AccentColor` registry value as the primary source, falling back to `DwmGetColorizationColor` (dwmapi.dll) only if that key is absent — per PITFALLS.md's leading hypothesis. Do not build a dual-path comparison/diagnostic-logging system defensively; treat this ordering as a hypothesis to be confirmed on the rig, not a settled fact requiring hedged implementation. If the rig pass proves it wrong, that's a small follow-up fix, not a redesign — matches this codebase's established convention (one clear source + graceful fallback, not defensive multi-path logic).
- **D-02:** Extend `WindowsThemeProvider`'s existing `SystemEvents.UserPreferenceChanged` handler to also read/diff the accent color and raise a new `AccentColorChanged` event — do NOT add a second `SystemEvents` subscription (per ARCHITECTURE.md's explicit anti-pattern warning and Phase 20's D-09/20-CONTEXT.md canonical-refs note that this is exactly what Phase 21 was expected to do). `IThemeProvider` is extended (not replaced) with `AccentColor` + `AccentColorChanged`; existing `CurrentTheme`/`ThemeChanged` contract is untouched.

### Rig Ground Truth (verified by user, 2026-08-10)
- **D-03:** User's actual rig accent color is **manually set (not "automatic from background"), a custom blue** — not the Windows default accent. This directly matches the roadmap's Success Criterion 3 ("including for a custom (non-default) accent color") — implementation and any rig-verification pass should use this real value as the target, not a default-blue assumption. "Show accent color on title bars and window borders" is **ON** on the rig PC — this reduces (but per PITFALLS.md doesn't eliminate) the risk of `ColorizationColor`/`DwmGetColorizationColor` diverging from the raw accent swatch, since that divergence risk is specifically flagged for the title-bar-toggle-OFF case.

### Scope of Accent-Tinted Elements
- **D-04:** The accent-tinted element set is **exactly what already consumes the fixed placeholder today** — no new consumers added this phase:
  - `MonitorTile.AccentColor` (ON-state icon fill) + `MonitorTile.FocusRingColor` (both set by `ThemeApplier.ThemeMonitorTile`)
  - `ToggleSwitch.OnColor` (ON-state track fill) + `ToggleSwitch.FocusRingColor` (both set by `ThemeApplier.ThemeToggleSwitch`)
  - `MainForm.AccentColor` — consumed by `DrawButtonFocusRing` for the Identify button ring (line ~1179) and Settings button ring (line ~1242)
  - The DWM title bar/window border (`DwmTitleBar.cs`) is explicitly **NOT** added — it stays light/dark-only (Mica/rounded-corners), exactly as it is today. This was a considered option (extending `DWMWA_CAPTION_COLOR` with the live accent) and explicitly declined as new scope beyond the roadmap's "toggle switch + designated interactive elements" framing.
  - Because all of the above already funnel through one shared placeholder value (either `MainForm.AccentColor` or the identical literal duplicated in `ThemeApplier`), replacing that single source with a live-read value is expected to flip all consumers together with no per-consumer rework — consistent with Phase 20's D-09 comment that this was designed to happen "for free."

### Verification Ownership
- **D-05:** User will **personally run the rig-verification pass** (accent-swatch match against Settings > Colors using a color picker not eyeballing, the title-bar-toggle-ON scenario per D-03, and multiple live accent flips in one session including a same-color no-op — per PITFALLS.md #4/#5's explicit warning-sign checklists) and report PASS/FAIL back before the phase is considered fully done. This matches how Phase 12/13 closed: implement + write the checklist, user runs it live on the rig PC, gap-closure round if something fails. Do not mark this phase done without that reported rig pass.

### Claude's Discretion
- Exact registry-value parsing/masking (packed `0xAARRGGBB` format, stripping alpha correctly per PITFALLS.md's explicit warning about alpha-byte handling) — implementation detail, not a product decision.
- Whether `AccentColorChanged` needs a defensive periodic re-check alongside the event subscription, or event-only is sufficient — per PITFALLS.md's guidance, only add polling if the message-only approach fails rig verification; don't add it preemptively.
- Exact diff/no-op logic for same-color re-selection (skip re-raising/repainting when the read value hasn't actually changed) — follows the same pattern `WindowsThemeProvider.OnUserPreferenceChanged` already uses for theme.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (THEME-07 — this phase's sole requirement)
- `.planning/ROADMAP.md` §"Phase 21: Accent-Color Reading & Live Update" (goal, 3 success criteria, "UI hint: yes", depends on Phase 20)
- `.planning/PROJECT.md` (Current Milestone: v2.1 section — full milestone framing)

### Research (this milestone — read before planning; this phase is the milestone's single most research-flagged item)
- `.planning/research/SUMMARY.md` §"Phase C: Accent-Color Reading & Live Update (THEME-07)" and §"Research Flags" — explicitly flags this phase as needing the heaviest rig verification of anything in the milestone; no official Microsoft documentation confirms the accent-color source
- `.planning/research/ARCHITECTURE.md` — `IThemeProvider` extension shape (`+ AccentColor`, `+ AccentColorChanged`), the explicit anti-pattern warning against a second `SystemEvents` subscription (§"What people do" around line 278), `DwmGetColorizationColor` P/Invoke integration point (line ~302)
- `.planning/research/PITFALLS.md` — **Pitfall 4** (accent-color source ambiguity — read in full before implementing D-01; contains the exact registry-key/masking guidance), **Pitfall 5** (unreliable change notification — contains the exact rig-verification warning-signs checklist D-05 references)
- `.planning/research/FEATURES.md` — accent-color feature framing for THEME-07

### Prior phases (precedent this phase must follow, not regress)
- `.planning/phases/20-custom-toggle-switch-control/20-CONTEXT.md` — D-09 (the exact placeholder this phase replaces: `Color.FromArgb(0, 90, 158)` dark / `SystemColors.Highlight` light — explicitly written as "this same property becomes the live Windows accent color for free once Phase 21 lands")
- `.planning/phases/19-monitor-tile-dashboard-monitorpanelform-retirement/19-CONTEXT.md` — D-02 (tile on/off convention using the same AccentColor source)
- `.planning/milestones/v1.2-phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — theme application must fire via both `OnHandleCreated`/`InitializeTrayState()` and `OnThemeChanged`, not `Form_Load`/`OnShown` alone; this is the precedent D-02's event-diffing approach mirrors

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/Abstractions/IThemeProvider.cs` — extend with `AccentColor` property + `AccentColorChanged` event (D-02)
- `src/RigToggle.Windows/WindowsThemeProvider.cs` — extend the existing `OnUserPreferenceChanged` handler to also read/diff accent color (D-02); follow its established lock/diff/fallback-to-safe-default conventions
- `src/RigToggle.App/MainForm.cs` (line ~184) — `AccentColor` property, currently `IsDark ? Color.FromArgb(0, 90, 158) : SystemColors.Highlight`, becomes a pass-through to `_themeProvider.AccentColor`; also needs an `AccentColorChanged` subscription alongside the existing `ThemeChanged` subscription (line ~118) so `ApplyDashboardTheming()` re-runs on a live accent flip, not just a light/dark flip
- `src/RigToggle.App/ThemeApplier.cs` (lines ~177-243) — `ThemeMonitorTile` and `ThemeToggleSwitch` both currently duplicate the same fixed literal for `AccentColor`/`OnColor`/`FocusRingColor`; both need to source from the live value instead (D-04)
- `src/RigToggle.Tests/Doubles/FakeThemeProvider.cs`, `src/RigToggle.Tests/ThemeProviderContractTests.cs` — existing test doubles/contract tests for `IThemeProvider`, need updating for the extended interface

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WindowsThemeProvider`'s existing lock-guarded diff-against-last-known-value pattern (`_themeLock`, compare-then-raise-only-on-change) — directly reusable for `AccentColor`'s diff logic (D-02, addresses the same-color no-op discretion item).
- `WindowsThemeProvider`'s "never throw from a Load-time read, default to a safe value" convention — the fallback posture for D-01's registry read.

### Established Patterns
- Fail-loud only where user-visible correctness matters (mode state, DISPLAY-11); cosmetic/theming paths (per `ThemeApplier`'s existing try/catch-wrapped methods) fail silently to "leave unchanged" — accent-color reads follow the cosmetic convention, not the fail-loud one.
- `ThemeApplier`'s explicit per-control theming with a two-call-site rule (`OnThemeChanged` AND `InitializeTrayState()`) — any new accent-driven repaint must reach both call sites, exactly like Phase 19/20 already established for light/dark.

### Integration Points
- `MainForm`'s constructor-time subscription block (line ~118, `_themeProvider.ThemeChanged += OnThemeChanged`) — needs a parallel `_themeProvider.AccentColorChanged += OnThemeChanged` (or a dedicated handler that also calls `ApplyDashboardTheming()`) so an accent-only change (no light/dark flip) still triggers a repaint.
- `MainForm.ApplyDashboardTheming()` (line ~1019) — already the single funnel both `OnThemeChanged` and `InitializeTrayState()` go through; no new call sites needed as long as accent changes route through the same method.

</code_context>

<specifics>
## Specific Ideas

- User's actual rig accent color is manually set to a custom blue (not Windows-default, not "automatic from background") — use this as the concrete target for any implementation/verification reasoning, per D-03.
- "Show accent color on title bars and window borders" is ON on the user's rig — per D-03, this is the less-risky configuration for the ColorizationColor/AccentColor-divergence pitfall, but D-01/D-05 still apply the full verification checklist rather than assuming it's a non-issue.

</specifics>

<deferred>
## Deferred Ideas

- **Accent-tinted DWM title bar/window border** — considered during the "scope of accent-tinted elements" discussion and explicitly declined (D-04) as new capability beyond this phase's boundary. Would extend `DwmTitleBar.cs` with `DWMWA_CAPTION_COLOR`. Not folded into any specific future phase — note for a future backlog item if ever wanted.

None else — discussion stayed within phase scope.

</deferred>

---

*Phase: 21-Accent-Color-Reading-Live-Update*
*Context gathered: 2026-08-10*
