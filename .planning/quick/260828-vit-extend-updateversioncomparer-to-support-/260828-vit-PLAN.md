---
phase: quick-260828-vit
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Core/UpdateVersionComparer.cs
  - src/RigToggle.Core/UpdateOrchestrator.cs
  - src/RigToggle.Tests/UpdateVersionComparerTests.cs
  - src/RigToggle.Tests/UpdateOrchestratorTests.cs
  - src/RigToggle.App/RigToggle.App.csproj
autonomous: true
requirements: [UPDATE-01, UPDATE-02]

estimate:
  tokens: 45000
  raw_tokens: 45000
  tasks: 2
  confidence: low

must_haves:
  truths:
    - "A three-segment tag vX.Y.Z reads as newer than the same X.Y build at a lower patch (UPDATE-02)"
    - "Every historical two-segment tag (v1.0 through v2.2) still parses and orders exactly as before, treated as patch 0 (UPDATE-02)"
    - "Skipping a version no longer silently suppresses a later point release of that same X.Y (UPDATE-02)"
    - "A published GitHub release v2.2.1 exists with the exe + sha256 assets, downloadable for Phase 26 rig UAT (UPDATE-01)"
  artifacts:
    - "src/RigToggle.Core/UpdateVersionComparer.cs — three-component parse/compare"
    - "src/RigToggle.App/RigToggle.App.csproj — <Version>2.2.1</Version>"
    - "annotated git tag v2.2.1 present on origin"
    - "GitHub release v2.2.1 carrying RigToggle.App.exe and RigToggle.App.exe.sha256"
  key_links:
    - "UpdateOrchestrator skip-tracking round-trip: TryParseTag -> new Version(major, minor, patch) -> IsNewer must carry the patch through, or point releases get swallowed by a prior skip"
    - "csproj <Version> -> release.yml p:Version=${tag#v} -> assembly version read at Program.cs:299 -> UpdateVersionComparer.IsNewer"
---

<objective>
Extend `UpdateVersionComparer` from Major.Minor-only to three-component semver (vX.Y.Z),
keeping every existing two-segment tag in git history (v1.0 ... v2.2) working as patch 0,
then cut a real `v2.2.1` release so Phase 26's auto-update feature can be exercised against
live GitHub Releases on the rig.

Purpose: Phase 26 (auto-update) is built but its UAT is blocked — there is no release newer
than the running build to update *to*. The project is also switching its tag scheme to
three-component semver going forward, which today's comparer would misread.
Output: A patch-aware comparer + orchestrator, updated tests and doc comments, a version
bump to 2.2.1, and a published GitHub release at tag `v2.2.1`.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

@src/RigToggle.Core/UpdateVersionComparer.cs
@src/RigToggle.Core/UpdateOrchestrator.cs
@src/RigToggle.Tests/UpdateVersionComparerTests.cs
@src/RigToggle.Tests/UpdateOrchestratorTests.cs
@src/RigToggle.App/RigToggle.App.csproj
</context>

<preflight>
All commands below run from the repository root `/home/bpivk/moza` on branch `master`.

