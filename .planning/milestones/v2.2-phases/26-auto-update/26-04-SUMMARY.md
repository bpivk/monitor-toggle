---
phase: 26-auto-update
plan: 04
subsystem: ui
tags: [dotnet, winforms, richtextbox, markdown-lite, settings-json, self-update]

requires:
  - phase: 26-auto-update
    plan: 01
    provides: "UpdatePromptDialog's two-button tracer shape, UpdateOrchestrator.CheckOnLaunchAsync's fetch/compare/confirm/apply sequencer, and rtbReleaseNotes as a plain-text control"
  - phase: 26-auto-update
    plan: 02
    provides: "ReleaseInfo.ChecksumDownloadUrl and the verify-before-swap contract this plan does not touch"
  - phase: 26-auto-update
    plan: 03
    provides: "The retained-backup/marker/rollback mechanism this plan's Skip/Later paths sit above without altering"
provides:
  - "UpdatePromptChoice (UpdateNow/Later/Skip) threaded through UpdateOrchestrator.CheckOnLaunchAsync in place of the tracer's bool confirm callback"
  - "AppSettings.SkippedUpdateVersion + honourSkippedVersion: numeric (UpdateVersionComparer) skip comparison so a skip suppresses exactly one version, never every future one (T-26-14)"
  - "btnSkip on UpdatePromptDialog with UpdatePromptDialog.Choice mapping OK/Ignore/else to UpdateNow/Skip/Later"
  - "ReleaseNotesFormatter (Core): pure Markdown-lite parser producing ReleaseNoteRun sequences (headers/bullets/inline bold), with a verbatim empty-body fallback and degrade-not-break handling for unsupported constructs"
  - "ReleaseNotesRenderer (App): applies ReleaseNoteRun sequences to rtbReleaseNotes via SelectionFont/SelectionBullet only, never Rtf/markup-interpreting properties"
affects: [26-05-operator-rig-verification]

actuals:
  tokens: 12100
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Skip suppression compares the persisted tag against the release tag via UpdateVersionComparer.TryParseTag + IsNewer (never a string match) -- the numeric comparison is what proves a strictly-newer release still prompts after an earlier skip"
    - "ReleaseNotesFormatter/ReleaseNotesRenderer split exactly on the Core/App seam: the parser is a pure, platform-neutral static class (testable, no WinForms reference); the renderer is the sole place that touches RichTextBox, and only via SelectionFont/SelectionBullet -- never Rtf or any markup-interpreting property (T-26-12)"

key-files:
  created:
    - src/RigToggle.Core/ReleaseNotesFormatter.cs
    - src/RigToggle.App/ReleaseNotesRenderer.cs
    - src/RigToggle.Tests/ReleaseNotesFormatterTests.cs
  modified:
    - src/RigToggle.Core/Models/AppSettings.cs
    - src/RigToggle.Core/UpdateOrchestrator.cs
    - src/RigToggle.App/UpdatePromptDialog.cs
    - src/RigToggle.App/UpdatePromptDialog.Designer.cs
    - src/RigToggle.App/MainForm.cs
    - src/RigToggle.App/Program.cs
    - src/RigToggle.Tests/UpdateOrchestratorTests.cs

key-decisions:
  - "UpdateOrchestrator's skip check parses the persisted tag with UpdateVersionComparer.TryParseTag and re-uses IsNewer(Version, string) by constructing a Version from the parsed major/minor -- reuses the codebase's one trusted numeric comparator rather than adding a second tag-vs-tag comparison method"
  - "A settings-store failure while persisting a skip (Load or Save) is fully swallowed inside the Skip branch's own try/catch -- the task's behavior block requires this be best-effort, so ThrowingSettingsStore (both Load and Save throw) doubles as valid coverage for that case, no new test double needed"
  - "ReleaseNotesFormatter's unpaired-'**' detection uses Split('**').Length parity (even length = odd delimiter count = unpaired) rather than manual bracket-matching -- a simpler invariant that falls naturally out of how String.Split counts delimiter occurrences"
  - "ReleaseNotesRenderer sets SelectionStart = TextLength before every AppendText call (not relying solely on AppendText's own selection-advance behavior) so SelectionFont/SelectionBullet are guaranteed to apply to the run about to be typed, not a stale selection"

