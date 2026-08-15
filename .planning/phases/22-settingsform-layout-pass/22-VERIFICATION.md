---
phase: 22-settingsform-layout-pass
verified: 2026-08-15T00:00:00Z
status: gaps_found
score: 4/12 must-haves verified (5 FAILED, 3 UNCERTAIN/blocked)
overrides_applied: 0
gaps:
  - truth: "SettingsForm has no overlapping or crowded controls at its default window size (SETTINGS-01)"
    status: failed
    reason: "Real Windows rig-hardware test (22-03-SUMMARY.md Task 2, Check 1) shows a stronger failure than crowding: the monitor DataGridView and the audio device ComboBox do not render at all in either the Normal or Rig mode column. Only the caption/explain text is visible. Rig Check 3 additionally shows the window cannot actually be resized (drag preview flickers, no resize occurs)."
    artifacts:
      - path: "src/RigToggle.App/SettingsForm.Designer.cs"
        issue: "tlpNormalColumn / tlpRigColumn declare their DataGridView's host row as SizeType.Percent(100F) while the container itself (and its AutoSize-chain ancestors tlpModeColumns, tlpRoot) has AutoSize=true -- a circular sizing dependency that WinForms appears to resolve by collapsing the Percent row (and everything below it in that table) to zero/invisible height. This is unconfirmed root-cause (Hypothesis 2 in 22-03-SUMMARY.md) but matches the observed symptom exactly."
    missing:
      - "A fix for the grid/audio-picker non-rendering (Bug B) -- likely changing the DataGridView's row from Percent(100F) to AutoSize+MinimumSize, or removing the AutoSize circularity some other way, verified against actual WinForms TableLayoutPanel percent-row-inside-autosize-container behavior before committing to a specific fix"
      - "A fix for the broken manual window resize (Bug A) -- Form.AutoSize=true is very likely fighting the user's WM_SIZING edge-drag; 22-03-SUMMARY.md names a candidate remedy (disable Form.AutoSize after first show, keep container AutoSize) but it is unconfirmed and unapplied"
      - "A fresh rig-hardware verification pass confirming both fixes, followed by the 14 remaining checks (2, 4-17) that were never reached"
  - truth: "Each mode's monitor grid, the audio device pickers, the app path control, and the hotkey capture box are visually grouped and consistently spaced (SETTINGS-02)"
    status: failed
    reason: "Rig Check 1 shows the grid and audio picker -- the two controls this criterion is specifically about grouping -- do not render at all in either mode column. Grouping cannot be evaluated as satisfied when its core members are invisible. This is the same rig evidence as the SETTINGS-01 gap above (single root cause, Bug B)."
    artifacts:
      - path: "src/RigToggle.App/SettingsForm.Designer.cs"
        issue: "Same Bug B (grid/audio-picker collapse) as the SETTINGS-01 gap. No separate evidence of a spacing/grouping-specific defect independent of the non-render issue -- cannot be assessed until Bug B is fixed."
    missing:
      - "Same Bug B fix as SETTINGS-01, plus a rig-confirmed visual check that once rendering, the grid+picker in each column and the shared section below read as consistently spaced groups (rig checks 1, 2, 5, 6, 7, 8, 12, 17 -- none completed)"
