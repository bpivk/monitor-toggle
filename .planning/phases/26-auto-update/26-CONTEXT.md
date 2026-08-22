# Phase 26: Auto-Update - Context

**Gathered:** 2026-08-22
**Status:** Ready for planning

<domain>
## Phase Boundary

On launch, the app checks GitHub Releases for a version newer than the running build and — if one exists — shows a confirm dialog before doing anything else. Confirming downloads the new release exe, applies it in place (rename-in-place, not overwrite), and relaunches on the new version with autostart still pointed at the correct path. A failed apply (interrupted download, disk full, locked file, or a new exe that fails to start) never leaves the app stranded — the original exe is always recoverable. A manual "Check for Updates" action triggers the same check on demand from both the tray menu and Settings, independent of the automatic on-launch check.

This phase depends on Phase 25's `--apply-update <args>` CLI bypass flag (`StartupArgs.TryGetApplyUpdateArgs`), which is already built with a placeholder body — this phase replaces that placeholder with the real update-apply logic. The flag name and parsing contract are locked and must not change.

</domain>

<decisions>
## Implementation Decisions

### Update prompt content
- **D-01:** The confirm dialog shows the new version number plus the GitHub release's Markdown body (release notes), lightly formatted (basic Markdown — headers, bullet lists, bold — parsed into a nicer read-only view, not raw text dump). This is new UI work with no existing precedent in this codebase (no Markdown renderer exists today) — plan for a small hand-rolled formatter, not a full Markdown library. — **Reversibility:** reversible — a formatting downgrade to plain text later would be a local UI change.
- **D-02:** Three buttons: **Update Now** / **Later** / **Skip this version**. "Skip this version" persists a suppressed-version marker (e.g. in settings.json) so that specific version doesn't re-prompt on future launches until a newer one ships; "Later" just closes the dialog and re-prompts next launch (and remains available via manual check at any time).
- **D-03:** Between confirming and relaunch, feedback is an indeterminate "Updating…" state (e.g. tray balloon or small status indicator) — no percentage/progress bar. The exe is small enough that a real download-progress UI wasn't judged worth the added plumbing.
- **D-04:** The dialog is a themed `Form` (constructed with `themeProvider`), following `MonitorConfirmDialog`'s existing pattern — never a bare `MessageBox.Show` (would not pick up `OverridableThemeProvider`, confirmed as Anti-Pattern 3 in `ARCHITECTURE.md`).

### Manual check UX
- **D-05:** "Check for Updates" is available from **both** the tray context menu (alongside the existing "Switch to Rig Mode" / "Settings" / "Exit" items in `trayContextMenu`) and SettingsForm. No new top-level menu bar is introduced for this — see Deferred Ideas.
- **D-06:** A manual check that finds you're already up to date reports via a tray balloon toast ("Rig Toggle: you're already on the latest version"), matching the existing `notifyIcon.ShowBalloonTip` pattern used for toggle results.
- **D-07:** A manual check that itself fails (network unreachable, GitHub API error) shows a toast with the error — explicitly **different** from the automatic on-launch check's silent no-op. Because the user explicitly asked, a manual check must not look identical to "already up to date" on failure.

### Failure visibility
- **D-08:** If download/apply fails after the user has confirmed "Update Now" (interrupted download, disk full, locked file, failed checksum — see D-11), a toast explains the failure and the app keeps running the old version normally. Never silent — the user explicitly opted in by confirming, so they should be told it didn't work.
- **D-09:** If the swap succeeds but the new exe fails/crashes on its very first startup (before reaching a confirmed-healthy state), the next launch auto-rolls back to the retained `.old` exe and shows a toast explaining the revert (e.g. "Update to vX.Y failed to start — reverted to vY.Z"). This requires a persisted "update applied, not yet confirmed" marker — deliberately parallel to this codebase's existing `ToggleInProgressMarker`/`StartupRecoveryChecker` crash-recovery pattern (`StartupRecoveryChecker.cs`) — cleared only once the new exe reaches a confirmed-running state, so an immediately-crashing update doesn't retry-loop forever. — **Reversibility:** costly — removing the confirmed-healthy marker later means re-deriving the auto-rollback trigger from scratch; keep the marker mechanism even if the UX around it changes.

### Download integrity check
- **D-10:** `release.yml` publishes a `.sha256` checksum file alongside the release exe (in addition to the existing raw-exe attachment — no change to what's already attached).
- **D-11:** The updater computes the downloaded file's SHA256 and verifies it against the published checksum **before** the rename-in-place swap touches anything. A mismatch is treated as an apply failure — same toast-and-rollback behavior as D-08, not a separate failure mode. This closes the corrupted/truncated-download gap `PITFALLS.md` Pitfall 5 flags, going beyond the bare requirements (UPDATE-01..06 don't mandate this) as a deliberate hardening step the user chose to include in this phase's scope.

