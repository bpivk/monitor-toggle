# Phase 5: Orchestration, Full Toggle & Packaging - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 5-Orchestration-Full-Toggle-Packaging
**Areas discussed:** Partial-failure reporting, Rig-mode failure isolation, .exe packaging specifics, Startup crash-recovery UX

---

## Partial-Failure Reporting

| Option | Description | Selected |
|--------|-------------|----------|
| Step checklist | Message box listing each step with status marker (disabled OK / FAILED / not attempted) | ✓ |
| Enhanced single message | One MessageBox, prefixed with which step it came from | |
| Current behavior is enough | Keep today's generic exception message | |

**User's choice:** Step checklist
**Notes:** Most literal reading of CORE-04 — the user sees exactly what succeeded/failed/skipped.

| Option | Description | Selected |
|--------|-------------|----------|
| Structured result object | ToggleService methods return a result instead of throwing; MainForm renders checklist from it | ✓ |
| Exception carries step context | Void methods still throw, but wrap exceptions with step-name context | |

**User's choice:** Structured result object
**Notes:** Cleaner contract, testable; accepted that it touches ToggleService's public API and existing tests.

---

## Rig-Mode Failure Isolation

| Option | Description | Selected |
|--------|-------------|----------|
| Stop on first failure | ToggleToRigMode stays linear/stop-on-first-failure, unlike restore | ✓ |
| Isolate all three like restore | Mirror ToggleToNormalMode's try-each-independently pattern | |

**User's choice:** Stop on first failure
**Notes:** Forward-direction steps have real ordering dependencies (no point switching audio/launching the app if monitor disable failed); keeps the failure surface simple to report.

| Option | Description | Selected |
|--------|-------------|----------|
| Both directions | Structured result also covers ToggleToNormalMode's existing restore isolation | ✓ |
| Rig-mode only | Only ToggleToRigMode gets the new structured result | |

**User's choice:** Both directions
**Notes:** Consistent checklist UX regardless of toggle direction.

---

## .exe Packaging Specifics

| Option | Description | Selected |
|--------|-------------|----------|
| csproj PublishProfile + docs | Add PublishProfiles/win-x64.pubxml + README documenting the publish command | ✓ |
| Docs-only, no profile | Just document the full dotnet publish command | |
| Build script | Wrap the publish call in build.ps1/publish.ps1 | |

**User's choice:** csproj PublishProfile + docs
**Notes:** —

| Option | Description | Selected |
|--------|-------------|----------|
| Bare default icon | No custom .ico, no version info | ✓ |
| Custom icon | Add a rig/racing-themed .ico | |

**User's choice:** Bare default icon
**Notes:** Personal single-user utility, not distributed to others.

---

## Startup Crash-Recovery UX

| Option | Description | Selected |
|--------|-------------|----------|
| Label is enough | Existing "Mode: Rig" label + flipped toggle button IS the crash-recovery signal | ✓ |
| One-time startup notice | Show a dismissible MessageBox on startup if already in rig mode | |

**User's choice:** Label is enough
**Notes:** CORE-05 already works structurally (D-14, snapshot-file presence) — this phase's job here is verification, not new code.

---

## Claude's Discretion

- Exact structured-result type shape (record vs. class, step-name representation).
- Whether the publish profile is a `.pubxml` file vs. inline conditioned `PropertyGroup`.
- README location and wording for publish instructions.

## Deferred Ideas

None — discussion stayed fully within phase scope.
