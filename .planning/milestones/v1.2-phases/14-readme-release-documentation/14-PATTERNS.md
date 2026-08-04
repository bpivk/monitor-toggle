# Phase 14: README & Release Documentation - Pattern Map

**Mapped:** 2026-08-03
**Files analyzed:** 5 (2 new workflow YAML, 1 new LICENSE, 1 rewritten README, 1 new empty directory)
**Analogs found:** 1 / 5 (README.md only — this repo has no prior CI/workflow YAML or LICENSE file)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `README.md` | config/docs | N/A (static markdown) | `README.md` (existing root file, rewritten in place) | exact — the file IS its own analog for tone/structure/existing content to preserve |
| `.github/workflows/build.yml` | config (CI pipeline) | event-driven (push/PR trigger) | none in this repo | no analog — use RESEARCH.md Pattern 1 verified example |
| `.github/workflows/release.yml` | config (CI pipeline) | event-driven (tag-push trigger) | none in this repo | no analog — use RESEARCH.md Pattern 1 verified example |
| `LICENSE` | config/legal | N/A (static text) | none in this repo | no analog — use RESEARCH.md's OSI-canonical MIT template verbatim |
| `docs/screenshots/` (empty dir) | asset placeholder | file-I/O (future manual drop-in) | none — no `docs/` directory currently exists | no analog — directory only, no content this phase |

**Confirmed absence (verified directly this pass):** `ls .github` → does not exist. `find . -iname "LICENSE*"` → no results. `find . -iname "*.yml" -o -iname "*.yaml"` → no results anywhere in the repo. This is genuinely greenfield infrastructure for the project — do not spend more search budget looking for analogs that don't exist.

## Pattern Assignments

### `README.md` (docs, rewrite-in-place)

**Analog:** itself — `/home/bpivk/moza/README.md` (current full contents, 29 lines, reproduced below since the planner needs the exact baseline being replaced)

**Current full content (all 29 lines — this is the entire existing file):**
```markdown
# Rig Toggle

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode"
with one click. Toggling to rig mode disables the primary monitor at the OS level,
switches the default audio output to the rig speakers, and launches/focuses the Moza
Companion app. Toggling back restores the exact previous monitor/audio state and
minimizes the Moza Companion app.

## Build a standalone .exe

Publish is self-contained, single-file, and untrimmed (win-x64 only). From the repo root:

\`\`\`bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
\`\`\`

If the RID is ever not picked up for any reason (e.g. an older/mismatched SDK), fall back
to the explicit-flag form:

\`\`\`bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64
\`\`\`

The output single-file exe lands in `src/RigToggle.App/bin/publish/win-x64/` and requires
no separate .NET runtime install to run (PACKAGING-01).

Note: the build is intentionally untrimmed (`PublishTrimmed=false`) — trimming can strip
the COM interop (audio default-device switching) and P/Invoke (display CCD topology)
marshalling this app depends on — and it targets Windows x64 only.
```

**What to preserve verbatim (do not silently rewrite/improve):**
- The two `dotnet publish` code blocks (lines 13-15 and 20-22) — reuse character-for-character in both the README's own "build from source" section AND as the `run:` step in `.github/workflows/release.yml`. This is CONTEXT.md's explicit instruction (canonical_refs: "Its documented `dotnet publish` command... is the exact command the new CI release workflow must reuse") and RESEARCH.md's Anti-Pattern warning against "improving or shortening this command in the workflow."
- The untrimmed/COM-interop/P-Invoke rationale note (final paragraph) — still true, still relevant, carry forward into the new README's build section rather than dropping it.
- The output path callout (`src/RigToggle.App/bin/publish/win-x64/`) — same path the release workflow's `softprops/action-gh-release` `files:` input must reference.