human_verification:
  - test: "Re-run rig Check 1 (baseline layout) after Bug B is fixed: confirm both mode columns show their monitor grid and audio device ComboBox rendered, not just caption/explain text."
    expected: "Both grids and both audio pickers are visible and populated at app launch, at 100% Windows display scale."
    why_human: "WinForms TableLayoutPanel render-time layout behavior cannot be observed or simulated in this headless Linux sandbox -- no Windows GUI, no DWM, no layout engine."
  - test: "Re-run rig Check 3 (manual resize) after Bug A is fixed: drag the window's right/bottom edge."
    expected: "The window actually resizes to track the drag, with no flicker-then-snap-back."
    why_human: "Form.AutoSize vs. user-driven WM_SIZING interaction is a live-rendering behavior with no unit-testable surface."
  - test: "Rig checks 2, 4-9 (shared-section rendering, no-minimize-button, tab order, drag-drop hit-testing on the whole app-path box, validation-feedback visibility, live theme flip, AutoSize-vs-manual-resize interaction) -- all still blocked/not attempted."
    expected: "Each behaves as specified in 22-03-PLAN.md's Task 2 <how-to-verify> block, once Bugs A and B are fixed."
    why_human: "Same class of real-rendering, real-hardware-only checks; the user's report explicitly stopped after Check 3 because continuing against an already-broken 100%-scale layout would not produce meaningful data."
  - test: "Rig checks 10-17 (grid columns, overlap/crowding, button text, resize, whole-window sanity, and shared-section-width judgment call, each repeated at 125% and 150% Windows display scale)."
    expected: "No overlap/crowding/truncation and correct resize behavior at both non-100% scales, per 22-03-PLAN.md."
    why_human: "DPI-scale-dependent rendering; requires a live OS display-scale change and app relaunch on real hardware, per D-03's accepted tradeoff -- this is the entire reason Plan 03's rig checkpoint exists."
---

# Phase 22: SettingsForm Layout Pass Verification Report

**Phase Goal:** SettingsForm reads as an intentionally laid-out screen instead of a crowded, organically-grown one.
**Verified:** 2026-08-15
**Status:** gaps_found
**Re-verification:** No — initial verification

## Summary

This phase's own Plan 03 already ran the decisive test: a blocking, non-auto-advance rig-hardware checkpoint on the user's real Windows machine. That checkpoint returned **explicit FAIL verdicts for both Phase 22 success criteria**, fully documented in `22-03-SUMMARY.md`'s Task 2 section (17-check result table, two named root-cause hypotheses, and a final verdict table). This VERIFICATION.md treats that rig evidence as authoritative ground truth for every claim that cannot be checked from source alone — it does not re-derive or second-guess it, and it does not accept 22-01-SUMMARY.md's/22-02-SUMMARY.md's earlier `requirements-completed: [SETTINGS-01, SETTINGS-02]` claims, which were written before the rig test ran and are now directly contradicted by it. **ROADMAP.md itself already reflects this**: Phase 22's checkbox is explicitly annotated `(BLOCKED — rig verification FAILED 2026-08-15, gap-closure plan required)`.

