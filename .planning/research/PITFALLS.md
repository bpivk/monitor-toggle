# Pitfalls Research

**Domain:** GitHub-Releases auto-update, cross-process single-instance guarding, and further self-contained-exe size reduction — added to an existing tray-resident, autostart-capable, unsigned .NET 10 WinForms app (Rig Toggle v2.2)
**Researched:** 2026-08-18
**Confidence:** MEDIUM-HIGH — every pitfall is anchored either in this codebase's own current source (`Program.cs`, `ToggleOrchestrator.cs`, `WindowsAutostartConfigurator.cs`, `WindowsAppController.cs`, `StartupRecoveryChecker.cs`, `RigToggle.App.csproj`, `win-x64.pubxml`, `.github/workflows/*.yml`, all read directly 2026-08-18) or external WebSearch findings (flagged MEDIUM per-pitfall where no official Microsoft Learn source was found) — these three features are entirely new capabilities with no prior art anywhere in this project's own history, unlike v2.1's theming work which could build on Phase 12/13's rig-proven patterns.

This document is scoped tightly to what v2.2 adds. It deliberately does not restate generic "how a self-updater works" tutorials — every pitfall below is either grounded in a specific fact already established in this codebase (e.g., no `<Version>` property exists anywhere in `RigToggle.App.csproj` today, `WindowsAutostartConfigurator.Enable()` bakes `Environment.ProcessPath` into the registry Run value, `ToggleOrchestrator._busy` is an in-process-only `Interlocked` flag) or is a documented behavior of the specific Windows/.NET mechanisms this milestone must use (named `Mutex`, `HttpClient` downloads, GitHub Releases API, MSBuild publish feature switches).

## Critical Pitfalls

### Pitfall 1: The running exe cannot be overwritten in place — a naive `File.Copy`/`File.WriteAllBytes` over the current process's own exe throws or silently corrupts the install

**What goes wrong:**
Windows holds an exclusive file-system lock on the image file of any running process. `File.Copy(newExe, currentExePath, overwrite: true)` (or `HttpClient` streaming a download directly onto `Environment.ProcessPath`) throws `IOException`/`UnauthorizedAccessException` the moment it's attempted while `RigToggle.App.exe` is the process making the call — there is no code path in which a self-contained single-file exe can replace itself directly, ever, on Windows.

**Why it happens:**
This is the single most common mistake in a first-pass auto-updater implementation: the developer downloads the new exe, then reaches for the same file-write APIs already used elsewhere in this codebase (`JsonSettingsStore`, `JsonModeStore` — both write directly to their target path) without realizing the target this time is the currently-executing binary itself, which is categorically different from every other file this app writes.

**How to avoid:**
Windows *does* allow **renaming** (not overwriting) a running exe's file — the file handle stays valid at the OS level even after the path it points to changes. The standard safe sequence is: (1) download the new exe to a temp file next to the real exe; (2) rename the currently-running exe (`Environment.ProcessPath`) to a `.old` suffix; (3) move/rename the downloaded temp file into the now-vacated original path; (4) relaunch the app from that original path (Pitfall 4 covers the relaunch/handoff details); (5) on the *next* successful launch, delete the leftover `.old` file (it cannot be deleted by the process that just renamed it away from under itself, but a fresh process starting up has no lock on it). This is exactly the same "leave breadcrumb evidence for the next launch to clean up" shape this codebase already uses for `ToggleInProgressMarker` (Pitfall 5 below) — reuse that idiom rather than inventing a new one.

**Warning signs:**
Test the update-apply step specifically while the app is running from its real installed path (not `dotnet run`/debugger-attached, which behaves differently) — a rename-based implementation that was only ever tested by manually copying files around in a build script will not have exercised the actual live-process file lock at all.

**Phase to address:**
The phase implementing the auto-update apply step — should explicitly rig-verify the full rename→replace→relaunch→cleanup-.old sequence against the real installed exe path, not just a scratch/temp-folder simulation.

---

### Pitfall 2: No version identifier exists anywhere in this codebase today — "compare running version against latest GitHub tag" has nothing to compare against

**What goes wrong:**
`RigToggle.App.csproj` has no `<Version>`, `<AssemblyVersion>`, or `<FileVersion>` property (confirmed by direct read, 2026-08-18) — the compiled exe currently has no embedded version string at all beyond MSBuild's implicit `1.0.0.0` default. An auto-update phase that starts by writing "compare `Assembly.GetExecutingAssembly().GetName().Version` against the GitHub tag" will discover this comparison is meaningless: every build, regardless of which git tag it was published from, reports the same implicit `1.0.0.0`.

**Why it happens:**
Every prior milestone shipped without needing a runtime-readable version (the GitHub Release *tag* was the only version record, attached externally by `release.yml` after the fact — the exe itself never needed to know its own version). Auto-update is the first feature that requires the *running process* to know its own version, which is a new requirement this milestone introduces, not a preexisting gap that was merely unused.

**How to avoid:**
Add an explicit `<Version>` (or `<AssemblyInformationalVersion>`) property to `RigToggle.App.csproj`, and make `release.yml` set it from the pushed tag at build time (e.g., `dotnet publish -p:Version=${{ github.ref_name }}` after stripping the `v` prefix) rather than hand-maintaining it — otherwise the version baked into the exe and the tag the release workflow publishes under can drift apart, which defeats the whole comparison. Existing tags are two-component (`v1.0`, `v1.1`, `v1.2`, `v2.0`, `v2.1` — confirmed via `git tag`, not three-component semver like `v1.0.0`); whatever parsing/comparison logic is written must handle that shape without choking (`System.Version` handles 2-part versions fine; a strict-semver library that requires `major.minor.patch` may not).

**Warning signs:**
If the update-check code compiles and "runs" but always reports either "always up to date" or "always an update available" regardless of the actual latest release, this is very likely the root cause — a hardcoded/default version being compared will produce one of those two constant results, not a correct comparison.

**Phase to address:**
The phase implementing the version-check logic — should include "add and wire `<Version>` into the release pipeline" as an explicit prerequisite task, not assume it already exists.