requirements-completed: [UPDATE-02, UPDATE-03]

coverage:
  - id: D1
    description: "Update prompt exposes three real choices (Update Now / Later / Skip this version), differentiated by label and AcceptButton/CancelButton status only -- no destructive/new colour introduced for Skip"
    requirement: "UPDATE-03"
    verification:
      - kind: other
        ref: "grep -q 'btnSkip' UpdatePromptDialog.Designer.cs; grep -q 'Skip this version' ...Designer.cs; ThemeButton invoked 6x (3 buttons x constructor+OnThemeChanged); ! grep -qE 'Color\\.(Red|Firebrick|Crimson|DarkRed)' across both files -- all pass"
        status: pass
    human_judgment: true
    rationale: "This build sandbox is Linux -- the actual visual layout, button differentiation, and neutral-palette rendering of the three-button row can only be confirmed on real Windows hardware. Deferred to plan 26-05's operator rig checkpoint, same open item this phase's prior plans have carried forward."
  - id: D2
    description: "Skip this version persists AppSettings.SkippedUpdateVersion; the automatic path suppresses only that exact-or-older tag, and a strictly-newer release still prompts (T-26-14 prohibition: skip suppresses exactly one version, never every future one)"
    requirement: "UPDATE-03"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_HonourSkippedVersion_LatestTagEqualsSkippedVersion_ReturnsSkipped_ConfirmNeverInvoked, #CheckOnLaunchAsync_HonourSkippedVersion_LatestTagStrictlyNewerThanSkippedVersion_StillInvokesConfirm, #CheckOnLaunchAsync_PromptReturnsSkip_PersistsSkippedVersion_ApplierNeverInvoked_ReturnsSkipped, #CheckOnLaunchAsync_SettingsSaveThrowsWhilePersistingSkip_DoesNotPropagate_ReturnsSkipped"
        status: pass
    human_judgment: false
  - id: D3
    description: "Later, Esc, and the window close button all resolve to UpdatePromptChoice.Later (never Skip) -- closing the window is never silently equivalent to skipping; AcceptButton/CancelButton stay btnUpdateNow/btnLater"
    requirement: "UPDATE-03"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/UpdateOrchestratorTests.cs#CheckOnLaunchAsync_PromptReturnsLater_PersistsNothing_ApplierNeverInvoked_ReturnsDeclined_SecondCheckPromptsAgain proves the orchestrator-side contract for a Later result"
        status: pass
    human_judgment: true
    rationale: "UpdatePromptDialog.Choice's DialogResult switch and the Designer's AcceptButton/CancelButton wiring are statically correct and unit-tested at the orchestrator boundary, but the actual Esc-key and window-close-button behavior can only be exercised on a real WinForms message loop on Windows hardware -- deferred to plan 26-05."
  - id: D4
    description: "Release notes render with headers, bullets, and inline bold as styled runs -- not a raw Markdown text dump (D-01)"
    requirement: "UPDATE-03"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/ReleaseNotesFormatterTests.cs#Format_TopLevelHeader_YieldsOneHeadingRun_NoBullet, #Format_SubHeader_YieldsOneLabelRun, #Format_BulletLine_YieldsBulletTrueWithMarkerStripped, #Format_LineContainingBold_YieldsThreeRuns_PlainBoldPlain_AsterisksStripped -- proves the Core parser's run sequence is correct"
        status: pass
    human_judgment: true
    rationale: "The parser's output (ReleaseNoteRun sequence) is fully unit-tested and platform-neutral, but the actual visual result -- SelectionFont/SelectionBullet applied to a live RichTextBox inside a themed dialog -- requires the real WinForms runtime this Linux sandbox does not have. Deferred to plan 26-05's operator rig checkpoint."
  - id: D5
    description: "A release whose body is null or whitespace renders the fallback copy 'This release doesn't include notes. See the full release on GitHub for details.' instead of a blank control"
    requirement: "UPDATE-03"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/ReleaseNotesFormatterTests.cs#Format_NullOrWhitespaceBody_ReturnsSinglePlainRunWithFallbackText (null/empty/whitespace-only/newlines-only cases)"
        status: pass
    human_judgment: false
  - id: D6
    description: "The prompt is populated with a headline naming the new version, the formatted release notes, and the three-button row"
    requirement: "UPDATE-03"
    verification:
      - kind: other
        ref: "UpdatePromptDialog constructor: lblHeadline.Text set from release.TagName, ReleaseNotesRenderer.Render(rtbReleaseNotes, release.Body) called after ThemeRichTextBox, btnSkip/btnLater/btnUpdateNow all added to Controls (static code review + grep 'ReleaseNotesRenderer.Render' in UpdatePromptDialog.cs)"
        status: pass
    human_judgment: true
    rationale: "Full-dialog population is a composed visual claim across three static-review-verified pieces; final confirmation that the three pieces actually appear together correctly on a live rendered dialog is deferred to plan 26-05's operator rig checkpoint."
  - id: D7
    description: "Markdown constructs outside the supported subset (tables, images, links, nested lists, code fences) degrade to plain unformatted text rather than breaking or mangling the hand-rolled formatter (backstop truth, UI-SPEC overflow row)"
    verification:
      - kind: unit
        ref: "src/RigToggle.Tests/ReleaseNotesFormatterTests.cs#Format_UnsupportedConstruct_DegradesToPlainText_NeverBreaksOrDropsContent (table row, image, link, fenced code block, nested list) and #Format_ArbitrarilyLongBody_NeverThrows_NeverTruncates"
        status: pass
    human_judgment: true
    rationale: "Declared as a `backstop` truth in the plan's own must_haves (26-UI-SPEC.md overflow row) -- the automated tests prove the Core parser degrades correctly for every listed construct, but the plan's own action text defers the remaining evidence (how a real GitHub release body with rich formatting actually looks in the rendered dialog) to plan 26-05's operator checkpoint. Per the backstop status vocabulary, this always routes to a human regardless of passing automated evidence."

