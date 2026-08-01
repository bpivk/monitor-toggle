# Phase 8: Tray Residency, Autostart & Toast Notification - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-30
**Phase:** 8-tray-residency-autostart-toast-notification
**Areas discussed:** Tray icon appearance/click behavior, Minimize-to-tray scope, Tray context menu, Autostart mechanism, Startup-mode flag, Toast notification trigger/content

**Mode:** `--auto` — Claude selected the recommended option for each question without interactive prompts (continuing the chain from Phase 7 closure, per user's confirmation).

---

## Tray Icon Appearance & Click Behavior (TRAY-04, TRAY-05)

| Option | Description | Selected |
|--------|-------------|----------|
| Two distinct icons swapped via `NotifyIcon.Icon` | Normal-mode icon and rig-mode icon, plus matching tooltip text | ✓ |
| Single icon + drawn badge/overlay | One base icon with a programmatically-drawn state indicator | |

**Selected:** Two distinct icons.
**Notes:** [auto] Simpler and more reliable than runtime icon composition; exact glyph/color design deferred to UI-SPEC.md since this phase carries `UI hint: yes` (unlike Phase 7's false-positive trigger). Left-click restores/focuses (TRAY-05 literal reading); double-click left unhandled since `NotifyIcon` firing both `Click` and `DoubleClick` on a double-click just causes a harmless second restore.

---

## Minimize-to-Tray Scope (TRAY-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Only Close (X) redirects to tray | Taskbar minimize keeps standard OS behavior | ✓ |
| Both Close and Minimize redirect to tray | Any minimize action also hides to tray | |

**Selected:** Close-only.
**Notes:** [auto] TRAY-01's literal wording scopes this to "closing the main window" — extending it to the minimize button would be scope creep past what's asked, even though many tray apps do both.

---

## Tray Context Menu (TRAY-03)

| Option | Description | Selected |
|--------|-------------|----------|
| Dynamic toggle label reusing `MainForm.btnToggle.Text` wording | One shared source of truth for the string | ✓ |
| Static "Toggle Mode" label | Simpler but loses the mode-specific clarity the GUI button already has | |

**Selected:** Dynamic, reused wording.
**Notes:** [auto] Consistency with the existing GUI button; "Settings" opens the existing modal form; "Exit" must dispose/hide the `NotifyIcon` before exiting to avoid the well-known WinForms "ghost tray icon" bug.

---

## Autostart Mechanism (TRAY-02) — carried forward, not re-discussed

Already locked in STATE.md's v1.1 roadmap decisions: plain `HKCU\...\Run` registry key, not Task Scheduler. Applied directly as D-05, following the existing `chkEnableDebugLogging` checkbox pattern for the new "Start with Windows" checkbox.

---

## Startup-Mode Flag for Autostart (cross-cutting, not in ROADMAP.md verbatim)

| Option | Description | Selected |
|--------|-------------|----------|
| `--tray` CLI flag in the Run registry command, checked in `Program.cs Main(args)` | App starts hidden when autostarted, shown when launched manually | ✓ |
| No flag — app always shows the main window on launch | Simpler, but defeats the point of "tray-resident + start with Windows" (window pops up every boot) | |
| Runtime heuristic detecting "was I autostarted" | Fragile, unnecessary when a flag can be supplied directly in the registry command | |

**Selected:** `--tray` CLI flag.
**Notes:** [auto] Not explicitly stated in ROADMAP.md's success criteria, but required to make TRAY-02 non-annoying in combination with TRAY-01. Flagged explicitly as a deliberate, minimal addition — not a preview of Phase 10's general CLI-trigger feature (macro-pad/Stream-Deck toggle args are a separate, larger scope).

---

## Toast Notification (NOTIF-01)

| Option | Description | Selected |
|--------|-------------|----------|
| Always toast on tray-menu-triggered toggles, unconditionally | Simple, robust rule — no visibility/focus check | ✓ |
| Only toast if the main window is currently hidden/not focused | Matches "without the GUI open" more literally, but adds a fragile visibility check | |

**Selected:** Always toast on tray-triggered toggles.
**Notes:** [auto] The tray menu is inherently the "without the GUI open" scenario NOTIF-01 describes — a visibility check adds fragility for no real benefit. GUI-button toggles keep their existing MessageBox behavior unchanged. Content reuses `MainForm.FormatChecklist` verbatim (mirrors GUI's existing partial-failure detail, matching NOTIF-01's explicit requirement) rather than inventing new wording.

---

## Claude's Discretion

- Whether `NotifyIcon`/`ContextMenuStrip` lives on `MainForm` or a separate class — `MainForm`-hosted is the natural default, left to planner.
- Exact registry value name and how the Settings checkbox reads current autostart state — left to planner.
- Exact `FormatChecklist` reuse mechanism (visibility widening vs. duplication) — reuse strongly preferred, left to planner.

## Deferred Ideas

None — discussion stayed within phase scope. Phase 10's general CLI-trigger feature remains correctly scoped there; D-06's `--tray` flag here is a minimal, distinct addition.
