# Phase 14: README & Release Documentation - Research

**Researched:** 2026-08-03
**Domain:** GitHub project documentation + release engineering (GitHub Actions CI/CD, GitHub Releases, shields.io badges, MIT licensing)
**Confidence:** HIGH

## Summary

This phase is mostly plumbing, not docs-writing risk. The README content itself (feature overview, generic framing, screenshot placeholders) is low-risk prose work fully specified by CONTEXT.md's D-06 through D-13. The real research value is in getting three pieces of *real* backing infrastructure right on the first try, since CONTEXT.md explicitly rejected decorative/static badges: (1) a GitHub Actions build workflow that runs `dotnet build`/`dotnet test` on `windows-latest` (required — this is a WinForms/P-Invoke/COM app, `ubuntu-latest` cannot build it), (2) a release workflow triggered on `v*` tag push that runs the project's existing self-contained single-file publish command and attaches the resulting `.exe` via `softprops/action-gh-release`, and (3) exact `gh` CLI invocations for the one-time repo-visibility flip and the two-tag release backfill.

All facts needed were verified directly against this repo (csproj/pubxml contents, git tag types, `gh` auth/version, `gh release list`/`gh repo view` live output) plus current official sources for the three GitHub Actions used and the `gh` CLI manual. No Context7 library docs applied here (this phase has no NuGet/npm dependencies — the "packages" are GitHub Marketplace Actions, audited below in lieu of the standard Package Legitimacy Audit).

**Primary recommendation:** Two workflow files (`.github/workflows/build.yml` and `.github/workflows/release.yml`), both on `windows-latest`, using `actions/checkout@v7`, `actions/setup-dotnet@v6` (dotnet-version `10.0.x`), and `softprops/action-gh-release@v3` for the release step with `permissions: contents: write`. The publish output artifact is `src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe`. Repo visibility flip uses `gh repo edit --visibility public --accept-visibility-change-consequences`. Tag backfill uses `gh release create v1.0 --notes-from-tag --verify-tag` (repeat for v1.1) since both tags are annotated tags with existing meaningful messages — no need to hand-write notes.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| README content (feature overview, install/build instructions) | Repo docs (root `README.md`) | — | Static markdown, no runtime tier involved |
| Build-status badge truth source | CI / GitHub Actions | GitHub badge endpoint (`.../badge.svg`) | Badge is a passive image pointing at a live workflow's generated SVG — GitHub serves it, not shields.io |
| License badge truth source | Repo metadata (`LICENSE` file) | shields.io `img.shields.io/github/license/...` | shields.io reads GitHub's repo API license detection, which requires a real `LICENSE` file at root in a recognized format |
| Release-version badge truth source | GitHub Releases (repo API) | shields.io `img.shields.io/github/v/release/...` | shields.io queries GitHub's Releases API; requires at least one non-draft, non-prerelease release to exist |
| CI build/test execution | GitHub-hosted `windows-latest` runner | — | Only tier capable of compiling `net10.0-windows` WinForms/COM-interop code; `ubuntu-latest` cannot build this project |
| Release publish + asset upload | GitHub Actions (`dotnet publish` step) + GitHub Releases API (`softprops/action-gh-release`) | — | CI produces the artifact; the Release action's job is solely to attach it, not rebuild it |
| Repo visibility | GitHub repo settings (via `gh repo edit` or web UI) | — | One-time manual/scripted setting flip, not code |

## Standard Stack

### Core
| Tool/Action | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `actions/checkout` | v7 (current as of June 2026) `[VERIFIED: GitHub official repo]` | Checks out repo source in each workflow job | Official GitHub action, 8.6k stars, used in effectively every workflow; v7 is current major, includes June 2026 "pwn request" hardening `[CITED: github.com/actions/checkout]` |
| `actions/setup-dotnet` | v6 `[VERIFIED: GitHub official repo]` | Installs the .NET 10 SDK on the runner | Official GitHub action, 1.2k stars; v6 is current major (ESM migration, no input/behavior changes) `[CITED: github.com/actions/setup-dotnet]` |
| `softprops/action-gh-release` | v3 `[CITED: github.com/softprops/action-gh-release]` | Creates/updates a GitHub Release and uploads asset files on tag push | De facto standard third-party release action (5.7k stars, actively maintained); v2 is EOL/unsupported (Node 20 runtime deprecated by GitHub Actions), v3 required going forward |

