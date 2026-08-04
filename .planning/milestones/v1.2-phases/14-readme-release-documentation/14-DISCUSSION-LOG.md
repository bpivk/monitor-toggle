# Phase 14: README & Release Documentation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-03
**Phase:** 14-readme-release-documentation
**Areas discussed:** Badges & backing infra, Release artifact for v1.2, Screenshots & visuals, Feature overview depth

---

## Badges & backing infra

| Option | Description | Selected |
|--------|-------------|----------|
| MIT | Simplest, most common permissive license; standard shields.io badge | ✓ |
| All rights reserved / no license | "Source visible but not reusable"; badge would read "no license" | |
| Unlicense / public domain | More permissive than MIT, no attribution required | |

**User's choice:** MIT

| Option | Description | Selected |
|--------|-------------|----------|
| Add a minimal GitHub Actions workflow | `dotnet build` + tests on push/PR; badge reflects real pipeline | ✓ |
| Skip real CI — omit the build-status badge | Docs-only phase; drop or placeholder the badge | |
| Static/manual badge, no automation | shields.io static badge not wired to any pipeline | |

**User's choice:** Add a minimal GitHub Actions workflow

| Option | Description | Selected |
|--------|-------------|----------|
| Make the repo public | Matches "GitHub-ready README" intent; badges/CI/Releases render for anyone | ✓ |
| Keep it private | Personal tool, no obligation to open-source; badges still work when logged in | |
| Leave visibility alone — not this phase's call | Note as a call-out, don't act on it here | |

**User's choice:** Make the repo public
**Notes:** Repo confirmed private via `gh repo view bpivk/monitor-toggle --json visibility`.

| Option | Description | Selected |
|--------|-------------|----------|
| Backfill v1.0/v1.1 + cut v1.2 as real GitHub Releases | Makes DOCS-02 and the release badge literally true | ✓ |
| Just cut v1.2, skip backfilling v1.0/v1.1 | Only current milestone gets a real release | |
| README describes the general flow only, no actual release cut | Badge shows "no releases yet" until user cuts one manually | |

**User's choice:** Backfill v1.0/v1.1 + cut v1.2 as real GitHub Releases
**Notes:** Confirmed via `gh release list` that no GitHub Release currently exists despite `v1.0`/`v1.1` git tags being present.

---

## Release artifact for v1.2

| Option | Description | Selected |
|--------|-------------|----------|
| CI builds and attaches the .exe on tag push | Extends the Actions workflow to publish + attach on version-tag push | ✓ |
| CI builds/tests only — exe attachment stays manual | Build-status badge backed, but publish/upload stays manual | |

**User's choice:** CI builds and attaches the .exe on tag push

| Option | Description | Selected |
|--------|-------------|----------|
| Release notes only for v1.0/v1.1, no binary | Old builds weren't kept; only v1.2 gets a real downloadable artifact | ✓ |
| I have the old builds — attach them too | User would supply old .exe files separately | |

**User's choice:** Release notes only for v1.0/v1.1, no binary

---

## Screenshots & visuals

| Option | Description | Selected |
|--------|-------------|----------|
| Two: MainForm normal + rig mode | Matches DOCS-01's literal wording | |
| Four: MainForm (both modes) + SettingsForm + tray menu | Broader tour, shows Phase 12 theming + Phase 13 icons | ✓ |
| You decide | Claude picks during planning/research | |

**User's choice:** Four placeholders — MainForm normal mode, MainForm rig mode, SettingsForm, tray context menu

| Option | Description | Selected |
|--------|-------------|----------|
| Markdown image syntax pointing to a not-yet-existing file path | Correct final syntax already in place; renders broken-image until filled | ✓ |
| Explicit bracketed placeholder text | Unambiguous but requires a second README edit later | |

**User's choice:** Markdown image syntax pointing to a not-yet-existing file path

| Option | Description | Selected |
|--------|-------------|----------|
| docs/screenshots/ | Keeps repo root clean, groups documentation assets | ✓ |
| screenshots/ at repo root | Shorter path, adds another top-level directory | |

**User's choice:** docs/screenshots/

| Option | Description | Selected |
|--------|-------------|----------|
| No — static screenshots only | Simpler asset scope | ✓ |
| Yes — add a 5th placeholder for a demo GIF | More compelling but harder to produce | |

**User's choice:** No — static screenshots only

---

## Feature overview depth

| Option | Description | Selected |
|--------|-------------|----------|
| Full automation surface | Toggle + tray/autostart + hotkey + multi-monitor + theming + icons | ✓ |
| Toggle headline only | Keep tight, matches current terse README style | |

**User's choice:** Full automation surface

| Option | Description | Selected |
|--------|-------------|----------|
| Name it — Moza rig, BeamNG.drive example | Matches PROJECT.md/CLAUDE.md's own internal framing | |
| Keep it generic/configurable-sounding | Frame as "secondary display + companion app" without naming specifics | ✓ |

**User's choice:** Keep it generic/configurable-sounding
**Notes:** Deliberate divergence from the recommended option — user wants the public README more general-sounding than the internal project docs. Flagged explicitly in CONTEXT.md so downstream agents don't "correct" it back to Moza/BeamNG naming.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — generic problem statement | Explains "why" without naming specific apps | ✓ |
| Skip the problem statement | Feature-list only, no motivating context | |

**User's choice:** Yes — generic problem statement

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, brief system requirements note | Windows 10/11 x64, Mica/rounded-corners Windows-11-only fallback | ✓ |
| No — omit system requirements | Keep README focused on features/install/build only | |

**User's choice:** Yes, brief system requirements note

---

## Claude's Discretion

- Exact README section ordering/headings and badge visual style (flat vs. flat-square).
- Exact wording of the generic problem statement.
- GitHub Actions workflow YAML structure (job/step naming, precise trigger conditions).
- Whether v1.0/v1.1 backfilled release notes are auto-generated from commit history or hand-written.
- Badge grouping/layout at the top of the README.

## Deferred Ideas

None — discussion stayed within phase scope. An animated demo GIF was considered and explicitly declined (not deferred, not expected to resurface).