Verified at planning time (2026-08-28):
- Working tree is clean; the only untracked path is `.gsd/`, which must NEVER be staged.
  Always `git add` explicit file paths — never `git add -A` / `git add .`.
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` runs on this Linux host and
  passes: 209/209, ~430 ms. `RigToggle.Tests` targets `net10.0` and references only
  `RigToggle.Core`, so it builds here. The `net10.0-windows` projects (`RigToggle.App`,
  `RigToggle.Windows`, `RigToggle.Windows.Tests`) are NOT locally buildable — the
  `windows-latest` release workflow is their build gate. Do not attempt a solution-wide
  `dotnet build` / `dotnet test` and do not treat its failure as a regression.
- `gh` is installed and authenticated as `bpivk` with push access to
  `origin` = https://github.com/bpivk/monitor-toggle.git.
- Existing tags: v1.0, v1.1, v1.2, v2.0, v2.1. `v2.2` was never tagged; `v2.2.1` is free.
</preflight>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Extend UpdateVersionComparer and its orchestrator call sites to three components</name>

  <files>
src/RigToggle.Core/UpdateVersionComparer.cs
src/RigToggle.Core/UpdateOrchestrator.cs
src/RigToggle.Tests/UpdateVersionComparerTests.cs
src/RigToggle.Tests/UpdateOrchestratorTests.cs
  </files>

  <read_first>
`src/RigToggle.Core/UpdateVersionComparer.cs` in full (91 lines — the class doc comment
carries the entire design rationale being amended).
`src/RigToggle.Core/UpdateOrchestrator.cs` lines 145-210 (the running-version text at ~152,
the `IsNewer` call at ~158, and the skip-tracking block at ~177-206).
`src/RigToggle.Tests/UpdateVersionComparerTests.cs` in full (99 lines).
`src/RigToggle.Tests/UpdateOrchestratorTests.cs` lines 20-35 (the `RunningVersion` /
`NewerRelease` constants) and lines 185-237 (the two skip-tracking tests).
  </read_first>

  <behavior>
Adding a fourth `out` parameter to `TryParseTag` is a breaking signature change that
ripples into production and test code in the same compile pass, so this task lands the
comparer, both call sites, and the tests together — the RED phase here surfaces first as
compile errors, then as assertion failures, before going green. Write the new test cases
before changing the comparer body.

`TryParseTag(string? tag, out int major, out int minor, out int patch)`:
  - `"v1.2"` / `"V1.2"` / `"1.2"` -> true, (1, 2, 0)   — two segments mean patch 0, which
    is what keeps every tag already in git history (v1.0 ... v2.2) comparing exactly as before
  - `"v2.2.1"` -> true, (2, 2, 1)
  - `"v2.10.5"` -> true, (2, 10, 5)                     — was previously (2, 10) with the
    third segment discarded; the third segment is now meaningful
  - `"1.2.3.4"` -> true, (1, 2, 3)                      — segments beyond the third are still ignored
  - `"v2.2.x"` -> false                                 — a present-but-unparseable patch segment
    is a parse failure, not a silent 0
  - `null` / `""` / `"v"` / `"2"` / `"vX.Y"` -> false   — unchanged
  - never throws for any input

`IsNewer(Version? runningVersion, string? tagName)` — compare major, then minor, then patch,
returning strictly-greater at the FIRST differing level:
  - running 2.2.0 vs `"v2.2.1"` -> true
  - running 2.2.1 vs `"v2.2.1"` -> false
  - running 2.2.1 vs `"v2.2"`   -> false   (tag patch 0 is not greater than running patch 1)
  - running 2.2.9 vs `"v2.2.10"` -> true   (numeric, not lexical, at the patch level too)
  - running 2.2.10 vs `"v2.2.9"` -> false
  - running 2.2.5 vs `"v2.3"`    -> true   (a higher minor wins over a higher patch)
  - running 2.3.0 vs `"v2.2.9"`  -> false
  - running `new Version(2, 2)` vs `"v2.2"` -> false  — CRITICAL: a two-component
    `System.Version` reports `Build == -1`, and `Program.cs:299` really can produce one
    (`?? new Version(0, 0)`). Reading `Build` raw would make patch 0 look greater than -1
    and report a phantom update. Normalize with `Math.Max(runningVersion.Build, 0)`.
  - running `new Version(0, 0)` vs `"v0.0.0"` -> false — same guard, at the real fallback shape
  - running 2.2.0.0 (four components) vs `"v2.2"` -> false — the existing Anti-Pattern 4
    guard test must keep passing untouched
  - null running version, or an unparseable tag -> false, never throws

Every existing test case in both test files must keep passing; none may be deleted. The
existing `TryParseTag_ValidTag_ParsesMajorMinorAndIgnoresExtraSegments` theory gains an
`expectedPatch` parameter (and a name that no longer claims extra segments are ignored),
with `[InlineData("v2.10.5", true, 2, 10)]` becoming `[InlineData("v2.10.5", true, 2, 10, 5)]`.

New `UpdateOrchestratorTests` case (the point-release regression this change exists to fix):
persisted `SkippedUpdateVersion = "v2.2"`, feed returns a release tagged `"v2.3"`-shaped but
at `"v2.2.1"`, `honourSkippedVersion: true` -> `confirm` IS invoked (outcome `Declined` when
the fake returns `Later`). Today that release is silently suppressed, because the skip round
trip discards the patch. Mirror the existing
`CheckOnLaunchAsync_HonourSkippedVersion_LatestTagStrictlyNewerThanSkippedVersion_StillInvokesConfirm`
shape and add a sibling proving the equal case still suppresses: skipped `"v2.2.1"` against a
`"v2.2.1"` release -> `Skipped`, `confirm` never invoked.
  </behavior>

  <action>
In `src/RigToggle.Tests/UpdateVersionComparerTests.cs`, first add the cases enumerated in
`<behavior>` — extend the `IsNewer` theory with the patch-level rows, extend the
`TryParseTag` theories with the third-segment rows and the `"v2.2.x"` failure row, and add a
dedicated fact for the two-component `Build == -1` running version. Update the class doc
comment so it describes three-component ordering and names the `Build == -1` guard alongside
the existing four-component Anti-Pattern 4 guard.

Then in `src/RigToggle.Core/UpdateVersionComparer.cs`:
  (a) Add `out int patch` to `TryParseTag`, initialised to 0. After the existing major/minor
      parse succeeds, if `segments.Length >= 3` parse `segments[2]` with the identical
      `int.TryParse(..., NumberStyles.None, CultureInfo.InvariantCulture, ...)` call already
      used for the other two segments, returning false if that parse fails; if there is no
      third segment, leave patch at 0. Assign the out params only on the success path, exactly
      as the current code does.
  (b) In `IsNewer`, keep the existing major-then-minor cascade and append a patch level,
      comparing the parsed tag patch against `Math.Max(runningVersion.Build, 0)`. Keep reading
      the raw integer components off both sides directly, exactly as today — the whole reason
      this class exists is to avoid delegating ordering to `System.Version`'s own comparison
      (see the class doc comment's Anti-Pattern 4 note), and that must survive this change.
      `Revision` is never consulted.
      <!-- planner-discipline-allow: CompareTo -->
  (c) Rewrite the class doc comment. It currently asserts the tag scheme excludes a third
      segment — that claim is now false and must be corrected, not left stale. The replacement
      states: three-component vX.Y.Z going forward, two-segment historical tags (v1.0 ... v2.2)
      parsed as patch 0 for backward compatibility. PRESERVE and extend the two rationale
      paragraphs that follow: the Anti-Pattern 4 paragraph (why raw components are compared
      rather than delegating to `System.Version`) now also covers the `Build == -1` case for a
      two-component running version, and the numeric-not-lexical paragraph (PITFALLS.md
      Pitfall 3) now applies at the patch level too.
  (d) Update `TryParseTag`'s own doc comment, which currently states segments beyond the
      first two are ignored — that is now only true from the fourth segment on.

Then in `src/RigToggle.Core/UpdateOrchestrator.cs`:
  (e) Skip-tracking block (~line 198): destructure the fourth out param
      (`out int skippedPatch`) and reconstruct with the three-arg
      `new Version(skippedMajor, skippedMinor, skippedPatch)`. Without this the persisted skip
      round-trips as X.Y.0 and swallows every point release of that same minor — the exact
      T-26-14 prohibition the surrounding comment already invokes. Extend that comment to say
      the comparison is three-component.
  (f) Running-version text (~line 152): `runningVersionText` currently renders Major.Minor
      only, so a 2.2.1 build would tell the user "You're already on the latest version (v2.2)"
      (`MainForm.cs:2361`) — actively misleading during the very UAT this release enables.
      Render three components using the same `Math.Max(_runningVersion.Build, 0)`
      normalization. Update the stale comment above it, which asserts a two-component tag
      scheme. Still never `_runningVersion.ToString()`, which would render "2.2.1.0".
  (g) Leave the `IsNewer` call at ~line 158 as-is — its signature is unchanged.

Finally add the two `UpdateOrchestratorTests` cases described in `<behavior>`, reusing the
existing `FakeReleaseFeed` / `RecordingUpdateApplier` / `InMemorySettingsStore` doubles and the
`NewerRelease with { TagName = ... }` idiom already in that file. Add no mocking library.
  </action>

  <verify>
    <automated>cd /home/bpivk/moza &amp;&amp; dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj 2>&amp;1 | grep -oP 'Passed:\s+\K[0-9]+' | tail -1)" -gt 209</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(grep -v '^\s*///' src/RigToggle.Core/UpdateVersionComparer.cs | grep -c 'CompareTo')" = "0"</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(grep -c 'never three-component' src/RigToggle.Core/UpdateVersionComparer.cs)" = "0"</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; grep -q 'out int patch' src/RigToggle.Core/UpdateVersionComparer.cs</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; grep -q 'Math.Max(runningVersion.Build, 0)' src/RigToggle.Core/UpdateVersionComparer.cs</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(grep -c 'skippedPatch' src/RigToggle.Core/UpdateOrchestrator.cs)" -ge 2</automated>
  </verify>

  <done>
`dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` passes with strictly more than the
209 tests that passed before this task, zero failures. `TryParseTag` yields a patch component,
missing third segments read as 0, and `IsNewer` orders major-then-minor-then-patch off raw
integers with the `Build == -1` guard in place. The orchestrator's skip-tracking and
running-version text are both three-component. No doc comment still claims a two-component-only
scheme.
  </done>
</task>

<task type="auto">
  <name>Task 2: Bump to 2.2.1, commit, and cut the real v2.2.1 release</name>

  <files>
src/RigToggle.App/RigToggle.App.csproj
  </files>

  <precondition>
`gh auth status` reports an authenticated account with push access to `origin`, `git status
--porcelain` shows no tracked modifications other than the Task 1 files plus this csproj, and
`git ls-remote --tags origin refs/tags/v2.2.1` is empty (the tag has never been pushed).
  </precondition>

  <reversibility rating="costly">
Pushing `v2.2.1` publishes a public GitHub release. It is deletable
(`gh release delete v2.2.1 && git push origin :refs/tags/v2.2.1`) but is publicly observable
first, and re-cutting the same version number after a bad publish is poor practice — a failed
CI run should be fixed and re-tagged as `v2.2.2` rather than by force-moving `v2.2.1`.
  </reversibility>

  <action>
Change `<Version>2.2</Version>` to `<Version>2.2.1</Version>` in
`src/RigToggle.App/RigToggle.App.csproj` (line 38). Leave the explanatory comment block above
it intact except for its trailing sentence naming the checked-in value, which must now name
2.2.1 rather than 2.2. Change nothing else in that file, and change nothing in
`.github/workflows/release.yml` — its "Resolve version from tag" step already overrides this
value at publish time with the tag minus its leading v.

Re-run `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` and confirm it is still green
before committing.

Commit Task 1 and this bump. Stage explicit paths only — `.gsd/` is untracked and must not be
included:

    git add src/RigToggle.Core/UpdateVersionComparer.cs \
            src/RigToggle.Core/UpdateOrchestrator.cs \
            src/RigToggle.Tests/UpdateVersionComparerTests.cs \
            src/RigToggle.Tests/UpdateOrchestratorTests.cs \
            src/RigToggle.App/RigToggle.App.csproj
    git commit -m "feat(update): three-component semver comparison; bump to 2.2.1"

Then create the ANNOTATED tag and push it. This is a deliberate, explicitly requested action —
it is the whole point of the task, not optional cleanup, and must not be deferred or downgraded
to a lightweight tag:

    git push origin master
    git tag -a v2.2.1 -m "v2.2.1"
    git push origin v2.2.1

Push `master` BEFORE the tag so the tagged commit is reachable on the branch when the workflow
checks it out.

The tag push triggers `.github/workflows/release.yml` on `windows-latest`, which runs
`dotnet restore`, a solution-wide `dotnet build -c Release`, and a solution-wide
`dotnet test -c Release` — including `RigToggle.Windows.Tests`, which never ran locally on this
Linux host — before it publishes. Watch that run to completion:

    gh run watch --exit-status "$(gh run list --workflow=release.yml --limit 1 --json databaseId --jq '.[0].databaseId')"

If the run FAILS: do not force-move or re-push the tag. Delete the remote tag
(`git push origin :refs/tags/v2.2.1`), delete any draft release, report the CI failure with its
log excerpt, and stop — the fix belongs in a follow-up commit and a fresh tag.

On success, confirm the release carries both assets, then report the release URL so the
operator can download the exe for Phase 26 rig UAT of the auto-update path.
  </action>

  <verify>
    <automated>cd /home/bpivk/moza &amp;&amp; grep -q '&lt;Version&gt;2.2.1&lt;/Version&gt;' src/RigToggle.App/RigToggle.App.csproj</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; git status --porcelain | grep -v '^?? \.gsd/' | grep -q . &amp;&amp; exit 1 || exit 0</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(git cat-file -t v2.2.1)" = "tag"</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; git ls-remote --tags origin refs/tags/v2.2.1 | grep -q v2.2.1</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; test "$(gh release view v2.2.1 --json assets --jq '.assets | length')" = "2"</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; gh release view v2.2.1 --json assets --jq '.assets[].name' | grep -q 'RigToggle.App.exe.sha256'</automated>
  </verify>

  <done>
`src/RigToggle.App/RigToggle.App.csproj` carries `<Version>2.2.1</Version>`; the comparer +
bump are committed on `master` and pushed; `v2.2.1` is an ANNOTATED tag (`git cat-file -t`
reports `tag`, not `commit`) present on `origin`; the Release workflow run completed
successfully; and `gh release view v2.2.1` shows a published release with exactly two assets,
`RigToggle.App.exe` and `RigToggle.App.exe.sha256`. Nothing under `.gsd/` was committed.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| GitHub Releases API -> UpdateOrchestrator | Remote, attacker-influenceable tag strings and asset URLs cross into local version-comparison logic that can trigger a self-replacing binary update |
| Local tag push -> release.yml (`contents: write`) | A pushed tag grants a workflow write access to repository contents and publishes a downloadable binary |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-QT-01 | Tampering | `UpdateVersionComparer.TryParseTag` | medium | mitigate | A malformed or hostile third segment (`"v2.2.x"`, `"v2.2.-1"`) returns false rather than degrading to patch 0; `IsNewer` returns false on any parse failure, so an unparseable remote tag can never be read as newer and can never trigger an apply. Covered by the negative-tag theory rows in Task 1. |
| T-QT-02 | Tampering | `UpdateVersionComparer.IsNewer` | medium | mitigate | A comparison regression could allow a downgrade to an older build or a phantom "newer" read. Mitigated by strictly-greater-at-first-differing-level semantics plus explicit downgrade test rows (2.2.1 vs `"v2.2"`, 2.3.0 vs `"v2.2.9"`, 2.2.10 vs `"v2.2.9"`) and the `Build == -1` normalization test that closes the phantom-update path reachable from `Program.cs:299`'s `new Version(0, 0)` fallback. |
| T-QT-03 | Spoofing | Downloaded update asset | low | transfer | Asset integrity is out of scope for this change and already handled by the existing SHA256 verification path (`UpdateChecksumTests`) and the workflow's generated `.sha256` sidecar; this task neither weakens nor touches it, and Task 2's verify asserts the sidecar asset is actually published. |
| T-QT-04 | Elevation of Privilege | `release.yml` triggered by tag push | low | accept | The tag push grants `contents: write` to a workflow that publishes a binary. Accepted: the workflow file is unchanged by this task, is gated on restore/build/test passing first, and the tag is created locally by the repository owner from a verified-clean tree at a known commit. |

No package-manager installs (npm/pip/cargo) are performed by this plan, so no package
legitimacy gate applies.
</threat_model>

<verification>
1. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` passes with more than 209 tests and
   zero failures — the sole locally runnable gate on this Linux host.