### Supporting
| Tool | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `gh` CLI | 2.46.0 (confirmed installed and authenticated in this environment) `[VERIFIED: gh --version / gh auth status]` | One-off repo visibility flip, tag-based release backfill | Run manually (or scripted) once for D-03/D-04; not part of the CI workflow itself |
| shields.io | N/A (hosted service, no install) | Renders the three badges | Standard badge host; URLs documented below |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `softprops/action-gh-release` | `actions/upload-release-asset` (official) + manual `gh release create` step | Official action is lower-level (requires a pre-existing release ID as input, doesn't auto-create the release), more steps to wire up for the same "tag push -> release with asset" outcome. `softprops/action-gh-release` does both (create-or-update + upload) in one step — standard choice for this exact use case. |
| `gh release create --notes-from-tag` for v1.0/v1.1 backfill | Hand-written `--notes "..."` string | Both tags are annotated (`git cat-file -t v1.0` → `tag`, confirmed) with substantive multi-paragraph messages already written at tag time — `--notes-from-tag` reuses that verbatim with zero risk of drift/rewrite. Hand-writing is only needed if the tag message were empty or lightweight. |
| `windows-latest` for build workflow | `ubuntu-latest` | Not viable — `net10.0-windows` TFM + WinForms + COM interop (`IPolicyConfig`) + `WindowsDisplayAPI` P/Invoke will not compile on Linux. Must be `windows-latest` (or a pinned `windows-2025`/`windows-2022` image) for both build and release jobs. |

**Installation:**
No package installation needed — this phase adds GitHub Actions workflow YAML (references actions by tag, no local install) and a plain-text `LICENSE` file. No NuGet/npm/pip packages are introduced.

**Version verification:** `actions/checkout@v7`, `actions/setup-dotnet@v6`, and `softprops/action-gh-release@v3` version currency confirmed via direct WebFetch of each project's GitHub page on 2026-08-03 (see Sources). `gh` CLI version (2.46.0) and auth state confirmed live in this environment via `gh --version` / `gh auth status`.

## Package Legitimacy Audit

> This phase installs no NuGet/npm/pip packages. The equivalent supply-chain surface is the three third-party GitHub Marketplace Actions referenced by tag in the new workflow YAML. Audited below in place of the standard package audit; `slopcheck` does not apply to GitHub Actions.

| Action | Registry | Age | Popularity | Source Repo | Verdict | Disposition |
|--------|----------|-----|------------|-------------|---------|-------------|
| `actions/checkout` | GitHub Marketplace | Created 2019-07-19 (~7 yrs) | 8,596 stars, not archived | github.com/actions/checkout (official GitHub org) | OK | Approved |
| `actions/setup-dotnet` | GitHub Marketplace | Created 2019-06-18 (~7 yrs) | 1,194 stars, not archived | github.com/actions/setup-dotnet (official GitHub org) | OK | Approved |
| `softprops/action-gh-release` | GitHub Marketplace | Created 2019-08-25 (~7 yrs) | 5,721 stars, not archived | github.com/softprops/action-gh-release | OK | Approved — third-party (not GitHub-official) but long-established, widely used, actively maintained through v3; requires `permissions: contents: write` scoped to the release job only |

**Actions removed due to risk verdict:** none
**Actions flagged as suspicious:** none

Recommendation for the plan: pin all three to the major-version tags above (`@v7`, `@v6`, `@v3`) rather than `@main`/`@latest`, and rather than full commit SHAs — major-version tags are the project convention used by all three publishers and balance supply-chain stability against staying on patched releases. If stricter pinning is desired later (SHA pinning), that is a hardening task outside this phase's scope, not a blocker.

## Architecture Patterns

### System Architecture Diagram

```
Developer pushes commit / opens PR
        │
        ▼
.github/workflows/build.yml  (trigger: push, pull_request)
  runs-on: windows-latest
        │
        ├─► actions/checkout@v7        (get source)
        ├─► actions/setup-dotnet@v6    (install .NET 10 SDK)
        ├─► dotnet restore
        ├─► dotnet build --no-restore
        └─► dotnet test --no-build     (RigToggle.Tests + RigToggle.Windows.Tests,
                                         no live display/hardware required — see Pitfall 1)
        │
        ▼
GitHub renders workflow result as build-status badge.svg
  (README badge #1 reads this live)


Developer pushes a tag matching v*  (e.g. `git tag v1.2 && git push --tags`)
        │
        ▼
.github/workflows/release.yml  (trigger: push tags: 'v*')
  runs-on: windows-latest
  permissions: contents: write
        │
        ├─► actions/checkout@v7
        ├─► actions/setup-dotnet@v6
        ├─► dotnet publish src/RigToggle.App/RigToggle.App.csproj
        │     -c Release -p:PublishProfile=win-x64
        │     → produces src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
        └─► softprops/action-gh-release@v3
              files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
              → creates (or updates) a GitHub Release for the pushed tag,
                uploads the .exe as a release asset
        │
        ▼
GitHub Releases API now has a "latest release" with an asset
  (README badge #3 — img.shields.io/github/v/release — reads this;
   DOCS-02 "download the released .exe" link points at this asset)


One-time, outside CI (run manually via gh CLI during this phase):
  gh repo edit --visibility public --accept-visibility-change-consequences
  gh release create v1.0 --notes-from-tag --verify-tag
  gh release create v1.1 --notes-from-tag --verify-tag
        │
        ▼
Repo becomes publicly viewable (badges render for outside visitors — D-03)
v1.0 / v1.1 appear in Releases tab with notes, no assets (D-04 backfill)
```

### Recommended Project Structure
```
.github/
└── workflows/
    ├── build.yml       # push/PR: dotnet build + dotnet test on windows-latest
    └── release.yml     # tag push (v*): dotnet publish + attach .exe to GitHub Release
LICENSE                 # MIT, root of repo
docs/
└── screenshots/        # new directory; 4 placeholder targets, files added later by user
    ├── main-normal.png       (not created this phase)
    ├── main-rig.png          (not created this phase)
    ├── settings.png          (not created this phase)
    └── tray-menu.png         (not created this phase)
README.md               # rewritten in place, root
```

### Pattern 1: Two-workflow split (build vs. release)
**What:** Separate `build.yml` (push/PR trigger, no publish/release side effects) from `release.yml` (tag-push trigger only, does the self-contained publish + asset upload).
**When to use:** Whenever a build-status badge and a release-automation pipeline coexist — keeps the noisy, frequent push/PR badge signal decoupled from the rarer, higher-stakes tag-triggered publish+release job. Also means a failed/red build badge never implies "a bad release went out" and vice versa.
**Example:**
```yaml
# .github/workflows/build.yml
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
```yaml
# .github/workflows/release.yml
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
      - run: dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
      - uses: softprops/action-gh-release@v3
        with:
          files: src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe
```
Source: pattern synthesized from official `actions/setup-dotnet` usage docs and `softprops/action-gh-release` README example, adapted to this repo's confirmed publish profile output path (`src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` → `PublishDir` = `bin\publish\win-x64\`, confirmed by direct file read).

### Pattern 2: Badge URLs sourced from live state, not hand-typed values
**What:** All three badges point at endpoints GitHub/shields.io compute live — never hardcode "passing" or a version number as static badge text.
**When to use:** Always, per CONTEXT.md's explicit "badges must be truthful, not decorative" decision (D-02/D-03/D-04 framing).
**Example:**
```markdown
![Build Status](https://github.com/bpivk/monitor-toggle/actions/workflows/build.yml/badge.svg)
![License](https://img.shields.io/github/license/bpivk/monitor-toggle)
![Latest Release](https://img.shields.io/github/v/release/bpivk/monitor-toggle)
```
Source: `[CITED: shields.io, docs.github.com Actions badge docs]` — GitHub natively serves `actions/workflows/{filename}/badge.svg`; shields.io's `github/license` and `github/v/release` endpoints both read GitHub's public repo/releases API and require (a) a real `LICENSE` file GitHub can detect, (b) at least one non-draft GitHub Release to exist — both satisfied by D-01 and D-04 in this same phase.

### Anti-Patterns to Avoid
- **Hardcoding a badge's visual state as a static shields.io "plastic" badge with fixed text (e.g. `img.shields.io/badge/build-passing-green`):** Defeats the entire point — CONTEXT.md explicitly rejected this. Always use the live-endpoint forms above.
- **Triggering the release workflow on every push instead of tag push only:** Would attempt to create/attach assets to a release on every commit, which is either a no-op error (no tag context) or accidentally spams releases. Trigger must be `on: push: tags: ['v*']` only.
- **Setting `RuntimeIdentifier` only in the `.pubxml` and expecting CI's `dotnet publish` to honor it:** Already a documented pitfall in this repo's own `.csproj` comments (see file read) — RID is correctly set in the `.csproj` `PropertyGroup`, not the `.pubxml`. CI workflow must call the exact same command the README already documents (`dotnet publish ... -p:PublishProfile=win-x64`, no separate `-r win-x64` needed since the csproj already sets it) — don't "improve" or shorten this command in the workflow.
- **Using `ubuntu-latest` for either workflow "for speed":** Will fail to compile — this is a `net10.0-windows` WinForms COM-interop project.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Creating a GitHub Release + uploading a binary asset on tag push | Custom `curl`/`gh api` calls to the Releases REST API scripted inline in workflow YAML | `softprops/action-gh-release@v3` | Handles create-or-update-existing-release, multi-file glob upload, and draft/prerelease flags declaratively; hand-rolled REST calls would need to reimplement idempotency (what if the tag's release already exists?) and error handling for no real benefit |
| Build-status badge rendering | A custom badge-generation script/action writing a static SVG | GitHub's built-in `actions/workflows/{file}/badge.svg` endpoint | GitHub already generates and serves this automatically per workflow file — zero setup beyond having the workflow exist |
| License/version badges | Custom API polling + badge image generation | shields.io `img.shields.io/github/license/...` and `img.shields.io/github/v/release/...` | Free hosted service purpose-built for exactly this; polls GitHub's API server-side, caches appropriately, no maintenance burden |

**Key insight:** Every piece of "release infrastructure" this phase needs has a standard, hosted, zero-maintenance solution (GitHub's own badge endpoint, shields.io, softprops' action). The only genuinely custom work is wiring the existing, already-correct `dotnet publish` command into a workflow step — everything else is composition of existing, official/well-established tools.

## Runtime State Inventory

> Not applicable — this phase is net-new infrastructure addition (new workflows, new LICENSE file, README rewrite, new empty directory), not a rename/refactor/migration. No existing runtime state (stored data, live service config, OS-registered state, secrets, build artifacts) is being renamed or moved. The one "state-touching" action — flipping repo visibility and backfilling releases — is additive/one-time via `gh` CLI, not a rename. Confirmed via CONTEXT.md's `<canonical_refs>` section and direct `gh repo view`/`gh release list` checks: no prior GitHub Releases exist to migrate, no prior workflow files exist to replace.

## Common Pitfalls

### Pitfall 1: Assuming `dotnet test` needs a display/GUI on `windows-latest`
**What goes wrong:** Teams sometimes assume any project referencing WinForms needs `xvfb`-equivalent tooling or a virtual display to run its test suite in CI, and either skip tests in CI or add unnecessary display-virtualization steps.
**Why it happens:** WinForms projects often *do* have UI-automation tests that instantiate real `Form`/`Control` objects, which can fail or hang on headless runners.
**How to avoid:** Confirmed by direct inspection of this repo's test projects: `RigToggle.Tests` targets plain `net10.0` (no WinForms reference at all — pure logic tests: `HotkeyFormatterTests`, `JsonStoreTests`, `StartupArgsTests`, `ThemeProviderContractTests`, `ToggleOrchestratorTests`, `ToggleResultFormatterTests`, `ToggleServiceTests`). `RigToggle.Windows.Tests` targets `net10.0-windows` and references `RigToggle.Windows` (which pulls in `WindowsDisplayAPI`), but its actual test bodies (read directly, `WindowsMonitorControllerTests.cs`) only exercise pure reflection-patch and source-assignment logic against constructed fake `PathTargetInfo`/`PathDisplaySource` objects — the code comments in that file explicitly note the live-hardware CCD calls (`GetActivePaths`/`ApplyPathInfos`) are "NOT unit-tested here... remain verified only via live rig testing." No test in either project instantiates a `Form` or requires a real/virtual display. `dotnet test` on `windows-latest` should run both projects cleanly headless. `[VERIFIED: direct source read of both test projects and their .csproj files]`
**Warning signs:** If a future test is added that calls `PathInfo.GetActivePaths()`/`ApplyPathInfos()` directly or instantiates a `Form`, it would need to be tagged/skipped in CI — not a concern for the current test suite, but worth a one-line comment in the workflow if this changes later.

### Pitfall 2: Badge renders "no releases found" / broken license badge because of ordering
**What goes wrong:** If the README (with its three badges) is committed/merged before the LICENSE file exists and before any GitHub Release is published, the license and release badges will render as "invalid"/red/broken on GitHub even though the workflow YAML and README markdown are both syntactically correct.
**Why it happens:** shields.io's `github/license` and `github/v/release` endpoints query live GitHub API state at *render* time (every page load), not at commit time — there's no "will populate later" grace period.
**How to avoid:** Sequence the plan so LICENSE and the v1.0/v1.1/v1.2 releases exist *before* (or in the same merged state as) the README badges go live. Since this is one phase merged together, order within the plan matters less than ensuring the final merged state has all three prerequisites (LICENSE file present, repo public, ≥1 GitHub Release present) simultaneously.
**Warning signs:** Badge shows "license: NOASSERTION" or "release: none" after merge — check LICENSE file is at repo root (not `docs/LICENSE`) and in a format GitHub's license-detector recognizes (standard MIT text works).

### Pitfall 3: `gh repo edit --visibility public` silently requires the extra consequences flag
**What goes wrong:** Running `gh repo edit --visibility public` alone fails with an error demanding `--accept-visibility-change-consequences` — easy to miss in a scripted/non-interactive context and treat as a transient failure.
**Why it happens:** GitHub CLI intentionally gates this specific flag combination to force acknowledgment of consequences (detaching public forks, losing certain settings) even when run non-interactively.
**How to avoid:** Always pair the two flags: `gh repo edit --visibility public --accept-visibility-change-consequences`. `[VERIFIED: cli.github.com/manual/gh_repo_edit]`
**Warning signs:** Command exits non-zero with a message about required consequences flag.

### Pitfall 4: Release workflow publish path drift
**What goes wrong:** Workflow YAML references an artifact path that doesn't match the actual publish output directory (e.g. assuming `bin/Release/net10.0-windows/win-x64/publish/` — the .NET SDK default — instead of this project's explicitly overridden `PublishDir`).
**Why it happens:** The default self-contained single-file publish output path differs from what this project's `.pubxml` explicitly sets (`<PublishDir>bin\publish\win-x64\</PublishDir>`).
**How to avoid:** Use the confirmed actual path: `src/RigToggle.App/bin/publish/win-x64/RigToggle.App.exe`. Confirmed by direct read of `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` and cross-referenced against the existing root `README.md`, which independently documents the same path ("The output single-file exe lands in `src/RigToggle.App/bin/publish/win-x64/`"). `[VERIFIED: direct file read]`
**Warning signs:** `softprops/action-gh-release` step fails with "no files matched the glob pattern."

## Code Examples

### MIT LICENSE file (standard OSI template)
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
Source: OSI/opensource.org canonical MIT License text `[CITED: opensource.org/license/mit]`. Copyright holder name (`Blaz Pivk`) and year (`2026`) taken directly from this repo's `git config user.name` (`[VERIFIED: git config user.name]`) and current date; substitute if the user prefers a different holder name (e.g. GitHub handle `bpivk`) — flag as a one-line confirmation in planning, not a research gap.

### gh CLI: repo visibility flip (D-03)
```bash
gh repo edit bpivk/monitor-toggle --visibility public --accept-visibility-change-consequences
```
Source: `[VERIFIED: cli.github.com/manual/gh_repo_edit, plus live gh auth status confirming 'repo' scope token is present in this environment]`

### gh CLI: backfill v1.0 / v1.1 releases from existing annotated tags (D-04)
```bash
gh release create v1.0 --notes-from-tag --verify-tag --repo bpivk/monitor-toggle
gh release create v1.1 --notes-from-tag --verify-tag --repo bpivk/monitor-toggle
```
`--notes-from-tag` pulls the existing annotated-tag message verbatim (confirmed both `v1.0` and `v1.1` are annotated tags via `git cat-file -t`, each with a substantive multi-line message already written) — no binary asset flag is passed, matching D-04's "notes only, no binary attached" requirement. `--verify-tag` aborts if the tag somehow doesn't exist on the remote, a safe no-op guard here since both tags are already pushed. Source: `[VERIFIED: cli.github.com/manual/gh_release_create, cross-checked against live git tag inspection in this repo]`

### GitHub Actions: full release workflow (D-02/D-05)
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
Source: composed from `[VERIFIED: actions/checkout GitHub page, actions/setup-dotnet GitHub page, softprops/action-gh-release GitHub page — all fetched 2026-08-03]`, publish command reused verbatim from this repo's existing `README.md` and cross-verified against `RigToggle.App.csproj`/`win-x64.pubxml`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `softprops/action-gh-release@v2` | `@v3` (Node 24 runtime) | v2.6.2 marked final/unmaintained; v3 required | v2 still technically functions but relies on a GitHub Actions-deprecated Node 20 runtime — use v3 for a new workflow written today |
| `actions/checkout@v4`/`v5`/`v6` | `@v7` | June 2026, "pwn request" hardening for `pull_request_target`/`workflow_run` | Not directly relevant to this project's simple push/PR/tag-push triggers (no `pull_request_target` usage planned), but v7 is current and has no known downside for this use case |

**Deprecated/outdated:**
- Manually maintained/static badge images: superseded project-wide by live-endpoint badges (GitHub's own workflow badge + shields.io's GitHub-API-backed endpoints) — not a versioned tool change, just the correct pattern per this project's own CONTEXT.md decision.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Copyright holder name for LICENSE should be "Blaz Pivk" (from `git config user.name`) rather than the GitHub handle "bpivk" or a company name | Code Examples — MIT LICENSE | Low — cosmetic, one-line swap if the user prefers a different holder string; does not affect license validity or badge rendering either way |
| A2 | `windows-latest` currently resolves to a Windows Server 2025 (or 2022) image with .NET-buildable toolchain support for `net10.0-windows` via `actions/setup-dotnet@v6` with no additional Windows SDK/component installation steps needed | Architecture Patterns, Code Examples | Medium — if the default `windows-latest` image lacks a component this project needs (e.g. specific Windows SDK headers for COM interop compilation), the build step could fail; standard `net10.0-windows`/WinForms projects have not historically needed anything beyond the .NET SDK to *build* (only to *run* certain live-hardware-dependent code paths, which this project's tests already avoid per Pitfall 1) — this is a reasonable but not independently execution-verified assumption, since no CI run was actually triggered during this research pass |

**If this table is empty:** N/A — two low/medium-risk assumptions logged above; everything else in this document (package/action versions, CLI flag syntax, publish paths, tag types, repo visibility state, `gh` auth state) was verified directly against either official current sources or this repo's live state.

## Open Questions (RESOLVED)

1. **Should the v1.2 release badge/DOCS-02 download link be tested end-to-end before this phase is marked complete?**
   - What we know: The workflow YAML pattern is verified against current official docs and this repo's exact publish path/command. `gh` CLI commands for visibility flip and v1.0/v1.1 backfill were verified against the CLI manual and can be run directly (auth already confirmed present in this environment).
   - What's unclear: Whether the plan should include an actual `git tag v1.2 && git push --tags` step to trigger the new release workflow live (producing a real, verifiable v1.2 release + asset) as part of this phase's completion criteria, or whether that's deferred to whenever v1.2 actually ships as a milestone (per CONTEXT.md's phrasing, "v1.2: a real, current release once this milestone ships" — implying it may happen at milestone close, not mid-phase).
   - Recommendation: Planner should treat "workflow files exist and are syntactically/logically correct, verified by the build workflow triggering successfully on the phase's own commits (push trigger)" as the phase's own completion bar, and leave the actual `v1.2` tag push as a milestone-close action (outside this phase) — consistent with how CONTEXT.md scopes D-04/D-05 (backfill v1.0/v1.1 *now*, but v1.2 "once this milestone ships"). Flag this explicitly in the plan so it isn't silently dropped.
   - **RESOLVED:** Adopted as recommended. 14-03's human-verify checkpoint (Task 3) sets the completion bar as "build workflow ran green on this phase's own commits" and explicitly excludes a v1.2 tag push, which is deferred to milestone close per D-04.

2. **Exact wording/section ordering of the README's generic feature overview (D-10 through D-13)**
   - What we know: Content requirements are fully specified in CONTEXT.md (D-10 feature list, D-11 generic naming constraint, D-12 problem statement, D-13 system requirements note) — this is explicitly left to Claude's Discretion for ordering/headings.
   - What's unclear: Nothing blocking — this is discretionary by design, not a research gap.
   - Recommendation: Planner can proceed directly; no further research needed for README prose content.
   - **RESOLVED:** 14-03 Task 2 specifies concrete section ordering (badges → problem statement → feature overview → screenshots → download/build → system requirements) and exact content per D-10 through D-13, with no ambiguity left for the executor.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `gh` CLI | D-03 (visibility flip), D-04 (release backfill) | Yes | 2.46.0, authenticated as `bpivk` with `repo`/`workflow` scopes | — |
| `dotnet` SDK (local) | Verifying publish command locally before relying on CI | No (`dotnet: command not found` in this research environment) | — | Not blocking — the exact publish command is already proven correct (documented in this repo's own README and cross-verified against `.csproj`/`.pubxml`); CI's `windows-latest` runner (which does have the SDK via `actions/setup-dotnet`) is the actual execution environment for this command, not this research sandbox. No local build/test verification was possible or necessary for this research pass. |
| GitHub Actions (windows-latest runner) | Build + release workflows | Assumed available (standard GitHub-hosted runner label) | — | See Assumption A2 |

**Missing dependencies with no fallback:**
- None — the one missing local tool (`dotnet` SDK in this research sandbox) has a full fallback (CI runner has it; the command itself is already verified via existing docs/config, not execution).

**Missing dependencies with fallback:**
- Local `dotnet` SDK — fallback is CI execution on `windows-latest`, which is this project's actual target build environment for this command regardless.

## Validation Architecture

> `workflow.nyquist_validation` is `false` in `.planning/config.json` — this section is skipped per instructions.

## Project Constraints (from CLAUDE.md)

- Publish must remain self-contained, single-file, untrimmed (`PublishTrimmed=false`), win-x64 only — the CI release workflow's `dotnet publish` step MUST reuse the exact existing command (`dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64`) rather than reconstructing flags manually, to avoid accidentally reintroducing trimming or dropping `SelfContained`/`PublishSingleFile`.
- Do not suggest `PublishTrimmed=true` or framework-dependent (`--self-contained false`) alternatives anywhere in this phase's workflow YAML or documentation — confirmed not proposed anywhere in this research.
- No admin/elevation manifest — not relevant to this phase (no exe behavior changes), noted only for completeness since CLAUDE.md treats it as a standing constraint on `RigToggle.App.csproj`.
- GSD workflow enforcement: file-changing work for this phase must go through `/gsd:plan-phase` → execution, not direct ad-hoc edits — this research document itself and the eventual PLAN.md/execution are the correct workflow path already in use.

## Sources

### Primary (HIGH confidence)
- Direct repo inspection: `src/RigToggle.App/RigToggle.App.csproj`, `src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml`, `README.md`, `RigToggle.sln`, `src/RigToggle.Tests/RigToggle.Tests.csproj`, `src/RigToggle.Windows.Tests/RigToggle.Windows.Tests.csproj`, `src/RigToggle.Windows.Tests/WindowsMonitorControllerTests.cs` — confirmed publish output path, test project TFMs/dependencies, and absence of display-hardware-dependent test bodies
- Live environment checks: `gh --version`, `gh auth status`, `gh release list --repo bpivk/monitor-toggle` (empty), `gh repo view bpivk/monitor-toggle --json visibility,isPrivate` (`PRIVATE`), `git cat-file -t v1.0`/`v1.1` (both `tag` = annotated), `git tag -l -n99` (message contents), `git config user.name`/`user.email`
- https://github.com/actions/checkout (fetched 2026-08-03) — confirmed current major version v7, June 2026 hardening context
- https://github.com/actions/setup-dotnet (fetched 2026-08-03) — confirmed current major version v6, example `dotnet-version: '10.0.x'` usage
- https://github.com/softprops/action-gh-release (fetched 2026-08-03) — confirmed current major version v3 (v2 explicitly marked unmaintained/Node-20-deprecated), `permissions: contents: write` requirement, `files:` input syntax
- https://cli.github.com/manual/gh_release_create (fetched 2026-08-03) — confirmed `--notes-from-tag`, `--verify-tag` flag syntax
- https://cli.github.com/manual/gh_repo_edit (fetched 2026-08-03) — confirmed `--visibility public --accept-visibility-change-consequences` required-pair syntax
- `gh api repos/{softprops/action-gh-release,actions/setup-dotnet,actions/checkout}` — confirmed star counts, creation dates, non-archived status for Package Legitimacy Audit equivalent

### Secondary (MEDIUM confidence)
- WebSearch: GitHub Actions badge URL pattern (`.../actions/workflows/{file}/badge.svg`) — corroborated by both WebSearch synthesis and general GitHub Actions documentation knowledge; not independently fetched from docs.github.com directly in this session, but the pattern is stable/unchanged and low-risk to get wrong (would simply 404/show a broken badge, easily caught in review)
- WebSearch: shields.io `img.shields.io/github/license/{owner}/{repo}` and `img.shields.io/github/v/release/{owner}/{repo}` endpoint syntax — standard, long-stable shields.io GitHub-integration endpoints, consistent across multiple search result summaries

### Tertiary (LOW confidence)
- None — all findings in this research either verified directly against live repo/environment state or fetched from the authoritative source (official GitHub project page or `gh` CLI manual) on the research date.

## Metadata

**Confidence breakdown:**
- Standard stack (Actions versions, gh CLI syntax): HIGH — every version/flag directly fetched from the authoritative source or verified live in this environment on 2026-08-03
- Architecture (workflow structure, publish path): HIGH — publish path confirmed via direct file read of this exact repo's `.pubxml`/`.csproj`, cross-checked against its own README
- Pitfalls (headless test execution, badge ordering, visibility flag): HIGH — headless-test claim verified by direct source read of both test projects (not inferred), remaining pitfalls verified against official `gh` CLI docs

**Research date:** 2026-08-03
**Valid until:** 2026-09-02 (30 days — GitHub Actions marketplace action major versions and shields.io endpoints are relatively stable, but action major-version bumps do occur on this kind of cadence; re-verify `actions/checkout`/`actions/setup-dotnet`/`softprops/action-gh-release` current major version tags if planning is delayed past this window)
