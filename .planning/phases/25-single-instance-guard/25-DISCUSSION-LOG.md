# Phase 25: Single-Instance Guard - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-20
**Phase:** 25-single-instance-guard
**Areas discussed:** Duplicate-launch feedback, Internal-relaunch bypass contract, Verification approach

---

## Duplicate-launch feedback

| Option | Description | Selected |
|--------|-------------|----------|
| Silent focus only | Window comes to front, no toast — matches the tray-icon left-click restore path, which is also silent | ✓ |
| Toast on duplicate launch | Reuse `notifyIcon.ShowBalloonTip` the same way toggle results and hotkey failures are surfaced | |
| Toast only if it was tray-hidden | No toast when already visible; toast only when restoring from fully tray-hidden | |

**User's choice:** Silent focus only

| Option | Description | Selected |
|--------|-------------|----------|
| One universal rule | Any second launch — double-click, autostart race, accidental relaunch — treated identically: blocked, existing instance focused | ✓ |
| Something else | Freeform | |

**User's choice:** One universal rule
**Notes:** No special-casing by launch source or reason for the duplicate launch.

---

## Internal-relaunch bypass contract

| Option | Description | Selected |
|--------|-------------|----------|
| Dedicated CLI flag | A specific argument like `--apply-update <args>`, parsed by a new `StartupArgs.TryGetApplyUpdateArgs(args)` helper mirroring `ShouldStartHidden(args)`; checked first in `Main()`, transfers control before mutex/tray/hotkey path is reached | ✓ |
| Generic bypass flag | A general-purpose `--bypass-single-instance` flag that skips only the mutex check, still running the normal startup sequence after | |

**User's choice:** Dedicated CLI flag

| Option | Description | Selected |
|--------|-------------|----------|
| Real `--apply-update` flag now | Commit to the actual flag name Phase 26 will use, even without real update-apply logic behind it yet; scripted test invokes it with dummy args and asserts the mutex/tray/hotkey path was skipped. Phase 26 replaces the placeholder body only. | ✓ |
| Generic placeholder flag | A neutral name (e.g. `--internal-relaunch`) not tied to "update," leaving Phase 26 to decide its own final flag name on top | |

**User's choice:** Real `--apply-update` flag now
**Notes:** Locks the flag name and parse contract now so Phase 26 doesn't re-decide it — user explicitly favored committing to the final name over a generic placeholder.

---

## Verification approach

| Option | Description | Selected |
|--------|-------------|----------|
| Automated xUnit tests | Add to `RigToggle.Tests`, launching the built/published exe as a real child process N times, asserting process count + bypass behavior; runs in CI, matches Phase 7's 4 deterministic reentrancy tests | ✓ |
| Standalone dev script | A PowerShell/batch script outside the test suite, run manually against the built exe | |

**User's choice:** Automated xUnit tests

| Option | Description | Selected |
|--------|-------------|----------|
| Run in normal CI | Include in the default test run in `build.yml`; catches a regression automatically on every push | ✓ |
| Local/rig-only, skip CI | Tag to skip CI, rely on manual local runs + rig verification, to avoid hosted-runner flakiness | |

**User's choice:** Run in normal CI

---

## Claude's Discretion

- Activation-signal mechanism to wake the existing instance (`RegisterWindowMessage`/`PostMessage(HWND_BROADCAST,...)` per STACK.md vs. named pipe per ARCHITECTURE.md's data-flow diagram — the two research docs disagree; not raised as a user concern, resolve during research/planning)
- Named-mutex GUID value and exact handling of the PITFALLS.md Pitfall 8 receiver-ready race (retry-with-backoff on the loser, early receiver setup on the winner, or both)
- Whether `SingleInstanceGuard` is a new standalone class (expected, following the `ToggleOrchestrator` precedent) — exact shape left to planning
- Test harness specifics: build vs. publish output target, rapid-launch iteration count, CI flakiness mitigation if it appears

## Deferred Ideas

None — discussion stayed within phase scope; `todo.match-phase` returned zero matches.