### Claude's Discretion
- Exact Markdown subset supported by the lightly-formatted release-notes renderer (D-01) — headers/bullets/bold at minimum; anything beyond that (tables, links, images) is an implementation judgment call, not locked here.
- Exact wording of all toast/dialog copy (D-02, D-06, D-07, D-08, D-09) — the tone and information content are locked by the decisions above; precise phrasing is Claude's to write, consistent with the app's existing `ToggleResultFormatter`-style copy.
- What "confirmed-healthy" means for the new exe (D-09) — e.g. reaching `Application.Run`'s message loop, a short post-launch timer, or some other signal. Resolve during research/planning; the requirement is only that it's a deliberate signal, not "the process merely started."
- Persistence mechanism for the "skip this version" marker (D-02) and the "update applied, not yet confirmed" marker (D-09) — likely `settings.json` (`JsonSettingsStore` precedent) for the former and a dedicated marker file (mirroring `ToggleInProgressMarker`) for the latter, but exact shape is a planning-time decision.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### This milestone's research (v2.2) — directly covers this phase
- `.planning/research/STACK.md` §"1. GitHub-Releases Auto-Update (Self-Replacing Exe)" — `HttpClient`/`System.Text.Json`/`System.IO.File`-only recommendation, zero new NuGet packages, exact GitHub API endpoint and `User-Agent` requirement, rename-in-place mechanism, `-p:Version=` release.yml gap
- `.planning/research/ARCHITECTURE.md` — full component breakdown (`IReleaseFeed`/`GitHubReleaseFeed`, `ReleaseInfo`, `UpdateVersionComparer`, `UpdateOrchestrator`, `IUpdateApplier`/`WindowsUpdateApplier`, `UpdateApplyEntryPoint`, `UpdatePromptDialog`), Pattern 1 (startup-gate bypass, already built in Phase 25), Pattern 2 (detached-helper process-replication self-update), Pattern 3 (MSBuild size-lever switches touching this phase's `HttpClient` usage), Anti-Patterns 1–4, the full Data Flow diagrams, and the flagged open verification question about `mainForm.BeginInvoke`/`Handle` timing under `--tray` hidden startup
- `.planning/research/PITFALLS.md` — all 9 auto-update-relevant pitfalls (1–9), especially Pitfall 1 (rename not overwrite), Pitfall 2 (no version identifier exists yet — hard prerequisite), Pitfall 3 (numeric not lexical version comparison), Pitfall 4 (relaunch races the single-instance mutex — this phase must design the mutex-release-before-spawn ordering explicitly, per Phase 25's context), Pitfall 5 (partial-failure recovery — directly grounds D-09's confirmed-healthy marker), Pitfall 6 (SmartScreen/MOTW — `HttpClient` download choice is deliberate, document why), Pitfall 9 (must preserve the exact original exe path so `WindowsAutostartConfigurator`'s baked-in Run-key path never goes stale)
- `.planning/research/FEATURES.md` — MVP definition confirming all P1 auto-update items are in this phase's scope; explicitly notes the manual "Check for Updates" menu item as originally "add after validation" — this discussion has now locked it into this phase (D-05) rather than deferring it further
- `.planning/research/SUMMARY.md` — milestone-level framing

### Constraints (locked, do not re-litigate)
- `.planning/REQUIREMENTS.md` UPDATE-01 through UPDATE-06 — exact requirement text this phase satisfies (version-stamping, on-launch check, confirm-before-apply, download+apply+relaunch, never-stranded recovery, manual check parity); UPDATE-07 (single-instance-guard-does-not-block-relaunch) already complete via Phase 25
- `.planning/REQUIREMENTS.md` "Out of Scope" table — silent/background auto-update, manual-check-only (no automatic check), delta/differential updates, IL trimming/Native AOT/PublishReadyToRun for size are all explicitly excluded; do not reopen
- `.planning/ROADMAP.md` Phase 26 section — the 5 success criteria this phase must satisfy, and the dependency on Phase 25's bypass pattern
- `.planning/phases/25-single-instance-guard/25-CONTEXT.md` — D-03/D-04: the `--apply-update <args>` flag name and `StartupArgs.TryGetApplyUpdateArgs` parsing contract are one-way locked; this phase replaces the Phase 25 placeholder body, does not rename or reshape the contract

### Current code this phase touches or must follow the pattern of
- `src/RigToggle.App/Program.cs` — composition root; `StartupArgs.TryGetApplyUpdateArgs(args)` branch (Phase 25 placeholder) gets its real body here; `mainForm.BeginInvoke(UpdateOrchestrator.CheckOnLaunchAsync)` is the new on-launch check trigger point (verify `mainForm.Handle` exists under `--tray` per the ARCHITECTURE.md open question)
- `src/RigToggle.Core/StartupArgs.cs` — existing `TryGetApplyUpdateArgs` (Phase 25) and `ShouldStartHidden` — pattern to follow for any new arg parsing
- `src/RigToggle.App/MonitorConfirmDialog.cs` / `.Designer.cs` — exact pattern `UpdatePromptDialog` (D-04) must follow: themed `Form`, constructed with `themeProvider`, not `MessageBox`
- `src/RigToggle.App/MainForm.cs` / `.Designer.cs` — `trayContextMenu` (`trayToggleMenuItem`, `traySettingsMenuItem`, `trayExitMenuItem`) is where the new "Check for Updates" tray item slots in (D-05); existing `notifyIcon.ShowBalloonTip` calls (~lines 1955-1995) are the exact toast pattern D-06/D-07/D-08/D-09 reuse
- `src/RigToggle.App/StartupRecoveryChecker.cs` — existing "persist a marker before a risky operation, clear it on confirmed success, next-launch-detects-uncleared-marker" pattern (`ToggleInProgressMarker`) that D-09's update-applied-not-yet-confirmed marker directly parallels
- `src/RigToggle.Windows/WindowsAutostartConfigurator.cs` — confirms `Environment.ProcessPath` is baked into the HKCU Run key at `Enable()`-call time; grounds why rename-in-place at the identical path (not a new location) is required
- `src/RigToggle.App/RigToggle.App.csproj`, `Properties/PublishProfiles/win-x64.pubxml` — needs `<Version>` property added (currently absent entirely)
- `.github/workflows/release.yml` — needs `-p:Version=<tag-without-v>` passed to `dotnet publish`, plus the new `.sha256` checksum publish step (D-10)
- `src/RigToggle.Tests/` — home for `UpdateVersionComparer` unit tests (numeric not lexical comparison, per Pitfall 3) and any testable `UpdateOrchestrator` logic

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MonitorConfirmDialog` — exact structural precedent for the new `UpdatePromptDialog` (themed, non-MessageBox confirm dialog)
- `notifyIcon.ShowBalloonTip` + `ToggleResultFormatter`-style copy — the toast mechanism and tone every D-06/D-07/D-08/D-09 notification reuses
- `StartupRecoveryChecker` / `ToggleInProgressMarker` — the "risky-operation marker, cleared on confirmed success" pattern D-09's update-confirmation marker directly parallels
- Phase 25's `StartupArgs.TryGetApplyUpdateArgs` placeholder — this phase's real implementation target, contract already locked

### Established Patterns
- Composition-root-only construction (`Program.cs` wires real adapters; Forms never `new` a concrete adapter themselves) — any new `GitHubReleaseFeed`/`WindowsUpdateApplier` follows this
- Core/Windows/App layering: platform-neutral logic (`GitHubReleaseFeed`, `UpdateVersionComparer`, `UpdateOrchestrator`) lives in `RigToggle.Core`; OS-touching code (`WindowsUpdateApplier`, `UpdateApplyEntryPoint`) lives in `RigToggle.Windows`
- Best-effort, non-blocking startup idiom for most `Program.cs` steps — the on-launch update check must never block or delay startup, matching `RegisterHotkeyAtStartup`'s existing posture

### Integration Points
- `Program.cs Main()` — the `--apply-update` bypass branch (already gated first, before the single-instance guard, per Phase 25) and the new `mainForm.BeginInvoke(UpdateOrchestrator.CheckOnLaunchAsync)` call after tray/hotkey setup
- `MainForm` — new `trayContextMenu` item (D-05) and `SettingsForm` — new manual-check button (D-05)
- `.github/workflows/release.yml` — version-stamping and checksum-publishing additions (D-10)

</code_context>

<specifics>
## Specific Ideas

The user wants release notes shown inline in the update dialog with light Markdown formatting (not a bare version-number-only prompt, and not just a link out to GitHub) — this is a deliberate step up from the "simplest" option offered. The user also explicitly chose to add SHA256 checksum verification (D-10/D-11) even though it goes beyond what UPDATE-01..06 strictly require, treating it as worthwhile hardening rather than scope creep, since it directly closes a gap `PITFALLS.md` already flagged.

</specifics>

<deferred>
## Deferred Ideas

- **Top-level app menu bar (MenuStrip) with Help and About sections** — raised during the Manual check UX discussion ("Add a menu at the top like every other app uses. We can then also build a help section as well and about section"). This is a new UI capability (MainForm currently has no menu bar — it's a monitor-tile dashboard with a tray context menu) beyond Phase 26's auto-update scope. Redirected: "Check for Updates" placement was resolved without a new menu bar (D-05: tray menu + Settings). Candidate for a future phase or backlog item if the user wants a proper Help/About surface later.

### Reviewed Todos (not folded)
None — `todo.match-phase` returned zero matches for this phase.

</deferred>

---

*Phase: 26-auto-update*
*Context gathered: 2026-08-22*
