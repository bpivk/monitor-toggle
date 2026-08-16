# Phase 23: Manual Light/Dark Override - Context

**Gathered:** 2026-08-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Users get a System/Light/Dark choice in SettingsForm (defaulting to System) that overrides the app's live Windows theme-follow. Selecting Light or Dark locks the app to that theme and previews it live in the running app immediately — before Save is clicked. A live OS theme flip no longer silently overrides a locked choice; selecting System restores today's live-follow behavior everywhere. This is the natural, already-scheduled moment (MonitorPanelForm is being deleted this same milestone) to collapse the codebase's three independently-mirrored `IsDark`/`IsDarkTheme` properties (`MainForm`, `SettingsForm`, `MonitorConfirmDialog`) into one shared "effective theme" resolver, per Pitfall 6. No fourth theme option, no time-of-day auto-switching, no SettingsForm restructuring beyond the radio group Phase 22 already reserved space for. Requirement: THEME-09.

</domain>

<decisions>
## Implementation Decisions

### Apply Timing
- **D-01:** Selecting Light or Dark in the radio group applies **immediately** — the running app (MainForm, SettingsForm itself, and MonitorConfirmDialog if shown) repaints to the previewed theme right away, without waiting for Save. This deviates from every other field in SettingsForm today (which only take effect on Save) — a deliberate, discussed choice, not an oversight. — **Reversibility:** costly — changes the SettingsForm interaction contract for this one field relative to every other field; a later reversal would need to re-thread the immediate-apply wiring back into Save-only and explain why theme differs from every other setting.
- **D-02:** Clicking **Discard Changes** after previewing an unsaved Light/Dark selection reverts the live theme back to whatever override was last persisted (or to System/live-follow if none was ever saved) — matching Discard's existing meaning for every other field. Only clicking **Save** persists the new override to `AppSettings`.
- **D-03:** Closing SettingsForm via the window's X/Close button (if that bypasses both Save and Discard) should be treated the same as Discard for the theme preview — revert to last-saved override. Confirm this against however SettingsForm's existing close-without-save handling works for other fields (if there's inconsistency there already, match it; don't introduce a new pattern).

### Scope of the Override's Reach
- **D-04:** All three of the codebase's current independent theme-resolution points — `MainForm.IsDark`, `SettingsForm.IsDarkTheme`, `MonitorConfirmDialog.IsDark` — must resolve through **one shared effective-theme resolver**, not be updated independently. No surface is exempt; a Dark override must be visible everywhere in the app, including the confirm dialog, with zero live-Windows-flip leakage on any of the three. This directly implements Pitfall 6's prescribed fix and uses the MonitorPanelForm deletion (same milestone) as the moment to collapse to one resolver instead of perpetuating a fourth copy.
- **D-05:** The narrow edge case of the theme override changing in SettingsForm while a MonitorConfirmDialog happens to already be open elsewhere is explicitly out of scope for special handling — read the resolver fresh each time a dialog opens (matching today's `IsDark` pattern), no additional synchronization needed. Not part of the rig-verification checklist.

### Radio Group Presentation
- **D-06:** Option labels are exactly **"System" / "Light" / "Dark"** — matches the roadmap's own wording and Windows' Settings > Personalization > Colors terminology, not an explanatory rewrite like "Follow Windows".
- **D-07:** The default (System) option carries an explicit **"(default)" suffix** in its label (e.g. "System (default)") in addition to being pre-selected on first run — stays legible as the default even after a user has changed their selection and returns to look at the option later.

### Claude's Discretion
- Exact shared "effective theme" resolver shape (single method vs. property, where it lives — `OverridableThemeProvider` per the research ARCHITECTURE.md decorator design is the natural home) — architecture is already locked by research, implementation-level wiring is not discussed further here.
- Whether `AppSettings.ThemeOverride` is a nullable `AppTheme?` (per ARCHITECTURE.md's exact proposal) or a small tri-state enum — research already recommends nullable `AppTheme?` (`null` = System, consistent with this class's existing "unset" convention); follow that unless planning finds a concrete reason not to.
- Exact radio group control layout inside the `pnlThemeReserved` slot Phase 22 already reserved (RadioButton stack vs. a labeled row) — Phase 22's CONTEXT.md left this to planning; nothing new decided here beyond the label text (D-06/D-07).
- Exact rig-verification checklist steps for Pitfall 6's "set Dark, flip Windows to Light, confirm app doesn't follow" scenario across all three surfaces (MainForm, SettingsForm, MonitorConfirmDialog) — Claude should produce a concrete checklist during planning, following the same pattern established in Phase 21/22's rig-verification requirements.
- How exactly Discard's revert-to-last-saved (D-02) is implemented — re-reading `AppSettings` from `ISettingsStore` vs. caching the pre-open value in `SettingsForm` — implementation detail, not a product decision.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (THEME-09 — this phase's sole requirement)
- `.planning/ROADMAP.md` §"Phase 23: Manual Light/Dark Override" (goal, 3 success criteria, "UI hint: yes", depends on Phase 21, reordered behind Phase 22 at user request 2026-08-11)
- `.planning/PROJECT.md` (Current Milestone: v2.1 section — full milestone framing)