2. Every pre-existing test in `UpdateVersionComparerTests.cs` and `UpdateOrchestratorTests.cs`
   still passes; none were deleted or weakened to accommodate the signature change.
3. No doc comment in `UpdateVersionComparer.cs` or `UpdateOrchestrator.cs` still describes a
   two-component-only scheme.
4. Ordering is still computed from raw parsed integers on both sides, never delegated to
   `System.Version`'s own comparison.
5. The Release workflow run for tag `v2.2.1` completed successfully on `windows-latest`,
   including the solution-wide build and test that cover the Windows-only projects.
6. `gh release view v2.2.1` shows a published release with both the exe and its sha256 sidecar.
</verification>

<success_criteria>
- `UpdateVersionComparer` parses and orders vX.Y.Z, with two-segment tags treated as patch 0.
- All historical tags (v1.0 through v2.2) compare exactly as they did before this change.
- A persisted "skip this version" no longer suppresses a later point release of the same X.Y.
- `RigToggle.App` is stamped 2.2.1 and the change is committed on `master`.
- Annotated tag `v2.2.1` exists on `origin` and its Release workflow run succeeded.
- A GitHub release `v2.2.1` is published with `RigToggle.App.exe` and `RigToggle.App.exe.sha256`,
  unblocking Phase 26's real-hardware auto-update UAT.
</success_criteria>

<output>
Create `.planning/quick/260828-vit-extend-updateversioncomparer-to-support-/260828-vit-SUMMARY.md` when done.
Include the published release URL so the operator can pull the exe onto the rig.
</output>
