# Phase 2: Foundations & GUI Shell - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-24
**Phase:** 2-Foundations & GUI Shell
**Areas discussed:** GUI framework, Real vs fake boundary for controllers, Settings screen layout & flow, Main window / mode indicator design

---

## GUI Framework

| Option | Description | Selected |
|--------|-------------|----------|
| WinForms | Smallest self-contained publish size, no XAML, trivial dropdown/list binding | ✓ |
| WPF | Nicer visuals/data binding, more modern look, adds XAML | |
| You decide | Let Claude pick per CLAUDE.md's recommendation | |

**User's choice:** WinForms

| Option | Description | Selected |
|--------|-------------|----------|
| System default | Standard WinForms controls/colors, no custom theming | ✓ |
| Light custom styling | Custom accent color/font for toggle button and mode indicator | |

**User's choice:** System default

| Option | Description | Selected |
|--------|-------------|----------|
| Modal dialog | Opens on top of Main, blocks interaction until closed/saved | ✓ |
| Separate non-modal window | Can stay open alongside Main | |

**User's choice:** Modal dialog

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed size | Small, fixed-size window | ✓ |
| Resizable | User can resize freely | |

**User's choice:** Fixed size

**Notes:** All four sub-questions in this area resolved to the recommended option.

---

## Real vs Fake Boundary for Controllers

| Option | Description | Selected |
|--------|-------------|----------|
| Real enumeration, fake mutation | Pickers show real monitor/audio data; only Disable()/SetDefault() stay faked | ✓ |
| Fully fake data for both | Hardcoded placeholder names | |

**User's choice:** Real enumeration, fake mutation

| Option | Description | Selected |
|--------|-------------|----------|
| File-browser dialog | OpenFileDialog filtered to .exe | ✓ |
| Free-text field | Manually typed path | |

**User's choice:** File-browser dialog

| Option | Description | Selected |
|--------|-------------|----------|
| Real detection now | Process.GetProcessesByName, zero-risk read-only | ✓ |
| Fake it too | Keep IAppController fully faked in Phase 2 | |

**User's choice:** Real detection now

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, build ToggleService now | Zero Windows API refs, fully unit-testable, Phase 5 becomes adapter swap | ✓ |
| Defer to Phase 5 | Phase 2 stays pure GUI + persistence | |

**User's choice:** Yes, build it now

**Notes:** All four sub-questions resolved to the recommended option.

---

## Settings Screen Layout & Flow

| Option | Description | Selected |
|--------|-------------|----------|
| Three sections, one window | Monitor/Audio/App in one scroll-free window | ✓ |
| Tabs | Monitor / Audio / App tabs | |

**User's choice:** Three sections, one window

| Option | Description | Selected |
|--------|-------------|----------|
| Show as unselected + inline warning | Forces user to notice and reselect missing device | ✓ |
| Keep stale saved value greyed out | Shows old selection even if not present | |

**User's choice:** Show as unselected + inline warning

| Option | Description | Selected |
|--------|-------------|----------|
| Re-enumerate on open only | Simplest, matches rarely-opened one-time setup framing | ✓ |
| Add explicit Refresh button | Lets user re-scan without closing dialog | |

**User's choice:** Re-enumerate on open only

| Option | Description | Selected |
|--------|-------------|----------|
| Block Save until complete | Prevents saving half-configured state | ✓ |
| Allow partial save | Save whatever is filled in | |

**User's choice:** Block Save until complete

**Notes:** All four sub-questions resolved to the recommended option.

---

## Main Window / Mode Indicator Design

| Option | Description | Selected |
|--------|-------------|----------|
| Full intended layout, wired to fakes | Toggle actually runs snapshot→fake-mutate→flip-mode now | ✓ |
| Placeholder layout only | Static mockup, reworked in Phase 5 | |

**User's choice:** Full intended layout, wired to fakes

| Option | Description | Selected |
|--------|-------------|----------|
| Derive from snapshot file presence | Mode = Rig iff valid snapshot file exists, per ARCHITECTURE.md Pattern 3 | ✓ |
| Separate in-memory mode flag | Simpler now, doesn't test crash-survival behavior | |

**User's choice:** Derive from snapshot file presence

| Option | Description | Selected |
|--------|-------------|----------|
| Show as small status line | "Moza Companion: Running/Not running" next to toggle | ✓ |
| No UI for it yet | Detection exists in code/logs only | |

**User's choice:** Show as small status line

**Notes:** All three sub-questions resolved to the recommended option.

---

## Claude's Discretion

None — every question reached an explicit user choice.

## Deferred Ideas

None. (Confirmation dialog before disabling the primary monitor was noted as already correctly scoped to Phase 4 per ROADMAP.md — not treated as a deferred idea since it was never in scope for this phase to begin with.)
