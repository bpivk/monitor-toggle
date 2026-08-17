---
phase: 19
slug: monitor-tile-dashboard-monitorpanelform-retirement
status: verified
threats_open: 0
asvs_level: 1
created: 2026-08-10
---

# Phase 19 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| OS display enumeration → app | `GetAllMonitors()`/CCD-derived `MonitorInfo` values (`FriendlyName`, `DevicePath`) drive tile count, ordering, numbering, and window sizing. Trusted OS data, but arbitrary in count/ordering across a hotplug event. | Display topology metadata |
| User input (mouse/keyboard) → tile / dialog | Click and Space/Enter on a tile reach `ActionRequested`; from Plan 19-03 onward this can trigger a real CCD mutation via the confirm dialog. | UI event → privileged action |
| OS event thread → UI thread | `SystemEvents.DisplaySettingsChanged` fires on a background thread at any time, including during process exit. | Cross-thread event delivery |
| Nested message loop (`ShowDialog`) → concurrent hotkey toggle | A global `WM_HOTKEY` can be dispatched while the tile confirmation dialog is open (nested loop), racing against the dialog's own mutation. | Concurrent privileged mutation |
| Pre-dialog `MonitorInfo` snapshot → post-dialog mutation | The device path acted upon may have been unplugged while the confirm dialog's nested loop was running. | Stale identity → mutation |
| Composition root → `MainForm` | Dependency wiring changes arity (5-arg ctor); a mis-wired argument would inject the wrong collaborator. | Constructor wiring |
| Build-environment claims → shipped behaviour | CI/dev environment cannot observe display hardware, theme flips, or DPI — gap between "static audit passed" and "the rig confirmed it". | Verification evidence |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-19-01 | Elevation of Privilege | `MonitorTile` | mitigate | `Controls/MonitorTile.cs` — 0 hits for `IMonitorController`/`ToggleOrchestrator`/mutation-API references; tile only raises `ActionRequested` (lines 82, 161, 170) | closed |
| T-19-02 | Denial of Service | `MonitorTile.OnPaint` | mitigate | Every GDI object (`GraphicsPath`/`Pen`/`SolidBrush`) in `using` (223-224, 264-265); `MonitorIconGeometry.cs` likewise; 0 `Bitmap` fields in either file | closed |
| T-19-03 | Denial of Service | `MonitorTile.OnPaint` | mitigate | `MonitorTile.cs:210,269-272` — whole paint body in try/catch, `Trace.WriteLine` on exception | closed |
| T-19-04 | Denial of Service | `MainForm.OnDisplaySettingsChanged` | mitigate | `MainForm.cs:969-993` — `IsDisposed` checked before `InvokeRequired`; `BeginInvoke` wrapped in `catch (ObjectDisposedException)`; whole body in bare catch | closed |
| T-19-05 | Denial of Service | `MainForm.RefreshMonitorTiles` | mitigate | `MainForm.cs:566-630` — reconciles `_tiles` against new count via grow/shrink; disposes only surplus tiles (614-620) | closed |
| T-19-06 | Information Disclosure | Tile tooltip/`AccessibleName` | accept | Local single-user desktop utility; same names already shown by the retired panel and `SettingsForm` | closed |
| T-19-07 | Spoofing | Tile identity vs. CCD order | mitigate | `MainForm.cs:571` sole `OrderBy(DevicePath, Ordinal)`; `MonitorTile.cs:80,131` — `DevicePath` is sole identity | closed |
| T-19-08 | Tampering | `OnTileAction` post-dialog mutation | mitigate | `MainForm.cs:728-733` — re-validates `_lastKnownMonitors` after `ShowDialog()`; shows "no longer connected" + refreshes instead of mutating if stale | closed |
| T-19-09 | Tampering | `OnTileAction` vs. concurrent hotkey | mitigate | `MainForm.cs:696-699,711,715` — exclusive lease acquired before dialog construction/`ShowDialog`, held via `using` across the mutation | closed |
| T-19-10 | Elevation of Privilege | Duplicate DISPLAY-12 guard | mitigate | `MainForm.cs:743` calls the same `DeactivateMonitors` both toggle directions call; 0 local survivor-counting patterns found by grep | closed |
| T-19-11 | Denial of Service | `BtnIdentify_Click` overlay construction | mitigate | `MainForm.cs:835-848` — per-overlay try/catch + `Trace.WriteLine`; loop continues past a single overlay failure | closed |
| T-19-12 | Spoofing | Identify overlay vs. tile numbering | mitigate | `MainForm.cs:812,823,831,850` — both derive from shared `_lastKnownMonitors`; counter increments on skip so ordinals stay aligned | closed |
| T-19-13 | Denial of Service | Deleting still-referenced collaborators | mitigate | `MonitorConfirmDialog.cs`/`MonitorIdentifyOverlay.cs` confirmed present; `ThemeMonitorGrid` still has 4 `SettingsForm.cs` callers | closed |
| T-19-14 | Repudiation | Partially-removed panel | mitigate | `grep -rn 'MonitorPanelForm' src/` (excluding comments) = 0 hits; 0 `[Obsolete]` shims; both panel source files absent from tree | closed |
| T-19-15 | Repudiation | Rig check recorded without being run | mitigate | `19-05-SUMMARY.md` — all 10 rig checks carry explicit verdicts across 4 rig rounds, each with root cause + fix commit, final APPROVED | closed |
| T-19-16 | Denial of Service | Invisible-to-CI DPI/theming defect | mitigate | `19-05-PLAN.md` Task 2 is a blocking `checkpoint:human-verify` gate; checks 8 (theming) and 10 (DPI 125%/150%) explicitly approved after 6 real defects found and fixed | closed |
| T-19-SC (×5, one per sub-plan 19-01..05) | Tampering | npm/pip/cargo installs | accept | `grep -c 'PackageReference' RigToggle.App.csproj` = 0 — zero external packages installed across all 5 sub-plans | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

Total register rows: 21 (15 unique `mitigate` threats + `T-19-06` accept + 5× `T-19-SC` accept, one per sub-plan).

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|--------------|------|
| AR-19-01 | T-19-06 | Monitor `FriendlyName` shown in tooltip/`AccessibleName` — local single-user desktop utility; identical names already surfaced by the retired panel and `SettingsForm`, so no new disclosure | Phase 19-02 plan author | 2026-08-10 |
| AR-19-02 | T-19-SC (×5) | No external packages (npm/pip/cargo/NuGet) installed in any of the 5 sub-plans — verified via `PackageReference` count = 0 | Phase 19 plan authors | 2026-08-10 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-10 | 21 | 21 | 0 | gsd-security-auditor |

### Observations (non-blocking)

- **New owner-draw paint handlers added during Plan 19-05 rig-fix rounds** (`MainForm.BtnToggle_Paint`, `BtnIdentify_Paint`, `BtnSettings_Paint`, commits `30482c1`/`44a9796`/`fe4168c`/`8de03b8`) are new `OnPaint`-equivalent surfaces not present when Plans 01-04's threat registers were authored, so they have no explicit threat-register entry. Verified directly: all three follow T-19-03's established try/catch + `Trace.WriteLine` swallow pattern (`MainForm.cs:1041-1054, 1065-1089, 1125-1150`), so no new DoS risk is actually open. Recommend folding these into T-19-03's scope on the next register update.
- Several plan-reported grep-count discrepancies in Plans 19-02 through 19-05's own SUMMARY.md files (e.g. `_lastKnownMonitors =` counting 3 vs. 2 expected) were independently re-derived from the code by this audit rather than trusted at face value; all held up as plan-authoring/grep-precision issues, not mitigation gaps.

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-10