duration: ~10min
completed: 2026-08-22
status: complete
---

# Phase 26 Plan 04: Skip Version & Formatted Release Notes Summary

**Three-way Update Now/Later/Skip prompt result with a per-version persisted skip (compared numerically, never by string) threaded through UpdateOrchestrator, plus a hand-rolled Markdown-lite formatter/renderer pair that turns a GitHub release body into styled RichTextBox runs with a verbatim empty-notes fallback.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-08-22T17:27:01Z (approx., base commit)
- **Completed:** 2026-08-22T17:37:24Z
- **Tasks:** 2
- **Files modified:** 10 (3 created, 7 modified)

## Accomplishments

- `UpdatePromptChoice` (`UpdateNow`/`Later`/`Skip`) replaces the tracer's `Func<ReleaseInfo, bool>` confirm callback in `UpdateOrchestrator.CheckOnLaunchAsync`, with a new `UpdateCheckOutcome.Skipped` outcome and a `honourSkippedVersion` parameter that only the automatic on-launch path sets `true`.
- `AppSettings.SkippedUpdateVersion` persists exactly one release tag; the automatic path compares it against the latest release via `UpdateVersionComparer.TryParseTag`/`IsNewer` (numeric Major.Minor, never a string match) so a strictly-newer release still prompts after an earlier skip -- the T-26-14 prohibition's proof.
- `btnSkip` added to `UpdatePromptDialog` in the identical neutral `ThemeButton` palette as `btnLater`/`btnUpdateNow` (no destructive styling introduced); `UpdatePromptDialog.Choice` maps `OK`/`Ignore`/anything-else (including `Cancel`, Esc, and window-close) to `UpdateNow`/`Skip`/`Later` respectively -- closing the dialog is never silently equivalent to skipping.
- `ReleaseNotesFormatter` (Core, new): a pure, hand-rolled Markdown-lite parser -- `#` headers, `##`/`###` sub-headers, `-`/`*` bullets, inline `**bold**` spans -- producing an ordered `ReleaseNoteRun` sequence; unsupported constructs (tables, images, links, fenced code, nested lists) and unpaired `**` degrade to verbatim plain text rather than breaking the parse; a null/whitespace body yields the exact UI-SPEC fallback sentence; the whole parse is wrapped in a try/catch that degrades to a raw-text single run on any unexpected failure.
- `ReleaseNotesRenderer` (App, new): applies `ReleaseNoteRun`s to `rtbReleaseNotes` via `SelectionFont`/`SelectionBullet` only -- never `Rtf` or any markup-interpreting property (T-26-12) -- and deliberately never touches `BackColor`/`ForeColor` so it doesn't fight `ThemeApplier.ThemeRichTextBox`; falls back to raw text assignment if rendering itself fails.
- 20 new `ReleaseNotesFormatterTests` plus 5 new `UpdateOrchestratorTests` cases; full 203-test cross-platform suite green, zero new NuGet packages, zero new warnings beyond the pre-existing 6 documented in prior plans' summaries.