---

### Pitfall 3: Naive string/lexical tag comparison misorders versions, and `/releases/latest` silently excludes anything marked prerelease or draft

**What goes wrong:**
Two related but distinct correctness bugs: (a) comparing tag strings lexically (`"v2.10".CompareTo("v2.9")`) rather than numerically will incorrectly conclude `v2.10` is *older* than `v2.9` the moment a double-digit minor version is ever tagged — not yet a problem at `v2.1`, but a latent bug baked in from day one that won't surface until months later, at which point it will silently stop offering real updates; (b) GitHub's `/repos/{owner}/{repo}/releases/latest` endpoint returns the most recently *published, non-prerelease, non-draft* release only — it is not "the highest semver tag," it's "whichever release GitHub currently flags as latest." If a release is ever created as a draft first (a natural workflow habit) or marked prerelease, `/latest` will silently skip it and the app will keep reporting the previous version as current, with no error to indicate why.

**Why it happens:**
Both mistakes come from treating the GitHub tag/release model as a plain semver feed rather than reading how `/releases/latest` actually resolves ("latest" is a GitHub-computed flag, not a computed max over tags) and from the reasonable-looking but wrong shortcut of comparing version *strings* instead of parsing them into comparable numeric components first.

**How to avoid:**
Strip the leading `v` and parse each tag into a `System.Version` (or equivalent numeric-component comparison) before comparing — never compare the raw tag strings. Use `/releases/latest` as the primary source (simplest, matches this project's existing `release.yml` which never marks anything prerelease/draft today) but be aware that if a future release is ever staged as a draft, the auto-updater will simply see no new release rather than erroring — document this behavior rather than treating it as a bug to be surprised by later.

**Warning signs:**
Manually verify the comparison logic against this project's own actual tag history (`v1.0` → `v1.1` → `v1.2` → `v2.0` → `v2.1`) as a unit-testable table, including at least one synthetic double-digit case (`v2.9` vs `v2.10`) even though it hasn't happened yet — this is exactly the kind of off-by-one-digit bug that has no natural trigger in today's data and will only be caught by a deliberately constructed test case, not by testing against the real release history as it stands.

**Phase to address:**
The phase implementing the version-comparison logic — should include a unit test asserting numeric (not lexical) ordering, seeded with this project's real tag history plus at least one two-digit-minor synthetic case.

---

### Pitfall 4: Relaunching the updated exe races the single-instance guard — the new process can see the old process's mutex still held and refuse to start

**What goes wrong:**
Once single-instance guarding ships (this same milestone), the update-apply sequence's final step — "relaunch the app from the new exe" — becomes a self-instance collision if not sequenced carefully: the *old* process is the one performing the rename→replace→relaunch dance, and while it's still executing that code it still holds whatever kernel object (named `Mutex`) the single-instance guard uses. If it spawns the new process *before* releasing that mutex/exiting itself, the new process's own startup-time single-instance check will see the mutex still held, correctly (from its own narrow point of view) conclude "another instance is already running," and either exit immediately or try to activate what it thinks is "the existing instance" — which is actually the old process mid-shutdown, possibly with no visible window to activate (it may be running `--tray`-hidden, or may have no MainForm shown at all). The net effect: the update silently "fails" to relaunch, or launches into a confusing activate-a-ghost-window state, even though the file-replace step itself succeeded.

**Why it happens:**
Single-instance guarding and auto-update are being added in the same milestone with no prior interaction between them to have already surfaced this — each feature, designed in isolation, has an internally consistent story ("only one process holds the mutex at a time" / "download, replace, relaunch") that only conflicts once the two are combined into one actual handoff sequence.

**How to avoid:**
Design the handoff explicitly, not incidentally: release the mutex (or exit the old process entirely) *before* — or, if the launch needs to be more robust, have the *new* process wait/retry briefly for mutex availability rather than failing on the very first check — spawning the new process. The simplest robust approach: the old process (a) performs the rename/replace, (b) starts the new process via `Process.Start(UseShellExecute = true)` — consistent with this codebase's existing, rig-proven relaunch pattern for companion-app activation (`WindowsAppController.LaunchOrFocus`, chosen specifically because raw `SetForegroundWindow`/window-handle manipulation desynced Moza's own window procedure post-H9) — and (c) *then* releases its mutex and calls `Application.Exit()` immediately after the spawn, not before. Whatever the new process's single-instance check does, it must tolerate "the previous version's process may still be in the middle of exiting" as a normal, expected condition during an update-triggered relaunch, not treat it identically to "a user genuinely double-launched the app."

**Warning signs:**
Trigger an update-apply on the real rig and watch specifically for: the app window never reappearing after "Downloading update..." (silent relaunch failure), or the toast/tray icon flickering between two processes briefly. A single manual pass that happens to run cleanly is not sufficient evidence this race doesn't exist — races are inherently timing-dependent and may pass on a fast/idle machine and fail on a loaded one.

**Phase to address:**
Whichever phase ships last of {auto-update, single-instance guard} — that phase's plan must explicitly design the mutex-release-vs-spawn ordering as a named integration step, not leave it as an incidental detail of whichever feature happens to touch `Process.Start` for the relaunch.

---

### Pitfall 5: An update that fails partway (interrupted download, disk full, AV lock on the freshly-written new exe) can leave the app unable to start at all, with no recovery path

**What goes wrong:**
The rename→replace sequence from Pitfall 1 has multiple points of partial failure: the rename to `.old` can succeed but the move-in of the new exe can fail (disk full, or — very plausible for an unsigned freshly-downloaded exe, see Pitfall 6 — a real-time antivirus scan holding a transient lock on the just-written file exactly when the move is attempted). If that happens with no rollback, the app is left with `RigToggle.App.exe.old` present (a working prior build) and no `RigToggle.App.exe` at all — the autostart Run-key entry (`"<path>\RigToggle.App.exe" --tray`, see Pitfall 8) now points at a file that doesn't exist, silently breaking autostart on every future login until the user notices and manually intervenes.

