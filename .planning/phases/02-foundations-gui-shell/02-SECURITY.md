---
phase: 2
slug: foundations-gui-shell
status: verified
threats_open: 0
asvs_level: 1
created: 2026-07-24
---

# Phase 2 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Developer machine → NuGet registry | WindowsDisplayAPI/NAudio third-party package code pulled and compiled into the app | Supply-chain (compiled binary) |
| Disk ↔ app | settings.json / state.json read/written on shared local filesystem; hand-edited/corrupt file crosses back into the app | Local config/state (device IDs, file paths) |
| User file-picker input → persisted CompanionAppPath | Chosen executable path validated before being written to settings.json | File path |
| Persisted settings → enumerated hardware | Saved device/path ID may no longer resolve on reopen | Device ID / file path |
| Configured file path → process lookup | Companion-app path decomposed to a process name for detection (read-only this phase) | Process name string |
| Native COM/CCD APIs → managed adapter | WindowsDisplayAPI/NAudio marshal native display/audio state into managed objects | Display/audio device state |
| Disk (state.json presence) → startup mode | App trusts snapshot-file presence to decide Rig vs Normal on launch | Boolean mode signal |
| Composition root → adapters | The one place native adapters are instantiated; forms receive only interfaces | N/A (architectural boundary) |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-02-SC | Tampering | NuGet installs (WindowsDisplayAPI, NAudio) | mitigate | Blocking human-verify gate approved before install (02-01-SUMMARY.md); versions pinned exactly — `RigToggle.Windows.csproj` lines 13-14: `WindowsDisplayAPI Version="1.3.0.13"`, `NAudio Version="2.3.0"` | closed |
| T-02-ELEV | Elevation of Privilege | App/Windows csproj manifest | mitigate | No elevation manifest/`requestedExecutionLevel`/`ApplicationManifest` anywhere under `src/` (verified: `find src -iname "*.manifest"` and `grep -rl requestedExecutionLevel\|ApplicationManifest src` both empty); `asInvoker` default preserved in both `RigToggle.App.csproj` and `RigToggle.Windows.csproj` | closed |
| T-02-CORRUPT | Tampering / DoS (self-inflicted) | JsonSettingsStore / JsonSnapshotStore Save | mitigate | Atomic write: temp file + `File.Move(tempPath, _path, overwrite: true)` — `JsonSettingsStore.cs:61-63`, `JsonSnapshotStore.cs:34-36` | closed |
| T-02-BADJSON | Tampering | JsonSettingsStore.Load on hand-edited/corrupt file | mitigate (upgraded from accept by CR-01) | `try/catch (JsonException)` and `catch (IOException)` around deserialize, degrades to fresh `AppSettings()` — `JsonSettingsStore.cs:34-50` | closed |
| T-02-APPEXEC | Elevation/Tampering (deferred) | IsRunning path handling | accept (this phase) | Detection is read-only; `WindowsAppController.cs` contains zero `Process.Start` calls — `IsRunning` uses only `Process.GetProcessesByName` (line 31), `LaunchOrFocus`/`MinimizeIfRunning` are empty no-op stubs (lines 48-58). Logged in Accepted Risks Log below | closed |
| T-02-COMLEAK | Denial of Service (self-inflicted) | Repeated MMDeviceEnumerator creation | mitigate | Fresh `using var enumerator = new MMDeviceEnumerator()` per call in `GetPlaybackDevices`, `CaptureState`, `TryResolveDevice` — never cached as a field (`WindowsAudioController.cs:18,41,79`); strengthened by WR-03 (each `MMDevice` also disposed via `using` at lines 25, 42, 80) | closed |
| T-02-NULLID | Denial of Service | GetDevice on a missing saved audio ID | mitigate | `TryResolveDevice` — null-check (line 72-75) + `try/catch (Exception)` (lines 77-86) around `GetDevice` — `WindowsAudioController.cs:70-87` | closed |
| T-02-BADPATH | Tampering / arbitrary-executable pointer | CompanionAppPath save-time validation | mitigate | `IsValidExePath` requires `File.Exists` + case-insensitive `.exe` extension (`SettingsForm.cs:220-223`); gates `ValidateSettingsForm` (line 215) and re-checked as a defensive guard immediately before `_settingsStore.Save` in `BtnSaveSettings_Click` (line 245) | closed |
| T-02-STALECRASH | Denial of Service | Reopening Settings after a saved device/path disappears | mitigate | D-10 stale-selection handling: unselected + `ShowStaleWarning` inline warning across monitor/audio/app-path pickers (`SettingsForm.cs:110,170,197,202-208`); enumeration calls wrapped in `try/catch` degrading to empty-state (lines 64-72, 121-128), never propagating | closed |
| T-02-FIRSTRUN | UX correctness (not security) | First-run warning suppression | mitigate | `savedId is not null` gates the stale-warning branch in all three pickers; a null saved ID takes the no-warning branch (`SettingsForm.cs:98-112`, `161-172`, `183-199`) | closed |
| T-02-MODESPOOF | Spoofing (mode state) | Startup mode derivation from state.json | accept (single-user local) | `ToggleService.IsInRigMode() => _snapshotStore.Exists()` (`ToggleService.cs:107`) — intentional design (D-14). Logged in Accepted Risks Log below | closed |
| T-02-FAKEFAIL | Info disclosure / silent failure | Toggle exception handling | mitigate (basic) | `BtnToggle_Click` wraps the full toggle call in `try/catch (Exception)`, shows a clear `MessageBox` (`MainForm.cs:89-99`); full CORE-04 partial-failure reporting explicitly deferred to Phase 5 | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

