# Phase 14: README & Release Documentation - Context

**Gathered:** 2026-08-03
**Status:** Ready for planning

<domain>
## Phase Boundary

A GitHub-ready README.md that communicates what the app does, how to get it, and how to build it — feature overview with screenshot placeholders, install/build instructions, and three badges (build status, license, latest release) — referencing the finished visual polish from Phases 12-13. To make the badges honest (not decorative), this phase also covers the minimal backing infrastructure they depend on: a LICENSE file, a minimal GitHub Actions CI/release workflow, and actually publishing GitHub Releases (with the built .exe attached going forward). Does not touch toggle/monitor/audio/tray/hotkey logic, MainForm/SettingsForm theming (Phase 12, shipped), or tray/exe icon artwork (Phase 13, shipped) — this phase is documentation, license, and release-plumbing only.

</domain>

<decisions>
## Implementation Decisions

### License
- **D-01:** Add an MIT LICENSE file at the repo root. Chosen as the simplest, most standard permissive license for a small personal utility going public on GitHub, with a standard shields.io badge available.

### CI / Build-Status Badge
- **D-02:** Add a minimal GitHub Actions workflow that runs `dotnet build` (and existing tests) on push/PR, so the build-status badge reflects a real pipeline rather than being decorative or manually maintained. This expands phase scope slightly beyond pure docs, but was an explicit, deliberate choice — DOCS-03's badge should be truthful.

### Repo Visibility
- **D-03:** Make the `bpivk/monitor-toggle` GitHub repo public as part of this phase. Currently private (confirmed via `gh repo view`), which would break shields.io badge rendering for outside viewers and contradicts a "GitHub-ready README." This is a repo-setting change, not a code change — flag it explicitly in the plan as a one-time manual/scripted GitHub setting flip.

