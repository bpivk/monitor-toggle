# Phase 3: App & Audio Control - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 3-App-Audio-Control
**Areas discussed:** Audio role granularity, Post-switch verification, Companion app edge cases (preflight, launch/focus retry scope)

---

## Audio Role Granularity

| Option | Description | Selected |
|--------|-------------|----------|
| One device, all 3 roles | Settings keeps a single dropdown per mode; SetDefault/Restore applies that device to eConsole, eMultimedia, eCommunications internally | ✓ |
| Per-role pickers | Settings gains up to 6 dropdowns (3 roles x 2 modes) for independent per-role routing | |

**User's choice:** One device, all 3 roles
**Notes:** Per-role pickers rejected as over-engineering for a personal 2-device rig setup.

---

## Post-Switch Verification

| Option | Description | Selected |
|--------|-------------|----------|
| Verify and report mismatch | Re-query default device after SetDefault per role; treat mismatch as a failure signal | ✓ |
| Trust the API result | Treat a non-throwing SetDefaultEndpoint call as success, no re-query | |

**User's choice:** Verify and report mismatch
**Notes:** Directly addresses PITFALLS.md Pitfall 6 (silent success-but-no-effect).

---

## Mismatch Reporting Mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Throw an exception | Bubbles up through ToggleService/MainForm's existing exception handling now; Phase 5 can later replace with richer per-step reporting | ✓ |
| Log only, don't throw | Verification runs but mismatch only recorded internally, no user-visible signal until Phase 5 | |

**User's choice:** Throw an exception
**Notes:** Phase 5 (CORE-04) owns full step-by-step failure reporting; Phase 3 just needs a visible signal now rather than trusting a lying HRESULT.

---

## Companion App Edge Cases (missing path / no window handle)

| Option | Description | Selected |
|--------|-------------|----------|
| Fail loud / wait-and-retry | Missing exe path throws clearly; running-but-no-window gets a brief retry window before giving up gracefully | ✓ |
| Best-effort silent | Missing path or no window handle silently skipped, no error surfaced | |

**User's choice:** Fail loud / wait-and-retry
**Notes:** Best-effort silent risks a toggle silently failing to launch the companion app with no visible signal.

---

## Preflight Ordering for Missing App Path

| Option | Description | Selected |
|--------|-------------|----------|
| Preflight check first | Verify .exe path exists before any monitor/audio mutation in ToggleToRigMode; bad path fails clean, nothing touched | ✓ |
| Check at launch step only | Leave ordering as-is (monitor → audio → app last); throw only when reaching the launch step, after monitor/audio already mutated | |

**User's choice:** Preflight check first
**Notes:** Avoids a half-switched state on the most common misconfiguration (app moved/uninstalled since Settings was saved).

---

## Retry Scope for Window-Handle Wait

| Option | Description | Selected |
|--------|-------------|----------|
| Retry only on fresh launch | Poll MainWindowHandle for a few seconds after Process.Start; if already-running with a zero handle, don't retry — move on immediately | ✓ |
| Retry in both cases | Poll for a window handle for a few seconds whenever it's zero, whether freshly launched or already running | |

**User's choice:** Retry only on fresh launch
**Notes:** Retrying for an already-running, genuinely tray-only app just adds a pointless multi-second delay every toggle.

---

## Claude's Discretion

- Exact retry/poll duration and interval for the fresh-launch window-handle wait — behavior was settled (retry only on fresh launch), not the precise seconds/interval.
- COM interop specifics (vtable layout, GUIDs, object lifecycle/disposal per call) beyond what STACK.md already specifies (modern-only, no Vista fallback).

## Deferred Ideas

None — discussion stayed within phase scope. Full step-by-step partial-failure reporting/recovery (CORE-04) remains correctly scoped to Phase 5.
