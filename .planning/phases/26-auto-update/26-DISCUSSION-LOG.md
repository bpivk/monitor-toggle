# Phase 26: Auto-Update - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-22
**Phase:** 26-auto-update
**Areas discussed:** Update prompt content, Manual check UX, Failure visibility, Download integrity check

---

## Update prompt content

| Question | Options | Selected |
|---|---|---|
| What should the update-available confirm dialog show? | Version number only / Version + link to release notes / Version + inline release notes body | Version + inline release notes body ✓ |
| How should the release notes body be rendered? | Plain text (strip Markdown) / Lightly formatted | Lightly formatted ✓ |
| What buttons should the update dialog have? | Update Now / Later / Update Now / Later / Skip this version | Update Now / Later / Skip this version ✓ |
| What feedback do you see while the update downloads and applies? | Indeterminate "Updating…" state / Progress bar with percentage | Indeterminate "Updating…" state ✓ |

**Notes:** No follow-up clarifications beyond the selected options — user moved through this area quickly, choosing the more informative/featureful option at each step (release notes shown, skip-version supported) while declining extra UI complexity (progress bar).

---

## Manual check UX

| Question | Options | Selected |
|---|---|---|
| Where should the manual "Check for Updates" action live? | Tray context menu only / Settings screen only / Both tray menu and Settings | Both tray menu and Settings ✓ (after redirect — see below) |
| When a manual check finds you're already up to date, how should that be reported? | Tray balloon toast / Small dialog | Tray balloon toast ✓ |
| If a manual check itself fails, how should that surface? | Toast with the error / Same silent no-op as automatic check | Toast with the error ✓ |

**User's first response (free text):** "Add a menu at the top like every other app uses. We can then also build a help section as well and about section." — interpreted as a request for a top-level `MenuStrip` with Help/About sections, which is new UI capability beyond Phase 26's auto-update scope (MainForm has no menu bar today — it's a monitor-tile dashboard). Redirected per scope guardrail: captured as a deferred idea, then re-asked the narrower "where does Check for Updates live" question scoped to what's already in the app (tray menu, Settings).

**Notes:** User wants failure feedback for manual checks to be visibly different from the silent posture of the automatic on-launch check, since a manual check is an explicit user action.

---

## Failure visibility

| Question | Options | Selected |
|---|---|---|
| If download/apply fails after confirming Update Now, what do you see? | Toast explaining the failure, app keeps running old version / Silent rollback, retry next launch | Toast explaining the failure, app keeps running old version ✓ |
| If the swap succeeds but the new exe crashes/fails on first startup, what should happen? | Auto-rollback to the .old exe on next launch / Leave both files, notify, let me sort it out | Auto-rollback to the .old exe on next launch ✓ |

**Notes:** User consistently chose "tell me what happened" over silence for any failure the user was actively involved in triggering (confirmed update), and chose full self-healing (auto-rollback) over a manual-intervention path for the startup-crash edge case — consistent with the app's existing crash-recovery precedent (`StartupRecoveryChecker`/`ToggleInProgressMarker`).

---

## Download integrity check

| Question | Options | Selected |
|---|---|---|
| Should this phase add a checksum verification step? | Yes — publish + verify SHA256 / No — treat as accepted limitation | Yes — publish + verify SHA256 ✓ |

**Notes:** User opted into hardening beyond the strict requirements (UPDATE-01..06 don't mandate a checksum), directly closing the corrupted/truncated-download gap flagged in `PITFALLS.md` Pitfall 5. A checksum mismatch is treated as an apply failure, reusing the Failure visibility area's D-08 toast-and-rollback behavior rather than introducing a separate failure mode.

---

## Claude's Discretion

- Exact Markdown subset supported by the lightly-formatted release-notes renderer (headers/bullets/bold at minimum)
- Exact wording of all toast/dialog copy — tone and information content are locked, precise phrasing is not
- What "confirmed-healthy" means for the new exe after an update-apply (e.g. reaching the message loop vs. a short post-launch timer)
- Persistence mechanism for the "skip this version" marker and the "update applied, not yet confirmed" marker (likely `settings.json` and a dedicated marker file respectively, following existing precedent)

## Deferred Ideas

- **Top-level app menu bar (MenuStrip) with Help and About sections** — raised during Manual check UX discussion. New UI capability beyond Phase 26's scope; candidate for a future phase or backlog item.
