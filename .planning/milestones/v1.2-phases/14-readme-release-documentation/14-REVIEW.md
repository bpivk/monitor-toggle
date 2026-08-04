---
phase: 14-readme-release-documentation
reviewed: 2026-08-03T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - .github/workflows/build.yml
  - .github/workflows/release.yml
  - LICENSE
  - README.md
  - docs/screenshots/.gitkeep
findings:
  critical: 0
  warning: 4
  info: 1
  total: 5
status: issues_found
---

# Phase 14: Code Review Report

**Reviewed:** 2026-08-03
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the two net-new GitHub Actions workflows, the new root LICENSE file, and the
rewritten README, per the phase's CI/release-documentation scope.

The previously-fixed `branches: [main]` → `branches: [master]` bug (commit `10f401f`) is
confirmed complete and correct: `build.yml` now triggers on `push`/`pull_request` to
`master`, which matches the repo's actual default branch (verified via `git branch -a` and
`origin/master`). No other trigger/branch-name mismatches were found — `release.yml`
correctly triggers on tag push (`v*`), which is branch-independent.

The `dotnet publish` command and output path are consistent across all three places that
reference them: `RigToggle.App.csproj` (sets `RuntimeIdentifier=win-x64` at the project
level, per the pubxml's own documented workaround for a `dotnet publish` CLI limitation),
`win-x64.pubxml` (`PublishDir=bin\publish\win-x64\`), `release.yml`'s `files:` path, and
`README.md`'s documented output path. No mismatch found there.

The LICENSE file is well-formed, unmodified standard MIT license text with correct
copyright holder/year — no defects.

The main defect found is not in the reviewed files' internal logic but in what they claim
about each other: the README's "Screenshots" section references four PNG files
(`main-normal.png`, `main-rig.png`, `settings.png`, `tray-menu.png`) that do not exist
anywhere in the repository — `docs/screenshots/` contains only the `.gitkeep` placeholder.
This renders the entire Screenshots section broken on GitHub. Beyond that, both workflows
have real (if lower-severity) CI/CD hardening gaps: `build.yml` sets no explicit
`permissions:` block despite running on `pull_request`, and `release.yml` publishes and
attaches a release asset with no build/test verification gate immediately beforehand, so a
tag pushed against an untested or broken commit will still produce and publish a release
exe. Third-party/first-party Actions are pinned to floating major-version tags rather than
immutable commit SHAs, which is a supply-chain hardening gap most relevant to
`release.yml` given its `contents: write` permission.

## Warnings

### WR-01: README Screenshots section references image files that don't exist in the repo

**File:** `README.md:41-49`
**Issue:** The Screenshots section embeds four images:
```markdown
| ![MainForm — normal mode](docs/screenshots/main-normal.png) | ![MainForm — rig mode](docs/screenshots/main-rig.png) |
| ![Settings](docs/screenshots/settings.png) | ![Tray menu](docs/screenshots/tray-menu.png) |
```
None of `main-normal.png`, `main-rig.png`, `settings.png`, or `tray-menu.png` exist in the
repo — `docs/screenshots/` contains only `.gitkeep` (confirmed via `git ls-files` and
filesystem listing: zero `.png` files tracked anywhere in the repository). Every image in
this section will render as a broken-image icon on GitHub. This isn't a placeholder-pending
note in the text (unlike the "Download" section's honest "earlier releases are notes-only"
caveat) — the markdown asserts these images exist now.
**Fix:** Either add the actual screenshot PNGs to `docs/screenshots/` before merging this
phase, or (if genuinely deferred to a follow-up) replace the embedded images with an
explicit "screenshots coming soon" note so the README doesn't ship with silently broken
image links.

### WR-02: build.yml has no explicit `permissions:` block

**File:** `.github/workflows/build.yml:1-17`
**Issue:** Unlike `release.yml` (which correctly scopes `permissions: contents: write`),
`build.yml` declares no `permissions:` key at all, so the `GITHUB_TOKEN` used by the
`build` job falls back to the repository/organization default. This matters more here than
in a typical internal-only workflow because the job also runs on `pull_request` — any PR
branch's code gets `dotnet restore`/`dotnet build`/`dotnet test` executed against it, which
is arbitrary-code-execution territory (NuGet restore can execute build-time MSBuild
targets/scripts from PR-controlled `.csproj`/`Directory.Build.props`/`NuGet.config`
content). If the repo/org default token permission is ever set to read-write (it is not
guaranteed read-only across all GitHub account/org configurations), a malicious or
compromised PR could use that ambient token scope.
**Fix:** Add an explicit least-privilege permissions block, since the build job never needs
to write anything:
```yaml
permissions:
  contents: read
```

### WR-03: Actions pinned to floating major-version tags, not immutable commit SHAs

**File:** `.github/workflows/build.yml:11-12`, `.github/workflows/release.yml:15,17,25`
**Issue:** All three Actions used (`actions/checkout@v7`, `actions/setup-dotnet@v6`,
`softprops/action-gh-release@v3`) are referenced by mutable major-version tags. Tags can be
force-moved by the action's maintainer (or an attacker who compromises that maintainer's
account/npm-equivalent), silently swapping in different code on the next run without any
change to this repo's own files. This is the standard GitHub Actions supply-chain
hardening gap (flagged explicitly by GitHub's own security hardening guide and tools like
`StepSecurity`/`zizmor`). It's most consequential in `release.yml`, which runs with
`contents: write` — a compromised `softprops/action-gh-release@v3` (a third-party, non-
GitHub-owned action) running with that token could modify/delete releases, push tags, or
alter repo content.
**Fix:** Pin all three actions to a full-length commit SHA, with the version tag as a
trailing comment for readability, e.g.:
```yaml
- uses: actions/checkout@<full-sha>  # v7
```
Renovate/Dependabot can then keep the SHA pins current.

### WR-04: release.yml has no build/test verification before publishing the release exe

**File:** `.github/workflows/release.yml:12-27`
**Issue:** `release.yml` triggers on any pushed tag matching `v*` and immediately runs
`dotnet publish` followed by attaching the resulting exe to a GitHub Release — there is no
`dotnet restore`/`dotnet build`/`dotnet test` step beforehand, and no dependency on the
separate `build.yml` workflow having passed for that commit. Since `push: tags:` is a
distinct trigger from `push: branches: [master]`, a tag can be created and pushed against
any commit (e.g., a commit that was never pushed to `master`, or one still mid-review) with
no CI signal at all gating the release. If `dotnet publish` happens to succeed with warnings
on code that has broken tests or would fail `dotnet build` under the Release workflow's
own checks, a broken exe is still published to end users.
**Fix:** Add a test step before publish (mirrors `build.yml`'s steps), or require the tag
commit's build.yml run to have succeeded, e.g.:
```yaml
- run: dotnet restore
- run: dotnet build --no-restore -c Release
- run: dotnet test --no-build -c Release
- name: Publish self-contained single-file exe
  run: dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```

## Info

### IN-01: No `timeout-minutes` set on either workflow's job

**File:** `.github/workflows/build.yml:8-9`, `.github/workflows/release.yml:12-13`
**Issue:** Neither job sets `timeout-minutes`, so a hang in `dotnet restore`/`build`/`test`/
`publish` (e.g. a stalled NuGet feed) would run until GitHub Actions' default 6-hour job
timeout, burning Actions minutes for a low-value hang instead of failing fast.
**Fix:** Add a modest timeout, e.g. `timeout-minutes: 15` at the job level in both files.

---

_Reviewed: 2026-08-03_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
