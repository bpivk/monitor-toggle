---
phase: 25
slug: single-instance-guard
status: verified
threats_open: 0
asvs_level: 1
created: 2026-08-21
---

# Phase 25 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Compiled from the `<threat_model>` blocks authored at plan time in all four plans (25-01 through 25-04) — `register_authored_at_plan_time: true`. ASVS L1, block-on threshold `high`. No threat in any plan is rated `high` or `critical`, so nothing meets the block threshold; per the short-circuit rule this register was compiled directly from the plan-authored threat models without a separate auditor pass.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| any local process (same desktop, same integrity level) → the named `Global\RigToggle-*` mutex and readiness handle | Named kernel objects addressable by name by any process that can guess/read the name; the name is compiled into a shipped exe, treated as public. | Presence/ownership signal only, no payload |
| any local process (same desktop) → the registered broadcast window message received by `MainForm.WndProc` | `RegisterWindowMessage` returns the same id to any caller passing the same string; `HWND_BROADCAST` reaches every top-level window on the desktop. | wParam/lParam both zero — no payload delivered |
| the losing process → the winning process | One-way, payload-free signal that a duplicate launch occurred. | None |
| whoever can launch the exe (same user, same desktop) → the `--apply-update` branch of `Main()` | Deliberate, sanctioned bypass of the single-instance gate for internal self-relaunch (UPDATE-07). | Command-line tokens only |
| process command line → `StartupArgs.TryGetApplyUpdateArgs` | Command lines are readable by any process on the machine and writable by anyone who can start this exe. | File paths / process ids (contract forbids secrets) |
| test host → real `RigToggle.App.exe` child processes (25-03 only) | Test harness starts/waits/kills real processes on the dev machine and CI runner. | Process lifecycle only |
| (unchanged) user → WinForms UI | No new input surface introduced by this phase. | N/A |
| (absent) network, credentials, secrets, package installs | No task in any of the four plans makes a network call, reads a credential, or runs a package-manager install. | N/A |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-25-01 | Denial of Service | `Global\RigToggle-*` named mutex (25-01) | medium | accept | Name-squatting DoS accepted at L1 — an attacker already running code as this user can replace the unsigned flat exe outright; the mutex adds no capability they lack. Fail-visible, not silent. Explicitly not fail-open (recorded prohibition). | closed |
| T-25-02 | Spoofing | activation broadcast → `MainForm.WndProc` (25-01) | low | mitigate | Handler ignores wParam/lParam entirely; whole effect is `RestoreAndFocus()` on this app's own window. Enforced by acceptance criterion that the handler body is a single call. | closed |
| T-25-03a | Tampering | `NativeMethods.PostMessage` to `HWND_BROADCAST` (25-01) | low | accept | Private-range `RegisterWindowMessage` id; both params zero; UIPI blocks delivery to higher-integrity windows; app is non-elevated. | closed |
| T-25-03b | Elevation of Privilege | `--apply-update` branch in `Main()` (25-02) | medium | mitigate | `UpdateApplyEntryPoint.Run` performs no filesystem/process/registry/network/named-object operation and returns a constant — mechanically enforced by a comment-filtered negative grep. Phase 26 inherits the real-teeth version of this threat, recorded here as a known forward obligation. | closed |
| T-25-04 | Denial of Service | concurrent `--apply-update` processes (25-02) | low | accept | Entry point holds no lock, writes nothing, shares no state — concurrent invocations are trivially non-interfering. Serialising the real apply logic is Phase 26's obligation, carried forward as a `verification: backstop` marker. | closed |
| T-25-05 | Information Disclosure | `--apply-update` payload tokens (25-02) | low | mitigate | Contract (doc comment) states the payload carries file paths/process ids only, never a secret; the parser neither interprets nor persists it. | closed |
| T-25-06 | Denial of Service | `Global\` namespace creation privilege (25-01) | low | mitigate | Narrowly-typed catch retries in session-local namespace from a single resolved prefix; no other exception type is swallowed. | closed |
| T-25-07 | Denial of Service | activation signal lost in startup race (25-01) | medium | mitigate | Readiness handshake: winner signals only after its window handle exists; loser waits before broadcasting (3x retry). Verified by in-process and end-to-end tests. | closed |
| T-25-08 | Spoofing | the `--apply-update` token itself (25-02) | low | accept | No authenticable "this launch is our own relaunch" signal is possible without a shared secret on a readable command line, which would be worse; grants nothing a same-user caller doesn't already have. | closed |
| T-25-09 | Tampering | a future generic guard-disable switch (25-02) | medium | mitigate | Standing negative grep over parser and composition root plus a recorded `must_haves` prohibition guards against the specific flag being generalised into a broad bypass. | closed |
| T-25-10 | Denial of Service | leaked child test processes (25-03) | medium | mitigate | Every started process tracked and killed in `Dispose()` (runs even on test failure); all waits bounded; assertions by process name catch orphans the harness didn't start. | closed |
| T-25-11 | Tampering | test process holding the production mutex name (25-03) | low | mitigate | Acquired inside a `using` scope (compiler-emitted finally releases on every exit path); Windows also releases mutex ownership on process termination. | closed |
| T-25-12 | Denial of Service | app state directory on CI runner (25-03) | low | accept | Runner is ephemeral/per-job; file is a few bytes; no test asserts on it; not isolated because the Windows folder API resolves from the user token, not the environment. | closed |
| T-25-13 | Repudiation | a green e2e run that proves nothing (25-03) | medium | mitigate | Exe resolver fails loudly if the build is missing; standing negative grep bans skip/CI-exclusion traits; bypass test carries a negative control; Task 3's 3-consecutive-runs requirement targets races that pass once. | closed |
| T-25-20 | Denial of Service | `WaitForInstanceReady`'s readiness wait — **CR-01** (25-04) | medium | mitigate | `catch (AbandonedMutexException)` treats the abandoned-but-acquired wait as success, routing into the existing release branch. Deliberately not a process-wide unhandled-exception handler (would mask the class, not close it). Regression-netted by a RED-then-GREEN CI fact, independently re-run by this verifier (1/1 pass) and confirmed by two independent code reviews reading the actual diff. | closed |
| T-25-21 | Denial of Service | readiness-mutex construction in `Acquire()` — **WR-02** (25-04) | medium | mitigate | Degrades to a logged null readiness handle; every downstream consumer (`MarkReady`, `Dispose`, `WaitForInstanceReady`, `Program.cs`'s broadcast) already null-guards correctly. Independently re-confirmed by code review. | closed |
| T-25-22 | Tampering | `Global\`/`Local\` namespace divergence — **WR-01** (25-04) | medium | accept | Requires deliberately mismatched security contexts (elevation mismatch, or two login/RDP sessions) never observed on this single-user, single-session, non-elevated rig. Not accepted silently — the source (class doc comment) names the trigger and the deferred stronger fix (probe the opposite namespace before concluding primary) right next to the fallback. Carried as this plan's disclosed backstop truth. | closed |
| T-25-23 | Repudiation | the CR-01 regression test's own construction (25-04) | medium | mitigate | Abandonment mechanism is genuine owning-thread exit (not a `Dispose()`-based simulation, explicitly prohibited — would pass green either way); RED observation is a mandatory, verbatim-recorded step; grep criterion requires `new Thread(` in the test file. Independently verified: the planner and this verifier both confirmed the abandonment mechanism is real, not cosmetic. | closed |
| T-25-SC | Tampering | npm/pip/cargo installs (all four plans) | low | accept | Zero package-manager install steps in any task across all four plans; zero `PackageReference` entries added/removed/version-changed in any project — asserted mechanically per-plan and independently re-confirmed by this phase's regression gate (`git log --oneline -- '*.csproj'` shows no phase-25 commit touching any project file). Consistent with the v2.2 milestone's recorded zero-new-NuGet decision. | closed |

*Status: open · closed · open — below `high` threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above `high` count toward `threats_open`*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

**Note on ID reuse:** `T-25-03` was independently assigned by both 25-01 (Tampering / `PostMessage` broadcast) and 25-02 (Elevation of Privilege / `--apply-update` branch) — an authoring collision across plans, not a real ambiguity in the code. Disambiguated here as `T-25-03a`/`T-25-03b`; both are `closed` regardless.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|--------------|------|
| AR-25-01 | T-25-01 | Mutex name-squatting DoS: attacker already running code as this user gains no new capability (can replace the unsigned flat exe outright); fail-visible, not silent; deliberately not fail-open. | Plan 25-01 threat model, ASVS L1 | 2026-08-19 |
| AR-25-02 | T-25-03a | Broadcast reaches whole desktop but delivers no payload; private-range message id; UIPI blocks cross-integrity delivery. | Plan 25-01 threat model, ASVS L1 | 2026-08-19 |
| AR-25-03 | T-25-04 | Concurrent `--apply-update` invocations are trivially non-interfering (no lock, no writes, no shared state) for Phase 25's side-effect-free placeholder. | Plan 25-02 threat model, ASVS L1 | 2026-08-20 |
| AR-25-04 | T-25-08 | No authenticable self-relaunch signal is possible without a worse-than-nothing shared secret on a readable command line; grants a same-user caller no new capability. | Plan 25-02 threat model, ASVS L1 | 2026-08-20 |
| AR-25-05 | T-25-12 | CI runner state-directory seeding is ephemeral, unasserted, and per-job. | Plan 25-03 threat model, ASVS L1 | 2026-08-20 |
| AR-25-06 | T-25-22 (WR-01) | `Global\`/`Local\` namespace divergence requires a mismatched-security-context trigger never observed on this single-user, single-session, non-elevated rig; deferred fix and trigger both named in source, not silently dropped. | Plan 25-04 threat model, ASVS L1 (re-affirmed after 25-REVIEW.md's WR-01 finding) | 2026-08-21 |
| AR-25-07 | T-25-SC | Zero package-manager installs / zero `PackageReference` changes across all four plans — not applicable. | Plans 25-01 through 25-04 threat models, ASVS L1 | 2026-08-19 to 2026-08-21 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-08-21 | 19 (18 distinct + 1 ID-collision split) | 19 | 0 | Claude (gsd-execute-phase orchestrator, compiled from plan-time threat models per the ASVS-L1 short-circuit rule; CR-01/WR-02 mitigations independently re-confirmed via code review + regression test re-run rather than trusted from SUMMARY narration) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-08-21
