---
phase: 22
slug: settingsform-layout-pass
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 1
created: 2026-08-16
---

# Phase 22 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Windows shell → `pnlAppPath`/`tlpAppPath`/`txtAppPath` drag-drop | Only untrusted external input path in the form — a dropped `.lnk`/`.exe` path string, validated (extension filter, existence check) before persisting as the launch target. | Filesystem path string |
| `IAudioDeviceEnumerator` / `IMonitorController` → combos and grids | OS-supplied device/monitor friendly names rendered into `ComboBox`/`DataGridView`. | Display strings (device/monitor names) |
| Validation feedback (`ErrorProvider` icons + warning labels) → user's save decision | If reparenting/reflow clips an icon or collapses a label, an invalid configuration could be saved with no visible objection. | UI validation state |
| Persisted settings → autostart registry Run value | `chkStartWithWindows` drives a real registry write; untouched by this phase, only relocated. | Registry write |
| Verification evidence (Plans 03/05) → phase-complete decision | Rig checkpoints are the only thing that can close the phase; a weakened or eyeballed pass would let a broken layout ship as verified. | Human-attested PASS/FAIL evidence |
| Human operator → live Windows display-scale setting | Rig sessions mutate a real system display-scale setting three times and must restore it. | OS display setting |
| Rig session artifacts → planning record | Screenshots/notes could incidentally capture the user's real app path and device names. | Personal filesystem/device metadata (single-user machine) |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-22-01 | Tampering | `pnlAppPath` drop target shrinking to `txtAppPath`, silently dropping a valid drag-drop | medium | mitigate | `tlpAppPath.AllowDrop=true` + handler wiring on all 3 targets; verified by grep count + rig check 15 (confirmed PASS on hardware) | closed |
| T-22-02 | Tampering | `ErrorProvider` icon clearance clipped after `Dock=Fill`/`Anchor` reflow | medium | mitigate | 20px right `Margin` reserved on every error-target control; rig check 13 confirmed icon renders | closed |
| T-22-03 | Tampering | Warning labels collapsing to one line under `AutoSize` rows, hiding a multi-line message | low | mitigate | `AutoSize=false` + explicit `MinimumSize` floor equal to prior height; no visibility logic changed | closed |
| T-22-04 | Tampering | Grid `AutoSizeColumnsMode.Fill` math breaking inside new `TableLayoutPanel` cell, clipping Off/On checkbox columns | medium | mitigate | Column config untouched (hard constraint); 120px `MinimumSize` floor; rig checks 2/5 verified column widths at 125%/150% | closed |
| T-22-05 | Spoofing | Normal/Rig columns visually confusable after left/right swap | medium | mitigate | Verbatim per-column captions/explain text retained as row 0/1; rig check 1 confirmed unambiguous labelling | closed |
| T-22-06 | Repudiation | A "layout-only" change quietly altering text/tooltips/event wiring | low | mitigate | Grep acceptance criteria on exact caption strings, control counts; empty `git diff` required for `SettingsForm.cs`/`ThemeApplier.cs` | closed |
| T-22-07 | Tampering | Path-validation `ErrorProvider` icon clipped by new `Anchor=Left\|Right` cell edge | medium | mitigate | 20px right `Margin` on `txtAppPath`/`txtHotkey`/`chkStartWithWindows`; rig check 13 confirmed | closed |
| T-22-08 | Information Disclosure | `chkEnableDebugLogging` reworded/reordered causing unintentional enablement of file logging | low | mitigate | Verbatim `Text` literal enforced by grep; own row retained, path spelled out in caption | closed |
| T-22-09 | Elevation of Privilege | `chkStartWithWindows` registry-write behavior changing as side effect of relocation | low | accept | No code-behind change permitted; `git diff` against baseline required empty for `SettingsForm.cs` | closed |
| T-22-10 | Denial of Service | `AutoSize=GrowAndShrink`+`Sizable` producing unusable window sizing | high | mitigate | Control-level `MinimumSize` floors on all labels/grids; rig checks 6/7/8/12 exercised fresh-open sizing and edge dragging at 3 scales — initial FAIL, resolved via Plan 04 gap-closure and confirmed PASS on Plan 05 re-run | closed |
| T-22-11 | Repudiation | Phase 23's reserved theme slot quietly used to ship a half-built control | low | mitigate | Acceptance criterion asserts zero `Text`/`BorderStyle`/`Controls.Add` on `pnlThemeReserved` | closed |
| T-22-12 | Repudiation | Rig check claimed without being performed (esp. 125%/150%, easiest to skip) | medium | mitigate | Per-grid/per-scale/per-check answers required, not a single verdict; SUMMARY records Windows build + binary path | closed |
| T-22-13 | Tampering | Defect found during verification patched inline, producing an unverified change | low | mitigate | Verification plans change no source (`git status --porcelain src/` empty required); defects routed to gap-closure plans with their own gate (Plan 04) | closed |
| T-22-14 | Tampering | Validation feedback silently lost in migration | medium | mitigate | Audit asserted all 18 `SetError` call sites survive with icon clearance; rig check 7 confirmed on hardware | closed |
| T-22-15 | Tampering | Drag-drop target narrowing to text box only | medium | mitigate | Audit asserted 3 `AllowDrop`/handler wirings; rig check 6 tested empty-area and text-field independently | closed |
| T-22-16 | Denial of Service | Content-driven `AutoSize` overshooting screen at 150%, or snapping back over manual resize | high | mitigate | Rig checks 15/9 targeted directly; initial FAIL addressed via Plan 04 `Screen.FromControl(this).WorkingArea` clamp, confirmed PASS on Plan 05 | closed |
| T-22-17 | Tampering | User's display scale left at 150% after rig session | low | mitigate | SUMMARY required to confirm scale restoration | closed |
| T-22-18 | Information Disclosure | Rig screenshots capturing user's real app path/device names | low | accept | Single-user personal project on own machine; evidence recorded as PASS/FAIL notes, not screenshots | closed |
| T-22-19 | Spoofing | Source-only fix presented as confirmed behavioral fix, closing phase without second rig pass | high | mitigate | Phase could not close without Plan 05's blocking rig checkpoint; both gap-closure tasks forbid a "fixed" claim | closed |
| T-22-20 | Tampering | Gap-closure fix flattening `Percent 100F` rows to `AutoSize`, regressing grid growth behavior | medium | mitigate | Exact-count assertions on all `Percent 100F`/`Percent 50F`/`AutoSize` row styles in verify command | closed |
| T-22-21 | Tampering | `SettingsForm.cs` touch expanding beyond the one justified `OnLoad` override | low | mitigate | Diff capped at 2 files; acceptance criteria require single-method addition | closed |
| T-22-22 | Tampering | Reintroducing hardcoded pixel positioning under guise of a sizing fix | medium | mitigate | `Location`/`ClientSize`/`SizeType.Absolute` asserted 0 in Designer file; no numeric literals in new `OnLoad` body | closed |
| T-22-23 | Denial of Service | `MinimumSize` floor or content-driven `ClientSize` exceeding screen at 150% | high | mitigate | `WorkingArea` clamp bounds initial size and derived floor; rig check 15 confirmed on hardware | closed |
| T-22-24 | Repudiation | Deliberate deviation from "SettingsForm.cs byte-identical" invariant going unrecorded | low | mitigate | Deviation stated in plan; SUMMARY required to record it explicitly | closed |
| T-22-25 | Repudiation | Final rig check claimed without being performed | medium | mitigate | Per-grid/per-box/per-scale answers required; SUMMARY records Windows build + binary path | closed |
| T-22-26 | Repudiation | Premature `requirements-completed` claim written before user's result returned | medium | mitigate | Explicit acceptance criterion forbidding it, naming the two prior SUMMARY files that made this error | closed |
| T-22-27 | Tampering | Newly-found defect patched inline during final session | low | mitigate | No source change permitted; empty `git status --porcelain src/` required; defects route to new gap-closure plan | closed |
| T-22-28 | Denial of Service | Session stopping at first failure, leaving remaining checks unexercised | low | mitigate | Explicit instruction to continue past failure; known-failing checks ordered first | closed |
| T-22-29 | Tampering | Validation feedback or drag-drop target silently lost while fixing layout | medium | mitigate | Checks 6/7 tested independently on real hardware | closed |
| T-22-30 | Denial of Service | New `MinimumSize` floor larger than expected, or working-area clamp misbehaving at 150% | medium | mitigate | Check 3(c)/15 targeted directly; framed as intentional in interfaces | closed |
| T-22-31 | Tampering | User's display scale left at 150% after final session | low | mitigate | Acceptance criterion requires SUMMARY to confirm restoration | closed |
| T-22-32 | Information Disclosure | Rig screenshots capturing real app path/device names into planning record | low | accept | Single-user personal project; evidence is PASS/FAIL notes, not screenshots | closed |
| T-22-SC | Tampering | npm/pip/cargo/NuGet installs | low | accept | Zero packages added across all 5 plans; `22-RESEARCH.md` Package Legitimacy Audit confirms no external packages | closed |

*Status: open · closed · open — below {block_on} threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-22-01 | T-22-09 | Registry autostart write behavior untouched by this layout-only phase; hard constraint + empty-diff acceptance criterion confirm no code-behind change | Phase plan (22-02-PLAN.md) | 2026-08-16 |
| AR-22-02 | T-22-18, T-22-32 | Single-user personal project on the user's own machine; rig evidence recorded as PASS/FAIL notes, not screenshots | Phase plan (22-03/05-PLAN.md) | 2026-08-16 |
| AR-22-03 | T-22-SC | Zero external packages added across all 5 plans in this phase | Phase plan (all plans) | 2026-08-16 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-16 | 33 | 33 | 0 | /gsd-secure-phase (retroactive, from PLAN.md threat models + confirmed rig UAT) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-16