## Task Commits

1. **Task 1: Three-way prompt result and a per-version skip that suppresses exactly one version** - `3d72c3b` (feat)
2. **Task 2: Markdown-lite release-notes formatting with an empty-notes fallback** - `7564ab6` (feat)

**Plan metadata:** commit pending (this SUMMARY + STATE/ROADMAP writes are owned by the wave orchestrator in worktree mode; REQUIREMENTS.md was checked via `requirements mark-complete` and found already `[x]` for UPDATE-02/UPDATE-03 from an earlier plan, so no REQUIREMENTS.md write was needed this plan)

## Files Created/Modified

- `src/RigToggle.Core/Models/AppSettings.cs` - Added nullable `SkippedUpdateVersion` property and its class-doc-comment paragraph
- `src/RigToggle.Core/UpdateOrchestrator.cs` - `UpdatePromptChoice` enum, `UpdateCheckOutcome.Skipped`, `ISettingsStore` constructor param, `honourSkippedVersion` param, the numeric skip-comparison branch, and the Skip/Later/UpdateNow choice handling
- `src/RigToggle.App/UpdatePromptDialog.Designer.cs` - `btnSkip` field/instantiation/`Controls.Add`, left-anchored per UI-SPEC geometry
- `src/RigToggle.App/UpdatePromptDialog.cs` - `ThemeApplier.ThemeButton(btnSkip, ...)` in both theming blocks; `Choice` property; `rtbReleaseNotes.Text` direct assignment replaced with `ReleaseNotesRenderer.Render(...)`
- `src/RigToggle.App/MainForm.cs` - Confirm callback now returns `dialog.Choice` (an `UpdatePromptChoice`) instead of a bool; `honourSkippedVersion: true` passed on the automatic path
- `src/RigToggle.App/Program.cs` - `settingsStore` threaded into the `new UpdateOrchestrator(...)` composition-root call
- `src/RigToggle.Core/ReleaseNotesFormatter.cs` - New: `ReleaseNoteStyle` enum, `ReleaseNoteRun` record, `Format(string?)` static method
- `src/RigToggle.App/ReleaseNotesRenderer.cs` - New: `internal static class` with `Render(RichTextBox, string?)`
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` - All existing tests updated for the new constructor/confirm signature; 5 new tests for skip/later branches
- `src/RigToggle.Tests/ReleaseNotesFormatterTests.cs` - New: 20 tests covering every case in Task 2's behavior block

## Decisions Made

See `key-decisions` in the frontmatter above for the full list, most notably:
- Skip comparison reuses `UpdateVersionComparer`'s existing `TryParseTag`/`IsNewer` pair (parsing the skipped tag into a synthetic `Version`) rather than adding a second tag-vs-tag comparator, keeping exactly one trusted numeric-comparison code path in the codebase.
- A settings-write failure while persisting a skip is fully best-effort (both Load and Save inside the same try/catch), so the existing `ThrowingSettingsStore` double (both methods throw) is valid, sufficient coverage for that behavior -- no new test double was needed.
- `ReleaseNotesFormatter`'s unpaired-`**` detection is a `Split("**").Length` parity check, not manual bracket matching -- simpler and derives directly from how `String.Split` counts delimiter occurrences.

## Deviations from Plan

None - plan executed exactly as written. Both tasks' `<action>` sections were followed as specified; all acceptance-criteria grep checks and the plan-level `<verification>` block (solution build, full test suite, `RigToggle.Windows.Tests` compile, zero new `PackageReference`) all pass without any auto-fix needed.

## Issues Encountered

- **This build sandbox is Linux, not Windows** (same constraint every prior plan in this phase has documented). `dotnet build RigToggle.sln -c Release` and `dotnet test src/RigToggle.Tests` both ran and passed directly in this session (203/203 tests green, including all 25 new tests from this plan). `dotnet build src/RigToggle.Windows.Tests` also succeeded (0 errors, 0 warnings). What could **not** be exercised: the actual rendered `UpdatePromptDialog` on a live WinForms message loop -- button layout/differentiation, the RichTextBox's real `SelectionFont`/`SelectionBullet` visual result, and Esc/window-close behavior. All deferred to plan 26-05's operator rig checkpoint, consistent with every prior plan in this phase.
- **Pre-existing `xUnit1031` warnings (6, unrelated to this plan) still present.** Same gap documented in `.planning/phases/26-auto-update/deferred-items.md` and every prior plan's summary in this phase: `SingleInstanceGuardTests.cs` (2) and `ToggleOrchestratorTests.cs` (4). This plan's own new/modified files introduce 0 new warnings.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

The update-prompt UX is now feature-complete at the Core/orchestration level: three real choices, a numerically-compared per-version skip, and a Markdown-lite-formatted notes area with a tested empty-state fallback and tested unsupported-construct degradation. Plan 26-05 (the phase's final plan, carrying the operator rig checkpoint) inherits a fully-wired, unit-tested `UpdatePromptDialog`/`ReleaseNotesFormatter`/`ReleaseNotesRenderer` to visually confirm on real Windows hardware -- specifically: the three-button row's actual layout/differentiation, a real GitHub release body's rendered appearance (including any rich-formatting overflow beyond the supported subset, per the UI-SPEC backstop row), Esc/window-close resolving to "Later" in practice, and Skip's persisted-value round-trip through the real `settings.json`. No known blockers.

**Blocker/concern carried forward:** this plan is unverified on real Windows hardware (build/unit-test only, per the Linux sandbox limitation above) -- the actual rendered dialog, its button/notes visuals, and live Esc/close-button behavior are all still open until plan 26-05's rig pass, consistent with every prior plan in this phase's carried-forward blocker.

---
*Phase: 26-auto-update*
*Completed: 2026-08-22*

## Self-Check: PASSED

All 3 created files (`ReleaseNotesFormatter.cs`, `ReleaseNotesRenderer.cs`, `ReleaseNotesFormatterTests.cs`) and all 7 modified files confirmed present on disk. Commits `3d72c3b` (Task 1) and `7564ab6` (Task 2) both confirmed present in `git log --oneline`.
