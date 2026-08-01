# Phase 9: Global Hotkey Trigger - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-31
**Phase:** 9-global-hotkey-trigger
**Areas discussed:** Hotkey capture UI, Registration mechanism/timing, Failure surfacing, Hotkey-vs-Settings-dialog race

**Mode:** `--auto` — Claude selected the recommended option for each question without interactive prompts (continuing the chain from Phase 8 closure, per user's confirmation).

---

## Hotkey Capture UI (TRIG-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Single "recording" textbox — captures next key combo pressed | One control, standard Windows "press a shortcut" UX | ✓ |
| Separate modifier checkboxes + key dropdown | Two+ controls, more explicit but less standard | |

**Selected:** Single recording textbox.
**Notes:** [auto] Matches the immediately-recognizable pattern used throughout Windows software; no default pre-filled (unconfigured/off by default, matching TRAY-02's autostart checkbox precedent).

---

## Registration Mechanism & Timing (TRIG-01)

| Option | Description | Selected |
|--------|-------------|----------|
| `RegisterHotKey`/`WM_HOTKEY` via `MainForm.WndProc`, registered at startup + on Settings Save | Two registration points, one shared helper | ✓ |
| Registration only at startup (Settings just persists the preference) | Simpler, but no immediate conflict feedback when the user picks a new combo | |

**Selected:** Both startup and Settings-Save registration attempts.
**Notes:** [auto] Immediate feedback on Save is better UX than waiting for a restart to discover a conflict. Hotkey handler mirrors `TrayToggleMenuItem_Click` exactly — same orchestrator call, same toast-only (never MessageBox) error surfacing — since NOTIF-01 explicitly lists "hotkey" as a covered trigger source.

---

## Failure Surfacing (TRIG-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Settings: inline warning, Save not blocked | Preference still saved even if currently conflicting | ✓ |
| Settings: block Save until the hotkey registers successfully | Prevents saving a "broken" preference, but the user may know the conflict is temporary | |
| Startup: toast via NotifyIcon.ShowBalloonTip | Reuses Phase 8's toast infrastructure | ✓ |
| Startup: silent log only | Fails the "not silently swallowed" requirement | |

**Selected:** Inline warning (non-blocking) in Settings; startup failures traced + toasted.
**Notes:** [auto] "Surfaced, not silently swallowed" doesn't require blocking Save — a user might legitimately want to save a combination that's temporarily taken. Startup toast covers the case where Settings isn't open to see an inline warning.

---

## Hotkey-vs-Settings-Dialog Race (TRIG-01 success criterion 3)

| Option | Description | Selected |
|--------|-------------|----------|
| Unregister hotkey while Settings is open, re-register on close | Zero possibility of a toggle racing an in-progress edit | ✓ |
| Ignore WM_HOTKEY while a "settings open" flag is set | Avoids unregister/re-register churn, but more state to track correctly | |
| Queue the hotkey press and fire after Settings closes | Most "nothing is ever lost" but adds real complexity for a rare edge case | |

**Selected:** Unregister/re-register bracketing `SettingsForm.ShowDialog()`.
**Notes:** [auto] Simplest and most robust — directly satisfies "explicitly suppressed... not left to race" without introducing queuing complexity for an edge case (pressing the hotkey while Settings happens to be open).

---

## Claude's Discretion

- Exact `AppSettings` field shape for the persisted hotkey (packed int vs. separate Keys/modifier fields) — left to planner.
- `RegisterHotKey`'s required unique hotkey-ID constant — left to planner (single fixed ID suffices, only one hotkey exists).
- Whether the registration helper lives on `MainForm` or a small dedicated class — left to planner.

## Deferred Ideas

None — discussion stayed within phase scope. CLI trigger and single-instance IPC remain correctly scoped to Phase 10.