The static/source-level work (three plans' worth of `TableLayoutPanel` migration, five static audits, a clean build/test regression gate) is real, substantive, and — as far as source analysis alone can prove — correct. That is not in dispute. What is in dispute, and what fails, is the actual rendered outcome on the platform this app ships to: the monitor grid and audio device picker are **entirely absent** from both mode columns (a stronger failure than "crowded"), and manual window resize does not work at all. Task completion (correct `TableLayoutPanel` structure in the Designer file) did not translate into goal achievement (an intentionally laid-out, functioning screen).

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | **SETTINGS-01** (roadmap SC): SettingsForm has no overlapping or crowded controls at its default window size | ✗ FAILED | 22-03-SUMMARY.md rig Check 1: grid + audio picker entirely absent from both mode columns (stronger than "crowded" — a total non-render). Rig Check 3: manual resize does not work. |
| 2 | **SETTINGS-02** (roadmap SC): Each mode's monitor grid, audio device pickers, app path control, and hotkey capture box are visually grouped and consistently spaced | ✗ FAILED | Same rig Check 1 — grouping cannot be evaluated when the grid and audio picker (the controls the criterion is about) don't render at all. |
| 3 | Two monitor grids sit side by side in an evenly split, resize-responsive two-column container, not hardcoded 396x234 panels at fixed x=12/x=420 (Plan 01) | ✗ FAILED | Source correct (`tlpModeColumns` with two `Percent(50F)` columns confirmed via direct read, lines 320-326; Audit 1 confirms zero `Location`/`ClientSize`/`Absolute` usages) — but on real hardware the grids inside that container do not render at all (rig Check 1). Structural correctness did not produce the claimed runtime behavior. |
| 4 | Each mode column contains its own monitor grid AND its own audio device picker (D-01 mode-based grouping) | ✗ FAILED | Source-level parentage is correct (22-03-SUMMARY.md Audit 2's 32-row control→parent table confirms `dgvMonitors`/`cboAudioRig` under `tlpRigColumn`, `dgvMonitorsNormal`/`cboAudioNormal` under `tlpNormalColumn`) — but rig Check 1: "Both modes only have explanations and monitor and audio are both missing" (user's own words). |
| 5 | The old shared `pnlAudioDevices` panel and its "Audio Devices" caption no longer exist anywhere in the form | ✓ VERIFIED | `grep -rc` for `pnlAudioDevices`/`lblAudioDevicesCaption` returns 0 across all of `src/` (22-03-SUMMARY.md Audit 2); string-literal diff against baseline confirms exactly one line removed (`"Audio Devices"`), nothing else. This is a pure source-code fact, unaffected by the rendering bug. |
| 6 | Shared full-width section (app path, hotkey, debug logging, tray/autostart checkboxes) exists below the two mode columns (D-02) | ? UNCERTAIN | Source-level parentage into `flpShared`/`pnlSharedSection` confirmed (Audit 2). Not rig-confirmed — rig checks 2 and 5-8 (which probe this section specifically) are recorded blocked/not-attempted because testing stopped at Check 3. Given the leading root-cause hypothesis describes a *cascading* AutoSize/Percent circularity across nested containers (`tlpRoot` → `tlpModeColumns` → mode columns, all three levels flagged), it cannot be assumed the shared section beneath is unaffected. |
| 7 | Phase 23's reserved insertion point (`pnlThemeReserved`) exists, empty, zero-size, with an identifying comment (D-04) | ? UNCERTAIN | Source confirmed present (`Size(0, 0)` at line 752, parented in `flpShared` per Audit 2). Not rig-confirmed for the same reason as #6 — its host container's rendering health is unverified. |
| 8 | Save Settings / Discard Changes sit right-aligned in their own row and can never truncate their text | ? UNCERTAIN | Source-level `AutoSize=true` + `MinimumSize` floor confirmed for both buttons (Audit 1). Rig Check 4 (button text at 125%) is recorded not-attempted — never reached. |
| 9 | The form has no `ClientSize` assignment, sizes itself to its content, and can be resized by dragging its edges with no maximize and no minimize button (D-05, D-06) | ✗ FAILED | `ClientSize` absence and `MaximizeBox=false` are confirmed source facts (Audit 1). But "can be resized by dragging its edges" is directly falsified: rig Check 3 — "settings window can be dragged to be resized but then nothing happens... resize preview flickers when dragged but then disappears, nothing resizes" (user's own words). |
| 10 | No control anywhere in `SettingsForm.Designer.cs` is positioned by a `Location` assignment | ✓ VERIFIED | `grep -c ".Location = new System.Drawing.Point("` returns 0 (Audit 1). Pure static source fact, independent of the rendering bug. |
| 11 | The solution still builds with 0 errors and the 82-test suite still passes (all three plans) | ✓ VERIFIED | Re-run live in 22-03: `dotnet build` → 0 Errors, 4 pre-existing unrelated warnings; `dotnet test` → 82/82 passed. Matches phase-base baseline exactly. |
| 12 | Blast radius is exactly one file; `SettingsForm.cs`/`ThemeApplier.cs`/every `.csproj`/`.sln` are byte-identical to the phase base commit `0c1234f` | ✓ VERIFIED | `git diff --stat 0c1234f -- src/` shows only `SettingsForm.Designer.cs` changed (523 insertions, 171 deletions); `git diff --stat 0c1234f -- '*.csproj' '*.sln'` empty; re-confirmed directly in this verification session. |

**Score:** 4/12 truths fully VERIFIED, 5/12 FAILED, 3/12 UNCERTAIN (blocked, never reached by the rig test).

**On the two truths that matter most — the roadmap Success Criteria themselves (#1, #2) — the verdict is unambiguous FAIL, corroborated directly by the user's own real-hardware report, not inferred.**

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/RigToggle.App/SettingsForm.Designer.cs` | `TableLayoutPanel`-based mode columns + shared section, replacing all `Location`/`ClientSize` positioning | ⚠️ **STRUCTURALLY CORRECT, RUNTIME HOLLOW** | Exists, substantive (694-line diff, no stub markers, no TBD/FIXME/XXX found in this verification's own grep pass), and wired (32/32 original controls confirmed present, parented, and event-wired per Audit 2/4). **Fails Level 4 (does it actually render real content on the target platform):** rig hardware confirms the monitor grid and audio picker rows collapse to invisible in both mode columns, and the form's `AutoSize=true` fights its own `FormBorderStyle.Sizable` resize. The file is a textbook example of "artifact exists, is substantive, is wired — and is still not the goal" because the failure is in WinForms' own layout-engine resolution of nested `AutoSize`/`Percent` containers, not in anything a static grep can catch. |
| `.planning/phases/22-settingsform-layout-pass/22-03-SUMMARY.md` | Recorded build/test output, five static audit results, and per-item rig verdict for both success criteria | ✓ VERIFIED | Present, contains the full 17-check rig result table, explicit per-criterion FAIL verdicts, and two named unconfirmed root-cause hypotheses for gap-closure research. This is exactly the artifact Plan 03's own `must_haves.artifacts` specified. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| `SettingsForm.Designer.cs` | `System.Windows.Forms.TableLayoutPanel` | `tlpModeColumns` two `Percent(50F)` `ColumnStyle`s hosting `pnlMonitorNormal`/`pnlMonitor` | ✓ WIRED (source) / ✗ **NOT OBSERVABLE ON HARDWARE** | Confirmed present in source (lines 320-330). Rig evidence shows the columns' *content* (grid, audio picker) does not render, even though the column scaffold itself is wired correctly. |
| `SettingsForm.Designer.cs` | `SettingsForm.cs` | `cboAudioNormal`/`cboAudioRig` instance fields resolvable by name after reparenting | ✓ WIRED | `ThemeApplier.*` calls in `SettingsForm.cs` target named instance fields, not a recursive `Controls` walk (`grep -n ".Parent"` returns 0 matches, confirmed in 22-03-SUMMARY.md Audit 5). Reparenting required zero code-behind changes, as designed. This link is a source-code fact and is unaffected by the rendering bug. |
| `SettingsForm.Designer.cs` | `SettingsForm.cs` | `AppPath_DragEnter`/`AppPath_DragDrop` wired to `pnlAppPath`, `txtAppPath`, and `tlpAppPath` (T-12-07) | ✓ WIRED (source) / ? **UNCONFIRMED ON HARDWARE** | `AllowDrop=true` on all three + both handler subscriptions confirmed (Audit 4: 3/3/3 matches). Rig Check 6 (drag-drop still works on the whole box) was never reached. |
| `.planning/phases/22-settingsform-layout-pass/22-03-SUMMARY.md` | `.planning/ROADMAP.md` | Explicit per-criterion verdict naming its evidence source | ✓ WIRED | 22-03-SUMMARY.md's final verdict table names SETTINGS-01/SETTINGS-02 explicitly with FAIL and cites the specific rig checks as evidence. ROADMAP.md's Phase 22 line is independently annotated `(BLOCKED — rig verification FAILED 2026-08-15, gap-closure plan required)`, confirming the link was actually acted on, not just written. |

### Data-Flow / Render Trace (Level 4 — adapted for WinForms rig evidence)

| Artifact | Claimed Behavior | Source Supports It? | Renders On Real Hardware? | Status |
|----------|-------------------|----------------------|----------------------------|--------|
| `dgvMonitors` / `dgvMonitorsNormal` inside `tlpRigColumn`/`tlpNormalColumn` | Visible monitor grid, 120px height floor, `Dock=Fill`/`Percent(100F)` row | Yes — `MinimumSize(0,120)` and `Percent(100F)` row confirmed (Audit 1, Audit 4) | **No** — rig Check 1: grid entirely absent | ✗ DISCONNECTED (row collapses to zero height; see Hypothesis 2, likely AutoSize/Percent circularity across `tlpRoot`→`tlpModeColumns`→mode columns) |
| `cboAudioNormal` / `cboAudioRig` inside `tlpAudioNormal`/`tlpAudioRig` (an `AutoSize` row, not the `Percent` row) | Visible audio picker per mode column | Yes — parentage and `AutoSize` row confirmed (Audit 2) | **No** — rig Check 1: audio picker entirely absent | ✗ DISCONNECTED — notably, this row is `AutoSize`, not `Percent`, so the leading hypothesis (Percent-row collapse) does not fully explain its absence; 22-03-SUMMARY.md itself flags this as a distinct open question for gap-closure research |
| `SettingsForm` window edge (`FormBorderStyle.Sizable` + `AutoSize=true`) | User can drag-resize the window | Yes — both properties confirmed set (lines 905-907) | **No** — rig Check 3: resize preview flickers, no actual resize | ✗ DISCONNECTED — classic `Form.AutoSize` vs. user-driven `WM_SIZING` conflict (Hypothesis 1), unconfirmed but well-grounded in the file's own pre-existing code comment about this exact interaction |

### Behavioral Spot-Checks

Step 7b: **SKIPPED** — this phase's subject matter (WinForms rendering, live window resize, live DWM theming) has no runnable entry point in this headless Linux sandbox. No server, CLI, or API surface exists to spot-check; the phase's own Plan 03 already substituted the correct mechanism (a blocking rig-hardware checkpoint) for what would otherwise be this step, and that checkpoint's result is used directly above.

### Probe Execution

Step 7c: **No probes found.** `find scripts -path '*/tests/probe-*.sh'` returned nothing, and no PLAN/SUMMARY file in this phase references a probe script. N/A for this phase.

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|--------------|----------------|--------------|--------|----------|
| SETTINGS-01 | 22-01, 22-02, 22-03 | SettingsForm has no overlapping or crowded controls at its default window size | ✗ **BLOCKED** | Rig Checks 1 and 3 (22-03-SUMMARY.md). **Note:** 22-01-SUMMARY.md and 22-02-SUMMARY.md both wrote `requirements-completed: [SETTINGS-01, SETTINGS-02]` in their frontmatter — those claims were made before the rig test ran and are now directly contradicted by 22-03's own hardware evidence. This is a concrete instance of the exact failure mode this verification process exists to catch: a SUMMARY claiming completion that the actual runtime behavior does not support. |
| SETTINGS-02 | 22-01, 22-02, 22-03 | Related controls (each mode's monitor grid, audio device pickers, app path, hotkey capture) are visually grouped and consistently spaced | ✗ **BLOCKED** | Same rig evidence as SETTINGS-01; same premature `requirements-completed` claim issue in 22-01/22-02-SUMMARY.md. |

**No orphaned requirements.** REQUIREMENTS.md's traceability table maps exactly SETTINGS-01 and SETTINGS-02 to Phase 22, and both are declared in all three plans' `requirements:` frontmatter field. Full coverage of the requirement *surface*; the requirements themselves are not yet *satisfied*.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/RigToggle.App/SettingsForm.Designer.cs` | — | `TBD`/`FIXME`/`XXX`/`TODO`/`HACK`/`PLACEHOLDER` | None found | Re-checked directly in this verification session (`grep -n -E "TBD\|FIXME\|XXX\|TODO\|HACK\|PLACEHOLDER"`) — zero matches. No debt markers, no stub-classification anti-patterns in the modified file. |
| `.planning/phases/22-settingsform-layout-pass/22-01-SUMMARY.md`, `22-02-SUMMARY.md` | frontmatter `requirements-completed` field | Premature completion claim | ℹ️ Info (documented above under Requirements Coverage, not a blocker on its own since the phase's own later plan caught and corrected it) | Illustrates why SUMMARY.md claims cannot be trusted at face value — both plans marked SETTINGS-01/02 complete a full plan-wave before the rig test that falsified that claim ran. No corrective edit was made to those two SUMMARY files' frontmatter after the FAIL was discovered; 22-03-SUMMARY.md is the authoritative override but the stale claim remains in the earlier files' frontmatter as written. |

No blocker-level anti-patterns (debt markers, empty implementations, hardcoded stub returns) found in the source diff itself. The blocker in this phase is not a code-smell — it is a demonstrated, hardware-confirmed runtime layout failure that static analysis structurally cannot detect, which is exactly why this phase's own plan correctly escalated to a mandatory rig-hardware checkpoint rather than declaring victory on green static audits alone.

### Human Verification Required

See `human_verification` in the frontmatter above. In summary, once a gap-closure plan lands a fix for Bug A (resize) and Bug B (grid/audio-picker collapse), the following must be re-run on real Windows rig hardware — this is not new work invented by this verification, it is the same 14 checks (2, 4-17) that 22-03-PLAN.md already specified and that the user's own report left blocked/not-attempted:

1. Baseline render (Check 1) — grid + audio picker actually visible in both columns
2. Manual resize (Check 3) — window actually tracks the drag
3. Shared-section rendering, no-minimize-button, tab order, drag-drop on the whole app-path box, validation-feedback visibility, live theme flip, AutoSize-vs-manual-resize interaction (Checks 2, 4-9)
4. Full repeat of grid/overlap/button-text/resize/whole-window-sanity/shared-section-width checks at 125% and 150% Windows display scale (Checks 10-17)

## Gaps Summary

Phase 22's static/structural work is real and correctly executed — three plans' worth of `TableLayoutPanel` migration replaced 100% of the form's pixel-positioned layout, preserved every one of 32 original controls with verbatim properties, and left a clean one-file blast radius. None of that is in question.

But the phase's actual goal — "SettingsForm reads as an intentionally laid-out screen instead of a crowded, organically-grown one" — is not achieved. On real Windows hardware, the form is not "intentionally laid out"; large parts of it (the monitor grid and audio device picker, in **both** mode columns) do not render at all, and the newly-added edge-resize capability does not work. This is a stronger failure than the "crowded controls" the phase set out to fix — controls that don't render can't be crowded, but they also can't be used, which is a regression relative to the pre-phase baseline where these controls at least worked (even if inelegantly laid out).

This phase's own Plan 03 already did the correct thing: it ran the mandatory rig checkpoint, recorded the FAIL honestly rather than inferring or assuming a pass, named two unconfirmed root-cause hypotheses to seed a gap-closure plan's research step, and explicitly declared "Phase 22 is not complete." This verification confirms that self-assessment rather than overriding it — both because the rig evidence is authoritative and because there is no static-analysis basis to disagree with it. ROADMAP.md already reflects the blocked state; no change to it was needed as part of this verification.

**Next step:** a gap-closure plan targeting Bug A (`Form.AutoSize` vs. manual resize conflict) and Bug B (`Percent`-row-inside-`AutoSize`-container collapse hypothesis, plus the separate open question of why the `AutoSize`-row audio picker is also missing) is required before a fresh rig-verification pass can close this phase.

---

*Verified: 2026-08-15*
*Verifier: Claude (gsd-verifier)*
