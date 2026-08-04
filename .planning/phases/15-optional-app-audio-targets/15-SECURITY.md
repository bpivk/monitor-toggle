---
phase: 15
slug: optional-app-audio-targets
status: verified
threats_open: 0
asvs_level: 1
created: 2026-08-04
---

# Phase 15 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| persisted settings → ToggleService | Nullable audio-device IDs / app path loaded from `%APPDATA%\RigToggle\settings.json` cross into toggle logic | Local config values (device IDs, file path) — no network, single-user machine |
| ToggleService → OS (File.Exists → Process.Start; audio device set) | Configured targets are resolved and acted on at toggle time | File path existence check, audio endpoint ID |
| Settings UI → settings.json | User selections for app path / audio devices are persisted as (now legitimately nullable) fields | Local UI input → local JSON file |
| operator → running app on rig | Human-driven rig verification of persisted-settings-to-toggle behavior against real hardware | N/A — verification boundary, not data flow |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-15-01 | Information Disclosure | `ToggleStepOutcome.Skipped` vs `NotAttempted` | mitigate | Distinct enum member + doc comment keeps "deliberately unconfigured" from being misread as "blocked by failure" (Plan 15-01). Verified: `ToggleStepOutcome.cs` carries a dedicated `Skipped` member, rendered distinctly by `ToggleResultFormatter`, 70/70 tests pass. | closed |
| T-15-02 | Information Disclosure / Tampering | Optional Audio/App step guards | mitigate | Null/empty → `Skipped`; set-but-unresolvable → `Failed` via `TryResolveDevice`/`File.Exists` inside the step body, preventing a broken/removed target from being silently downgraded to "skipped" (Plan 15-02). Verified: `TryExecuteOptionalStep` in `ToggleService.cs`, 6 new paired Skipped/Failed tests, 75/75 tests pass. | closed |
| T-15-03 | Denial of Service (safety-relevant step skipped) | `IsFullyConfigured` relaxation | accept | Only Audio/App become optional; the monitor-disable step (the safety-relevant display action) is NOT optional and still gates the toggle via the monitor-set check. No safety step can be silently disabled by leaving a field unset. Low severity, local single-user tool. | closed |
| T-15-04 | Tampering | `_pendingAppPath` vs `txtAppPath.Text` | mitigate | Persist the app path from the dedicated `_pendingAppPath` field, never from the display textbox whose literal "No app shortcut..." text would otherwise round-trip into `AppSettings` (Plan 15-03). Verified in code: `SettingsForm.cs:894` — `CompanionAppPath = _pendingAppPath`. | closed |
| T-15-05 | Denial of Service (Save gate drift) | `ValidateSettingsForm` vs `ToggleService.IsFullyConfigured` | mitigate | Save gate relaxed to monitor-set-only in lockstep with `IsFullyConfigured`'s relaxation; a configured-but-broken audio device or app path still blocks Save ("broken != unset"). Verified in code: `SettingsForm.cs:690` (`appPathOk = _pendingAppPath is null \|\| IsValidLaunchTarget(...)`) and rig-confirmed (D-06 checkpoint item). | closed |
| T-15-06 | Repudiation / Information Disclosure | Skipped-vs-Failed rendering on the rig | mitigate | Rig checkpoint (Plan 15-04) explicitly verified unset renders as Skipped and broken renders as FAILED, both toggle directions, via the shared formatter. User confirmed all six on-rig checks "approved". | closed |
| T-15-09 | Tampering (TOCTOU) | `File.Exists` → later `Process.Start` in App step | accept | Pre-existing accepted risk T-03-09 from Phase 3 (fail-fast UX guard, not a security control). Moving the check into the App step body does not change its strictness. Not re-litigated this phase. Low severity, local single-user tool. | closed |
| T-15-SC | Tampering | npm/pip/cargo installs | accept | No packages added in Phase 15 (confirmed across all four plans — RESEARCH Package Legitimacy Audit: N/A each time). Nothing to verify. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-15-01 | T-15-03 | Only Audio/App are made optional; the monitor-disable step remains mandatory and continues to gate the toggle, so no safety-relevant action can be silently skipped. | Plan 15-02 (author-time disposition) | 2026-08-04 |
| AR-15-02 | T-15-09 | Pre-existing accepted risk (T-03-09, Phase 3) — TOCTOU window between `File.Exists` and `Process.Start` is an inherent fail-fast UX guard, not a security boundary, for a local single-user tool. | Plan 15-02 (author-time disposition, carried forward) | 2026-08-04 |
| AR-15-03 | T-15-SC | No third-party packages added during Phase 15. | Plans 15-01–15-04 (author-time disposition) | 2026-08-04 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-04 | 8 | 8 | 0 | gsd-secure-phase (orchestrator, retroactive verification against plan-time threat register — all 4 plans authored a `<threat_model>` block, no gaps) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-04