**Why it happens:**
Auto-update's own selling point — the user never has to think about it — is precisely what makes a partial failure dangerous: unlike a manual download-and-replace, where the user is present and would notice "the file didn't move," an unattended background update failure has no user watching at the moment it happens, and the app itself has no second chance to run its own recovery code (its own exe is the thing potentially missing).

**How to avoid:**
Never delete or rename-away the `.old` backup until the new exe has been confirmed to actually start successfully at least once — treat "new exe launched and reached a steady running state" as the commit point, not "the file move completed." If the move-in step itself fails, roll back immediately (rename `.old` back to the original name) rather than leaving the app in a half-updated state, mirroring this codebase's own established "stop-on-first-failure for the forward direction" convention (`ToggleService`'s documented stop-on-first-failure vs. isolate-and-continue split). Consider a persisted "update was applied, not yet confirmed" marker — deliberately parallel to `ToggleInProgressMarker`/`StartupRecoveryChecker`'s existing crash-detection pattern (save a marker before the risky operation, clear it on confirmed success, and have the *next* launch notice an uncleared marker and warn the user rather than silently retrying forever) — so a failure that happens to occur *after* the new exe launches once but crashes immediately doesn't loop silently on every subsequent autostart.

**Warning signs:**
Deliberately simulate a failed update on the rig (e.g., make the target path read-only, or kill the process mid-rename) and confirm the app is still launchable afterward — a happy-path-only test will never exercise this, since the whole point is it only matters when something goes wrong partway.

**Phase to address:**
The phase implementing the update-apply step — must budget an explicit "what if step N fails" design pass, not just a happy-path implementation, given this app's existing precedent (`ToggleInProgressMarker`) of treating "what if this specific operation is interrupted mid-flight" as a first-class design question, not an edge case to patch later.

---

### Pitfall 6: An unsigned, freshly-downloaded exe with no prior reputation may be flagged by SmartScreen/Defender on the very first relaunch after update — and this app has no code-signing certificate to prevent it

**What goes wrong:**
Windows Defender SmartScreen's "app reputation" check specifically gates off the Mark-of-the-Web (Zone.Identifier NTFS alternate-data-stream) tag that browsers and some download APIs apply to files fetched from the internet — if the auto-updater downloads the new exe via a mechanism that *does* apply MOTW (e.g., BITS, `URLDownloadToFile`, or anything routing through the Attachment Execution Services), first execution of the newly-placed exe can trigger a "Windows protected your PC" SmartScreen block, requiring a "More info → Run anyway" click the user won't be present for during an unattended background relaunch — silently stalling the update at the exact moment it should be invisible. Conversely, a plain `HttpClient`/`System.Net.Http` download does *not* automatically apply Zone.Identifier the way a browser does (MEDIUM confidence, WebSearch-sourced) — which sidesteps the SmartScreen prompt but is worth knowing is *why* it's sidestepped, not an accident, since a future change to the download mechanism (switching to a different HTTP client, or piping through a shell command) could reintroduce it unexpectedly.