**What CONTEXT.md requires changing/adding (per D-06 through D-13, planner-facing, not this agent's job to draft prose):**
- Add 3 badges at top (build status, license, latest release) — see Shared Patterns > Badge URLs below.
- Add feature overview section (D-10: toggle, tray/autostart, hotkey, multi-monitor sets, live theme-following, icons) — generic framing, no "Moza"/"BeamNG" naming (D-11).
- Add generic problem statement (D-12).
- Add system requirements note (D-13: Win10/11 x64, Mica/rounded-corners Win11-only graceful degradation).
- Add 4 screenshot placeholders using real markdown image syntax pointing at not-yet-existing files under `docs/screenshots/` (D-06/D-07/D-08).
- Keep the existing "Build a standalone .exe" section, verbatim commands as noted above.

**Tone/structure observations from the existing file (to carry forward):**
- Terse, single-purpose sections, no marketing fluff — plain declarative sentences.
- Uses parenthetical requirement-ID citations inline (e.g. `(PACKAGING-01)`) — this is a project convention worth preserving if the planner wants traceability, though not mandated by CONTEXT.md.
- Code blocks are fenced with explicit `bash` language tag.
- No existing badges, no existing screenshots section — this phase is additive, not a full-tone overhaul.

---

### `.github/workflows/build.yml` (config, event-driven — push/PR trigger)

**Analog:** none in this repo (confirmed absent). Use the verified pattern from `14-RESEARCH.md` "Pattern 1: Two-workflow split" and "Code Examples" sections instead — already cross-checked against this repo's actual project structure (test project TFMs, no display-hardware dependency in test bodies per RESEARCH.md Pitfall 1).

**Verified example (RESEARCH.md lines 146-164, reproduced for direct copy):**
```yaml
name: Build
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release
```

**Solution-level context confirmed this pass:** repo root has `RigToggle.sln` (verified via `find . -maxdepth 1 -name "*.sln"`), so the bare `dotnet restore`/`dotnet build`/`dotnet test` invocations (no explicit project/sln path argument) will resolve correctly from repo root — matches the pattern above with no path argument needed.

**Test project convention to respect:** two test projects exist — `src/RigToggle.Tests/RigToggle.Tests.csproj` (plain `net10.0`, pure logic tests) and `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj` (`net10.0-windows`, but per RESEARCH.md Pitfall 1, no live-hardware/Form instantiation in test bodies) — `dotnet test` at solution level picks up both; no per-project splitting needed in the workflow.

---

### `.github/workflows/release.yml` (config, event-driven — tag-push trigger)

**Analog:** none in this repo. Use RESEARCH.md's verified example (lines 287-317), which was cross-checked directly against this repo's own `.pubxml`/`.csproj` for the exact publish command and output path.

**Verified example (RESEARCH.md, reproduced for direct copy):**
```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7

      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'

      - name: Publish self-contained single-file exe
        run: dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64

      - name: Attach exe to GitHub Release
        uses: softprops/action-gh-release@v3
        with:
          files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
```

**Confirmed this pass directly from source (not re-deriving from RESEARCH.md alone):**
- `dotnet publish` command and `PublishProfile=win-x64` flag match `src/RigToggle.App/RigToggle.App.csproj` (Read directly — `RuntimeIdentifier` is set in the `.csproj` `<PropertyGroup>`, not only the `.pubxml`, exactly as RESEARCH.md Anti-Pattern warns must be preserved — do not add a redundant `-r win-x64` flag).
- `.pubxml` file exists at `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` (confirmed via `find`) — the output path `src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe` in the `files:` glob must match its `<PublishDir>` setting; do not use the .NET SDK's default publish path.
- `RigToggle.App.csproj` project references only `RigToggle.Core` and `RigToggle.Windows` (confirmed via Read) — no other project dependencies the release workflow needs to account for.

---

### `LICENSE` (legal/config, static)

**Analog:** none in this repo (confirmed via `find . -iname "LICENSE*"` — zero results).

**Use verbatim (RESEARCH.md Code Examples, OSI-canonical MIT text):**
```
MIT License

Copyright (c) 2026 Blaz Pivk

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Must be placed at repo root exactly as `LICENSE` (no extension) — shields.io's `img.shields.io/github/license/...` badge (Shared Patterns below) depends on GitHub's license-detector recognizing this exact standard MIT text at that path (per RESEARCH.md Pitfall 2).

Copyright holder name `Blaz Pivk` taken from `git config user.name` per RESEARCH.md Assumption A1 (low risk, cosmetic swap only if user prefers `bpivk` handle instead — flag to user if uncertain, otherwise proceed with `Blaz Pivk`).

---

### `docs/screenshots/` (new empty directory)

**Analog:** none — no `docs/` directory exists in the repo currently.

No pattern to extract; this is a placeholder directory only. Per D-06/D-07/D-08, the README will reference four not-yet-existing image files by markdown image syntax pointing into this directory:
- `docs/screenshots/main-normal.png`
- `docs/screenshots/main-rig.png`
- `docs/screenshots/settings.png`
- `docs/screenshots/tray-menu.png`

Since git does not track empty directories, planner should note this needs either a `.gitkeep`/`.gitignore` placeholder file or simply rely on the directory being created implicitly once real screenshots are dropped in later (git will not persist a truly empty dir — flag this as an implementation detail for the plan, not something this pattern-mapping pass resolves).

## Shared Patterns

### Badge URLs (live-endpoint, not decorative)
**Source:** RESEARCH.md Pattern 2 (verified against shields.io + GitHub Actions badge docs)
**Apply to:** `README.md` only (top-of-file badge row)
```markdown
![Build Status](https://github.com/bpivk/monitor-toggle/actions/workflows/build.yml/badge.svg)
![License](https://img.shields.io/github/license/bpivk/monitor-toggle)
![Latest Release](https://img.shields.io/github/v/release/bpivk/monitor-toggle)
```
Note the `build.yml` filename in the first badge URL must exactly match whatever filename is chosen for the build workflow file — if the plan names it something other than `build.yml`, update this URL to match.

### `dotnet publish` command (single source of truth)
**Source:** existing `README.md` lines 13-15 (repo root, current file) — do not re-derive or "simplify" this command anywhere else it's needed
**Apply to:** `README.md`'s own build section (keep as-is) AND `.github/workflows/release.yml`'s publish step (reuse identically)
```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```

### GitHub Actions runner OS
**Source:** RESEARCH.md Standard Stack / Anti-Patterns — not a codebase pattern (no prior workflows exist) but a hard project constraint
**Apply to:** both `build.yml` and `release.yml`
```yaml
runs-on: windows-latest
```
Required because the project is `net10.0-windows` WinForms + COM interop (`IPolicyConfig`) + P/Invoke (`WindowsDisplayAPI`) — will not compile on `ubuntu-latest`.

### Action version pins
**Source:** RESEARCH.md Package Legitimacy Audit (all three approved, no removals/flags)
**Apply to:** both workflow files
```yaml
actions/checkout@v7
actions/setup-dotnet@v6
softprops/action-gh-release@v3   # release.yml only
```
Pin to major-version tags (not `@main`/`@latest`, not full SHA) — matches each publisher's own convention.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `.github/workflows/build.yml` | config (CI) | event-driven | No `.github/workflows/` directory exists anywhere in this repo (confirmed via `ls`) — this is the project's first CI pipeline. Use RESEARCH.md's verified example directly. |
| `.github/workflows/release.yml` | config (CI) | event-driven | Same as above — no prior release automation exists (confirmed via `gh release list` returning empty and no workflow YAML in repo). Use RESEARCH.md's verified example directly. |
| `LICENSE` | legal/config | static | No `LICENSE*` file exists at any path in the repo (confirmed via `find`). Use the OSI-canonical MIT template from RESEARCH.md verbatim. |
| `docs/screenshots/` | asset placeholder | file-I/O (future) | No `docs/` directory exists yet. No pattern needed — directory creation only, no file content this phase. |

## Metadata

**Analog search scope:** repo root (`.github/`, `LICENSE*`, `*.yml`/`*.yaml` searched repo-wide), `README.md` (root), `src/RigToggle.App/RigToggle.App.csproj`, `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml`, `RigToggle.sln`, `src/*/*.csproj` (project listing).
**Files scanned:** 1 README (read in full), 1 csproj (read in full), directory listings for `.github`, `LICENSE*`, `*.yml`/`*.yaml`, `*.sln`, `src/**/*.csproj`.
**Pattern extraction date:** 2026-08-03