### Research (this milestone — read before planning)
- `.planning/research/SUMMARY.md` §"Phase D: Manual Light/Dark Override (THEME-09)" — delivers/implements/avoids summary; explicitly flags this refactor as needing a closer look during planning to sequence correctly relative to MonitorPanelForm's deletion
- `.planning/research/ARCHITECTURE.md` §"OverridableThemeProvider" and the component-responsibility table — the exact decorator shape (`AppTheme? ThemeOverride` on `AppSettings`, `OverridableThemeProvider` wrapping `IThemeProvider`, resolves effective theme = override ?? live OS signal), zero changes required to `WindowsThemeProvider`
- `.planning/research/PITFALLS.md` — **Pitfall 6** (read in full before implementing — the three-copy `IsDark`/`IsDarkTheme` consistency risk this phase's D-04 directly addresses, including its exact rig-verify warning-signs checklist)

### Prior phases (precedent this phase must follow, not regress)
- `.planning/phases/22-settingsform-layout-pass/22-CONTEXT.md` §D-04 and its Designer.cs comments — the `pnlThemeReserved` slot this phase's radio group fills; already an `AutoSize` Panel with no children, positioned in the shared global section, zero-size until this phase adds content
- `.planning/phases/21-accent-color-reading-live-update/21-CONTEXT.md` §D-05 — precedent for how this project structures a rig-verification checklist and requires the user to personally run it and report PASS/FAIL before the phase is considered done
- `.planning/milestones/v1.2-phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — the original `IThemeProvider`/`WindowsThemeProvider` design this phase decorates, and the two-call-site theming convention (`OnThemeChanged` + `InitializeTrayState()`) any new override-aware repaint must still reach

### Existing code (this phase's actual surface area)
- `src/RigToggle.Core/Models/AppSettings.cs` — add `ThemeOverride : AppTheme?` (nullable, `null` = follow system, consistent with every other unset field in this class)
- `src/RigToggle.Core/Models/AppTheme.cs` — existing `enum AppTheme { Light, Dark }`, referenced as-is (no "System" enum member — System is represented by `null` on `ThemeOverride`, not a third enum value)
- `src/RigToggle.Core/OverridableThemeProvider.cs` (new, per ARCHITECTURE.md) — decorator over `IThemeProvider`, resolves effective theme = override ?? live signal, belongs in `RigToggle.Core` (zero Windows API calls, injected `ISettingsStore` dependency, unit-testable without a Windows CI runner)
- `src/RigToggle.App/MainForm.cs` line 199 (`private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;`) — must resolve through the shared resolver, not `_themeProvider.CurrentTheme` directly, once `OverridableThemeProvider` is wired in at the composition root
- `src/RigToggle.App/SettingsForm.cs` line 210 (`private bool IsDarkTheme => _themeProvider.CurrentTheme == AppTheme.Dark;`) — same collapse; this form also owns the new radio group's Save/Discard/live-preview wiring (D-01/D-02)
- `src/RigToggle.App/MonitorConfirmDialog.cs` line 63 (`private bool IsDark => _themeProvider.CurrentTheme == AppTheme.Dark;`) — same collapse; third and final copy per Pitfall 6
- `src/RigToggle.App/SettingsForm.Designer.cs` lines 778-797 — `pnlThemeReserved`: named, empty, `AutoSize=true`, `Size(0,0)`, in the shared global section — this phase's insertion point, no reflow of anything above it needed
- `src/RigToggle.App/Program.cs` line 124 (`var themeProvider = new WindowsThemeProvider();`) — composition root; must construct `OverridableThemeProvider` wrapping this and pass the decorator to `MainForm`/`SettingsForm`/`MonitorConfirmDialog` instead of the raw `WindowsThemeProvider`
- `src/RigToggle.Core/Abstractions/ISettingsStore.cs` — existing settings persistence interface `SettingsForm.BtnSaveSettings_Click` already calls (`_settingsStore.Save(settingsToSave)`, line 1184) — the new override field flows through this same Save path

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WindowsThemeProvider`'s existing lock-guarded diff-against-last-known-value pattern (established in Phase 21 for accent color) — the same discipline applies to `OverridableThemeProvider`'s override resolution, though it's pure decision logic with no OS event subscription of its own.
- `SettingsForm`'s existing Save/Discard button wiring (`btnSaveSettings.Enabled` validation gate at line 993, `_settingsStore.Save` at line 1184) — the new radio group's persistence follows this same flow; only the live-preview-before-save behavior (D-01) is new.

### Established Patterns
- `ThemeApplier`'s two-call-site rule (`OnThemeChanged` AND `InitializeTrayState()`) — any new override-driven repaint must reach both, exactly like Phase 19/20/21 already established for light/dark and accent color.
- Fail-loud only where user-visible correctness matters; cosmetic/theming paths fail silently to "leave unchanged" (established Phase 12/21 convention) — the effective-theme resolver should follow the same posture: a corrupt/unreadable `ThemeOverride` value degrades to System (live-follow), not a crash.

### Integration Points
- `Program.cs`'s composition root (line 124) is the single wiring point — constructing `OverridableThemeProvider` here and threading it through to every form's `IThemeProvider` constructor parameter is what makes D-04's "all three surfaces" requirement mechanical rather than requiring three separate changes.
- `SettingsForm`'s existing `_themeProvider.ThemeChanged += OnThemeChanged` subscription (line 91) — the live-preview behavior (D-01) most likely raises this same event (or an equivalent) when the override changes, reusing the existing repaint pipeline rather than inventing a new one.

</code_context>

<specifics>
## Specific Ideas

- User explicitly chose immediate live-preview over the safer/more-consistent Save-gated option, and explicitly wants Discard to revert the preview — these are deliberate calls that make the theme field intentionally different from every other SettingsForm field, not an oversight to "fix" during planning.
- User explicitly wants zero exceptions on override reach — no surface should be allowed to keep following live Windows theme once an override is set, closing off Pitfall 6's exact failure mode as a design choice, not just a bug to avoid.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. The MonitorConfirmDialog-open-during-override-change edge case (D-05) was discussed and explicitly declined as special-case scope, not deferred to a future phase — it's simply out of scope entirely, resolved by the existing "read fresh" pattern.

</deferred>

---

*Phase: 23-Manual-Light-Dark-Override*
*Context gathered: 2026-08-16*