**Why it happens:**
This project has never shipped a code-signing certificate (confirmed — no Authenticode signing step exists anywhere in `release.yml`/`build.yml`, and CLAUDE.md's stack research explicitly frames the project as unsigned) — every prior release was manually downloaded and run by the same single user who already trusts their own GitHub repo, so this risk was latent but never actually exercised: a human clicking through a SmartScreen prompt once, deliberately, is a completely different failure mode from an *unattended* background update silently blocking on the same prompt with nobody there to click through it.

**How to avoid:**
Do not introduce a download mechanism that applies Zone.Identifier/MOTW to the downloaded exe (a plain `HttpClient.GetByteArrayAsync`/`GetStreamAsync` into a local file write is the simplest choice and, per the above, avoids this by default) — but be aware this is also the reason Windows' own defense-in-depth exists, and is not a substitute for signing if this project is ever distributed beyond the single current user. Given the project's explicit personal-single-user scope (per PROJECT.md constraints), accept this as a known, documented limitation rather than attempting to work around SmartScreen — but do surface a clear in-app error state (not a silent hang) if the relaunch step ever does get blocked, so the user isn't left staring at a vanished app with no explanation.

**Warning signs:**
On the real rig, after an update-apply, confirm the new exe actually launches without any OS-level interstitial dialog appearing — if a "Windows protected your PC" screen ever does appear during what should be a silent, unattended relaunch, that is this pitfall manifesting, not a bug in the app's own code.

**Phase to address:**
The phase implementing the download step — should explicitly note (in code comments, mirroring this codebase's existing convention of documenting *why* a specific API/approach was chosen, e.g. `WindowsAutostartConfigurator`'s doc comments) which download API was chosen and that MOTW-avoidance was a deliberate, verified consequence of that choice, not an assumption.

---

### Pitfall 7: Reusing `ToggleOrchestrator`'s in-process `_busy` flag mentality for cross-process single-instance detection — the two "already in progress" concepts are not the same mechanism

**What goes wrong:**
This codebase already has a well-established, rig-proven "reject a concurrent duplicate" pattern: `ToggleOrchestrator._busy`, a non-blocking `Interlocked.CompareExchange` int field, explicitly documented as deliberately *not* a cross-thread blocking lock and *not* a queue. It would be an easy, superficially reasonable mistake to reach for "the same kind of guard" for single-instance detection — but `_busy` is process-local memory; it has zero visibility into a second, entirely separate OS process launching a second copy of the exe. A second launched process starts with its own fresh `_busy = 0` and has no way to observe the first process's state via that field at all. Single-instance detection is a fundamentally different problem (cross-process, needs a kernel-level shared object) from reentrancy guarding (same-process, needs only a shared memory flag) — conflating them, even briefly during design, wastes a design pass on a mechanism that cannot work for the new requirement.

**Why it happens:**
The existing codebase's own Key Decisions table explicitly frames `_busy` as "the single shared flag," which is exactly the kind of established, trusted, already-tested primitive a developer would reasonably reach for first when a new "prevent a duplicate" requirement appears — without pausing to notice the new requirement crosses a process boundary the old one never did.

**How to avoid:**
Use a named `System.Threading.Mutex` (a genuine Win32 kernel object, visible across processes on the same login session) as the single-instance primitive — this is an entirely new, additional mechanism, not an extension of `ToggleOrchestrator`. Do not attempt to make `_busy` (or any `Interlocked`/in-memory field) do double duty for this. Keep the two guards conceptually and structurally separate in the codebase, matching this project's own established preference (per the Key Decisions table: the reentrancy guard was deliberately built as a new `ToggleOrchestrator` wrapper "not logic inside `ToggleService`," specifically to keep unrelated concerns from being tangled into one mechanism) — apply that same separation-of-concerns instinct here.

**Warning signs:**
If an early design draft proposes checking `ToggleOrchestrator._busy` (or any field on an already-constructed, in-process object) as part of "is another instance already running," that is the tell this pitfall is being walked into — the check has to happen before *any* of this process's own objects (which by definition only exist in this process) are constructed, ideally as close to the top of `Main()` as possible, before `ApplicationConfiguration.Initialize()`.

**Phase to address:**
The phase implementing the single-instance guard — should explicitly state, as a design note, that this is a new cross-process primitive unrelated to the existing reentrancy guard, to preempt this exact reach-for-the-familiar-tool mistake.

---

### Pitfall 8: `Mutex.CreateNew` succeeding doesn't guarantee the "loser" process can actually signal the winner — the activation handoff needs its own explicit mechanism and has its own race window

**What goes wrong:**
A named `Mutex`'s atomic "did I create this or did it already exist" check (`new Mutex(true, name, out bool createdNew)`) correctly and reliably answers "am I the first instance" with no race condition in the check itself — but that check alone does nothing to bring the *existing* instance's window to the foreground; it only tells the second process "someone else already holds this." A separate signal (a named pipe message, `WM_COPYDATA`/`RegisterWindowMessage` broadcast via `PostMessage`/`SendMessage`, or similar) is required to actually ask the first process to activate itself — and *that* handoff has its own, different race: if the "loser" process sends its activation signal before the "winner" process has finished setting up whatever it's listening on (e.g., a named pipe server not yet started, or a message-only window not yet created), the signal is silently lost — the second process exits cleanly (its job was just to signal and exit), but nothing visibly happens, and the user is left thinking their second launch attempt did nothing at all, with no error and no window ever appearing.

**Why it happens:**
It's easy to treat "detect I'm not first" and "make the first instance visible" as one solved problem once the Mutex check is written, because the Mutex check itself is the hard-looking, canonical-tutorial part — the signal-delivery half is comparatively unglamorous and easy to under-design as "just send a message," without considering that the receiver might not be ready yet, especially right at app startup (which is exactly when a second, near-simultaneous launch is most likely to occur — e.g., a user double-clicking impatiently, or an autostart entry plus a manual launch racing at login).

**How to avoid:**
Make the "existing instance" side create its signal-receiving mechanism (named pipe server / message-only window) as early as possible in startup — ideally immediately after winning the Mutex race, before any other startup work (settings load, mode-store bootstrap, etc.) — and give the "second instance" side a short retry-with-backoff window (a few attempts over ~1-2 seconds, not a single immediate try) rather than a single fire-and-forget signal attempt, so a winner that's still mid-startup has a chance to become ready before the loser gives up.

**Warning signs:**
Launch the app twice in extremely rapid succession (scripted, not manual double-click, to reliably produce a tight race) and confirm the existing window activates every time, not just most of the time — an intermittent failure here is exactly the signature of this race, and a single manual test is very likely to miss it since manual double-clicks rarely land inside the narrow startup window where the receiver isn't ready yet.

**Phase to address:**
The phase implementing the single-instance guard — should explicitly test rapid repeated launches (scripted loop, not one-off manual clicks) as part of its own verification, not just "launch twice, second one focuses the first."

---

### Pitfall 9: Auto-update relaunches into a different exe path than autostart's registry Run-key entry expects, silently breaking `--tray` autostart

**What goes wrong:**
`WindowsAutostartConfigurator.Enable()` (confirmed by direct read) resolves the exe path via `Environment.ProcessPath` *at the moment the user enables autostart in Settings* and bakes it verbatim into the HKCU `Run` registry value as `"<path>" --tray`. It is never automatically re-written except when the user re-visits Settings and toggles autostart again. If the update-apply mechanism ever places the new exe at a *different* path than the original (e.g., a version-numbered subfolder, or a "download to a new location and update the Run key" design instead of the in-place rename-and-replace from Pitfall 1), the registry Run entry silently goes stale — Windows will still try to launch the old path on next login, find nothing there (Pitfall 5's broken-autostart scenario), and the user gets no error at all, just a rig that no longer comes up in tray mode on boot.

**Why it happens:**
This is a subtle interaction the auto-update phase's own design might not surface unless it specifically cross-checks against `WindowsAutostartConfigurator`'s actual behavior — the two features (autostart, shipped in v1.1; auto-update, shipping now) were built more than a full milestone apart with no reason for the earlier one's implementer to have anticipated the later one's constraints.

**How to avoid:**
The update-apply mechanism must preserve the exact original exe path — this is the strongest argument for the rename-in-place pattern from Pitfall 1 over any design that downloads to a new location and leaves the old one behind, since rename-in-place is the only approach that guarantees the final path is bit-for-bit identical to what autostart already points at, requiring zero registry changes as a side effect of updating.

**Warning signs:**
After an update-apply, reboot (or log off/on) and confirm the app still autostarts in `--tray` mode — a same-session verification (just checking the app still runs after update) will not catch this, since the stale Run-key path only matters on the *next* cold boot/login, not the currently-running session.

**Phase to address:**
The phase implementing the update-apply step — should include a rig-verify step that specifically covers a reboot after an update, not just "the app relaunches immediately after downloading," since autostart correctness only shows up a full boot cycle later.

---

### Pitfall 10: Reaching for Native AOT or partial/selective IL trimming as a "smarter" size-reduction lever reproduces — or worsens — the exact COM/P-Invoke breakage `PublishTrimmed=false` was chosen to avoid

**What goes wrong:**
This project's own `win-x64.pubxml` and Key Decisions table already document, explicitly and at length, why `PublishTrimmed` is `false`: IL trimming's static reachability analysis misidentifies COM-interop (`IPolicyConfig` audio) and P/Invoke marshalling (`WindowsDisplayAPI`/CCD) code as unreachable and strips it, breaking those calls at runtime in exactly the two subsystems this app cannot function without. A size-reduction pass searching for "genuinely new levers" (since the four MSBuild-only wins from v2.0 are already spent) may reasonably arrive at either (a) `PublishReadyToRunComposite`/partial trimming ("only trim the safe assemblies, root the risky ones") or (b) Native AOT (`PublishAot=true`) as the next escalation — both are worse choices than the plain full trimming that was already rejected, not better ones: partial/selective trimming still runs the same static-analysis engine against whichever assemblies aren't explicitly rooted, with the exact same false-unreachable failure mode, just with a smaller (and easier to get subtly wrong) blast radius; and Native AOT's compilation model does not support COM interop at all (confirmed by WebSearch, MEDIUM confidence) and has essentially no realistic path to running a WinForms app that also does COM audio interop — it is strictly more restrictive than the trimming this project has already rig-disproven as safe for this codebase, not a workaround for it.

**Why it happens:**
Both options are widely recommended in generic ".NET app size reduction" guidance found via web search, and both genuinely do produce large size wins for apps that don't share this project's specific COM/P-Invoke-heavy surface — the mistake is applying general-purpose advice without re-deriving it against this specific codebase's already-documented, hard-won constraint.

**How to avoid:**
Treat "no IL trimming, full stop — including partial/selective trimming and Native AOT" as the standing constraint for this milestone's size-reduction work, not just "no full `PublishTrimmed=true`." Safe, additive levers that don't touch the trimmer at all include: `<DebugType>none</DebugType>` (drop PDB generation from the publish output entirely — a straightforward win with no COM/P-Invoke interaction), disabling individual small runtime feature switches that don't require static reachability analysis of this app's own interop code (e.g., `EventSourceSupport=false` to drop ETW provider support this personal single-user tool doesn't use), and auditing whether `IncludeNativeLibrariesForSelfExtract`/compression settings have any further headroom. Explicitly do NOT enable `PublishReadyToRun` while hunting for size wins — it is documented to *increase* single-file size roughly 2-3x (it's a startup-time lever, not a size lever) and would actively work against PERF-01's goal if a developer conflates "publish optimization flags" broadly and enables it by mistake while tuning something else.

**Warning signs:**
Any size-reduction proposal that mentions "trim," "AOT," or "ReadyToRun" in the same breath as this milestone should be treated as a red flag requiring explicit justification against the existing documented rejection — the acceptance bar should be "why is this different from the trimming we already rejected," not "does it reduce size."

**Phase to address:**
The phase implementing further size reduction — should open by re-stating the existing trimming rejection rationale as a hard constraint (not just implicitly inheriting it), specifically to preempt "AOT/partial-trim" being proposed as if it were a fresh idea unrelated to the prior decision.

---

### Pitfall 11: `UseSystemResourceKeys=true` (a real, legitimate size lever) silently degrades this app's own user-facing error/toast text into unreadable resource-key strings

**What goes wrong:**
`UseSystemResourceKeys` is a genuine, trimming-unrelated MSBuild size lever (confirmed by WebSearch — used by Blazor's own size-sensitive SDK defaults) that strips human-readable BCL exception message text and replaces it with bare resource-key identifiers (e.g., a framework exception's `.Message` becomes something like `"CultureInfoConverterDefaultCultureString"` instead of an actual sentence). This app already surfaces real exception-derived text to the user in multiple places established across prior milestones — `ToggleResultFormatter`'s toast/status text, `MessageBox`-based startup dialogs, and the opt-in `debug.log` trace output — several of which rely on *framework-thrown* exceptions (registry access failures, file I/O errors, `Process.Start` failures) surfacing readable `.Message` text, not just this app's own hand-written exception messages (which are unaffected by this switch since they're plain string literals, not resource-keyed). Enabling this switch as a size win would quietly turn some fraction of those already-shipped, already-verified error surfaces into cryptic garbage the single end user (who is also the sole person who has to debug their own rig from that text) can no longer act on.

**Why it happens:**
The switch's actual scope (BCL/framework exception messages only, not app-authored ones) is easy to misjudge without testing — it looks like a clean, mechanical, low-risk size win in isolation, but this app specifically has built its whole error-surfacing design (the `Skipped`/`NotAttempted`/`Failed` distinct-outcome work from v2.0, the debug.log opt-in) around exception text being genuinely readable.

**How to avoid:**
If this lever is used at all, explicitly test every user-facing error path that can originate from a framework/BCL exception (not just this app's own thrown exceptions) after enabling it, and compare the before/after text — do not assume "it only affects size, not behavior" without that check. Given this app's error-surfacing design was a deliberate, hard-won piece of work across two prior milestones, the safer default is to leave this switch off unless the measured size win is large enough to justify a real user-facing regression review, not to enable it reflexively alongside the other, behavior-neutral levers.

**Warning signs:**
After enabling, deliberately trigger a framework-originated failure this app already has an established path for (e.g., an autostart registry-key I/O failure, or a removed audio device) and read the resulting toast/MessageBox/debug.log text — if it now contains a bare `PascalCaseIdentifier`-looking string instead of a sentence, this pitfall has landed.

**Phase to address:**
The phase implementing further size reduction — if `UseSystemResourceKeys` is on the candidate list at all, it should carry its own explicit "compare user-facing error text before/after" verification step, separate from the generic "did the exe get smaller" check the other levers only need.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|--------------------|-----------------|------------------|
| Skipping the `.old`-backup-retained-until-confirmed-launch step in the update-apply sequence (Pitfall 5) | Simpler, more linear update code | A single interrupted or immediately-crashing update permanently breaks the app with no rollback, on an unattended background operation nobody is watching | Never — this is exactly the class of "unattended, nobody watching" operation this project's own `ToggleInProgressMarker` precedent already treats as needing a recovery story |
| Reusing `ToggleOrchestrator._busy` (or any in-process field) as part of single-instance detection (Pitfall 7) | Feels like reusing proven, already-tested infrastructure | Cannot work at all — it's not a shortcut with a cost, it's a design that silently doesn't do what's intended, discoverable only by actually launching two processes | Never |
| A single immediate activation-signal attempt with no retry (Pitfall 8) | Simpler signal-and-exit code for the "loser" process | Intermittent, hard-to-reproduce "second launch does nothing visible" reports that only manifest under rapid-relaunch timing | Never as shipped state — retry-with-backoff is a small addition once designed for |
| Enabling `UseSystemResourceKeys` without auditing this app's own established exception-surfacing paths (Pitfall 11) | An easy extra few KB/size-percentage win | Silently degrades error/toast text this app's users (the single dev-user) already rely on to debug their own rig | Only after an explicit before/after text-readability check; never enabled reflexively alongside the trimming-unrelated levers |
| Downloading the update to a new install location instead of rename-in-place at the original path (Pitfall 9) | Slightly simpler "just write the new file somewhere clean" logic | Silently breaks the autostart Run-key path, discoverable only a full reboot later | Never — rename-in-place at the identical path is a small amount of extra care that entirely avoids this class of bug |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|------------------|-------------------|
| GitHub Releases API (`/releases/latest`) | Comparing tag strings lexically instead of parsing numeric components; assuming `/latest` means "highest semver tag" rather than "GitHub's currently-flagged latest, non-prerelease, non-draft" release | Parse tags into `System.Version` after stripping the `v` prefix; treat `/latest`'s draft/prerelease exclusion as documented behavior, not a bug (Pitfall 3) |
| `release.yml` / running-exe version | Assuming the compiled exe already knows its own version (it doesn't — no `<Version>` property exists today) | Add `<Version>` to `RigToggle.App.csproj`, driven from the pushed tag in `release.yml`, before any comparison logic is written (Pitfall 2) |
| Named `Mutex` (single-instance) + `ToggleOrchestrator` | Treating the existing in-process `_busy` reentrancy flag as reusable for cross-process detection | Build single-instance detection as an entirely separate, new mechanism (named `Mutex`), never layered onto `_busy` (Pitfall 7) |
| Auto-update relaunch + single-instance guard | Spawning the new-version process before the old process has released its mutex/exited | Explicitly sequence: spawn new process via `Process.Start(UseShellExecute=true)`, then release mutex/`Application.Exit()` on the old process immediately after — and make the new process's own instance check tolerate a still-exiting predecessor (Pitfall 4) |
| Auto-update relaunch + `WindowsAutostartConfigurator`'s baked-in `Environment.ProcessPath` | Placing the updated exe at any path other than the exact original path | Rename-in-place at the identical path (Pitfall 1's mechanism) so the existing Run-key entry never needs to change (Pitfall 9) |
| `HttpClient` download + Windows SmartScreen/Mark-of-the-Web | Assuming any downloaded exe automatically triggers (or automatically avoids) SmartScreen without checking which download API was actually used | Confirm the chosen download mechanism's MOTW behavior deliberately, and document the choice inline (Pitfall 6) |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|-----------------|
| An unauthenticated GitHub API check on every single launch during active development/debugging iteration | Update-check failures start appearing that are actually rate-limit (403) responses, easily misdiagnosed as "the update check is broken" | Distinguish a rate-limit response from a genuine "no update"/network-failure response in logging, and consider a minimum re-check interval (e.g., not more than once per few hours) rather than unconditionally on every launch, even though a single personal user's normal usage pattern is very unlikely to exceed the 60/hour unauthenticated limit through legitimate app launches alone | Only realistically surfaces during rapid manual restart-testing/debugging of the update-check code itself, not normal end-user usage — but exactly the phase implementing this feature is when that rapid-restart pattern is most likely to happen |
| `EnableCompressionInSingleFile` (already enabled since v2.0) interacting with a growing exe from any new size-adjacent work this milestone touches | Cold-start time creeping up again without anyone noticing, since PERF-02's existing rig-verified cold-boot check was for the v2.0 baseline, not necessarily re-run after v2.2's changes | Re-run a cold autostart boot check on the real rig after this milestone's size-reduction changes, mirroring the existing PERF-02 precedent, rather than assuming "smaller file always means faster/neutral startup" | Would only be caught by an explicit cold-boot rig check, not a build-output size diff alone — the same lesson PERF-01/02 already established in v2.0 |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Downloading the update asset over plain HTTP, or without validating the response actually came from `api.github.com`/`github.com`/`objects.githubusercontent.com` release-asset redirect targets | An unsigned exe with no integrity check beyond "the download succeeded" is the single highest-value tampering target in this whole feature — a MITM'd or compromised download silently becomes the next thing that autostarts on every login | Always use HTTPS (GitHub's API and release asset CDN are HTTPS-only by default, so this mainly means never manually downgrading); given there is no code-signing certificate and no published checksum today, treat the current trust model as "HTTPS + GitHub's own hosting integrity" only, and flag that as an explicit, accepted, documented limitation for this personal single-user tool rather than an oversight |
| No published checksum/hash alongside the release asset (`release.yml` attaches only the raw exe today, confirmed by direct read) | A corrupted or truncated partial download could be applied as though it were a valid update, with failure only surfacing later (crash on next launch) rather than being caught before the risky rename/replace step | Consider having `release.yml` also publish a `.sha256` (or similar) alongside the exe, and have the updater verify it before touching the live installation — a small addition to the existing pipeline that meaningfully reduces the blast radius of Pitfall 5's partial-failure scenario |
| Treating the single-instance guard's activation signal (Pitfall 8) as implicitly trusted because "only this app would ever connect to it" | A named pipe or message-only window with a predictable/discoverable name is, in principle, addressable by any other local process on the same session — for a single-user personal tool this is a low-severity concern, but an activation signal that executes arbitrary logic (rather than just "show the window") on receipt would be worth a moment's thought | Keep whatever the activation handler does on receipt limited to "make the existing window visible/foreground" — do not extend it later into a general-purpose IPC command channel without reconsidering trust boundaries at that point |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-------------------|
| Update check silently blocks/delays app startup while waiting on a network call | A tray-resident, autostart-capable tool whose whole value proposition is "starts fast, sits quietly" gains a new, unpredictable network-dependent delay on every login | Perform the update check asynchronously, after the app is already usable (tray icon present, hotkey registered) — never gate `Application.Run`/the tray-ready state on the network round-trip completing, matching this app's existing best-effort/non-blocking startup idiom for everything except the two deliberate exceptions (mode-corruption, crash-recovery dialogs) |
| "An update is available" prompt appears with no visible difference from a routine toast, or interrupts an in-progress toggle | User dismisses it reflexively without registering it, or worse, it appears mid-toggle and creates a confusing "is this part of what I just did" moment | Make the update-available prompt visually and contextually distinct from `ToggleResultFormatter`'s existing toast styling, and avoid surfacing it while `ToggleOrchestrator` reports a toggle in flight |
| Second-launch activation silently does nothing when the existing instance is currently hidden to tray (`--tray` autostart running quietly) | User double-clicks the exe (forgetting it's already running in tray), sees nothing happen, and concludes the app is broken or that their click didn't register | Decide explicitly what "activate" means when the existing instance is tray-only: showing the main window is the more helpful, discoverable behavior (it's literally what the user just asked for by launching it again) — don't silently no-op just because the existing instance happens to currently be hidden |

## "Looks Done But Isn't" Checklist

- [ ] **Update-apply sequence:** Often only tested against a scratch/temp folder, never against the real installed exe path while it's the actively running process — verify the full rename→replace→relaunch→`.old`-cleanup sequence on the real rig install location (Pitfall 1).
- [ ] **Version comparison logic:** Often "works" against today's real tag history by coincidence (no double-digit minor version has happened yet) while still containing a lexical-string-comparison bug that will misfire the first time it does — verify with an explicit synthetic double-digit test case, not just today's actual tags (Pitfall 3).
- [ ] **Single-instance activation:** Often works on a single manual double-click test while still containing a signal-delivery race that only shows up under rapid/scripted repeated launches — verify with a scripted rapid-relaunch loop, not one manual click (Pitfall 8).
- [ ] **Autostart after update:** Often verified only by "the app relaunched right after downloading the update," never by an actual reboot/login cycle afterward — verify autostart survives a real reboot post-update, not just the immediate in-session relaunch (Pitfall 9).
- [ ] **Partial update-failure recovery:** Often has zero test coverage since the happy path is what gets exercised during normal development — deliberately simulate an interrupted update (read-only target path, killed mid-rename) and confirm the app is still launchable afterward (Pitfall 5).
- [ ] **Exe size reduction:** Often measured only as "did the file get smaller," never re-checked against the existing PERF-02 cold-boot rig precedent or against this app's own established error-text readability (Pitfalls 10, 11) — verify both a cold-boot timing check and a framework-exception-text readability spot-check, not file size alone.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|-----------------|-------------------|
| Update applied but new exe fails to start (Pitfall 5) | LOW-MEDIUM | Rename `.old` back to the live path (the backup was never deleted per the recommended design) — recoverable entirely from files already on disk, no network needed, as long as the "keep `.old` until confirmed" discipline was actually followed |
| Relaunch race against single-instance guard (Pitfall 4) leaves no window visible after update | LOW | Manually launch the exe again — the single-instance guard's own activation path (once its own race, Pitfall 8, is fixed) should surface the already-running instance; this is self-healing on the very next launch attempt, not a persistent broken state |
| Autostart Run-key pointing at a stale/wrong path post-update (Pitfall 9) | LOW | Re-open Settings and re-toggle the autostart checkbox off/on — `WindowsAutostartConfigurator.Enable()` unconditionally re-writes the value from the current `Environment.ProcessPath` on every call, so this is a one-click fix once the user notices, even without a code change |
| Single-instance mutex accidentally shared with a mechanism reused from `_busy` (Pitfall 7) discovered late | MEDIUM | Introduce the correct named-`Mutex`-based mechanism as a genuinely separate addition; does not require touching `ToggleOrchestrator`/`_busy` at all, since the fix is additive, not a modification of the existing reentrancy guard |
| `UseSystemResourceKeys` shipped and later found to have degraded user-facing error text (Pitfall 11) | LOW | Remove the single MSBuild property; no code changes needed elsewhere since the affected text was always framework-generated, not hand-authored |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|--------------------|----------------|
| Cannot overwrite the running exe directly | Auto-update apply phase | Rig-verify the full rename→replace→relaunch→cleanup sequence against the real installed path, process actually running from it |
| No embedded version identifier exists today | Auto-update version-check phase (prerequisite task) | Confirm `<Version>` is set in `RigToggle.App.csproj` and driven from `release.yml`'s tag, not hand-maintained |
| Lexical tag comparison / `/releases/latest` draft-prerelease exclusion | Auto-update version-check phase | Unit test with numeric-ordering assertions, including a synthetic double-digit-minor case |
| Relaunch races the single-instance mutex | Whichever of {auto-update, single-instance} phase ships second | Explicit mutex-release-before/around-spawn ordering design; rig-tested update-triggered relaunch, not just a manual double-launch |
| Partial update failure leaves app unlaunchable | Auto-update apply phase | Deliberately simulated interrupted-update rig test (read-only path, killed mid-rename) |
| Unsigned exe / SmartScreen on unattended relaunch | Auto-update download-mechanism phase | Rig-verify no OS-level interstitial appears during an unattended post-update relaunch |
| `_busy` reused for cross-process detection | Single-instance guard phase | Explicit design note distinguishing the two mechanisms; two-process launch test proving the new Mutex-based check, not `_busy`, is what's being read |
| Activation-signal race (winner not ready yet) | Single-instance guard phase | Scripted rapid-repeated-launch test, not a single manual double-click |
| Autostart Run-key path staleness after update | Auto-update apply phase | Rig-verify autostart survives a real reboot/login cycle post-update, not just the immediate relaunch |
| Native AOT / partial trimming reached for as a size lever | Size-reduction phase | Explicit re-statement of the existing PublishTrimmed=false rationale as a standing constraint at the start of the phase's plan |
| `UseSystemResourceKeys` degrading user-facing error text | Size-reduction phase | Before/after readability spot-check on at least one framework-originated exception's user-facing surfacing (toast, MessageBox, or debug.log) |

## Sources

- `/home/bpivk/moza/src/RigToggle.App/Program.cs` — read directly (2026-08-18) to confirm the composition-root startup sequence, the best-effort-swallow idiom for most startup side effects, and the two deliberate blocking exceptions (mode-corruption, crash-recovery) — HIGH confidence, primary source
- `/home/bpivk/moza/src/RigToggle.Core/ToggleOrchestrator.cs` — read directly to confirm `_busy`'s exact `Interlocked.CompareExchange` mechanism and its documented in-process-only, non-blocking, single-shared-flag design — HIGH confidence, grounds Pitfall 7
- `/home/bpivk/moza/src/RigToggle.Windows/WindowsAutostartConfigurator.cs` — read directly to confirm `Environment.ProcessPath` is baked into the HKCU Run key at `Enable()`-call time and never auto-rewritten — HIGH confidence, grounds Pitfall 9
- `/home/bpivk/moza/src/RigToggle.Windows/WindowsAppController.cs` — read directly to confirm the existing rig-proven `Process.Start(UseShellExecute=true)` relaunch pattern (chosen over raw window-handle manipulation post-H9) that Pitfall 4's recommended relaunch mechanism reuses — HIGH confidence
- `/home/bpivk/moza/src/RigToggle.App/StartupRecoveryChecker.cs` — read directly to confirm the existing "persist a marker before a risky operation, clear it before surfacing, next-launch-detects-uncleared-marker" pattern (`ToggleInProgressMarker`) that Pitfall 5's recommended recovery design reuses — HIGH confidence
- `/home/bpivk/moza/src/RigToggle.App/RigToggle.App.csproj`, `/home/bpivk/moza/src/RigToggle.App/Properties/PublishProfiles/win-x64.pubxml` — read directly to confirm no `<Version>` property exists today (Pitfall 2) and to confirm the exact documented rationale for `PublishTrimmed=false` this document's Pitfall 10 extends to Native AOT/partial trimming — HIGH confidence
- `/home/bpivk/moza/.github/workflows/release.yml`, `/home/bpivk/moza/.github/workflows/build.yml` — read directly to confirm the release pipeline attaches only a raw exe with no checksum and no code-signing step — HIGH confidence, grounds the Security Mistakes table
- `git tag` output in this repo (`v1.0`, `v1.1`, `v1.2`, `v2.0`, `v2.1`) — read directly, confirms the two-component (not three-component semver) tag shape referenced in Pitfall 2/3 — HIGH confidence
- `/home/bpivk/moza/.planning/PROJECT.md` — Constraints and milestone-context sections confirming the unsigned/no-Authenticode-certificate status and the pre-existing `.lnk`-path process-name-matching limitation the milestone context asked to be checked against — HIGH confidence
- .NET self-contained single-file exe self-update rename/replace pattern, helper-process approaches — WebSearch, MEDIUM confidence (community sources: andreasrohner.at, GitHub issue discussions; no single authoritative Microsoft Learn page addresses self-update specifically since it is inherently an application-level concern, not a documented platform feature) — grounds Pitfall 1
- Windows Mark-of-the-Web / Zone.Identifier / SmartScreen app-reputation behavior — WebSearch, MEDIUM confidence (textslashplain.com, cybertrainer.uk, MITRE ATT&CK T1553.005; no direct Microsoft Learn page fetched confirming whether `HttpClient` downloads apply MOTW by default) — grounds Pitfall 6, flagged for rig verification rather than treated as settled
- GitHub REST API `/releases/latest` semantics (excludes draft/prerelease) and unauthenticated rate limits (60/hour) — WebSearch, MEDIUM confidence for the rate-limit figure (docs.github.com result present but not directly fetched/read in full) — grounds Pitfall 3 and the Performance Traps table
- Named `Mutex` single-instance patterns, `AbandonedMutexException` semantics, Mutex+named-pipe/WM_COPYDATA activation-handoff pattern — WebSearch, MEDIUM confidence (dotnet-guide.com, autoitconsulting.com, CodeProject; core `AbandonedMutexException`/`ReleaseMutex`-thread-ownership semantics cross-checked against Microsoft Learn API reference pages, HIGH confidence for that specific sub-claim) — grounds Pitfalls 7 and 8
- WinForms + Native AOT COM-interop incompatibility — WebSearch, MEDIUM confidence (codevision.medium.com, Microsoft Learn Native AOT overview page surfaced but not fully fetched) — grounds Pitfall 10's Native AOT rejection
- `UseSystemResourceKeys` size/readability tradeoff — WebSearch, MEDIUM confidence (dotnet/runtime and dotnet/android GitHub issue discussions; no single canonical Microsoft Learn page found describing this specific switch's UX tradeoff) — grounds Pitfall 11
- `ReadyToRun` size-vs-startup-time tradeoff (~2-3x size increase) — WebSearch, MEDIUM confidence (jonathancrozier.com, dev.to sources; general direction cross-checked as plausible against Microsoft Learn's ReadyToRun overview page title surfaced in results, not fully fetched) — grounds Pitfall 10's explicit rejection of `PublishReadyToRun` as a size lever

---
*Pitfalls research for: GitHub-release auto-update, single-instance guarding, and further self-contained-exe size reduction (Rig Toggle v2.2)*
*Researched: 2026-08-18*