**Note:** T-02-ELEV was authored independently in both 02-01-PLAN.md and 02-05-PLAN.md with identical disposition and mitigation; collapsed into a single register row since both plans verify the same structural fact (no manifest anywhere under `src/`).

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-02-01 | T-02-APPEXEC | Companion-app detection is read-only (`Process.GetProcessesByName`) this phase; no `Process.Start`/execution occurs anywhere in `WindowsAppController` until Phase 3, where the already-enforced `.exe`-existence validation (T-02-BADPATH, Settings save-time) precedes any execution. No privilege boundary is crossed by read-only process enumeration. | Plan 02-03 (declared at plan time) | 2026-07-24 |
| AR-02-02 | T-02-MODESPOOF | Mode is intentionally derived from snapshot-file (`state.json`) presence rather than a separate trusted flag, so a crash while in Rig mode self-heals correctly on the next launch (D-14). A manually created/deleted `state.json` only mis-signals mode to the sole local user of a single-user personal utility — no privilege boundary, no multi-tenant exposure. Acceptable at ASVS Level 1. | Plan 02-05 (declared at plan time) | 2026-07-24 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Attack Surface (Informational — not blockers)

These items were found during verification but do not map to a declared threat ID in the Phase 2 register. Per audit policy these are `unregistered_flag` (WARNING), not `OPEN_THREATS` (BLOCKER), since `block_on: high` and none of these represent an un-mitigated *declared* threat.

1. **`JsonSnapshotStore.Load()` unguarded deserialize** (`JsonSnapshotStore.cs:39-40`) — unlike `JsonSettingsStore.Load()` (mitigated for T-02-BADJSON), `JsonSnapshotStore.Load()` has no `try/catch` around `JsonSerializer.Deserialize`. A corrupted/hand-edited `state.json` would throw an unhandled `JsonException`/`IOException` out of `ToggleService.ToggleToNormalMode()`. This was explicitly identified during code review as finding **IN-01** and explicitly deferred ("excluded by `fix_scope: critical_warning`... left for a future `--all` pass or manual fix" — 02-REVIEW-FIX.md). **Blast radius is currently contained**: the only caller of `ToggleToNormalMode()` is `MainForm.BtnToggle_Click`, which wraps the entire call in a generic `catch (Exception)` (T-02-FAKEFAIL's mitigation), so this would surface as the generic "Something went wrong" message box rather than crashing the app — but this relies on a UI-layer catch-all rather than the same defense-in-depth pattern used for settings.json. **Recommendation:** apply the same `try/catch (JsonException)`/`catch (IOException)` degrade-to-null pattern used in `JsonSettingsStore.Load()`, or formally register a T-02-BADJSON-SNAPSHOT threat with an `accept` disposition if the team judges the existing catch-all sufficient.
2. **WR-01 (incomplete-settings guard)** — added during code review, not originally in the Phase 2 threat register. Now closed in code (`ToggleService.IsFullyConfigured`/`IsSettingsConfigured`, `MainForm.BtnToggle_Click` pre-check) — informational only, no action needed.
3. **WR-02 (Process handle disposal)** — added during code review as a native-handle-leak fix adjacent to T-02-COMLEAK's DoS category but for `Process` objects rather than COM. Now closed in code (`WindowsAppController.IsRunning`, lines 31-45) — informational only, no action needed.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-07-24 | 12 | 12 | 0 | gsd-security-auditor |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-07-24