### Releases (Latest-Release Badge + DOCS-02 "download the released .exe")
- **D-04:** No GitHub Release has ever been published (confirmed via `gh release list` — empty, despite `v1.0`/`v1.1` git tags existing). This phase publishes real GitHub Releases, not just documentation describing a future workflow:
  - **v1.0 and v1.1:** backfilled as GitHub Releases from the existing tags, release notes only — **no binary attached** (original build artifacts weren't kept; user confirmed no old .exe files are available).
  - **v1.2:** a real, current release once this milestone ships, with the built .exe attached.
- **D-05:** The GitHub Actions workflow (D-02) is extended so that on a version-tag push, it runs the existing self-contained single-file `dotnet publish` command (per the current README's documented publish command) and attaches the resulting `.exe` to the auto-created GitHub Release. This automates DOCS-02 going forward — no more manual publish+manual-upload for future releases.

### Screenshots & Visuals (DOCS-01)
- **D-06:** Four screenshot placeholder slots: MainForm in normal mode, MainForm in rig mode, SettingsForm, and the tray context menu. Broader than the two-mode minimum literally named in DOCS-01 — shows off Phase 12's theming (Settings dialog) and Phase 13's new tray icons/menu, not just the main toggle window.
- **D-07:** Placeholders use real markdown image syntax pointing to not-yet-existing files (e.g. `![MainForm — normal mode](docs/screenshots/main-normal.png)`), not bracketed TODO text. Renders as a broken-image icon on GitHub until the user drops the real files in — correct final syntax is already in place, no README edit needed later.
- **D-08:** Screenshot files live under `docs/screenshots/` (new directory) — keeps the repo root clean and groups documentation assets together.
- **D-09:** No animated GIF/demo clip. Static screenshots only — keeps asset scope to the four placeholders above; a GIF requires screen recording/conversion tooling this phase doesn't need to take on.

### Feature Overview Depth & Tone
- **D-10:** Cover the full current automation surface, not just the v1.0 toggle headline: the core toggle action, tray residency/autostart, global hotkey, multi-monitor sets, live theme-following, and the redesigned icons. The app has grown substantially since v1.0 — a reader landing on this repo should see the current feature set, not the original MVP pitch.
- **D-11:** Keep the README generic/configurable-sounding — do **not** name "Moza Companion" or "BeamNG.drive" specifically, even though PROJECT.md/CLAUDE.md use those names internally. Frame it as "switch to a secondary display + launch a companion app" rather than naming the specific rig/game. **Note for downstream agents:** this diverges from the recommended option (which was to name them, matching PROJECT.md's own framing) — the user made a deliberate choice here to keep the public-facing README more general-sounding than the internal project docs. Do not silently "correct" this back to Moza/BeamNG-specific language when writing the README.
- **D-12:** Include a generic problem statement explaining *why* the tool exists (games misbehaving when launched on what Windows considers a secondary display) — without naming specific games, consistent with D-11's generic framing.
- **D-13:** Include a brief system-requirements note: Windows 10/11 x64, with a callout that full visual polish (Mica/rounded corners) is Windows 11-only and gracefully degrades on Windows 10 (per Phase 12's THEME-06 behavior).

### Claude's Discretion
- Exact README section ordering/headings, badge visual style (flat vs. flat-square shields.io style), and exact wording of the generic problem statement.
- Exact GitHub Actions workflow YAML structure (job/step naming, trigger conditions beyond "push/PR" for build and "version tag push" for release) — implementation detail for research/planning.
- Whether the v1.0/v1.1 backfilled release notes are auto-generated from commit history or hand-written summaries — left to planning.
- Whether badges live in a single line at the top of the README or are grouped/styled differently — standard GitHub README convention, not a vision decision.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & scope decisions
- `.planning/REQUIREMENTS.md` (DOCS-01 through DOCS-03)
- `.planning/ROADMAP.md` (Phase 14 section — goal, success criteria, depends on Phase 12 + 13)
- `.planning/PROJECT.md` (Current Milestone section — v1.2 target features, screenshot-placeholder framing already decided at milestone scoping)

### Existing repo state (current, being extended)
- `README.md` (repo root) — current minimal README: intro paragraph + build-from-source instructions only. No feature overview, no badges, no screenshots. Its documented `dotnet publish` command (self-contained, single-file, win-x64, untrimmed) is the exact command the new CI release workflow (D-05) must reuse.
- `src/RigToggle.App/RigToggle.App.csproj` — publish profile / project settings the CI workflow's `dotnet publish` step must reference.
- No `LICENSE` file exists (confirmed via `ls`) — created fresh per D-01.
- No `.github/workflows/` directory exists (confirmed via `ls`) — created fresh per D-02/D-05.
- Git tags `v1.0`, `v1.1` exist (confirmed via `git tag`) but have **no** corresponding GitHub Releases (confirmed via `gh release list` — empty output) — backfilled per D-04.
- GitHub repo `bpivk/monitor-toggle` is currently **private** (confirmed via `gh repo view --json visibility,isPrivate`) — flipped to public per D-03.

### Prior phases (what the screenshots/feature overview must accurately reflect)
- `.planning/phases/12-theme-infrastructure-live-theme-following/12-CONTEXT.md` — theming decisions (Mica, dark title bar, Windows 10 fallback) the README's system-requirements note (D-13) and screenshots must accurately represent.
- `.planning/phases/13-tray-app-icon-redesign/13-CONTEXT.md` — icon motif/style decisions (monitor vs. steering-wheel silhouette, monochrome tray + color exe icon) the tray-menu screenshot placeholder (D-06) and feature overview should reference correctly.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- README's existing `dotnet publish -c Release -p:PublishProfile=win-x64` command (and its explicit-flag fallback form) — reuse verbatim in the new CI release workflow rather than re-deriving the publish invocation.

### Established Patterns
- None specific to CI/release — this is new infrastructure for the project (no prior GitHub Actions workflows exist to follow a pattern from).

### Integration Points
- New `.github/workflows/*.yml` — build workflow (push/PR trigger) and release workflow (version-tag-push trigger, publish + attach .exe).
- New `LICENSE` file at repo root.
- `README.md` — rewritten/expanded in place at repo root.
- New `docs/screenshots/` directory — placeholder target for user-supplied screenshots (files not created this phase, only referenced).

</code_context>

<specifics>
## Specific Ideas

- Badges should reflect real state: build status from actual CI, license from an actual LICENSE file, latest release from actual GitHub Releases — the user explicitly rejected decorative/static badges during discussion.
- v1.0/v1.1 GitHub Releases get release notes but no binary (originals not retained) — v1.2 onward gets both notes and an attached .exe, automated via CI on tag push.
- Public-facing README should stay generic/configurable-sounding (no "Moza"/"BeamNG" naming) even though internal project docs (CLAUDE.md, PROJECT.md) use those specific names — a deliberate divergence, not an oversight.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. An animated demo GIF (D-09) was considered and explicitly declined, not deferred to a future phase (no follow-up expected).

</deferred>

---

*Phase: 14-readme-release-documentation*
*Context gathered: 2026-08-03*
