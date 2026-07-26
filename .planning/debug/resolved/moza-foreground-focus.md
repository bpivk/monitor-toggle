---
status: resolved
trigger: "Moza Companion window doesn't come to the foreground when toggling to Rig Mode if the app was already running — it stays minimized/in the taskbar instead of opening. Fresh launches work fine; only the 'already running, bring to focus' path is broken. This matches a known issue already logged in STATE.md from Phase 4 rig testing (hypothesis: SetForegroundWindow's Win32 restriction fails silently when the calling process isn't itself the foreground process)."
created: 2026-07-25T20:00:00Z
updated: 2026-07-26T16:00:00Z
---

## Symptoms

expected: Clicking "Switch to Rig Mode" brings the already-running Moza Companion window to the foreground (visible, focused).
actual: The window stays minimized/in the taskbar — it never appears. No error is thrown; the toggle otherwise reports success (App: Succeeded per the CORE-04 checklist, since `LaunchOrFocus` doesn't verify the focus call's outcome).
errors: None observed — fails silently.
timeline: First noticed during Phase 4 Plan 03 rig testing (2026-07-24), logged as a pending todo in STATE.md, deferred as out-of-phase-scope at the time. Never fixed. APP-01/APP-02 (launch/focus) were validated in Phase 3, but that validation apparently didn't catch this specific "already running, bring to focus" case reliably.
reproduction: 1) Launch Moza Companion manually (or via any prior rig-mode toggle) so it's running with a window. 2) From the Rig Toggle app, click "Switch to Rig Mode". 3) Observe: process is confirmed running (IsRunning check passes), but its window does not come to the foreground — stays in the taskbar.

## Prior Context

- `src/RigToggle.Windows/WindowsAppController.cs` — `LaunchOrFocus`'s "already running" branch calls `SetForegroundWindow` per D-06 (03-CONTEXT.md). Win32's `SetForegroundWindow` has a well-known OS-level restriction: it silently fails (returns FALSE, no exception) when the calling process is not itself the current foreground process, unless specific conditions are met (e.g. `AllowSetForegroundWindow`, matching input-processing thread via `AttachThreadInput`, or the calling process owns the foreground lock via a recent user input event).
- STATE.md's existing "Pending Todos" note (now superseded by this debug session): "Moza Companion window sometimes doesn't come to the foreground after toggling to Rig Mode, even though the process is confirmed running... Plausibly Win32's SetForegroundWindow restriction (fails silently when the calling process isn't itself in the foreground)."
- This is a Windows-only bug; this sandbox has no Windows runtime, so root-cause confirmation and fix verification are gated on the user testing on the actual rig.

## Current Focus

status_update_6 (FINAL, session closed 2026-07-26T16:00:00Z): Checkpoint response
  received and accepted. H7 (original bug -- wrong/no window focused on the
  already-running path) and H10 (app would never relaunch after being fully closed)
  are FINAL CONFIRMED FIXED -- user verified both hold up in normal day-to-day rig
  use, not just the rig-test moments captured during investigation. H9 (close/X
  button inert on RigToggle-focused Moza windows) is ACCEPTED as a documented,
  Moza-side, not-fixable-from-RigToggle known limitation per reasoning_checkpoint_12
  (investigated to the practical limit of passive Win32 diagnostics available to a
  separate process across three eliminated mechanisms: WS_DISABLED/H8, system-menu
  MF_GRAYED/H9b, FormClosing-visible-revert/H9-original). No further action pending.
  Session archived: status=resolved, file moved to .planning/debug/resolved/.
  next_action: NONE -- session closed. Code changes for H7+H10 (and H9's
  diagnostic-only instrumentation) remain uncommitted in the working tree for the
  user to review/commit directly, per this project's git safety protocol.

status_update_5: TENTH round. The requested closeGrayed= rig-test data arrived and
  FALSIFIES H9b per reasoning_checkpoint_10's own pre-registered falsification test
  (closeGrayed=False both before and after FocusWindow) -- see Eliminated. This
  exhausts every Win32-state mechanism (WS_DISABLED / H8, system-menu MF_GRAYED / H9b,
  FormClosing-visible-revert / H9-original) that this codebase can test via read-only
  P/Invoke queries from RigToggle's own process. Per this round's instructions,
  evaluated whether any further RigToggle-side diagnostic could still usefully
  discriminate H9's remaining mechanism -- concluded NO productive one exists (see
  reasoning_checkpoint_12) -- and concluded no defensive code mitigation is warranted
  (no evidence implicates any of RigToggle's own P/Invoke calls; the remaining
  candidate mechanism, Moza subclassing WM_NCHITTEST/WM_NCLBUTTONDOWN/WM_SYSCOMMAND in
  its own window procedure, is upstream of anything RigToggle's calls could touch). H9
  is therefore documented as a known, likely-unfixable-from-RigToggle Moza-side
  limitation rather than chased further. Given H7 (original bug) and H10 (relaunch
  after full close) are both confirmed fixed and rig-verified, and H9 has reached the
  practical limit of what is diagnosable/actionable from RigToggle's side, this session
  is moved to a human-verify/decision checkpoint to close out rather than continuing
  indefinitely -- see CHECKPOINT REACHED return and Resolution below. The zombie
  \bin\-process accumulation (now 5, up by 1 again this round) remains a documented,
  non-blocking, Moza-internal-architecture informational note (reasoning_checkpoint_11),
  not re-opened as a live thread.

reasoning_checkpoint_12:
  hypothesis: "No further RigToggle-side diagnostic can usefully discriminate H9's
    remaining candidate mechanism (Moza subclassing WM_NCHITTEST/WM_NCLBUTTONDOWN or
    filtering WM_SYSCOMMAND in its own window procedure) with a favorable
    risk/actionability tradeoff -- the investigation has reached the practical limit of
    what is diagnosable from RigToggle's own process without hooking Moza's message
    loop."
  confirming_evidence:
    - "Every read-only Win32 query available to a separate process (IsWindowEnabled,
      GetMenuState via GetSystemMenu, IsWindowVisible/IsIconic, GetForegroundWindow) has
      now been tried against the target window across H8/H9b and all read back
      'normal'/unremarkable values both before and after RigToggle's FocusWindow
      sequence -- the interception (if the WM_NCHITTEST/WM_SYSCOMMAND subclassing theory
      is correct) happens inside Moza's own window procedure, upstream of any state a
      passive external query can observe."
    - "The one remaining theoretically possible diagnostic considered -- having
      RigToggle itself PostMessage/SendMessage a synthetic WM_SYSCOMMAND(SC_CLOSE) to
      the window and observe whether that succeeds where a real physical X-click does
      not -- would discriminate 'input delivery is intercepted before WM_SYSCOMMAND'
      from 'WM_SYSCOMMAND is delivered but swallowed internally', but even a positive
      result would not yield an actionable fix: RigToggle has no legitimate place in its
      own UI/workflow to offer a 'close Moza for you' action (its core value per
      CLAUDE.md is monitor/audio/launch toggling, not managing a third-party app's
      window lifecycle), and unlike every other diagnostic used this session, injecting
      a synthetic close command is not a passive read -- it actively attempts to
      close/hide a window RigToggle does not own, with unknown Moza-side side effects,
      for a result that would only satisfy curiosity about mechanism, not enable a fix.
      This fails the same 'evidence must lead to an actionable decision' bar every other
      diagnostic this session met."
    - "A true fix for a WM_NCHITTEST/WM_SYSCOMMAND-level subclass would require
      injecting into or hooking Moza's own message loop (e.g. a WH_CALLWNDPROC/
      SetWindowsHookEx hook in Moza's process, or a raw window-subclass via
      SetWindowSubclass on a window RigToggle does not own) -- categorically different
      in kind from every technique used successfully this session (FindBestMainWindow,
      ShowWindow/SetForegroundWindow, read-only state queries), carries real stability/
      security risk (injecting code into another vendor's process), and is well outside
      the Windows-utility, standalone-.exe, Win32-API-surface scope CLAUDE.md/STACK.md
      define for this project."
  falsification_test: "If the user reports a change in Moza's own version (e.g. a Moza
    Companion update) that alters this close-button behavior, or reports the close
    button starts working normally even after RigToggle-initiated focus, that would
    indicate the mechanism was Moza-side and version-specific -- worth re-opening a new,
    separate debug session at that point rather than reviving this one, since the
    underlying code (RigToggle's) would be unchanged."
  fix_rationale: "No fix applied and none proposed -- this is the explicit conclusion
    that further investigation has reached diminishing/negative returns: continuing to
    add diagnostic instrumentation for a mechanism this session's own evidence
    increasingly points to being outside RigToggle's process boundary would violate the
    'don't guess without an actionable payoff' discipline this session has followed
    since H7. Documenting as a known limitation (see Resolution) is the correct action,
    not a further code change."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. It remains possible
    (though newly unlikely, given three independent Win32-state mechanisms have now been
    ruled out) that some other passively-observable Win32 state this session has not yet
    considered explains the symptom -- if the user or a future session identifies one, it
    should be tested before accepting the WM_NCHITTEST/WM_SYSCOMMAND-subclass theory as
    final. This session's own confidence in that specific final mechanism is moderate
    (reasoned by elimination, not directly observed), not high."

next_action: Session moved to a human-verify/decision checkpoint (see CHECKPOINT
  REACHED return) rather than continuing to chase H9 indefinitely -- awaiting the
  user's response on (1) confirming H7 and H10 continue to work in normal day-to-day
  use (not just the specific rig-test moments captured in this file so far), and (2)
  whether to accept H9 (close button on a RigToggle-focused Moza window) as a
  documented, Moza-side, not-fixable-from-RigToggle known limitation and archive this
  session, or continue investigating (with the caveat, stated in
  reasoning_checkpoint_12, that no further low-risk/actionable RigToggle-side
  diagnostic has been identified). If the user confirms archival, proceed to
  archive_session (status -> resolved, move file to .planning/debug/resolved/, commit
  code changes for H7 + H10, and record H9 as a documented known limitation rather than
  a resolved bug).

status_update_4: NINTH round. Both outstanding questions from the eighth round's
  checkpoint were answered: (1) H9 visual-reaction = "completely inert" (zero reaction),
  (2) Task Manager Details-tab data for all "MOZA Pit House" processes (5 total: 1
  Running root-folder exe, 1 Running \bin\-folder exe, 3 Suspended \bin\-folder exe).
  Two threads addressed this round: (A) the "completely inert" answer, applied against
  reasoning_checkpoint_7's own pre-registered falsification test, ELIMINATES H9 as
  originally framed (FormClosing-cancel-and-hide -- that mechanism would make the window
  visibly Hide()/vanish, not stay open and totally unresponsive) and produces a refined,
  more specific hypothesis, H9b (system-menu SC_CLOSE command disabled) -- see
  reasoning_checkpoint_10. Diagnostic-only instrumentation (GetSystemMenu/GetMenuState,
  zero behavior change) was added to test it directly on the next rig test, following
  this session's established evidence-before-fix discipline; no EnableMenuItem fix was
  applied without confirming evidence first. (B) The orchestrator-suggested "RigToggle
  launches the wrong (\bin\) exe directly, causing suspended zombies" theory was
  evaluated against existing evidence and judged NOT actionable without guessing: (i) the
  actual configured CompanionAppPath value is unknown from this sandbox and was not
  requested as blocking, because (ii) H10's own confirmed rig-test evidence (Evidence
  2026-07-26T13:00:00Z) already shows Process.Start(companionAppPath) -- whatever that
  configured path currently is -- reliably produces the correct, fully working, correctly
  titled dashboard window ('MOZA Pit House 1.3.9.35 release', 1456x849, successfully
  focused) on at least two separate confirmed occasions this session. This directly
  contradicts the premise that the configured launch target is broken or produces a
  non-functional child process, so no fix was applied for it. See reasoning_checkpoint_11
  for the full reasoning and why the suspended \bin\ processes are judged most likely a
  Moza-internal architecture detail (a Qt-based multi-process helper/worker pattern,
  consistent with the already-confirmed QTrayIconMessageWindow class-name evidence)
  outside RigToggle's control or knowledge, not a RigToggle-caused leak -- RigToggle's
  code only ever issues a single Process.Start call per launch, targeting exactly the one
  user-configured path, and never itself spawns or references the \bin\ exe as a distinct
  entity. (3) The "Task Manager said zero" (prior round) vs "Task Manager now shows 5"
  (this round) discrepancy is reconciled as ordinary timing/visibility -- consistent with,
  not contradicting, reasoning_checkpoint_9's prior conclusion (no code-level status-
  display bug; both readings are explained by the same persistent-background-process
  structural fact plus Task Manager's default view not surfacing windowless processes
  clearly).

reasoning_checkpoint_11:
  hypothesis: "The orchestrator's suggested root cause -- RigToggle's Process.Start call
    targets the \\bin\\ subfolder 'MOZA Pit House.exe' directly (bypassing the root
    supervisor exe), and the \\bin\\ exe is designed to only function correctly as a
    child spawned-and-resumed by the root supervisor -- does NOT hold up against this
    session's own confirmed evidence and should NOT be acted on."
  confirming_evidence:
    - "Evidence 2026-07-26T13:00:00Z (already in this file, re-examined this round): the
      H10 bounded-poll-then-fresh-launch fallback fired at 13:56:27.076 and by
      13:56:41.211-41.248 FindBestMainWindow found a real, titled, correctly-sized window
      (hWnd=0x2113EC, title='MOZA Pit House 1.3.9.35 release', normalRect=1456x849,
      area=1236144, iconic=False, enabled=True) belonging to whichever process
      Process.Start(companionAppPath) had just created -- i.e. the SAME single
      Process.Start call this round's orchestrator theory says might be targeting a
      non-functional child-only binary in fact produced the fully correct, working main
      dashboard. A binary that 'only runs correctly as a child spawned-and-resumed by a
      supervisor' would not be expected to independently boot a complete, correctly
      titled, interactive UI window when launched directly and unsupervised by
      Process.Start -- this observed outcome is inconsistent with that specific failure
      mode."
    - "Code re-read (WindowsAppController.cs LaunchFreshAndFocus, lines 194-217, this
      round): confirmed Process.Start(companionAppPath) is called with ONLY the
      configured path as the target -- no arguments, no working-directory override, no
      reference anywhere in this codebase to a '\\bin\\' path or any path transformation.
      RigToggle launches exactly and only whatever single .exe path the user configured
      via SettingsForm's OpenFileDialog (src/RigToggle.App/SettingsForm.cs
      BtnBrowse_Click) -- it has no code-level concept of 'root vs bin' exe at all."
    - "The actual configured CompanionAppPath value (which of the two physically distinct
      files the user picked) is not available from this Linux sandbox and was not
      requested this round -- per the reasoning above, even knowing it would not change
      the conclusion, since the observed outcome (a correctly working dashboard,
      confirmed twice: the H4-round 'closed -> toggle Rig -> works' test AND this
      session's H10 fallback test) already rules out the 'wrong/non-functional launch
      target' failure mode regardless of which specific path is configured."
  falsification_test: "If a future rig test shows Process.Start(companionAppPath) itself
    failing to produce a working window within the existing 10s LaunchPollTimeout (not
    the already-explained, already-fixed H10 already-running-branch case, but the
    baseline fresh-launch path), OR if the user reports the freshly-launched window is
    itself a secondary/incomplete window (mirroring the already-eliminated H6/H7 wrong-
    window symptom pattern) rather than the real dashboard, this conclusion would be
    falsified and the launch-target/CompanionAppPath value would need to be obtained and
    investigated directly."
  fix_rationale: "No fix applied -- this is a case where a plausible-sounding external
    theory is directly contradicted by this session's own already-gathered evidence
    rather than merely unconfirmed. Applying a 'launch the root exe instead' fix without
    any evidence of an actual problem would itself violate this session's established
    evidence-before-fix discipline (changing which binary gets launched is not a
    zero-risk change -- it could alter real, working behavior based on a theory the
    session's own data argues against)."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. Has not independently
    confirmed the actual CompanionAppPath value or resolved exactly why 3 \\bin\\-folder
    processes are Suspended with 0 K working set (Windows trims the working set of
    long-idle background/suspended processes toward zero over time -- a well-documented,
    generic OS behavior -- which is consistent with, but does not by itself prove, a
    legitimate Moza-internal helper/worker-pool pattern rather than some other
    explanation). This is treated as an informational, non-blocking finding: it does not
    block or change the fix for either the original bug (H7, confirmed working) or H9/H9b
    (in progress) -- it is not re-opened as a live hypothesis unless the falsification_test
    above is triggered."

reasoning_checkpoint_10:
  hypothesis: "H9b (supersedes H9's specific FormClosing-cancel-and-hide framing): the
    window's SYSTEM menu has its Close (SC_CLOSE) command disabled via
    EnableMenuItem(hMenu, SC_CLOSE, MF_GRAYED) -- a well-known technique, orthogonal to
    both WS_DISABLED (already eliminated) and FormClosing/Hide() logic, that makes
    DefWindowProc silently drop WM_SYSCOMMAND(SC_CLOSE) before it ever reaches the
    window's own message handling or triggers any repaint/hide/close sequence. All three
    observed close-request paths (title-bar X via WM_NCLBUTTONDOWN -> SC_CLOSE, Alt+F4 ->
    SC_CLOSE, taskbar 'Close window' -> SC_CLOSE) converge on this one gated command,
    while Minimize (SC_MINIMIZE, a structurally distinct command ID on the same system
    menu) is completely unaffected -- this reproduces every confirmed observation
    (minimize works, all three close mechanisms fail identically, zero visual reaction)
    more precisely than H9's original framing, which required a visible Hide()-then-
    revert or at minimum a FormClosing-triggered repaint that 'completely inert' rules
    out."
  confirming_evidence:
    - "Checkpoint response, this round (Q1, H9 visual-reaction, previously unanswered
      after two rounds of asking): 'completely inert' -- zero visible reaction of any
      kind on X-click. Per reasoning_checkpoint_7's own pre-registered falsification_test
      branch (b), this specifically weakens H9's FormClosing-runs-then-reverts mechanism
      (which implies SOME visible attempt/revert) in favor of an even earlier
      interception -- i.e., exactly what a disabled system-menu command produces
      (filtered by DefWindowProc before any window-level handling or repaint occurs)."
    - "A cancelled FormClosing (e.Cancel=true, the common WinForms tray-app pattern)
      would still typically result in the app's own Hide()-to-tray call firing as the
      handler's alternative action -- producing a visible disappearance, not an
      unresponsive-but-still-fully-visible window. 'Stays open, X does nothing at all'
      does not match a working (even if surprising) hide-to-tray outcome; it matches an
      input being silently dropped before any app-level code runs at all."
    - "Already-confirmed prior evidence (this file, Eliminated section) rules out
      WS_DISABLED (would also block Minimize, contradicted by 'you can minimize but not
      close') and the residual non-Torque-Curve WS_DISABLED variant of H8 (enabled=True
      confirmed both before and after FocusWindow in the eighth round's log) -- a
      disabled system-menu command is a structurally distinct, previously-unconsidered
      Win32 mechanism that is not excluded by either of those eliminations, since
      IsWindowEnabled/WS_DISABLED and a system menu's per-item MF_GRAYED state are
      orthogonal Win32 concepts (confirmed via Win32 documentation -- EnableWindow and
      EnableMenuItem operate on entirely separate state)."
  falsification_test: "On the next rig test (repro: toggle to rig with the app already
    running/tray-hidden; window opens; click X; observe it does nothing as before;
    capture and share the fresh debug.log, which now includes a new 'closeGrayed=' field
    in FocusWindow's before/after log lines): if closeGrayed=False (or null, meaning no
    system menu was found) both before and after FocusWindow's sequence, H9b is
    FALSIFIED -- the true mechanism would be neither WS_DISABLED nor a disabled
    system-menu command, and would most likely point to Moza subclassing
    WM_NCHITTEST/WM_NCLBUTTONDOWN directly (an even earlier, message-level interception
    this codebase cannot detect via any read-only Win32 query, only inferred by
    elimination) -- likely unfixable from RigToggle in that case. If closeGrayed=True is
    observed (either already before FocusWindow runs, meaning Moza itself disabled Close
    independently of RigToggle's calls -- or, less expected, only becomes True after
    FocusWindow's sequence runs, which would newly implicate one of RigToggle's own P/Invoke
    calls and require identifying which one), H9b is confirmed, and the fix direction
    (a defensive EnableMenuItem(hMenu, SC_CLOSE, MF_ENABLED) call at the end of
    FocusWindow, mirroring the same low-risk, purely-additive shape as H8's
    never-needed EnableWindow(hWnd, TRUE) candidate) can be applied with actual evidence."
  fix_rationale: "No behavior-changing fix applied yet -- deliberately withheld again,
    consistent with this session's established discipline (the one fix that worked, H7,
    was preceded by evidence-gathering; guessing a fix for H9/H9b without direct evidence
    would be an eighth blind guess in a session where multiple earlier blind guesses
    already failed). Added ONLY diagnostic, read-only instrumentation (GetSystemMenu +
    GetMenuState, logged via a new closeGrayed= field in FocusWindow's existing
    before/after Log() calls) with zero behavior change -- this cannot regress the
    already-confirmed-working H7 fix or the already-confirmed-working H10 fix, and
    directly targets this round's specific falsification test."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox -- H9b is reasoned
    from a well-documented, common Win32 technique (EnableMenuItem/MF_GRAYED on
    SC_CLOSE) and the newly-narrowed 'completely inert' symptom, not from Moza's actual
    (closed) source. If Moza's close-to-tray logic uses some other mechanism entirely
    that also produces zero visual reaction (e.g. subclassing WM_NCHITTEST to make the
    close button itself report HTNOWHERE, or filtering WM_SYSCOMMAND in its own window
    procedure before calling DefWindowProc, without ever touching the system menu's
    grayed state), the closeGrayed= reading would come back False/null and this
    hypothesis would be falsified even though 'one shared close-path interception'
    would still be the best-supported general conclusion. This is why the falsification
    test explicitly separates 'H9b confirmed' from 'H9b falsified but the general
    shared-interception pattern still stands' rather than treating a null/False reading
    as proof of nothing being wrong."

next_action: Two open items remain before this session can be archived, both requiring a
  fresh rig test with the newly-added diagnostic field:
  (1) H9b system-menu-close-grayed check (reasoning_checkpoint_10): the next time the
  close (X) button fails on a RigToggle-focused Moza window, capture and share a fresh
  debug.log covering that FocusWindow call -- the new 'closeGrayed=' field in both the
  before and after log lines directly discriminates H9b (confirmed if True) from an even
  earlier Moza-side interception outside RigToggle's detection ability (if False/null).
  (2) No further action needed on the CompanionAppPath/launch-target theory
  (reasoning_checkpoint_11) -- treated as resolved-by-existing-evidence (no fix
  warranted), not blocking archival. If the user wants to report the suspended \\bin\\
  helper processes to Moza's own support/vendor as a possible resource-cleanup issue on
  their end, that's a reasonable independent action, but it is outside RigToggle's code
  and not something to fix here absent new evidence per reasoning_checkpoint_11's
  falsification_test.

status_update_3: EIGHTH round. H10's fix mechanism is CONFIRMED WORKING on rig test (see
  Evidence 2026-07-26T13:00:00Z) -- the bounded poll + fresh-launch fallback fired exactly
  as designed and a new UI window was reachable afterward. The user's separately-reported
  "Rig toggle still says moza is running but task manager says it's not" complaint was
  investigated by reading MainForm.RefreshUi() and IsRunning() (both re-read this round,
  see Evidence) -- it is judged to be the SAME confirmed structural fact (a persistent,
  windowless, same-named "MOZA Pit House" process keeps IsRunning() == true) presented
  from the status-label angle rather than a new, distinct code bug. This is NOT treated as
  a re-opening of H10 or a new hypothesis (H11) requiring a fix -- no code-level mechanism
  exists for RigToggle to report stale/cached state (IsRunning is called fresh via
  Process.GetProcessesByName every time, confirmed via code re-read, no caching anywhere),
  and no code fix has been applied for it this round, per this session's established
  discipline of not guessing without discriminating evidence. Separately, the enabled=True
  (before AND after FocusWindow) reading for window 0x2113EC in this round's log is
  sufficient, per reasoning_checkpoint_7's own stated falsification_test, to formally
  eliminate the residual non-Torque-Curve WS_DISABLED variant of H8 (see Eliminated below)
  -- H9 (Moza-side WM_CLOSE-handler no-op) is now the sole remaining live hypothesis for
  the close-button regression, but the visual-reaction (flicker vs fully inert) question
  needed to fully confirm H9's specific mechanism per branch (b)/(c) of that same
  falsification_test is STILL not answered this round.

reasoning_checkpoint_9:
  hypothesis: "The 'still says running / Task Manager says not' complaint and H10 share
    the exact same root cause (a persistent, windowless, same-named 'MOZA Pit House'
    process) -- it is not a distinct status-display bug (e.g. stale cached state, a UI
    refresh gap, or a different process-matching code path)."
  confirming_evidence:
    - "Re-read src/RigToggle.App/MainForm.cs RefreshUi() (lines 51-63, this round):
      companion status label is derived by calling _appController.IsRunning(settings.
      CompanionAppPath) directly, every time RefreshUi() runs (OnLoad + after every
      toggle + after Settings dialog close) -- no caching, no stored boolean field, no
      timer-based staleness."
    - "Re-read src/RigToggle.Windows/WindowsAppController.cs IsRunning() (lines 55-85,
      this round): calls Process.GetProcessesByName(processName) fresh on every
      invocation, returns processes.Length > 0, disposes all Process handles in a
      finally block. Identical process-matching mechanism (same processName derivation,
      same GetProcessesByName call) as LaunchOrFocus's already-running branch -- there is
      no separate/divergent code path for the status label vs the toggle logic that could
      disagree with each other."
    - "This round's own debug.log excerpt independently reconfirms the persistent-helper
      structural fact first established in the prior round's evidence: PID=14736 recurs
      identically across this round's snapshots too (13:56:23.960 branch AND the fresh
      4-process branch at 13:57:21.202), consistent with a long-lived, same-named,
      windowless helper process that would make IsRunning() return true continuously
      regardless of whether the real UI-bearing instance is present."
  falsification_test: "If the user confirms (via the requested Task Manager Details-tab
    check, see next_action) that literally zero processes with any 'MOZA'-related image
    name exist at the exact moment RigToggle's status label reads 'Running', this
    hypothesis is falsified -- that would mean IsRunning()/Process.GetProcessesByName is
    somehow matching a process Task Manager cannot show at all (not just a windowless one
    the user overlooked), which would point to a genuinely different bug (e.g. a
    zombie/defunct process handle, or GetProcessesByName matching something unexpected)
    requiring separate investigation."
  fix_rationale: "No fix applied -- this round's evidence (fresh-every-time IsRunning(),
    identical matching mechanism to the already-confirmed H10 persistent-helper fact) does
    not point to a code-level status-display bug requiring correction. If the persistent
    headless helper is confirmed (via the requested Command Line / Image Path check) to be
    a legitimate, harmless Moza background component, the current 'Running' label is
    arguably even correct in a literal sense (a Moza process IS running) even though it is
    surprising to the user glancing at Task Manager's default Apps view, which does not
    clearly surface windowless background processes under a recognizable 'Moza' entry --
    in that case this may not even be a bug to fix, only a UX clarity question (e.g.
    whether 'Running' should mean 'has a real window' vs 'any matching process exists'),
    which is a product decision, not something to guess a code fix for without the user's
    input once the process identity is confirmed."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. Has not confirmed
    whether the persistent helper process(es) are a legitimate Moza background service
    (harmless) or some other artifact (e.g. a zombie process from a prior toggle's
    LaunchFreshAndFocus that never becomes a real UI instance and never exits) -- the
    requested Task Manager Command Line / Image Path check (next_action) is required to
    resolve this and to inform whether IsRunning()/LaunchOrFocus's process-name matching
    should eventually be narrowed (e.g. by command-line filtering) to exclude the headless
    role, which would also change what 'Running' means for the status label. Not
    resolving this now is a deliberate choice to avoid guessing a fix for a question that
    may not even require one."

next_action: Two open items remain before this session can be archived, both requiring
  user-supplied evidence (no further code changes proposed this round):
  (1) H9 close-button visual-reaction question (repeated from reasoning_checkpoint_7,
  still unanswered after two rounds): the next time the close (X) button fails on a
  RigToggle-focused Moza window, report whether there is ANY brief visual flicker/flash
  before it settles back to fully visible, or whether the click produces zero visible
  reaction at all (fully inert) -- this discriminates reasoning_checkpoint_7's branch (b)
  [no reaction -> likely an even-earlier input interception, e.g. Moza subclassing
  WM_NCHITTEST/WM_NCLBUTTONDOWN for its own close button] from branch (c) [brief
  flicker -> supports H9's specific FormClosing-attempts-then-reverts mechanism]. A fresh
  debug.log covering that close attempt (even though RigToggle does not itself log
  Moza's close handling, the surrounding FocusWindow before/after lines provide useful
  context) should be captured alongside the answer.
  (2) Task Manager Details-tab check for the persistent headless "MOZA Pit House"
  process(es): right-click the Details tab column header, add "Command line" and "Image
  path name" columns, and report both values for (a) a windowless MOZA Pit House process
  (visible during the ~3s bounded-poll window right after clicking "Switch to Rig Mode"
  with Moza already closed, or any time the RigToggle status label says "Running" while
  no Moza window is visible) and (b) the UI-bearing MOZA Pit House process once a window
  is open, so the two can be compared. This determines whether the persistent process is
  a genuinely separate executable, a background/service role of the same executable
  (e.g. invoked with a hidden command-line flag), or something else -- and informs
  whether IsRunning()/LaunchOrFocus's process matching should eventually be narrowed to
  exclude it. Do NOT apply a code change for the "status still says running" complaint
  until this data is available -- current evidence (reasoning_checkpoint_9) does not
  support a code-level bug there.

status_update_2: NEW DISTINCT THREAD, NOT a subsumption of H9 -- the seventh checkpoint
  response's report ("the app does not start if fully closed... Rig toggle app shows
  moza companion running even though it's not") describes a DIFFERENT failure mechanism
  than H9 (close-button no-op on an already-opened window). H9 requires a window to have
  been successfully opened first (the close handler runs on a real, visible dashboard);
  this new report (H10) is about LaunchOrFocus never opening any window at all once the
  real UI instance has been fully closed, because a same-named headless helper process
  keeps IsRunning() == true and the already-running branch's "no window found -> silent
  no-op forever" path (D-06) has no fallback to Process.Start. These are not the same
  bug and one does not explain the other -- both remain open, tracked separately (H9,
  H10). H10 is judged HIGHER PRIORITY for this round: it fully breaks the core "launch"
  functionality (APP-01/APP-02) any time the app has been genuinely closed, which is far
  more severe than H9's "close button is inert" (app is still usable via minimize /
  Task Manager kill in that case). Unlike H9 (where the fix direction is genuinely
  ambiguous between a Moza-side-only quirk and a residual RigToggle-side cause, and no
  low-risk fix candidate exists), H10 has a directly-confirmed code-level gap
  (LaunchOrFocus's already-running branch has NO path that ever calls Process.Start,
  confirmed via code re-read) with a clear, low-risk, bounded fix (poll briefly for a
  window across all matched processes; if genuinely none appears, fall back to a fresh
  Process.Start instead of no-op'ing forever) that does not depend on resolving the
  still-open question of exactly why/what the persisting same-named processes are. See
  reasoning_checkpoint_8 below for the mandatory pre-fix checkpoint. next_action updated
  to reflect the H10 fix being applied and requesting rig verification for BOTH H10 (new
  fix) and H9 (still needs the previously-requested debug.log + visual-reaction answer)
  in the same round, since both are now open threads awaiting the same rig-test cycle.

reasoning_checkpoint_8:
  hypothesis: "H10: LaunchOrFocus's already-running branch has no code path that ever
    calls Process.Start -- when Process.GetProcessesByName matches one or more
    processes by exact name but FindBestMainWindow (which scans regardless of
    visibility, unlike the old MainWindowHandle heuristic D-06 was written against)
    finds ZERO top-level windows across ALL of them, the method silently returns having
    done nothing, forever, on every subsequent toggle -- because at least one
    same-named process (a headless helper/service, e.g. PID=14736, confirmed present in
    two log snapshots 10 minutes apart spanning both an 'app open' and an 'app fully
    closed' state) keeps IsRunning() == true even when the real UI-bearing instance has
    fully exited, which is exactly what the user's report ('the app does not start if
    fully closed... rig toggle app shows moza companion running even though it's not')
    describes."
  confirming_evidence:
    - "Verbatim debug.log excerpt (this round): 3 processes matched while a real window
      was open and successfully focused (PID=14936, hWnd=0xA0FEE); 10 minutes later,
      after the user closed the app via its own UI, 2 processes still matched by the
      exact same name, BOTH with FindBestMainWindow => 0x0 (zero windows of any kind,
      not merely hidden ones, since the scan is visibility-independent)."
    - "PID=14736 is IDENTICAL across both the 'app open' (13:25:15) and 'app closed'
      (13:35:46) snapshots, 10 minutes apart -- rules out 'still in the middle of
      exiting' (which would resolve in milliseconds-to-seconds, not persist 10+
      minutes) as its explanation; far better explained by a persistent, same-named
      background/helper process that Moza itself keeps running independent of whether
      its main UI window is open."
    - "Code re-read (WindowsAppController.cs LaunchOrFocus, lines 100-147, this round):
      confirmed the already-running branch's foreach loop has no statement anywhere
      that calls Process.Start -- if hWnd stays IntPtr.Zero for every matched process,
      the loop completes and the method returns, unconditionally, with zero recovery
      action of any kind."
    - "D-06 (03-CONTEXT.md, re-read this round) was authored 2026-07-24, before H4/H5/H7
      replaced the visibility-gated Process.MainWindowHandle lookup with
      FindBestMainWindow's visibility-independent EnumWindows-by-PID scan -- D-06's
      'hWnd==Zero means genuinely tray-only, retrying is pointless' rationale no longer
      matches what a Zero result from FindBestMainWindow can mean today (a real
      tray-hidden window is now found, not missed) -- so a Zero result today is a
      narrower, more specific, more actionable signal than the one D-06 was written
      about."
  falsification_test: "If, after this fix (bounded poll for a window across all matched
    processes, falling back to Process.Start if none appears), the rig test still shows
    'app does not start after being fully closed', this hypothesis is falsified as a
    COMPLETE explanation -- the true remaining blocker would then be something else
    (e.g. Moza enforcing genuine single-instance behavior that makes a second
    Process.Start silently no-op or fail when a same-named helper is still alive, which
    would need a different, non-Process.Start recovery mechanism, or the persisting
    helper process is not actually harmless to relaunch against)."
  fix_rationale: "Add a short, bounded poll (mirroring the existing fresh-launch poll
    pattern but much shorter -- ~3s / LaunchPollInterval cadence) inside the
    already-running branch: recheck FindBestMainWindow across all currently-matched
    processes a few times before concluding no window will ever appear. This guards
    against a genuine startup race (real UI process just started, window not created
    yet) being misdiagnosed as 'headless forever' and triggering an unnecessary
    duplicate launch. If the poll times out with zero windows found across every
    matched process, fall through to the SAME Process.Start + poll path already used
    for the 'not running' case -- this directly closes the confirmed code gap (no
    recovery path exists today) with the minimum change: reusing the existing,
    already-correct fresh-launch logic rather than inventing a new one. This does not
    resolve or depend on resolving why the persisting same-named process(es) exist
    (legitimate Moza helper vs. shutdown artifact) -- it is a general robustness fix for
    the confirmed 'no window found for any matched process' state, whatever its cause."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. Has not confirmed
    whether Moza enforces single-instance behavior that could make a second
    Process.Start against an already-partially-running app silently no-op, error, or
    behave unexpectedly while a same-named helper is alive -- if the rig test shows
    Process.Start is called but still produces no window, this becomes the next
    hypothesis to investigate. Has not confirmed the exact identity/purpose of the
    persisting processes (PID=14736 across both snapshots, and PID=24756 newly
    appearing where PID=33456 previously was) -- Task Manager Details-tab image
    path/command-line data for these PIDs would help confirm this but is not required
    to justify this specific fix, since the fix's mechanism (poll then fall back to
    fresh launch) does not depend on that identity. Requested as bonus context in the
    next checkpoint rather than blocking on it. Also has not re-confirmed H9 (close
    button no-op) this round -- no fresh debug.log or visual-reaction answer was
    provided for H9 specifically; that checkpoint request remains open and is repeated
    below."

next_action: Applied H10 fix (bounded poll + fresh-launch fallback in LaunchOrFocus's
  already-running branch) below; awaiting rig test for BOTH H10 (does the app now
  relaunch after being fully closed, including a fresh reboot/clean-slate test if
  possible) and H9 (still needs a fresh debug.log capture with the enabled= field, plus
  the visual-reaction-on-X-click answer, per reasoning_checkpoint_7's falsification
  test) in the same round.

status_update: SUPERSEDED BY reasoning_checkpoint_7 below -- H7 (FindBestMainWindow) remains
CONFIRMED: the original "window doesn't come to foreground" bug is fixed and is NOT
re-opened by anything in this round. The close-button regression (H8) has now had a sixth
round of evidence-gathering (clarifying-question answers only, NOT a fresh debug.log --
none was captured this round) that ELIMINATES H8 as originally framed (Torque-Curve-window
triggers EnableWindow(dashboard, FALSE) -- Torque Curve was confirmed NOT open) and
significantly reframes the mechanism: minimize works normally but ALL THREE independent
close mechanisms (X button, Alt+F4, taskbar "Close window") fail identically, which is far
better explained by Moza's own WM_CLOSE/FormClosing handler no-oping (H9, a Moza-side
close-to-tray handler whose internal state gets desynced by RigToggle's raw Win32
show/activate calls bypassing WinForms' managed Form lifecycle) than by a literal
WS_DISABLED window (which should also block direct minimize-button input, not just close).
Genuinely uncertain between H9 (likely NOT fixable from RigToggle -- a Moza-side quirk) and
a residual non-Torque-Curve-triggered WS_DISABLED variant of H8 (potentially fixable via a
defensive EnableWindow(hWnd, TRUE)) -- did NOT apply a sixth blind fix; requested a fresh
debug.log capture (to read the real enabled= value) plus one visual-reaction clarifying
question instead, per this session's established discipline. See reasoning_checkpoint_7
(inserted after reasoning_checkpoint_6 below). The historical entries below (this
status_update's prior text, reasoning_checkpoint through reasoning_checkpoint_6,
new_hypotheses_under_consideration H1/H2/H3, and the older "hypothesis:"/"next_action:"
pairs) are preserved as this session's investigation history and are otherwise superseded.

reasoning_checkpoint:
  hypothesis: "SetForegroundWindow's documented foreground-lock restriction is silently
    denying RigToggle's activation request by the time WindowsAppController.FocusWindow
    calls it, causing Windows to fall back to its documented behavior of flashing the
    target window/taskbar button instead of truly bringing it to the foreground and
    keeping it visible -- this produces exactly the reported 'flicker... it just blinks
    and does not remain open', independent of whether the IsIconic/ShowWindow(SW_RESTORE)
    step (already applied) runs correctly."
  confirming_evidence:
    - "MSDN SetForegroundWindow docs (verified via web search this session): 'An application
      cannot force a window to the foreground while the user is working with another
      window. Instead, Windows flashes the taskbar button of the window to notify the
      user.' Conditions for success include 'the calling process received the last input
      event' OR 'the calling process is the foreground process' -- both of which can lapse
      during ToggleToRigMode's multi-step synchronous execution (Monitor.Disable then
      Audio.SetDefault both run BEFORE App.LaunchOrFocus per D-04 ordering, ToggleService.cs
      lines 82-125), giving real wall-clock time and intervening system activity (display
      topology change, audio endpoint change, possible driver/OS-level focus shifts as the
      monitor goes dark) for RigToggle's foreground eligibility to lapse before FocusWindow's
      SetForegroundWindow call runs."
    - "User's own description -- 'I do see a flicker of something... it just blinks and does
      not remain open' -- matches the classic external appearance of Windows'
      flash-instead-of-activate fallback far better than a real open-then-close cycle."
    - "User confirmed (Q7) it is 'the main moza app' that flickers, not a launcher/splash --
      rules out H1 (launcher stub transient window)."
    - "User confirmed (Q6) 'no' correlation with monitor-disable timing -- rules out H3
      (monitor-disable topology race)."
    - "User confirmed (Q5) the issue is 'only toggle to rig', never toggle-back -- consistent
      with H2, since MinimizeIfRunning (toggle-back path) never calls SetForegroundWindow at
      all, so it can never hit this restriction."
  falsification_test: "If, after adding a SetWindowPos(HWND_TOPMOST)->SetWindowPos(HWND_NOTOPMOST)
    step before SetForegroundWindow (a Z-order change NOT subject to the same foreground-lock
    restriction, since SetWindowPos doesn't gate on the same permission check), the window
    still only flickers and does not remain open and focused on rig re-test, this hypothesis
    is falsified and the true mechanism lies elsewhere (e.g. Moza's own app-level window
    management actively fighting external activation, which would require a fundamentally
    different, non-Win32-API approach outside RigToggle's control)."
  fix_rationale: "SetWindowPos to HWND_TOPMOST then immediately HWND_NOTOPMOST (both
    SWP_NOACTIVATE, so it doesn't itself request activation) forces the window to the top
    of the Z-order via a mechanism NOT subject to SetForegroundWindow's foreground-lock
    check, which reliably makes a subsequent SetForegroundWindow call succeed instead of
    falling back to a flash -- this is a standard, widely-documented technique (distinct
    from AttachThreadInput, which CLAUDE.md/STACK.md forbids and this code already avoids).
    It directly targets the documented root-cause mechanism (foreground-lock flash fallback)
    rather than working around a symptom."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox -- verified via Win32
    documentation + code reading only, not runtime observation. If Moza Companion has its
    own 'auto re-hide on deactivate/lose-focus' tray behavior (a real possibility for tray
    apps, not ruled out), this fix would not address that -- the falsification_test above
    is designed to surface that possibility on the next rig test if this fix doesn't fully
    resolve it. Also cannot verify whether Moza's window sets WS_EX_TOOLWINDOW/ShowInTaskbar
    false via an owned-window technique that could make Process.MainWindowHandle's own
    'GW_OWNER==0 && IsWindowVisible' heuristic fail to find it at all in some states -- this
    cannot be probed without a live Windows session."

next_action: SUPERSEDED -- second fix attempt (SetWindowPos TOPMOST/NOTOPMOST toggle) rig-tested
  and FAILED (see new Evidence/reasoning_checkpoint below). Root cause re-diagnosed as H4
  (hidden, not merely minimized, main window -- MainWindowHandle returns Zero, D-06's
  "already running but MainWindowHandle is zero -> no-op" path silently skips FocusWindow
  entirely). Fix (EnumWindows-by-PID fallback) implemented below; awaiting third rig test.

reasoning_checkpoint_2:
  hypothesis: "Moza Companion's main window uses the common WinForms 'minimize/close to
    tray' pattern: the form's Visible property is set to false (not merely
    WindowState=Minimized) when hidden to the tray. .NET's Process.MainWindowHandle
    heuristic (confirmed via web research this session and in the prior H2 investigation)
    only returns a non-zero handle for a top-level, owner-less window where
    IsWindowVisible==true. When Visible==false, MainWindowHandle returns IntPtr.Zero even
    though a live HWND exists. Per D-06 (03-CONTEXT.md line 30), WindowsAppController's
    'already running' branch treats a zero MainWindowHandle as 'genuinely no window to
    manipulate' and silently no-ops -- meaning FocusWindow (containing BOTH of the last two
    rig-tested fixes) is NEVER CALLED AT ALL whenever Moza is in its normal tray-only
    steady state (confirmed as Moza's typical state per Q1: 'moza was running under the
    tray icon'). This explains why two independent, correctly-implemented fixes inside
    FocusWindow produced zero observable change -- that code was dead for this exact
    repro case."
  confirming_evidence:
    - "Checkpoint response (this session): second fix (SetWindowPos TOPMOST/NOTOPMOST
      toggle before SetForegroundWindow, inside FocusWindow) was rig-tested and made NO
      difference -- 'Window does not come up' is unchanged from before either fix. If
      FocusWindow had been executing (with either fix active), some difference in
      behavior would be expected; observing IDENTICAL behavior across three different
      FocusWindow implementations (bare, +IsIconic/SW_RESTORE, +SetWindowPos toggle)
      is much better explained by FocusWindow never running at all than by all three
      versions failing identically."
    - "User's answer this session: the observed flicker happens 'right at the start' of
      the toggle sequence. ToggleService.ToggleToRigMode's D-04 ordering is Monitor.Disable()
      -> Audio.SetDefault() -> App.LaunchOrFocus(), stop-on-first-failure. App.LaunchOrFocus
      (and therefore FocusWindow/SetForegroundWindow) runs LAST, not first. A flicker
      occurring 'at the start' cannot temporally be caused by FocusWindow's
      SetForegroundWindow call -- this INVALIDATES the earlier elimination reasoning
      (timestamped 2026-07-25T02:00:05Z) that used 'the user observed a flicker, therefore
      FocusWindow must have executed on a non-zero handle' as grounds to rule out the
      hidden-window hypothesis. That premise no longer holds: the flicker is very likely an
      unrelated visual artifact of Monitor.Disable()'s display-topology change (a known,
      generic Windows behavior when SetDisplayConfig changes topology -- brief repaint/flash
      as the desktop reflows), not evidence about FocusWindow's execution at all."
    - "D-06 (03-CONTEXT.md, verified by direct re-read this session): 'Already running but
      MainWindowHandle is zero: do NOT retry/poll -- per CLAUDE.md, treat this as running
      but no window to manipulate right now (e.g. genuinely tray-only) and move on without
      failing the toggle.' This decision conflated two distinct states under one label
      ('MainWindowHandle == Zero'): (a) truly no window exists yet (e.g. app still starting),
      and (b) a window DOES exist but is hidden (Visible=false) rather than merely absent --
      .NET's own MainWindowHandle heuristic cannot distinguish these, but Win32 can
      (IsWindowVisible/EnumWindows by PID). Case (b) is exactly what a tray-hide pattern
      produces, and D-06's no-op silently discards the one case where a corrective action
      (ShowWindow) would actually work."
  falsification_test: "If, after adding an EnumWindows-by-process-ID fallback that finds
    Moza's main window even when Process.MainWindowHandle is Zero, and calling
    ShowWindow(SW_SHOW) on it (in addition to the existing IsIconic/SW_RESTORE and
    SetWindowPos-toggle steps) before SetForegroundWindow, the window STILL does not
    appear on rig re-test, this hypothesis is falsified -- the true blocker would then be
    something else entirely (e.g. Moza actively re-hiding itself in response to any
    external Show/Activate call, which would be outside RigToggle's control via any
    standard Win32 API)."
  fix_rationale: "EnumWindows (matched by GetWindowThreadProcessId against the known PID --
    NOT title/class matching, which CLAUDE.md forbids) finds ALL top-level windows
    regardless of visibility, unlike Process.MainWindowHandle which silently filters to
    visible-only. This directly targets the actual gap (D-06's zero-handle no-op discarding
    a recoverable hidden-window case) rather than further tweaking FocusWindow's internals,
    which the new evidence shows were never being reached in the first place."
  blind_spots: "Still cannot execute/build/observe on this Linux sandbox. If Moza's window
    is hidden via a mechanism other than Visible=false (e.g. WS_EX_TOOLWINDOW removing it
    from EnumWindows' top-level enumeration in some edge case, or the process legitimately
    has zero top-level windows because it truly hasn't created its main form yet at the
    moment of toggle), the EnumWindows fallback would also return zero, and the underlying
    problem would remain undiagnosed. Also has not been ruled out: Moza's tray-click handler
    might do something beyond ShowWindow+Activate (e.g. re-parent the window, use a
    different top-level HWND each time it's shown) that our EnumWindows-once approach
    wouldn't replicate exactly -- if this fix fails, the next diagnostic step should be
    asking whether Moza exposes any documented CLI/IPC 'show window' mechanism instead of
    relying on Win32-level window manipulation entirely."

next_action: SUPERSEDED -- third fix (EnumWindows-by-PID fallback) rig-tested with a
  precise new repro sequence and FAILED again (see Evidence 2026-07-25T04:00:00Z below and
  reasoning_checkpoint_3). Root cause re-diagnosed as H5 (FindHiddenMainWindow's
  first-owner-less-match heuristic can grab NotifyIcon's own always-present, invisible,
  owner-less helper window instead of Moza's real main form). Fix (title/size-scored
  candidate selection + real diagnostic logging) implemented below; awaiting fourth rig
  test.

reasoning_checkpoint_3:
  hypothesis: "In this specific repro (Moza's OWN tray-hide/close leaves it 'running with
    MainWindowHandle==Zero' BEFORE any RigToggle interaction occurs -- confirmed by tracing
    MinimizeIfRunning's guard, see confirming_evidence), FindHiddenMainWindow's
    EnumWindows-by-PID scan can match a DIFFERENT top-level, owner-less window belonging to
    the same process instead of Moza's real main form: .NET's System.Windows.Forms.NotifyIcon
    component creates its own top-level (owner=IntPtr.Zero), permanently-invisible native
    window (used solely to receive Shell_NotifyIcon-forwarded tray messages) that exists for
    the entire lifetime of the tray icon -- including while the real main form is visible.
    FindHiddenMainWindow's current 'return the first owner-less window matching PID, stop
    enumerating' logic (lines 140-163) cannot distinguish this always-present helper window
    from the real main form once the main form itself also becomes invisible -- if EnumWindows
    happens to enumerate the NotifyIcon helper window before the real form, FocusWindow
    executes its full ShowWindow/SetForegroundWindow sequence correctly against it, but since
    it has no client content/visible chrome, the result is indistinguishable from 'nothing
    happened' to the user -- explaining why three independently-reasoned, correctly-implemented
    FocusWindow fix layers (IsIconic/SW_RESTORE, SetWindowPos TOPMOST toggle,
    IsWindowVisible/SW_SHOW) produced zero observable change."
  confirming_evidence:
    - "Re-traced MinimizeIfRunning (lines 207-240) against the NEW repro's exact sequence:
      user closes Moza to tray via Moza's OWN UI while still in Rig mode (i.e. BEFORE toggling
      to Normal mode at all). By the time MinimizeIfRunning runs (on the Normal-mode toggle),
      p.MainWindowHandle is therefore ALREADY IntPtr.Zero (Moza's own hide-to-tray already
      happened) -- MinimizeIfRunning's `if (p.MainWindowHandle != IntPtr.Zero)` guard means
      ShowWindow(SW_MINIMIZE) is never even called; MinimizeIfRunning has no
      FindHiddenMainWindow fallback of its own (only LaunchOrFocus has one). This directly
      answers checkpoint question #2/#3: our own code does NOT trigger or compound the hidden
      state in this repro -- it is a pure no-op here. The bug is entirely within
      LaunchOrFocus's already-running/FindHiddenMainWindow/FocusWindow path being exercised
      against a window state Moza itself already created."
    - "FindHiddenMainWindow (current code) takes the FIRST owner-less (GW_OWNER==0) window
      matching the PID via EnumWindows and stops -- no title or size check exists, so it
      cannot distinguish a real UI window from any other top-level, owner-less helper window
      the same process happens to own."
    - "System.Windows.Forms.NotifyIcon is documented/well-known (WinForms source) to create
      its own native top-level window purely to receive tray-icon shell callback messages;
      this window has Parent=IntPtr.Zero (so GetWindow(hWnd, GW_OWNER) is also Zero -- it
      passes our current owner-only filter) and is never shown -- exactly the profile our
      current filter would wrongly accept as a valid 'hidden main window' candidate."
    - "All three previous, independently-reasoned fix attempts produced literally IDENTICAL
      observed behavior (no visible change) on rig test -- far better explained by
      FocusWindow correctly executing its full sequence against the WRONG (contentless)
      window every time, than by three different, individually-plausible Win32 techniques
      all failing identically against the CORRECT window."
  falsification_test: "This fix adds (a) a title/size-scored candidate selection in
    FindHiddenMainWindow (prefer a titled, non-trivially-sized window over an untitled/
    zero-size one, falling back to the old first-match behavior if no candidate qualifies)
    and (b) real, user-visible diagnostic logging (Trace.WriteLine routed to a
    TextWriterTraceListener writing to %LOCALAPPDATA%\\RigToggle\\debug.log) at every
    decision point. On the next rig test: if the log shows the window still does not appear
    AND shows either (i) only one untitled/zero-size candidate was ever found (no real form
    window exists at all for the PID -- meaning Moza destroys/recreates its form rather than
    hiding it, falsifying this hypothesis and supporting the alternative 'window destroyed on
    tray-hide' theory) or (ii) a titled/sized window WAS found and ShowWindow/
    SetForegroundWindow both returned TRUE and GetForegroundWindow()==hWnd afterward yet the
    window is still not visually present (Moza actively re-hiding itself post-activation) --
    either outcome falsifies this specific hypothesis, but in both cases the log itself
    supplies the next concrete piece of evidence instead of requiring another blind guess."
  fix_rationale: "(1) FindHiddenMainWindow now scores ALL owner-less PID-matched candidates
    by title-presence (GetWindowTextLength > 0) and non-trivial size (GetWindowRect width/
    height both > 0) instead of blindly accepting the first owner-less match -- directly
    targets the specific, well-evidenced gap (a same-process invisible helper window
    satisfying the old first-match owner-only filter). Falls back to the original first-match
    behavior if no candidate has a title/size, so this cannot make an already-working case
    regress to finding nothing. (2) Adds real, persisted diagnostic logging that has been
    completely absent from this code path across three blind fix attempts -- directly follows
    the Observability First discipline that should have been applied earlier, and ensures the
    next rig test yields hard evidence regardless of whether hypothesis H5 itself is correct."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. If Moza's window is
    genuinely destroyed/recreated on tray-hide (not the NotifyIcon-collision theory), the
    title/size fix will correctly find no qualifying candidate and no-op as before -- but the
    new logging will make that visible on the next rig test rather than remaining a silent,
    undiagnosable no-op, so the logging investment is not wasted even if H5 itself is wrong.
    Also not yet ruled out: Moza re-hiding itself immediately after any external
    Show/Activate call -- the new post-call GetForegroundWindow()/IsWindowVisible logging is
    designed to surface that distinctly from 'window never found' on the next test."

next_action: SUPERSEDED -- fourth rig test returned a REAL diagnostic log (first time this
  investigation has had actual runtime Win32-level evidence instead of user-described
  symptoms only). The log disproves H5 as stated and reveals a fifth, structurally different
  root cause: PROCESS-SELECTION ambiguity, not window-visibility/foreground-lock/minimize
  mechanics. See reasoning_checkpoint_4 and Evidence 2026-07-25T05:00:00Z below. Root cause
  is NOT yet fully confirmed -- genuinely blocked on user-supplied Task Manager data (see
  next_action at bottom). Did NOT apply a fifth blind code fix; four consecutive blind fixes
  have already failed and the debugging philosophy explicitly forbids a fifth guess without
  new evidence discriminating between the remaining live possibilities.

reasoning_checkpoint_4:
  hypothesis: "LaunchOrFocus's 'already running' branch iterates Process.GetProcessesByName's
    array in whatever order the OS returns it (undocumented/arbitrary), and focuses the FIRST
    process in that array that has ANY focusable window (via MainWindowHandle or the
    FindHiddenMainWindow fallback) -- it does not verify that process is 'the' main Companion
    app. When Process.GetProcessesByName(processName) matches MULTIPLE distinct PIDs (because
    two separate running processes share the same base executable name, minus '.exe'), the
    code can silently focus the WRONG process's window -- one that happens to have a
    focusable window at that moment -- while skipping past a different PID that is genuinely
    the main Companion app but has zero top-level windows at that instant. This is a
    process-identity bug, not a window-visibility/minimize/foreground-lock bug -- H1-H5 (all
    scoped to 'how do we find/restore/activate the ONE Companion window') were investigating
    the wrong layer of the problem for this specific repro."
  confirming_evidence:
    - "Real diagnostic log from this session's rig test (verbatim, see checkpoint response):
      TWO distinct PIDs matched the configured process name in one LaunchOrFocus call --
      PID=31616 (MainWindowHandle=0x0 AND FindHiddenMainWindow's full EnumWindows-by-PID scan
      also returned 0x0, i.e. genuinely ZERO owner-less top-level windows exist for that PID
      right now, not merely a hidden/invisible one -- EnumWindows enumerates ALL top-level
      windows regardless of visibility, so this is not a visibility-filtering artifact) and
      PID=34792 (MainWindowHandle=0x1D7036C directly, non-zero, IsIconic=True -- a real,
      currently-minimized top-level window)."
    - "Code reading (WindowsAppController.cs lines 102-136, this session): the foreach loop
      breaks on the FIRST process (in array order) whose hWnd resolves non-zero -- it never
      considers or compares the remaining processes in the array once one candidate is found.
      For this repro, PID 31616 was checked first (per log order) and skipped for having no
      window at all; PID 34792 was checked second and selected purely because it had SOME
      window, not because it was verified to be the correct one."
    - "At the Win32 level, PID=34792's window was successfully un-minimized and foregrounded:
      ShowWindow(SW_RESTORE) returned True, SetForegroundWindow returned True, and
      GetForegroundWindow() after the call matches the target hWnd exactly (match=True). This
      is NOT a Win32 failure of any kind (rules out further foreground-lock/minimize/hidden-
      window mechanics as the remaining problem for THIS window) -- the mechanism worked
      exactly as designed against the window it was given."
    - "User's own description of the result: 'it does grab a moza window but some kind of
      toggle curve window I can't close then' -- explicitly NOT the main Companion dashboard
      the user expects. This is a real appearance mismatch, not a Win32 return-code failure --
      corroborates that the code focused a real but WRONG window."
  falsification_test: "If the user's Task Manager 'Details' check (see next_action) shows only
    ONE process currently matches the configured Companion process name at any given moment
    (i.e. PID 31616 and PID 34792 are transient/coincidental and don't both exist under
    steady-state operation), this hypothesis would be weakened -- the two-PID log snapshot
    would need a different explanation (e.g. a process starting/exiting mid-enumeration). If
    Task Manager instead confirms two (or more) persistently co-existing processes sharing the
    same base name -- e.g. a main dashboard process plus a separate curve-editor/tool process
    -- this hypothesis is confirmed, and the specific fix approach (path-based filtering vs.
    title-based window selection vs. command-line-based filtering) depends on whether the two
    processes are the SAME physical .exe file (self-relaunched with different roles/args, in
    which case MainModule.FileName filtering cannot discriminate them) or DIFFERENT physical
    .exe files that merely share a file name (in which case MainModule.FileName filtering
    against the configured CompanionAppPath would correctly exclude the wrong one)."
  fix_rationale: "No fix applied yet -- deliberately withheld. Four independent, individually
    well-reasoned FocusWindow-layer fixes (SW_RESTORE, SetWindowPos TOPMOST-toggle,
    IsWindowVisible/SW_SHOW, title/size-scored FindHiddenMainWindow) have each been rig-tested
    and failed to produce the correct observable result, because none of them addressed the
    actual layer where the bug lives (process selection among multiple same-named processes,
    not window state within a single already-correctly-identified process). Applying a fifth
    fix without first confirming which of the two process-identity scenarios (same physical
    exe self-relaunched vs. two distinct exes sharing a name) is real would be exactly the
    'guess and fix' pattern the debugging philosophy and this session's own prior status notes
    explicitly forbid -- the correct fix mechanism (MainModule.FileName path filtering vs.
    window-title-based selection among confirmed-same-app windows vs. something else) differs
    materially between the two scenarios, and code-reading alone (confirmed:
    MainModule.FileName is never referenced anywhere in this codebase today -- process
    matching is 100% base-name-only, everywhere) cannot determine which scenario applies on
    this specific rig without the user's Task Manager data."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox. Also has not been ruled
    out: PID 31616 could be a legitimate always-running background/helper component of the
    Moza suite that never has a UI window at all (e.g. a hardware bridge/service process) and
    is entirely unrelated to 'which window is the main dashboard' -- in that case the real
    story is simpler (only PID 34792 ever has a window, and the actual root cause is that
    34792's window is a secondary tool, not the main app, launched by Moza's own tray-restore
    logic instead of the main dashboard -- which would point back at Moza's own app behavior,
    not a RigToggle process-selection bug at all). This is exactly why the Task Manager cross-
    check (full image path + command line for both PIDs, plus the exact window title of both
    the 'curve' window and the real main dashboard when opened normally) is required before
    committing to a fix direction."

next_action: SUPERSEDED -- user directly answered the process-identity question without a
  full Task Manager capture: the "Torque Curve" window is confirmed to belong to the SAME
  process as Moza's real main dashboard (PID 34792), not a second distinct process. This
  resolves H6 differently than either of its two sub-scenarios (same-exe-self-relaunched vs
  two-distinct-exes) -- PID=31616 is very likely an unrelated/no-UI process (a Moza
  background service) and not part of the "which window" story at all. Root cause
  re-diagnosed as H7 (Process.MainWindowHandle's single-window-pick heuristic fails on a
  process with multiple top-level windows) -- see reasoning_checkpoint_5. Fifth fix
  (FindBestMainWindow: always enumerate all top-level windows for the resolved PID and pick
  the largest-area titled candidate, replacing MainWindowHandle trust entirely, applied to
  LaunchOrFocus's already-running branch AND MinimizeIfRunning for consistency) implemented
  below; awaiting fifth rig test.

reasoning_checkpoint_5:
  hypothesis: "Process.MainWindowHandle's internal heuristic returns the FIRST owner-less,
    visible top-level window it finds for a given process and stops there -- it has no
    concept of 'the semantically primary window' when a process legitimately owns MULTIPLE
    top-level windows at once. Moza Companion, in the exact state this bug reproduces in, has
    two simultaneously open top-level windows belonging to the SAME process (PID 34792): its
    main dashboard (present but not focused / iconic) and a separate 'Torque Curve'
    utility/tool window. Process.MainWindowHandle picked the Torque Curve window, not the
    dashboard -- LaunchOrFocus then correctly executed its full, previously-verified-working
    FocusWindow sequence (ShowWindow/SetForegroundWindow, all Win32 calls succeeding) against
    that wrong window, producing exactly the reported symptom: 'it does grab a moza window
    but some kind of toggle curve window I can't close'."
  confirming_evidence:
    - "Self-caught implementation flaw before shipping (this session): the first draft of
      FindBestMainWindow scored candidates using GetWindowRect, which reports a
      degenerate/off-screen rect for a currently-MINIMIZED window (not its real restored
      size) -- verified via web search (learn.microsoft.com GetWindowRect docs +
      corroborating sources on the -32000,-32000 iconic-position convention). The confirmed
      rig-test log (Evidence 2026-07-25T05:00:00Z) shows PID=34792's dashboard was
      IsIconic=True at exactly the moment this comparison would run -- using GetWindowRect
      would have scored the correct dashboard as tiny/zero-area and lost to the
      non-iconic Torque Curve window, shipping a fix that would fail the exact repro it was
      meant to solve. Corrected to GetWindowPlacement's rcNormalPosition, which reports the
      window's RESTORED size regardless of current minimized/maximized/normal state --
      caught and fixed via source reasoning before the rig test, not discovered by a fifth
      failed rig test."
    - "Checkpoint response (this session): user directly clarified process identity -- 'The
      toggle curve window does not have its own process... the Torque Curve window and the
      real main Moza Companion dashboard both belong to the SAME process (PID 34792)'."
    - "Prior diagnostic log (Evidence 2026-07-25T05:00:00Z, re-examined with this new fact):
      for PID=34792, MainWindowHandle was non-zero DIRECTLY from .NET's own property read --
      FindHiddenMainWindow (the EnumWindows fallback) was never invoked for this PID (only
      invoked when MainWindowHandle==Zero, which only happened for PID=31616). This means
      .NET's OWN internal MainWindowHandle heuristic -- not any of this codebase's custom
      fallback logic -- is what selected the Torque Curve window over the dashboard. This is
      a real, previously-unconsidered failure mode: the four prior fix layers (H2/H4/H5) all
      operated INSIDE FocusWindow/FindHiddenMainWindow, code that is only reached AFTER a
      window handle has already been chosen -- none of them could have corrected a wrong
      choice made further upstream by Process.MainWindowHandle itself."
    - "PID=31616 (zero windows found via either MainWindowHandle or the full EnumWindows
      scan) is best explained as an unrelated background/service process that happens to
      share the configured process name by coincidence, or a genuinely UI-less Moza
      component -- not a second UI-bearing instance of the main app. It is a red herring for
      the 'which window' question, not a second candidate worth selecting between."
  falsification_test: "If, after making FindBestMainWindow always run (never conditioned on
    MainWindowHandle==Zero) and select the largest-area titled top-level window for the
    resolved PID instead of trusting Process.MainWindowHandle, the rig test STILL focuses the
    Torque Curve window (or still fails to bring up the dashboard) even though the dashboard
    window is confirmed larger on screen than the Torque Curve dialog, this hypothesis is
    falsified -- the true discriminator would then need to be something other than window
    area (e.g. Moza's dashboard reporting a smaller/zero rect while minimized despite being
    the correct target, in which case area-based scoring would need to run BEFORE minimizing
    is accounted for, or a different signal such as 'the window that was active most
    recently' would be needed instead)."
  fix_rationale: "FindBestMainWindow now always enumerates every owner-less top-level window
    for the resolved PID (not just as a MainWindowHandle==Zero fallback) and scores each by
    caption presence + rect area, selecting the largest titled candidate. This directly
    targets the newly-confirmed root cause (Process.MainWindowHandle's undocumented
    first-match-wins heuristic choosing the wrong window among several belonging to the same
    process) rather than adjusting FocusWindow's internal Win32 call sequence again -- the
    prior four fix layers already proved that sequence works correctly once given the right
    handle; the gap was entirely in which handle was selected in the first place. Applied
    consistently to MinimizeIfRunning too, since it has the identical
    trust-MainWindowHandle-blindly fragility and could equally minimize the wrong window on
    toggle-back."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox -- verified via code
    reading, Win32 documentation, and the user's process-identity clarification only, not
    runtime observation. Area is a reasonable, non-brittle heuristic (doesn't rely on
    title-string matching, which CLAUDE.md forbids for identification) but is not guaranteed
    universally correct -- if Moza ever presents the Torque Curve window at a larger
    RESTORED size than the dashboard (e.g. user resizes/maximizes the tool window and Moza
    persists that), area-based selection could still choose wrong. The
    minimized-state-degenerate-rect failure mode this hypothesis would otherwise have shipped
    with was caught and corrected pre-rig-test by switching from GetWindowRect to
    GetWindowPlacement's rcNormalPosition (see confirming_evidence) -- but this is still
    reasoning from documentation, not an observed rig result. The diagnostic logging (kept
    and extended to log every candidate's title/normalRect/area/iconic-state) is designed to
    surface exactly this on the next rig test if the hypothesis is still wrong, rather than
    requiring another blind guess."

reasoning_checkpoint_6:
  hypothesis: "H8: the Moza Companion main window that FindBestMainWindow selects and
    FocusWindow brings to the foreground already has the WS_DISABLED style set (Win32
    IsWindowEnabled == false) AT THE MOMENT RigToggle finds it -- most likely because Moza
    implements a custom app-level 'soft-modal' pattern where opening its 'Torque Curve'
    utility window (confirmed, same process, owner-less per H7's evidence) manually disables
    the main dashboard via EnableWindow(dashboardHwnd, FALSE) instead of using true
    owned-window modality (which would have made GetWindow(hWnd, GW_OWNER) non-zero for
    Torque Curve and excluded it from H7's candidate scan -- it did not, so Torque Curve is
    a standalone top-level window, consistent with an app-level rather than OS-level modal
    pattern). None of ShowWindow/SetWindowPos/SetForegroundWindow change or depend on
    WS_DISABLED state -- Windows explicitly allows showing/foregrounding a disabled window
    (it will paint, appear, and receive Z-order/foreground changes normally), but a disabled
    top-level window silently swallows ALL subsequent mouse/keyboard input, including clicks
    on its own title-bar close (X) button, with zero error and zero other visible symptom --
    this matches 'the main window opens... but the x does nothing' exactly, and explains why
    it is a NEW symptom that only appeared once H7 started correctly finding/foregrounding
    the dashboard (all four prior fix rounds never successfully got this far)."
  confirming_evidence:
    - "Checkpoint response (this session, verbatim): 'Yes it works now. The main window
      opens but we still have a small bug. You can't close it now. It's open and the x does
      nothing.' Follow-up: 'Only after Rig Toggle opens it' -- confirms the window IS the
      correct dashboard (user recognizes it, unlike H5/H6/H7's earlier wrong-window
      symptoms) and confirms this is caused by RigToggle's own interaction with the window,
      not baseline Moza behavior (rules out 'X always minimizes to tray and user is
      mistaking that for broken' -- that would reproduce on manual opens too)."
    - "Code re-read this session (src/RigToggle.Windows/WindowsAppController.cs FocusWindow,
      lines 290-320; NativeMethods.cs, full file): confirmed NO EnableWindow call exists
      anywhere in this codebase (grep-equivalent manual read of both files, the only two
      files with P/Invoke/window-manipulation code). The TOPMOST->NOTOPMOST SetWindowPos
      toggle (H2's fix) uses SWP_NOACTIVATE and only affects Z-order, not enabled state --
      ruling out prime-suspect #1 (topmost trick) as a mechanism that could itself disable
      the window. ShowWindow(SW_SHOW/SW_RESTORE) and SetForegroundWindow are also
      well-documented to not alter WS_DISABLED (Microsoft Learn ShowWindow/
      SetForegroundWindow docs, consistent with this session's prior H2/H4 web research) --
      ruling out prime-suspect #2 (some P/Invoke call inadvertently disabling the window).
      This makes 'the window was ALREADY disabled by Moza itself before we touched it, and
      none of our calls change that' the best-supported remaining mechanism among the four
      prime suspects listed in the checkpoint instructions."
    - "H7's own confirmed evidence (reasoning_checkpoint_5, this file): Moza Companion, in
      the exact repro state, has TWO simultaneously open top-level windows on one process --
      the dashboard and a separate 'Torque Curve' utility window, both owner-less. An
      owner-less-but-app-disabled secondary window is a known alternative pattern to true
      owned modal dialogs, and would fully explain why H7's owner-only filter did not
      exclude Torque Curve (it isn't Win32-owned) while the dashboard could still be
      soft-disabled by Moza's own code while Torque Curve is 'active'."
    - "Prime suspect #3 (wrong window again, e.g. a lookalike overlay) is NOT ruled out yet
      but is weaker than H8 given the user's own words -- 'the main window opens' implies
      recognition of the correct dashboard, and H7's selection logic (largest titled
      restored-area candidate) has no known mechanism for picking a contentless overlay
      over the real dashboard. GetClassName logging (added this session, see
      fix_rationale) will make this directly checkable on the next log capture instead of
      remaining an assumption."
  falsification_test: "On the next rig test (repro: toggle to rig with the app already
    running and, ideally, with the Torque Curve window open at the time, so the disabling
    condition is present; window opens; attempt to click X; observe it does nothing; then
    share the fresh debug.log): if the log's new 'enabled=' field shows enabled=True on the
    target window both before and after FocusWindow's full sequence, H8 is FALSIFIED -- the
    window is not Win32-disabled, and the true mechanism must be something else (e.g. Moza
    itself intercepting/suppressing WM_CLOSE or subclassing its own close button
    specifically -- which would be outside RigToggle's control via any standard Win32 API
    and would need to be reported as a Moza-side issue rather than fixed here). If
    enabled=False is observed (either already before FocusWindow runs, confirming Moza
    disabled it independently of us, or -- less expected given the code-reading evidence --
    only after our calls run, which would newly implicate one of our own P/Invoke calls
    after all and require re-examining that specific call), H8 is confirmed and the fix
    direction (e.g. EnableWindow(hWnd, TRUE) as a defensive step in FocusWindow, and/or
    understanding whether re-enabling the dashboard while Torque Curve is still open could
    itself cause a Moza-side conflict) can be chosen with actual evidence instead of a
    sixth blind guess."
  fix_rationale: "No behavior-changing fix applied yet -- deliberately withheld, per this
    session's explicit checkpoint-response instruction and the established pattern (4 of 5
    prior FocusWindow-layer fixes failed without evidence-gathering first; the one fix that
    worked, H7, was the one time process/window-identity evidence was gathered BEFORE
    fixing). Instead, added diagnostic-only instrumentation with zero behavior change: (1)
    NativeMethods.IsWindowEnabled P/Invoke + WindowsAppController logs enabled= state in
    both FindBestMainWindow's per-candidate scan and FocusWindow's before/after state dump,
    directly targeting H8's falsification test. (2) NativeMethods.GetClassName P/Invoke +
    logging of each candidate's window class name in FindBestMainWindow, to make prime
    suspect #3 (wrong-but-lookalike window) directly checkable rather than assumed away.
    Both additions are read-only Win32 calls wrapped in the existing best-effort Log()
    helper (never throws, never affects toggle behavior) -- this cannot regress the
    already-confirmed-working H7 fix."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox -- all reasoning above is
    from source reading (confirmed no EnableWindow call exists) and Win32 documentation
    (ShowWindow/SetForegroundWindow/SetWindowPos do not alter WS_DISABLED), not runtime
    observation. Has not ruled out: Moza subclassing its own window procedure to reject
    WM_NCHITTEST/WM_NCLBUTTONDOWN on its close button specifically while some other
    Moza-internal state flag is set (would look identical to WS_DISABLED from outside but
    isn't detectable via IsWindowEnabled) -- if H8 is falsified by enabled=True in the log,
    this becomes the next candidate, though it would likely be a Moza-side bug outside
    RigToggle's control rather than something fixable here. Also has not confirmed whether
    the Torque Curve window is reliably open at the moment of the reported bug -- the
    checkpoint below explicitly asks the user to confirm this, since H8's causal story
    depends on it."

next_action: SUPERSEDED -- sixth checkpoint response answered the H8 clarifying questions
  (repro-condition detail only; the user did not additionally capture/paste a fresh
  debug.log this round). This ELIMINATES H8 as originally framed (Torque-Curve-triggered
  EnableWindow) and produces a reframed hypothesis, H9 (Moza-side WM_CLOSE/FormClosing
  handler no-op caused by state desync from RigToggle's raw Win32 calls). See
  reasoning_checkpoint_7 below. Did NOT apply a sixth code fix -- genuinely uncertain
  between H9 (likely unfixable from RigToggle) and a residual non-Torque-Curve WS_DISABLED
  variant of H8 (fixable via defensive EnableWindow); requested one more evidence round
  (fresh debug.log + a visual-reaction clarifying question) before committing to a fix
  direction, per this session's established discipline.

reasoning_checkpoint_7:
  hypothesis: "H9 (supersedes H8 as the leading theory): the close (X) button, Alt+F4, and
    taskbar 'Close window' all route through the SAME single mechanism inside Moza's own
    process -- WM_SYSCOMMAND(SC_CLOSE) -> WM_CLOSE -> (for a WinForms app) the main Form's
    FormClosing event handler. Tray-style WinForms apps near-universally implement
    FormClosing to CANCEL the close (e.Cancel = true) and instead Hide()/minimize to tray,
    only allowing a REAL exit via a dedicated 'Exit' tray-menu item that sets an internal
    allow-close flag first. RigToggle's FocusWindow calls raw user32.dll ShowWindow(SW_SHOW)
    (when the window was found hidden, i.e. Visible=false, the confirmed steady tray-hidden
    state) directly on the native HWND, bypassing the managed WinForms Form.Show()/Visible
    setter entirely (Control's internal 'is this control visible' state bit, GetState
    (States.Visible), is set by SetVisibleCore -- called from the Visible property setter --
    NOT automatically kept in sync just because the native window was shown via an external
    P/Invoke call). This can leave Form.Visible (the MANAGED property Moza's own C# code
    would read) reporting a STALE value that disagrees with the native window's real
    on-screen state. If Moza's own FormClosing/close-to-tray logic branches on that managed
    Visible property (or a related internal flag it sets/reads only through its own
    Show()/Hide() code path) to decide 'should I cancel-and-hide, or is there nothing to
    hide because I think I'm already hidden', the desync can put it into a branch that does
    neither a real close NOR a hide -- i.e., a pure no-op -- exactly matching 'X does
    nothing, Alt+F4 does nothing, taskbar Close does nothing, only Task Manager kill works',
    while Minimize (a structurally unrelated WM_SYSCOMMAND(SC_MINIMIZE) code path that does
    not touch FormClosing/Hide-to-tray logic at all) continues to work normally."
  confirming_evidence:
    - "Checkpoint response, Q2 (this round): 'no' -- the Torque Curve window (or any other
      Moza dialog) was NOT open at the same time as the dashboard. This directly eliminates
      H8-as-originally-framed, which required Torque Curve to be open in order for its
      hypothesized EnableWindow(dashboard, FALSE) call to have fired at all."
    - "Checkpoint response, Q3 (this round): 'no. everything works. you can minimize but not
      close.' A genuinely WS_DISABLED top-level window is documented (learn.microsoft.com
      EnableWindow remarks, corroborated by this session's own prior H8 research) to block
      ALL mouse/keyboard input to that window, including clicks on its own title-bar
      buttons -- the classic real-world example being a parent window disabled by a modal
      child, where clicking ANY title-bar button (minimize included) produces a system beep,
      not a working minimize. Minimize working normally while close-related actions
      specifically fail is a pattern WS_DISABLED does not naturally produce, but a
      close-specific app-level handler (H9) produces exactly and only this pattern, since
      minimize and close are structurally different WM_SYSCOMMAND sub-messages handled by
      completely different code inside a WinForms app."
    - "Checkpoint response, Q4 (this round): Alt+F4 does not close it, and taskbar
      right-click -> 'Close window' does not close it either -- confirmed THREE independent
      OS-level close-request mechanisms (title-bar X via WM_NCLBUTTONDOWN, Alt+F4 via
      WM_SYSKEYDOWN, and the shell's taskbar context menu via a posted WM_SYSCOMMAND) all
      fail identically. All three, despite arriving via different input paths, converge on
      the exact same WM_CLOSE message once DefWindowProc processes WM_SYSCOMMAND(SC_CLOSE)
      -- one shared downstream handler failing (H9) parsimoniously explains all three
      failing together far better than three independent input-delivery paths coincidentally
      failing the same way (which is what H8's blanket WS_DISABLED theory would require, and
      which is itself already weakened by the minimize-works evidence above)."
    - "Code re-read (this round, WindowsAppController.cs FocusWindow lines 314-322):
      confirmed the raw ShowWindow(hWnd, SW_SHOW) P/Invoke call (added in the H4 fix round to
      un-hide a tray-hidden, Visible=false window) executes whenever IsWindowVisible(hWnd) is
      false at the moment FocusWindow runs -- i.e., precisely the steady tray-hidden state
      the user has consistently described Moza sitting in between toggles (Q1, prior round).
      This is the one call in the whole FocusWindow sequence that manipulates a native
      visibility bit .NET's own managed Form class also separately tracks -- SetWindowPos
      (Z-order only) and SetForegroundWindow (activation only) have no analogous managed-vs-
      native state to desync, which is why H9 implicates ShowWindow(SW_SHOW) specifically
      rather than 'some P/Invoke call' generically."
  falsification_test: "On the next rig test (repro: toggle to rig with the app already
    running/tray-hidden -- Torque Curve need NOT be open this time, per this round's
    evidence; window opens; click X; observe it does nothing; capture and share the fresh
    debug.log AND answer whether there is any brief visual flicker/flash on the window
    before it settles back to fully visible when X is clicked): (a) if the log's enabled=
    field on the target window reads False either before or after FocusWindow's sequence,
    H9 is FALSIFIED and a residual (non-Torque-Curve) variant of H8 is confirmed instead --
    fix direction becomes a defensive EnableWindow(hWnd, TRUE) in FocusWindow. (b) if
    enabled=True throughout AND the user reports NO visual reaction at all when clicking X
    (fully inert, as if the click never registered), H9's specific 'FormClosing runs but
    Hide() no-ops' mechanism is weakened in favor of an even earlier interception (e.g. Moza
    subclassing WM_NCHITTEST/WM_NCLBUTTONDOWN specifically for its own close button while
    some Moza-internal flag is set) -- likely still Moza-side and outside RigToggle's
    control, but a distinct enough mechanism to note separately rather than conflate with H9.
    (c) if enabled=True AND the user reports SOME brief visual reaction (flicker/flash) when
    clicking X before it settles back to visible, this is strong positive confirmation of H9
    exactly as framed (FormClosing fired, attempted its own hide/redraw, and failed/reverted)
    and directly supports removing/reducing RigToggle's raw ShowWindow(SW_SHOW) usage (or
    finding an alternative that keeps Form.Visible in sync) as the fix direction, while also
    flagging that this may ultimately be a Moza-side robustness gap RigToggle can only
    partially work around."
  fix_rationale: "No behavior-changing fix applied yet -- deliberately withheld again this
    round. Unlike H8 (which had one clear, low-risk, purely-additive candidate fix --
    EnableWindow(hWnd, TRUE) -- regardless of exactly why the window was disabled), H9's
    fix direction is NOT low-risk or unambiguous: RigToggle has no access to Moza's managed
    Form instance or source, so 'stop bypassing WinForms' Show() lifecycle' cannot be done by
    calling a different, safer managed API -- the only tool available cross-process is raw
    Win32 P/Invoke, which is exactly what (per this hypothesis) causes the desync in the
    first place. A speculative fix (e.g. removing the now-otherwise-unvalidated
    SetWindowPos TOPMOST/NOTOPMOST toggle -- added for the already-ELIMINATED H2
    foreground-lock-flash theory and never itself confirmed necessary or beneficial on any
    rig test -- as a pure risk-reduction step, since fewer raw Win32 state manipulations
    means less surface area for interfering with Moza's internal bookkeeping) is plausible
    but would be the sixth guess-based code change in this session, and per the
    falsification_test above, a fresh debug.log's enabled= field can definitively
    discriminate 'still H8, low-risk fix exists' from 'H9, no clearly safe fix exists' before
    any code is touched again -- gathering that evidence first, rather than guessing a sixth
    time, follows this session's own explicit discipline note (4 of 5 blind fixes failed;
    the one that worked, H7, was preceded by evidence-gathering) and the checkpoint
    instructions' explicit invitation to request more evidence rather than guess."
  blind_spots: "Cannot execute/build/observe on this Linux sandbox -- H9 is reasoned from a
    well-documented, extremely common WinForms tray-app implementation PATTERN (FormClosing
    cancel-and-hide) and a well-documented WinForms footgun (raw native show/hide bypassing
    managed Visible-state tracking), not from Moza's actual (closed) source, so it cannot be
    verified by code reading the way H7/H8's mechanisms could be. If Moza's close-to-tray
    logic does NOT branch on Form.Visible or any state that our raw ShowWindow(SW_SHOW) call
    could desync (e.g. it uses a completely independent boolean flag toggled only by its own
    tray-icon click handler, never touching Visible at all), H9 as specifically framed is
    wrong even though the 'all three close paths share one handler' observation would still
    stand and point to some other Moza-internal mechanism instead. Also not resolved: whether
    ShowWindow(SW_SHOW) is even the operative call in the failing repro state -- this
    depends on whether Moza was tray-hidden (Visible=false, triggering SW_SHOW) or merely
    minimized-but-visible (which would not trigger it) at the moment of the failing test;
    the fresh debug.log's 'before' state dump (visible=/iconic=) directly resolves this."

new_hypotheses_under_consideration:
  H1_launcher_stub: "The configured CompanionAppPath may point to (or Process.Start may resolve to) a launcher/bootstrapper process that shows a brief splash/loading window and then hands off to a separately-named main-app process and exits. Our poll loop (fresh-launch branch) or process-enumeration (already-running branch) would catch that transient window, call FocusWindow on it, and then the window is destroyed when the launcher process exits — producing exactly a 'flicker that does not remain open', independent of whether SW_RESTORE fixed the minimize issue."
  H2_foreground_lock_flash: "Win32's documented fallback when SetForegroundWindow's foreground-lock restriction blocks the calling process: instead of truly foregrounding the window, Windows flashes the taskbar button of the target window. SW_RESTORE un-minimizing the window is independent of this restriction -- if SetForegroundWindow itself is still silently failing due to foreground-lock, the user could see a taskbar flash/blink rather than the window actually coming to front and staying there. This would explain a 'flicker' that 'does not remain open' even with the restore fix correctly applied."
  H3_monitor_disable_race: "ToggleService.ToggleToRigMode runs Monitor.Disable() BEFORE App.LaunchOrFocus() (stop-on-first-failure ordering, D-04 -- not changeable without violating documented architecture). If the Moza Companion window was sitting on the monitor being disabled, Windows' own topology-change reflow (WindowsMonitorController.Disable's repositioning-aware survivor reconstruction) could relocate/reflow that window at roughly the same time our FocusWindow call runs, causing a race where the window is briefly shown then Windows' own reflow logic minimizes/moves it again."

status: Cannot execute/build on this Linux sandbox to differentiate H1/H2/H3 -- need targeted diagnostic answers from the user (see next_action) before attempting a second fix. Do NOT guess-and-fix again without evidence; the first single-hypothesis fix already failed once (per debugger-philosophy and the checkpoint response's explicit instruction).

next_action: Ask the user targeted diagnostic questions (see CHECKPOINT REACHED return) to differentiate H1 (launcher stub) / H2 (foreground-lock flash) / H3 (monitor-disable race) before applying any further code change.

reasoning_checkpoint:
  hypothesis: "The 'already running' branch of LaunchOrFocus (WindowsAppController.cs lines 93-104) calls only NativeMethods.SetForegroundWindow(p.MainWindowHandle) and never calls ShowWindow(hWnd, SW_RESTORE)/checks IsIconic first. SetForegroundWindow activates a window but does NOT un-minimize it (this is documented Win32 behavior, not a foreground-lock permission failure) -- so when the Moza Companion window is minimized (which is exactly the state MinimizeIfRunning leaves it in after every toggle-back, via ShowWindow(hWnd, SW_MINIMIZE) at line 136), the next toggle-to-rig-mode's focus attempt activates the window's z-order/input focus internally but the window remains visually minimized in the taskbar -- matching the exact reported symptom 'stays minimized/in the taskbar -- it never appears', with no error since SetForegroundWindow can still return TRUE."
  confirming_evidence:
    - "WindowsAppController.cs 'already running' branch (lines 93-104): only call is NativeMethods.SetForegroundWindow(p.MainWindowHandle); no ShowWindow/IsIconic call anywhere in that branch."
    - "MinimizeIfRunning (lines 128-140) is the only thing that runs between toggles and it explicitly minimizes the companion window (ShowWindow(hWnd, SW_MINIMIZE)) -- so by the time the user toggles back to rig mode, the window is reliably in the minimized state that triggers the bug."
    - "Fresh-launch branch (lines 56-81) also only calls SetForegroundWindow, but a newly-created window from Process.Start is not minimized by default, so that path doesn't hit the same failure -- consistent with symptom report 'Fresh launches work fine; only the already running... path is broken.'"
    - "Web research (Microsoft Learn ShowWindow docs + corroborating community sources): SetForegroundWindow activates/focuses a window but does not restore a minimized window; the documented fix pattern is IsIconic() check -> ShowWindow(hWnd, SW_RESTORE) -> SetForegroundWindow(hWnd)."
  falsification_test: "If the companion window were NOT minimized when 'already running' focus is attempted (e.g. user manually restores it, or MinimizeIfRunning is never called), SetForegroundWindow alone should succeed in bringing it to front. If real-rig testing shows the bug still occurs on a non-minimized-but-background window, the foreground-lock-restriction hypothesis (originally logged in STATE.md) would need re-investigation as a secondary/compounding cause."
  fix_rationale: "Add an IsIconic(hWnd) check before SetForegroundWindow in the 'already running' branch; if minimized, call ShowWindow(hWnd, SW_RESTORE) first. This directly addresses the documented root cause (minimized windows aren't un-minimized by SetForegroundWindow) rather than a workaround for the speculative foreground-lock restriction (AttachThreadInput etc., which CLAUDE.md/STACK.md already forbids using)."
  blind_spots: "Cannot execute/build on this Linux sandbox to observe actual runtime behavior -- fix is verified by source reasoning + authoritative Win32 documentation only, not by running the app. The foreground-lock restriction may still exist as a secondary factor in some window-manager states (e.g. if user is actively interacting with another app at the exact toggle moment) -- SetForegroundWindow can still silently fail to bring the restored window fully to top in that edge case, though ShowWindow(SW_RESTORE) itself is not subject to the same restriction and will at minimum un-minimize the window so it's visible (partially addressing the symptom even in that edge case)."

hypothesis: SetForegroundWindow does not un-minimize a minimized window (documented Win32 behavior) -- the 'already running' focus branch never calls ShowWindow(SW_RESTORE)/checks IsIconic, so a companion window left minimized by the prior MinimizeIfRunning call stays minimized even after SetForegroundWindow is called on it.
next_action: Add IsIconic/ShowWindow(SW_RESTORE) P/Invoke declarations to NativeMethods.cs and call them before SetForegroundWindow in WindowsAppController.LaunchOrFocus's 'already running' branch.

## Evidence

- timestamp: 2026-07-25T00:00:00Z
  checked: src/RigToggle.Windows/WindowsAppController.cs LaunchOrFocus 'already running' branch (lines 93-104)
  found: Only NativeMethods.SetForegroundWindow(p.MainWindowHandle) is called; no ShowWindow/IsIconic call in this branch or anywhere else in the file except MinimizeIfRunning's ShowWindow(SW_MINIMIZE).
  implication: If the target window is minimized, SetForegroundWindow alone cannot un-minimize it per documented Win32 behavior -- this is the direct mechanism for the reported symptom.

- timestamp: 2026-07-25T00:00:01Z
  checked: src/RigToggle.Windows/WindowsAppController.cs MinimizeIfRunning (lines 115-148)
  found: On every toggle-back-to-desktop, the companion window is explicitly minimized via ShowWindow(hWnd, SW_MINIMIZE).
  implication: The companion window is reliably left in a minimized state before the next 'toggle to rig mode' action -- exactly the precondition that triggers the SetForegroundWindow-doesn't-restore bug.

- timestamp: 2026-07-25T00:00:02Z
  checked: src/RigToggle.Windows/NativeMethods.cs
  found: Declares only ShowWindow, SetForegroundWindow, and SW_MINIMIZE constant. No IsIconic import, no SW_RESTORE constant.
  implication: Fix requires adding IsIconic P/Invoke declaration and SW_RESTORE constant.

- timestamp: 2026-07-25T00:00:03Z
  checked: Web search (Microsoft Learn ShowWindow docs + corroborating sources) on "SetForegroundWindow does not restore minimized window"
  found: Confirmed documented pattern -- SetForegroundWindow activates/focuses but does not restore a minimized window; correct sequence is IsIconic() check -> ShowWindow(hWnd, SW_RESTORE) -> SetForegroundWindow(hWnd).
  implication: Root cause confirmed via authoritative external documentation, not just code reading. Fix direction is the standard, well-known workaround.

- timestamp: 2026-07-25T01:00:00Z
  checked: Rig test of applied fix (IsIconic + ShowWindow(SW_RESTORE) + SetForegroundWindow in WindowsAppController.FocusWindow, uncommitted in working tree)
  found: User report -- "No. The window does not open but I do see a flicker of something that may (but also not sure if it is) be moza software. But it just blinks and it does not remain open."
  implication: The minimized-window-not-restored mechanism, even if correctly fixed, is not sufficient to explain the current symptom. A working restore would keep the window visible, not produce a transient flicker. Points to a second/different mechanism: launcher-stub process (H1), SetForegroundWindow foreground-lock taskbar-flash fallback (H2, Win32-documented), or a race with WindowsMonitorController.Disable's topology reflow given App.LaunchOrFocus runs strictly after Monitor.Disable in ToggleService.ToggleToRigMode (H3).

- timestamp: 2026-07-25T01:00:01Z
  checked: src/RigToggle.Core/ToggleService.cs ToggleToRigMode step ordering (lines 82-125)
  found: Confirmed order is Monitor.Disable() -> Audio.SetDefault() -> App.LaunchOrFocus(), stop-on-first-failure (D-04), documented as intentional architecture (not to be changed casually).
  implication: If the Moza Companion window happens to be on the monitor being disabled, by the time LaunchOrFocus/FocusWindow runs, the display topology has already changed underneath it (H3) -- worth asking the user whether the flicker timing correlates with the monitor going dark.

- timestamp: 2026-07-25T02:00:00Z
  checked: User's 7 diagnostic answers (checkpoint response), cross-referenced against the
    current working-tree state of WindowsAppController.cs / NativeMethods.cs.
  found: |
    (1) "moza was running under the tray icon" -- tray-only before toggle.
    (2) flicker happens "when I switch to rig mode" -- confirms it's the toggle-to-rig path.
    (3) "no. moza is not opened it remains in the tray" -- after the flicker, state settles
        back to tray-only.
    (4) "yes. one click on the icon opens the window normally" -- Moza's own tray-click show
        logic is reliable; a legitimate show path exists.
    (5) "only toggle to rig" -- never reproduces on toggle-back (MinimizeIfRunning path).
    (6) "no" -- no correlation with monitor-disable timing.
    (7) "main moza app" -- the flickering entity is the main app itself, not a launcher/splash.
  implication: (6) eliminates H3 (monitor-disable race). (7) eliminates H1 (launcher stub).
    (5) is positive evidence for H2 (SetForegroundWindow is only ever called on the
    toggle-to-rig path -- MinimizeIfRunning never calls it, so it can't hit the same
    restriction). (1)+(3)+(4) describe a tray-only app whose own show mechanism works but
    don't by themselves distinguish "hidden window" vs "genuinely-iconic window with a
    suppressed taskbar button" -- resolved by the next entry.

- timestamp: 2026-07-25T02:00:01Z
  checked: Independently verified the orchestrator's suggested "hidden-not-iconic window"
    interpretation against WindowsAppController.cs's actual "already running" branch (lines
    93-112) before acting on it, per instructions. Also checked how Process.MainWindowHandle
    is implemented (web search: "The MainWindowHandle property is just a guess based on
    heuristics" -- The Old New Thing; corroborated by a second source describing the same
    GetWindow(hWnd, GW_OWNER)==0 && IsWindowVisible(hWnd) heuristic).
  found: The "already running" branch only calls FocusWindow(p.MainWindowHandle) inside an
    `if (p.MainWindowHandle != IntPtr.Zero)` guard. Process.MainWindowHandle's own internal
    heuristic requires IsWindowVisible(hWnd) == true (plus no owner) to return a non-zero
    handle at all. This means: if Moza's window were genuinely hidden (Visible=false, the
    classic WinForms "minimize-to-tray" recipe), MainWindowHandle would already be
    IntPtr.Zero before our code ever reaches FocusWindow -- the IsIconic gate inside
    FocusWindow would never even be reached, so "IsIconic skips ShowWindow, leaving bare
    SetForegroundWindow on an invisible handle" is not a code path that can execute in that
    state.
  implication: Since the user DID observe a flicker (meaning FocusWindow's SetForegroundWindow
    call DID execute on a real, non-zero handle), the window found at that moment must
    already have been IsWindowVisible==true per Win32 (regardless of whether it was also
    iconic/minimized) -- the orchestrator's suggested mechanism (IsIconic gate causing
    ShowWindow to be skipped on a hidden window) cannot be the operative cause here. This
    independently-verified inconsistency is why the fix applied below targets the
    SetForegroundWindow foreground-lock/flash-fallback mechanism (H2) instead of the
    IsWindowVisible-gating change the orchestrator's interpretation suggested.

- timestamp: 2026-07-25T02:00:02Z
  checked: Web search -- Win32 SetForegroundWindow documentation on the foreground-lock
    restriction and its flash-fallback behavior.
  found: Confirmed (learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow,
    corroborated by damirscorner.com and elevenforum.com): "An application cannot force a
    window to the foreground while the user is working with another window. Instead, Windows
    flashes the taskbar button of the window to notify the user." Success requires the
    foreground-lock timeout to have expired AND at least one of: calling process is the
    foreground process, calling process was started by the foreground process, no current
    foreground window, calling process received the last input event, or either process is
    being debugged.
  implication: This is a real, well-documented, non-speculative Win32 mechanism that produces
    exactly a "flash/flicker instead of true activation" -- matching the user's description
    far better than any theory requiring undocumented assumptions about Moza's internals.
    ToggleToRigMode's multi-step synchronous execution (Monitor.Disable, then
    Audio.SetDefault, both before App.LaunchOrFocus) gives real time/opportunity for
    RigToggle's foreground eligibility to lapse before FocusWindow's SetForegroundWindow
    call runs. Chosen as the primary hypothesis to act on.

- timestamp: 2026-07-25T03:00:00Z
  checked: Second checkpoint response -- rig test of the SetWindowPos TOPMOST/NOTOPMOST
    fix (H2 fix attempt).
  found: "Outcome: FAILED. User report: 'Window does not come up. Something flickers on
    the screen but I can't see what. A window of some app.' Follow-up question (does the
    flicker happen at the start of the toggle sequence, i.e. Monitor.Disable, or the end,
    i.e. App.LaunchOrFocus): user answered 'Right at the start.'"
  implication: Since App.LaunchOrFocus (and FocusWindow/SetForegroundWindow within it) runs
    LAST per D-04 ordering (Monitor.Disable -> Audio.SetDefault -> App.LaunchOrFocus), a
    flicker occurring "at the start" cannot be caused by FocusWindow. This invalidates the
    reasoning used to eliminate the hidden-window hypothesis in the prior session (that
    elimination assumed "user saw a flicker" implied "FocusWindow executed on a non-zero
    handle"). The flicker is very likely an unrelated artifact of Monitor.Disable's display
    topology change. H2 (foreground-lock flash) is now unsupported as an explanation for the
    flicker specifically, and by extension the SetWindowPos fix targeting it correctly
    produced no change in the reported symptom (window still doesn't come up).

- timestamp: 2026-07-25T03:00:01Z
  checked: Re-read src/RigToggle.Windows/WindowsAppController.cs and NativeMethods.cs
    (current working-tree state) to confirm exactly what the last two fix attempts left in
    place before reasoning further, per checkpoint response instruction.
  found: FocusWindow (lines 134-146) does IsIconic->ShowWindow(SW_RESTORE), then
    SetWindowPos(TOPMOST)->SetWindowPos(NOTOPMOST), then SetForegroundWindow. LaunchOrFocus's
    "already running" branch (lines 93-112) only calls FocusWindow when
    p.MainWindowHandle != IntPtr.Zero; if all matching processes have MainWindowHandle ==
    Zero, the loop completes with no action taken (silent no-op, matching D-06 exactly).
  implication: Confirms the code is unchanged from what's documented in Resolution/fix below
    prior to this session's new fix. Confirms the exact no-op path that D-06 specifies.

- timestamp: 2026-07-25T03:00:02Z
  checked: Re-read .planning/phases/03-app-audio-control/03-CONTEXT.md D-06 verbatim.
  found: |
    "Already running but MainWindowHandle is zero: do NOT retry/poll -- per CLAUDE.md,
    treat this as 'running but no window to manipulate right now' (e.g. genuinely
    tray-only) and move on without failing the toggle."
  implication: D-06 explicitly assumed MainWindowHandle==Zero means "no window to
    manipulate" (i.e., no window exists). It does not distinguish that case from "a window
    exists but Process.MainWindowHandle's IsWindowVisible-gated heuristic can't see it
    because it's hidden (Visible=false), not merely absent" -- exactly the common
    WinForms 'minimize/close to tray' pattern. Given the user confirms Moza is normally
    tray-only (Q1) and that neither of two independent FocusWindow-internal fixes changed
    anything, this is now the best-supported explanation: FocusWindow is never being
    reached at all in the repro case, because D-06's no-op fires first.

- timestamp: 2026-07-25T04:00:00Z
  checked: Third checkpoint response -- rig test of the FindHiddenMainWindow EnumWindows-by-PID
    fallback fix (H4 fix attempt), with a newly-refined, precise repro sequence.
  found: |
    "No it does not work... If I have moza closed and go to rig mode then it works. It
    starts the app and the window is opened. If I then close to tray, switch to normal and
    then back to rig mode then the window does not open." Sequence: (1) Moza closed -> toggle
    Rig -> WORKS (fresh Process.Start + poll path, confirmed fine, never the problem). (2)
    Moza running with window open -> user closes it to tray via Moza's OWN UI -> toggle
    Normal (runs MinimizeIfRunning) -> toggle Rig again -> FAILS despite all three fix layers
    (IsIconic/SW_RESTORE, SetWindowPos-toggle, EnumWindows-by-PID+IsWindowVisible/SW_SHOW)
    being in place.
  implication: Traced MinimizeIfRunning against this exact sequence: since the user closes
    Moza to tray via Moza's OWN UI BEFORE toggling to Normal mode, p.MainWindowHandle is
    already IntPtr.Zero by the time MinimizeIfRunning runs -- its zero-handle guard means
    ShowWindow(SW_MINIMIZE) is never called (MinimizeIfRunning has no FindHiddenMainWindow
    fallback). This rules out "our own MinimizeIfRunning call triggers/compounds the hidden
    state" for this specific repro -- the failure is entirely within
    FindHiddenMainWindow/FocusWindow being exercised against a window Moza already hid via
    its own logic. Since the EnumWindows-by-PID fallback (which should find ANY top-level
    window for the PID, hidden or not) still produced no visible result, either (a) it finds
    zero candidates (Moza destroys/recreates its form rather than hiding it), or (b) it finds
    A window but the WRONG one (e.g. a same-process helper window that happens to also be
    owner-less and top-level, such as NotifyIcon's internal message window) -- both
    indistinguishable from "nothing happened" without direct instrumentation. Led to H5 and
    the decision to add real diagnostic logging rather than a fifth blind guess.

- timestamp: 2026-07-25T05:00:00Z
  checked: Fourth checkpoint response -- real diagnostic log from %LOCALAPPDATA%\RigToggle\debug.log
    (first actual Win32-level runtime evidence obtained this investigation, not just user-described
    symptoms), plus a re-read of WindowsAppController.cs's already-running loop and a repo-wide
    grep confirming MainModule.FileName / full-path process filtering is never used anywhere in
    this codebase (matching is 100% base-name-only via Path.GetFileNameWithoutExtension).
  found: |
    Log (verbatim): PID=31616 MainWindowHandle=0x0, FindHiddenMainWindow(PID=31616) => 0x0
    (zero owner-less top-level windows found for that PID at all -- not a visibility artifact,
    since EnumWindows enumerates regardless of visibility). PID=34792 MainWindowHandle=0x1D7036C
    (non-zero directly), IsIconic=True. FocusWindow's full sequence against 0x1D7036C succeeded
    at every Win32 step: ShowWindow(SW_RESTORE)=True, SetForegroundWindow=True,
    GetForegroundWindow() after == target hWnd (match=True). User's visual report: "it does grab
    a moza window but some kind of toggle curve window I can't close then" -- explicitly not the
    main Companion dashboard.
  implication: Two structurally new findings. (1) The code has ZERO Win32-level failures for
    this window -- H2/H4/H5's entire target layer (foreground-lock flash, hidden/minimized
    window restoration) is now confirmed fully WORKING as designed; there is nothing left to fix
    in FocusWindow itself. (2) Process.GetProcessesByName(processName) matched TWO distinct PIDs
    simultaneously, and LaunchOrFocus's foreach-with-break logic focuses whichever one happens to
    have any window first, with no verification that it is the correct/main one. This means the
    entire H1-H5 investigation thread (all scoped to "how do we correctly find/show/activate the
    Companion window") was solving the wrong layer for this specific repro: the true remaining
    problem is PROCESS SELECTION among multiple same-named processes, not window
    visibility/state manipulation. Confirmed via repo-wide grep that no existing code anywhere
    filters matched processes by full executable path (MainModule.FileName) against the
    configured CompanionAppPath -- matching is purely by truncated base name, so two physically
    different .exe files sharing a file name (or one exe self-relaunching under a different role)
    would be indistinguishable to this code today.

- timestamp: 2026-07-26T10:00:00Z
  checked: Fifth checkpoint response -- rig test of the H7 fix (FindBestMainWindow always-run
    + largest-titled-restored-area selection, applied to LaunchOrFocus's already-running
    branch and MinimizeIfRunning).
  found: |
    "Yes it works now. The main window opens but we still have a small bug. You can't close
    it now. It's open and the x does nothing." Follow-up (does this happen only after Rig
    Toggle opens the window, or also when the user opens Moza normally themselves): "Only
    after Rig Toggle opens it."
  implication: H7 CONFIRMED as the fix for the original bug (window-selection-among-
    multiple-top-level-windows) -- this is the first fix in this 5-round session to be
    rig-verified working. A NEW, distinct regression is confirmed genuinely caused by
    RigToggle's own code path (not baseline Moza behavior, since it never reproduces on
    manual opens) -- tracked as H8. See reasoning_checkpoint_6 for the disabled-window
    hypothesis this evidence supports.

- timestamp: 2026-07-26T10:00:01Z
  checked: Re-read src/RigToggle.Windows/WindowsAppController.cs (FocusWindow, lines
    290-320) and src/RigToggle.Windows/NativeMethods.cs (full file, current working-tree
    state) specifically for any EnableWindow call or any documented side effect of
    ShowWindow/SetForegroundWindow/SetWindowPos on a window's WS_DISABLED style, per
    checkpoint instructions (prime suspects #1 and #2).
  found: No EnableWindow call exists anywhere in either file. ShowWindow (SW_SHOW/
    SW_RESTORE), SetForegroundWindow, and SetWindowPos (with SWP_NOACTIVATE, Z-order only)
    are all well-documented (Microsoft Learn, consistent with this session's prior H2/H4 web
    research) to have no effect on a window's enabled/disabled state -- enabling/disabling is
    exclusively controlled by EnableWindow/WS_DISABLED, a completely orthogonal Win32
    concept from visibility, Z-order, iconic state, or foreground/activation state.
  implication: Prime suspects #1 (TOPMOST/NOTOPMOST trick) and #2 (some P/Invoke call
    inadvertently disabling the window) are both unsupported by the code as written -- no
    call in this codebase can disable a window, and none of the calls actually made are
    documented to have that side effect. This shifts weight toward the window having
    ALREADY been disabled by Moza's own logic (H8) before FocusWindow ever touches it, which
    would be entirely consistent with an app-level soft-modal pattern tied to the
    concurrently-open Torque Curve window (already confirmed to exist on the same process
    per H7's evidence).

- timestamp: 2026-07-26T10:00:02Z
  checked: Added diagnostic-only instrumentation (no behavior change) to
    src/RigToggle.Windows/NativeMethods.cs (IsWindowEnabled, GetClassName P/Invoke
    declarations) and src/RigToggle.Windows/WindowsAppController.cs (logs enabled= state in
    FindBestMainWindow's per-candidate scan and in FocusWindow's before/after state dump;
    logs each candidate's window class name alongside its existing title/rect/area/iconic
    fields).
  found: N/A -- code change, not yet rig-tested. Cannot execute/build on this Linux sandbox.
  implication: Next rig test's debug.log will directly show whether the target window's
    enabled= state is False (confirming H8) or True (falsifying H8 and pointing toward a
    Moza-internal message-handling mechanism outside RigToggle's control), and will show
    window class names for every FindBestMainWindow candidate (directly checkable against
    prime suspect #3, the wrong-lookalike-window theory) without requiring another blind
    guess.

- timestamp: 2026-07-26T11:00:00Z
  checked: Sixth checkpoint response -- answers to the H8 clarifying questions (repro-detail
    only; no fresh debug.log was captured/pasted this round). Cross-referenced against
    Win32 EnableWindow/WS_DISABLED documentation and a re-read of WindowsAppController.cs
    FocusWindow (lines 314-322) for exactly which raw Win32 call executes in the tray-hidden
    steady state.
  found: |
    Q2: "no" -- Torque Curve was NOT open at the same time as the dashboard.
    Q3: "no. everything works. you can minimize but not close." (in response to "is the
        entire window frozen, or only the X specifically?").
    Q4: "Alt and f4 does not close it. close window does not close it. You can only kill in
        task manager." (Alt+F4 and taskbar right-click -> "Close window" both fail, same as
        the title-bar X.)
  implication: (Q2) directly eliminates H8-as-originally-framed (Torque-Curve-triggered
    EnableWindow), since the hypothesized disabling trigger was never present. (Q3+Q4) show
    minimize succeeds while THREE independent close-request mechanisms (X, Alt+F4, taskbar
    Close) all fail identically -- a pattern that does not match a literal WS_DISABLED
    window (which is documented to block ALL title-bar/input interaction, not selectively
    allow minimize while blocking close) but matches exactly one shared downstream handler
    (WM_CLOSE, reached identically from all three close-request input paths via
    WM_SYSCOMMAND(SC_CLOSE)) misbehaving inside Moza's own process. Reframed as H9 (see
    reasoning_checkpoint_7): a Moza-side FormClosing/close-to-tray handler no-oping due to
    Form.Visible/native-visibility state desync caused by RigToggle's raw
    ShowWindow(SW_SHOW) P/Invoke call (confirmed, via code re-read, to be the one call in
    FocusWindow that executes specifically when the window was found tray-hidden --
    Visible=false -- the state Moza has consistently been reported sitting in between
    toggles). Genuinely uncertain between H9 (likely unfixable from RigToggle) and a
    residual non-Torque-Curve-triggered variant of H8 (fixable via defensive
    EnableWindow(hWnd, TRUE)) without either a fresh debug.log's enabled= reading or the
    visual-reaction clarifying question -- both requested via checkpoint rather than
    guessing a sixth fix.

- timestamp: 2026-07-26T12:00:00Z
  checked: Seventh checkpoint response -- user reported a NEW, more severe symptom
    ("opening the app is not working properly... closed the moza app and the app does
    not start if fully closed. Rig toggle app shows moza companion running even though
    it's not. Task manager does not have a moza process running.") plus a pasted
    debug.log excerpt spanning two toggle attempts (13:24:33-13:25:32 and
    13:35:46-13:36:05). Cross-referenced against src/RigToggle.Windows/WindowsAppController.cs
    (full re-read, LaunchOrFocus + IsRunning + FindBestMainWindow) and
    .planning/phases/03-app-audio-control/03-CONTEXT.md D-06 (full re-read).
  found: |
    Log excerpt shows, at 13:25:15: "LaunchOrFocus already-running branch: 3 process(es)
    matched 'MOZA Pit House'" -- PID=14736 (FindBestMainWindow => 0x0), PID=33456
    (=> 0x0), PID=14936 (=> 0xA0FEE, a real 1456x849 titled window, successfully
    focused: ShowWindow/SetForegroundWindow both succeeded, GetForegroundWindow()
    matched). Ten minutes later at 13:35:46 (after the user closed the app via its own
    UI): "LaunchOrFocus already-running branch: 2 process(es) matched 'MOZA Pit House'"
    -- PID=14736 (SAME PID as ten minutes earlier, still present, => 0x0) and PID=24756
    (a NEW PID not seen in the first snapshot, => 0x0). BOTH processes in this second
    snapshot are windowless (FindBestMainWindow's EnumWindows-by-PID scan, which finds
    ALL owner-less top-level windows regardless of visibility -- not just
    IsWindowVisible==true ones -- returned 0x0 for both, meaning genuinely ZERO
    top-level windows of any kind exist for either PID, not merely a hidden one). A
    third block at 13:36:05 (~19s after the failed already-running attempt, matching
    MinimizeIfRunning's log signature exactly -- no "N process(es) matched" line and no
    "PID=... FindBestMainWindow =>" lines, since only LaunchOrFocus's loop logs those --
    just two bare "FindBestMainWindow result: 0x0" lines) shows the same two PIDs still
    windowless when the user then toggled back to Normal mode. Code re-read confirms
    Process.GetProcessesByName(processName) is an EXACT (case-insensitive) match on the
    process image name, not a substring/prefix match -- so all 3, then 2, processes
    genuinely and literally share the identical process name "MOZA Pit House". The
    QTrayIconMessageWindow class name visible in the earlier debug.log (see
    reasoning_checkpoint_6 evidence) confirms Moza Companion is Qt-based -- Qt/Chromium-
    style multi-process-single-binary architectures (main UI process + background
    service/helper/watchdog processes, all literally sharing one executable's image
    name) are a well-known, common pattern in that ecosystem. Re-read of D-06
    (03-CONTEXT.md, verbatim): "Already running but MainWindowHandle is zero: do NOT
    retry/poll... treat this as 'running but no window to manipulate right now' (e.g.
    genuinely tray-only) and move on without failing the toggle." Critically, D-06 was
    authored on 2026-07-24, BEFORE this session's H4/H5/H7 fixes replaced the original
    Process.MainWindowHandle-based lookup (IsWindowVisible-gated, so it legitimately
    could return Zero for a real, tray-hidden-but-existing window) with
    FindBestMainWindow's EnumWindows-by-PID scan, which explicitly does NOT filter by
    IsWindowVisible and finds a window "regardless of visibility" (per its own doc
    comment, lines 169-199). Confirmed via code re-read: LaunchOrFocus's already-running
    branch, as currently written, has NO code path that ever calls Process.Start once
    IsRunning() has returned true even a single time -- if FindBestMainWindow returns
    0x0 for every matched process, the foreach loop simply completes and the method
    returns with zero action taken, permanently, with no retry and no fallback.
  implication: This is a distinct, more severe root cause than H9 (close-button no-op,
    which only affects an already-opened window) -- tracked as H10. Under the CURRENT
    code (post-H7), FindBestMainWindow returning 0x0 for every matched process can no
    longer mean "a real, tray-hidden instance exists but we can't see its window" (that
    case is now correctly found by the visibility-independent EnumWindows scan) -- it
    can now only mean "none of the matched processes has ANY top-level window at all,"
    i.e. either (a) a real UI instance is still starting up and hasn't created its
    window yet (transient), or (b) every matched process is a headless helper/service
    process with no UI, and no real UI-bearing instance is running (this is exactly what
    "app does not start if fully closed" describes: the user closes the real UI
    instance, one or more same-named headless helper processes persist -- PID=14736
    demonstrably persisted across a 10-minute span covering both an "app open" and an
    "app closed" snapshot, ruling out "still in the middle of exiting" as its
    explanation -- IsRunning() keeps returning true because of those helpers, the
    already-running branch fires every time, finds no window, and D-06's "move on
    without failing" behavior means Process.Start is NEVER reached again). D-06's
    original rationale ("retrying would add a pointless delay for an app that may never
    produce a window") was written for case (a)-and-legitimate-tray-hidden-(b) under the
    OLD MainWindowHandle-based lookup; it does not anticipate a same-named, permanently
    headless helper process making the "already running" conclusion permanently true
    while no real UI instance exists -- this is a genuine gap in the current code, not
    merely a re-application of an already-considered and rejected idea. The Task
    Manager-shows-zero-processes vs log-shows-2-processes-matched discrepancy the user
    separately reported is most parsimoniously explained by ordinary timing (the log
    snapshot was taken at the moment of the toggle click; by the time the user
    separately opened Task Manager afterward, whatever those 2 processes were had since
    fully exited) rather than a distinct bug in RigToggle's own matching logic --
    IsRunning()/LaunchOrFocus call Process.GetProcessesByName fresh every time with no
    caching (confirmed via code read), so there is no mechanism by which RigToggle could
    report stale/cached process state independent of what's actually running at call
    time. This specific point (why exactly 2 processes existed at 13:35:46 and whether
    they are a legitimate Moza-side background service or a shutdown-cleanup artifact)
    remains not fully confirmed and is called out as a residual blind spot below, but
    does not block fixing the confirmed code-level gap (permanent no-op once
    already-running is true and no window is ever found).

- timestamp: 2026-07-26T13:00:00Z
  checked: Eighth checkpoint response -- rig test of the H10 fix (bounded ~3s poll +
    fresh-launch fallback in LaunchOrFocus's already-running branch), plus a recurrence of
    H9 (close button) and a new complaint ("Rig toggle still says moza is running but task
    manager says it's not"). Cross-referenced against src/RigToggle.Windows/
    WindowsAppController.cs (LaunchOrFocus, IsRunning, FocusWindow -- full re-read) and
    src/RigToggle.App/MainForm.cs (RefreshUi -- full re-read, first time this session).
  found: |
    Debug.log excerpt (verbatim, this round) shows the H10 bounded-poll loop running
    exactly as designed: at 13:56:23.960, "LaunchOrFocus already-running branch: 2
    process(es) matched" with both PID=14736 and PID=24756 returning FindBestMainWindow =>
    0x0 on every iteration through 13:56:26.817 (~2.86s of polling, consistent with the
    3s AlreadyRunningWindowPollTimeout constant); at 13:56:27.076, "No window found for any
    of the 2 matched process(es) after polling for 3s -- falling back to a fresh launch."
    By 13:56:41.211-41.248 (~14s later, LaunchFreshAndFocus's own poll loop, separate from
    the already-running poll), FindBestMainWindow's candidate scan finds a real titled
    window (hWnd=0x2113EC, title='MOZA Pit House 1.3.9.35 release', normalRect=1456x849,
    area=1236144, iconic=False, enabled=True) and selects it as the largest titled
    candidate. A second toggle-to-rig at 13:57:21.202 ("4 process(es) matched") finds the
    same window (now iconic=True, i.e. minimized by the intervening toggle-to-normal's
    MinimizeIfRunning call) via the already-running branch's normal first-pass scan (no
    polling needed this time -- PID=24508 resolved directly to 0x2113EC on the first
    Refresh()), and FocusWindow's before/after dump shows: before: visible=True
    iconic=True enabled=True foreground=0xAC0CEE; ShowWindow(SW_RESTORE) returned True;
    after: SetForegroundWindow returned True, visible=True enabled=True
    foreground=0x2113EC (match=True). User confirmed the window then failed to close (X
    button) again -- reproducing H9 -- but did not answer the previously-requested
    visual-reaction (flicker vs fully inert) question this round either.
    Re-read of MainForm.cs RefreshUi() (lines 51-63) confirms the companion status label
    (lblCompanionStatus.Text) is derived by calling _appController.IsRunning(settings.
    CompanionAppPath) fresh, every time RefreshUi() runs (OnLoad, after every toggle,
    after Settings dialog close) -- no cached/stored boolean anywhere in MainForm. Re-read
    of WindowsAppController.IsRunning() (lines 55-85) confirms it calls
    Process.GetProcessesByName(processName) fresh on every call (same processName
    derivation as LaunchOrFocus) and disposes every Process handle in a finally block --
    no caching or stale-state mechanism exists anywhere in this call chain.
  implication: (1) H10's fix mechanism is directly demonstrated working end-to-end on this
    round's own log: bounded poll correctly detected a genuinely windowless state, fell
    back to a fresh launch, and the app became reachable again -- this is the FIRST
    concrete rig confirmation of H10 (not yet an explicit "yes it works" from the user in
    prose, but the log is unambiguous). (2) The "still says running / Task Manager says
    not" complaint cannot be explained by any RigToggle-side caching or divergent
    process-matching code path (both status label and toggle logic use the identical
    fresh Process.GetProcessesByName call) -- it is best explained by the SAME persistent
    windowless-helper-process structural fact already established in the prior round's
    evidence (PID=14736 recurs identically in this round's own log too, both in the
    2-process and 4-process snapshots), which the user's casual Task Manager glance likely
    did not surface as a recognizable "Moza" entry since it has no window/app-list
    presence. (3) window 0x2113EC's enabled=True reading both immediately before (13:57:
    21.215) and immediately after (13:57:21.669) FocusWindow's full sequence directly
    satisfies reasoning_checkpoint_7's own stated falsification condition for the residual
    WS_DISABLED variant of H8 ("if the log's enabled= field on the target window reads
    False either before or after FocusWindow's sequence, H9 is FALSIFIABLE and a residual
    variant of H8 is confirmed instead") -- since it reads True both times, that residual
    H8 variant is now formally eliminated, leaving H9 as the sole live hypothesis for the
    close-button regression, still short only the visual-reaction answer needed to fully
    confirm its specific mechanism.

- timestamp: 2026-07-26T14:00:00Z
  checked: Ninth checkpoint response -- answers to both outstanding eighth-round
    questions: (1) H9 visual-reaction = "completely inert"; (2) Task Manager Details-tab
    Command Line/Path/Status/Memory data for all 5 processes matching "MOZA Pit House".
    Cross-referenced against src/RigToggle.Windows/WindowsAppController.cs
    (LaunchFreshAndFocus, full re-read) and src/RigToggle.App/SettingsForm.cs
    (BtnBrowse_Click / CompanionAppPath persistence, full re-read, first time this
    session) to evaluate the orchestrator's suggested launch-target theory.
  found: |
    Task Manager data (verbatim): PID=14736/24756/13244 = "MOZA Pit House.exe", Suspended,
    0 K memory, Path=C:\Program Files (x86)\MOZA Pit House\bin\MOZA Pit House.exe.
    PID=23492 = same name, Running, 0 K, Path=C:\Program Files (x86)\MOZA Pit
    House\MOZA Pit House.exe (ROOT folder, no \bin\). PID=31048 = same name, Running,
    10 K, Path=...\bin\MOZA Pit House.exe. All 32-bit, UAC virtualization Disabled,
    identical quoted command line = bare path (no launch arguments on any of them).
    Code re-read (SettingsForm.cs BtnBrowse_Click, WindowsAppController.cs
    LaunchFreshAndFocus): CompanionAppPath is set entirely by the user via a standard
    OpenFileDialog (*.exe filter) in Settings -- no code anywhere in this repo references
    a "\bin\" path, transforms the configured path, or has any concept of "root vs bin"
    exe. LaunchFreshAndFocus calls Process.Start(companionAppPath) with the raw
    configured path and no arguments, exactly once per fresh-launch invocation.
  implication: The actual configured CompanionAppPath value (root vs \bin\) remains
    unknown from this sandbox, but is judged NOT to matter for this investigation:
    Evidence 2026-07-26T13:00:00Z (already in this file) already shows a single
    Process.Start(companionAppPath) call -- from THIS session's own confirmed rig test --
    producing a fully correct, working, correctly-titled dashboard window
    ('MOZA Pit House 1.3.9.35 release', 1456x849), directly contradicting the premise
    that the currently-configured launch target is broken or produces a non-functional
    child-only process. The 5-process Task Manager snapshot (1 root + 4 \bin\, 3 of the
    4 \bin\ ones Suspended with near-zero working set) is best explained as a
    Moza-internal multi-process architecture detail (consistent with the
    already-established Qt-based, QTrayIconMessageWindow evidence from earlier rounds --
    Qt/multi-process apps commonly use same-named helper/worker child processes) that
    Windows' own memory manager trims toward a near-zero working set once idle, not a
    RigToggle-caused resource leak -- RigToggle's own code issues exactly one
    Process.Start call per launch and has no code path capable of spawning or
    referencing the \bin\ exe as a distinct entity. See reasoning_checkpoint_11 for the
    full reasoning and explicit decision not to request the CompanionAppPath value as
    blocking. Separately, "completely inert" (H9 visual-reaction) is evaluated against
    reasoning_checkpoint_7's own pre-registered falsification test and narrows the
    close-button regression to H9b (system-menu SC_CLOSE command disabled) rather than
    H9's original FormClosing-cancel-and-hide framing -- see reasoning_checkpoint_10.
    Diagnostic-only instrumentation (GetSystemMenu/GetMenuState, logged as a new
    closeGrayed= field) was added to FocusWindow's before/after log lines to test H9b
    directly on the next rig test; no behavior-changing fix was applied without that
    evidence, per this session's established discipline.

- timestamp: 2026-07-26T15:00:00Z
  checked: Tenth checkpoint response -- fresh debug.log excerpt (verbatim) covering a
    FocusWindow call on window 0xB40E44 (title='MOZA Pit House 1.3.9.35 release',
    iconic=True->restored, i.e. the already-running/minimized branch, not a fresh
    launch), specifically requested to read the new closeGrayed= diagnostic field added
    in reasoning_checkpoint_10 to test H9b directly.
  found: |
    "FocusWindow(0xB40E44) before: visible=True iconic=False enabled=True
    closeGrayed=False foreground=0xA10CC" and "FocusWindow(0xB40E44) after:
    SetForegroundWindow returned True, visible=True enabled=True closeGrayed=False
    foreground=0xB40E44 (match=True)". closeGrayed=False in BOTH the before and after
    reads. Separately, the process count matched by "MOZA Pit House" in this round's log
    is now 5 (PIDs 14736/24756/13244/24104 all windowless, plus the real UI-bearing
    process 13156) -- one more windowless PID (24104) than the previous round's 3,
    consistent with the same-named-headless-helper structural fact (H10 investigation)
    continuing to accumulate by exactly one per fresh-launch fallback cycle across this
    long session. No explicit prose sentence this round states "I retried the close and
    it failed again" -- the checkpoint response is the raw log paste only. However,
    reasoning_checkpoint_10's own next_action explicitly instructed the user to capture
    this exact log "the next time the close (X) button fails... click X; observe it does
    nothing as before; capture and share the fresh debug.log" -- providing precisely the
    requested closeGrayed= data implies the requested repro (click X, observe no
    reaction) was performed as instructed, even without a separate confirming sentence.
  implication: closeGrayed=False both before and after FocusWindow directly matches
    reasoning_checkpoint_10's own pre-registered falsification condition ("if
    closeGrayed=False ... both before and after FocusWindow's sequence, H9b is
    FALSIFIED"). H9b (system-menu SC_CLOSE command disabled) is formally eliminated --
    see Eliminated below. Combined with the already-eliminated WS_DISABLED variant of H8
    and the already-eliminated FormClosing-cancel-and-hide framing of H9, all three
    Win32-state mechanisms this session could test via read-only P/Invoke queries
    (enabled=, closeGrayed=, visible=/iconic=) have now been ruled out for every close
    attempt tested. Per reasoning_checkpoint_10's own falsification_test text, the
    remaining candidate mechanism is Moza subclassing WM_NCHITTEST/WM_NCLBUTTONDOWN (or
    filtering WM_SYSCOMMAND) directly in its own window procedure -- an interception that
    happens before any state a read-only Win32 query (GetWindowLong/IsWindowEnabled/
    GetMenuState/GetForegroundWindow) can observe, and that this codebase has no
    passive/read-only way to detect. The zombie-process count (now 5, up by 1 again) is
    treated as the same already-judged-out-of-scope structural fact from
    reasoning_checkpoint_11 (Moza-internal multi-process architecture), not re-opened as
    a new question this round.

## Eliminated

- hypothesis: "H9 as specifically framed: a Moza-side FormClosing/close-to-tray handler
    runs, attempts its own Hide()/revert action, and that action itself no-ops or fails
    (implying at least some visible attempt/repaint before settling back to visible)."
  evidence: Ninth checkpoint response confirms the close-button failure is "completely
    inert" -- zero visual reaction of any kind on X-click. Per
    reasoning_checkpoint_7's own pre-registered falsification_test branch (b), this rules
    out a mechanism that requires any visible attempted action (FormClosing running and
    triggering some hide/repaint sequence before reverting) in favor of an even earlier
    interception that prevents any app-level handling from ever running at all. Refined
    to H9b (system-menu SC_CLOSE command disabled) -- see reasoning_checkpoint_10. The
    broader "one shared WM_CLOSE-adjacent mechanism explains all three failing close
    paths while minimize is unaffected" observation is NOT eliminated and continues to
    stand; only the specific FormClosing-runs-then-reverts framing is ruled out.
  timestamp: 2026-07-26T14:00:01Z

- hypothesis: "SetForegroundWindow-doesn't-restore-a-minimized-window is the COMPLETE and ONLY explanation for the symptom."
  evidence: Fix (IsIconic -> ShowWindow(SW_RESTORE) -> SetForegroundWindow) was applied and rig-tested; user still does not see the window remain open -- only a brief flicker. A correctly-working restore-then-foreground sequence would leave the window visible and focused, not flicker and vanish. The underlying mechanism may still be a real, partial contributor (it was confirmed via Win32 docs and is still believed correct as far as it goes), but it does not fully explain current behavior -- an additional mechanism (launcher stub / foreground-lock taskbar-flash / monitor-disable race) must also be present.
  timestamp: 2026-07-25T01:00:02Z

- hypothesis: "H1: launcher/bootstrapper stub process shows a transient window that gets destroyed when it hands off to the main app."
  evidence: User confirmed (Q7) the flickering entity is "main moza app", not a launcher/splash.
  timestamp: 2026-07-25T02:00:03Z

- hypothesis: "H3: race between WindowsMonitorController.Disable's topology reflow and the FocusWindow call."
  evidence: User confirmed (Q6) "no" correlation between the flicker and monitor-disable timing.
  timestamp: 2026-07-25T02:00:04Z

- hypothesis: "IsIconic gate in FocusWindow skips ShowWindow(SW_RESTORE) for a hidden-not-iconic window, leaving a bare SetForegroundWindow call on an invisible handle as the cause of the flicker (orchestrator's suggested interpretation)."
  evidence: Independently verified against WindowsAppController.cs -- FocusWindow is only ever reached when Process.MainWindowHandle is non-zero, and MainWindowHandle's own internal heuristic already requires IsWindowVisible==true to return non-zero. A genuinely hidden window could never produce a non-zero MainWindowHandle in the first place, so this code path is unreachable in that state -- yet the user did observe FocusWindow's effects (the flicker). This rules out the orchestrator's suggested mechanism as the operative cause, though the underlying IsIconic/ShowWindow(SW_RESTORE) fix itself remains correct and is kept (still needed for genuinely-minimized-but-visible windows).
  timestamp: 2026-07-25T02:00:05Z

- hypothesis: "The 2026-07-25T02:00:05Z elimination of the 'hidden-not-iconic window'
    mechanism (grounds: 'user DID observe a flicker, meaning FocusWindow's
    SetForegroundWindow call DID execute on a real, non-zero handle')."
  evidence: New checkpoint response establishes the flicker happens "right at the start"
    of the toggle sequence, which per D-04 ordering (Monitor.Disable -> Audio.SetDefault ->
    App.LaunchOrFocus) is temporally BEFORE FocusWindow could possibly run. The flicker is
    therefore not evidence that FocusWindow executed at all, and the premise used for this
    elimination is invalid. The hidden-window hypothesis (renamed H4) is reinstated as the
    leading hypothesis.
  timestamp: 2026-07-25T03:00:03Z

- hypothesis: "H5: FindHiddenMainWindow's first-owner-less-match heuristic grabs
    System.Windows.Forms.NotifyIcon's own always-present, invisible, owner-less helper
    window belonging to the SAME process as Moza's real main form, instead of the real form."
  evidence: Real diagnostic log (Evidence 2026-07-25T05:00:00Z) shows the window that was
    actually focused (0x1D7036C, PID=34792) was found via Process.MainWindowHandle DIRECTLY
    (non-zero on first read, never routed through FindHiddenMainWindow's EnumWindows fallback
    at all) and was a genuinely iconic (minimized), real, focusable window -- not an invisible
    zero-size helper window. FindHiddenMainWindow's scoring logic was only exercised against
    PID=31616, where it correctly found NOTHING (zero owner-less top-level windows exist for
    that PID), not a wrong candidate. The premise of H5 (a same-process helper window winning
    out over the real form within one process's EnumWindows scan) is not what happened here --
    the actual mismatch is at the PROCESS level (two different PIDs matched the same base
    name), not within a single process's window enumeration. FindHiddenMainWindow's title/size
    scoring logic is not proven wrong and is kept (harmless, may still help in other states),
    but it is not the mechanism explaining this repro's failure.
  timestamp: 2026-07-25T05:00:01Z

- hypothesis: "H6 sub-scenarios as originally framed: PID=31616 and PID=34792 are two
    DISTINCT UI-bearing instances of the Companion app (either the same physical .exe
    self-relaunched under different roles/args, or two different .exe files sharing a base
    name), and the fix must discriminate between them via MainModule.FileName/command-line
    filtering."
  evidence: User's direct clarification (checkpoint response, this session) that the Torque
    Curve window and the real main dashboard belong to the SAME process (PID 34792) resolves
    the ambiguity differently than either H6 sub-scenario: there are not two competing
    UI-bearing processes to discriminate between via path/command-line filtering. PID=31616
    (zero top-level windows, confirmed via the full EnumWindows scan, not merely a visibility
    artifact) is a red herring for the "which window" question -- likely an unrelated
    background/service process with no UI. The real mechanism is Process.MainWindowHandle
    picking the wrong window AMONG MULTIPLE WINDOWS OF ONE PROCESS (renamed H7), not
    process-identity ambiguity across multiple processes. MainModule.FileName filtering,
    which H6 would have required, would not have helped here at all.
  timestamp: 2026-07-26T09:00:00Z

- hypothesis: "H8 prime suspect #1: the SetWindowPos(HWND_TOPMOST) -> SetWindowPos(HWND_NOTOPMOST)
    foreground-lock-bypass trick (H2's fix) leaves the window in a state where its close
    button becomes non-functional, e.g. via a timing issue where NOTOPMOST doesn't
    correctly execute or take effect."
  evidence: Code re-read (WindowsAppController.cs FocusWindow, NativeMethods.cs) confirms
    both SetWindowPos calls use SWP_NOACTIVATE and only affect Z-order (position/size/
    activation explicitly excluded via SWP_NOMOVE/SWP_NOSIZE/SWP_NOACTIVATE) -- Z-order and
    topmost state are Win32 concepts entirely orthogonal to WS_DISABLED/enabled state, and
    no documented or plausible mechanism connects "stuck topmost" to "close button
    non-functional". Being stuck topmost would make a window annoyingly always-on-top, not
    input-unresponsive. Kept as a theoretically-possible but unsupported mechanism; not
    fully closed off without a rig-confirmed enabled=True reading, but de-prioritized below
    H8 (disabled window).
  timestamp: 2026-07-26T10:00:03Z

- hypothesis: "H8 prime suspect #2: one of the P/Invoke calls added across fix rounds
    (ShowWindow, SetForegroundWindow, SetWindowPos, IsWindowVisible, IsIconic, EnumWindows,
    GetWindowPlacement) inadvertently puts the window into a disabled/non-interactive state,
    e.g. via unintended flags or a missing EnableWindow call that should be there."
  evidence: Full re-read of both src/RigToggle.Windows/WindowsAppController.cs and
    NativeMethods.cs confirms EnableWindow is never called anywhere in this codebase, and
    every P/Invoke actually used (ShowWindow, SetForegroundWindow, SetWindowPos,
    IsWindowVisible, IsIconic, EnumWindows, GetWindowThreadProcessId, GetWindow,
    GetWindowTextLength/GetWindowText, GetWindowRect, GetWindowPlacement,
    GetForegroundWindow) is documented (Microsoft Learn) to have no effect on a window's
    WS_DISABLED style -- enabled/disabled state is exclusively controlled by
    EnableWindow, which this codebase never invokes. No mechanism in the current code can
    disable a window.
  timestamp: 2026-07-26T10:00:04Z

- hypothesis: "H8 as originally framed: Moza implements an app-level 'soft-modal' pattern
    where opening its 'Torque Curve' utility window disables the main dashboard via
    EnableWindow(dashboardHwnd, FALSE), and the close (X) button's failure is a direct
    result of that disabled state being present at the moment FocusWindow brings the
    dashboard to the foreground."
  evidence: Checkpoint response (this session) directly confirms the Torque Curve window
    was NOT open at the same time as the dashboard when the close-button bug was observed
    -- H8's hypothesized triggering condition (Torque Curve open, disabling the dashboard
    via app-level EnableWindow) was never present. Additionally, the same checkpoint
    response shows minimize works normally while all three independent close mechanisms
    (X, Alt+F4, taskbar "Close window") fail identically -- a pattern a literal
    WS_DISABLED window does not naturally produce (WS_DISABLED blocks ALL title-bar
    input, not close-specifically) but that a shared WM_CLOSE-handler-level mechanism
    does. Superseded by H9 (reasoning_checkpoint_7): a Moza-side FormClosing/close-to-tray
    handler no-oping due to Form.Visible/native-visibility desync caused by RigToggle's
    raw ShowWindow(SW_SHOW) call, OR (kept open, not yet fully ruled out) a residual
    non-Torque-Curve-triggered variant of WS_DISABLED -- both require a fresh debug.log's
    enabled= reading to discriminate, requested via checkpoint rather than guessed.
  timestamp: 2026-07-26T11:00:01Z

- hypothesis: "Residual, non-Torque-Curve-triggered variant of H8: the dashboard window
    RigToggle brings to the foreground has WS_DISABLED set (IsWindowEnabled == false) at
    or shortly after the moment FocusWindow runs, by some mechanism other than the
    already-eliminated Torque-Curve-triggered EnableWindow theory, and this residual
    disabled state is what blocks the close button."
  evidence: Eighth checkpoint response's debug.log shows window 0x2113EC's enabled= field
    reading True both immediately BEFORE (13:57:21.215) and immediately AFTER
    (13:57:21.669) FocusWindow's full ShowWindow/SetWindowPos/SetForegroundWindow
    sequence -- directly satisfying reasoning_checkpoint_7's own stated falsification
    condition for this hypothesis ("if the log's enabled= field on the target window
    reads False either before or after FocusWindow's sequence, H9 is falsified and a
    residual variant of H8 is confirmed instead" -- the converse holds: enabled=True
    throughout eliminates the residual H8 variant, leaving H9 as the sole remaining live
    hypothesis for the close-button regression).
  timestamp: 2026-07-26T13:00:01Z

- hypothesis: "H9b: the window's system menu has its Close (SC_CLOSE) command disabled
    via EnableMenuItem(hMenu, SC_CLOSE, MF_GRAYED), causing DefWindowProc to silently
    drop WM_SYSCOMMAND(SC_CLOSE) before it reaches any app-level handling -- explaining
    the 'completely inert' zero-visual-reaction close-button failure."
  evidence: Tenth checkpoint response's fresh debug.log shows closeGrayed=False on
    window 0xB40E44 both immediately before (14:29:28.231) and immediately after
    (14:29:28.639) FocusWindow's full ShowWindow/SetForegroundWindow sequence --
    directly matching reasoning_checkpoint_10's own pre-registered falsification
    condition ("if closeGrayed=False ... both before and after FocusWindow's sequence,
    H9b is FALSIFIED"). The system menu's Close command is not grayed at any point this
    session has been able to observe. Per reasoning_checkpoint_10's own falsification
    text, the true mechanism is most likely Moza subclassing WM_NCHITTEST/
    WM_NCLBUTTONDOWN (or directly filtering WM_SYSCOMMAND in its own window procedure)
    -- an interception that occurs before any state a read-only Win32 query can observe,
    and that this codebase has no passive way to detect. This exhausts every
    Win32-state mechanism (WS_DISABLED, system-menu MF_GRAYED, FormClosing-visible-
    revert) that could be tested via read-only P/Invoke queries from RigToggle's own
    process.
  timestamp: 2026-07-26T15:00:01Z

## Resolution

root_cause: CONFIRMED to the extent verifiable without a Windows runtime (H7,
  reasoning_checkpoint_5): Process.MainWindowHandle is an undocumented, first-match-wins
  heuristic that returns the first owner-less, IsWindowVisible==true top-level window it
  finds for a process -- it has no concept of "the semantically primary window" and cannot
  distinguish a real main dashboard from any other top-level window the SAME process happens
  to own. Moza Companion, in the exact repro state (rig test #4's real diagnostic log,
  Evidence 2026-07-25T05:00:00Z, re-examined this session with the user's process-identity
  clarification), has two simultaneously open top-level windows belonging to ONE process
  (PID=34792): its main dashboard (iconic/minimized at the time) and a separate "Torque
  Curve" utility window. Process.MainWindowHandle picked the Torque Curve window directly
  (never routed through any of this codebase's custom fallback logic), and LaunchOrFocus's
  existing FocusWindow sequence then correctly activated that wrong window -- explaining
  every Win32-level "success" in the prior diagnostic log despite the user seeing the wrong
  window appear. This supersedes H6 (multi-PID process-identity ambiguity, eliminated --
  PID=31616 is an unrelated/no-UI process, not a second UI-bearing Companion instance) and
  confirms H4/H5 (window-visibility/minimize/foreground-lock mechanics within
  FocusWindow/FindHiddenMainWindow) were never the remaining problem -- the gap was entirely
  upstream, in which window handle gets selected before FocusWindow ever runs.
fix: Applied (uncommitted). Replaced trust in Process.MainWindowHandle for the already-running
  path with a new FindBestMainWindow method that ALWAYS enumerates every owner-less top-level
  window for the resolved PID (not conditioned on MainWindowHandle == Zero, unlike the old
  FindHiddenMainWindow it replaces) and scores each candidate by caption presence + restored
  window area (read via GetWindowPlacement's rcNormalPosition, not GetWindowRect -- GetWindowRect
  reports a degenerate/off-screen rect for a currently-minimized window, which would have
  wrongly scored the correct-but-iconic dashboard as tiny and lost to the non-iconic Torque
  Curve window; this flaw was caught and corrected via Win32 documentation before shipping,
  not by a fifth failed rig test). The largest-area titled candidate is selected (a main
  dashboard is reliably larger than a small utility dialog), falling back to the largest-area
  candidate overall if no candidate has a caption. Applied to both LaunchOrFocus's
  already-running branch AND MinimizeIfRunning (same trust-MainWindowHandle-blindly fragility
  existed there too, risking minimizing the wrong window on toggle-back). Diagnostic logging
  extended to log each candidate's title/normalRect/area/iconic-state and kept in place.
verification: RIG-TEST CONFIRMED (fifth rig test, checkpoint response 2026-07-26): user
  confirmed "Yes it works now. The main window opens..." -- H7's FindBestMainWindow fix
  (always enumerate top-level windows for the resolved PID, score by caption + restored
  area via GetWindowPlacement, select the largest titled candidate) correctly resolves the
  ORIGINAL bug (Moza Companion window not coming to foreground / wrong-window-focused on
  the already-running path). This is the first fix in this session (5 rounds) to be
  rig-confirmed working. NOT YET archived/committed as fully resolved: rig-testing the H7
  fix immediately surfaced a NEW regression (window now opens correctly but its close (X)
  button is non-functional -- confirmed by the user to occur ONLY on windows RigToggle
  brought to focus, never when Moza is opened by the user directly, so this is a genuine
  side-effect of our own code/interaction with Moza's window state, not pre-existing Moza
  behavior). Investigation of this new regression continues below before this session can
  be archived -- the original symptom is fixed, but the toggle flow is not yet fully safe
  to ship until the close-button regression is resolved too. Sixth round of evidence
  (checkpoint response, 2026-07-26T11:00:00Z): ELIMINATED H8 as originally framed
  (Torque-Curve-triggered EnableWindow -- Torque Curve was confirmed NOT open) and reframed
  as H9 (reasoning_checkpoint_7: a Moza-side FormClosing/close-to-tray handler no-oping due
  to Form.Visible/native-visibility desync caused by RigToggle's raw ShowWindow(SW_SHOW)
  P/Invoke call -- all three independent close mechanisms, X/Alt+F4/taskbar-Close, fail
  identically while minimize works, which is far better explained by one shared
  Moza-internal WM_CLOSE-handler failing than by a literal WS_DISABLED window). Still
  genuinely uncertain between H9 (likely NOT fixable from within RigToggle -- a Moza-side
  quirk) and a residual non-Torque-Curve-triggered WS_DISABLED variant of H8 (fixable via a
  defensive EnableWindow(hWnd, TRUE)). No sixth code fix applied -- a fresh debug.log
  capture (for the real enabled= value) plus a visual-reaction clarifying question have
  been requested via checkpoint instead of guessing again, per this session's established
  discipline (4 of 5 prior blind FocusWindow-layer fixes failed without evidence-gathering
  first; the one that worked, H7, was preceded by evidence-gathering).

  SEVENTH round (2026-07-26T12:00:00Z, this update): a NEW, more severe, DISTINCT thread
  (H10) was reported and confirmed via a pasted debug.log excerpt: LaunchOrFocus's
  already-running branch has no code path that ever calls Process.Start, so once a
  same-named process (confirmed: a headless helper persisting across both an "app open"
  and an "app fully closed" snapshot 10 minutes apart) keeps IsRunning() == true forever,
  the app can never be relaunched via RigToggle again after being fully closed --
  matching the user's report verbatim ("the app does not start if fully closed. Rig
  toggle app shows moza companion running even though it's not."). Fix applied (H10,
  reasoning_checkpoint_8): LaunchOrFocus's already-running branch now does a short
  bounded recheck (up to 3s, polling FindBestMainWindow across all currently-matched
  processes) before concluding no window will ever appear, then falls back to the same
  Process.Start + poll path already used for the "not running" case (extracted into a
  shared LaunchFreshAndFocus helper) instead of silently no-op'ing forever. This
  supersedes D-06's original "do not retry/poll" rationale for the already-running
  branch, which was written before FindBestMainWindow's visibility-independent scan
  existed -- a persistent zero-window result today can no longer mean "genuinely running
  but merely tray-hidden" (that case is already found by the current scan), so it is a
  much narrower and more actionable signal than D-06 anticipated. NOT YET rig-tested --
  cannot execute/build on this Linux sandbox. H9 (close-button no-op) remains open and
  UNCHANGED by this round -- no fresh debug.log or visual-reaction answer was provided
  for H9 specifically this round; that checkpoint request is repeated below alongside
  the new H10 rig-test request. Both H9 and H10 must be confirmed before this session can
  be archived.

  EIGHTH round (2026-07-26T13:00:00Z, this update): H10's fix mechanism is CONFIRMED
  WORKING via a direct rig-test debug.log (bounded poll correctly detected a genuinely
  windowless state across both matched processes, fell back to a fresh launch, and the
  app became reachable/focusable again on both this toggle and the next one). A
  separately-reported complaint ("Rig toggle still says moza is running but task manager
  says it's not") was investigated (MainForm.RefreshUi() and IsRunning() both re-read)
  and judged to be the SAME confirmed persistent-headless-helper-process structural fact,
  not a distinct status-display bug -- no caching exists anywhere in that call chain, and
  the status label uses the identical process-matching mechanism as LaunchOrFocus. No
  code fix was applied for it; it remains an open question pending a Task Manager
  Command Line/Image Path check (requested via checkpoint) to determine whether the
  persistent process is a legitimate Moza background component (in which case there may
  be nothing to fix, only a possible future UX/semantics question) or something
  RigToggle's process matching should exclude. Separately, this round's enabled=True
  (before AND after FocusWindow) reading for the target window formally ELIMINATES the
  residual non-Torque-Curve WS_DISABLED variant of H8 (see Eliminated) -- H9 is now the
  sole remaining live hypothesis for the close-button regression, but the visual-reaction
  (flicker vs fully inert) question needed to fully confirm its specific mechanism is
  STILL unanswered after two rounds of asking. Session remains open: H9's visual-reaction
  answer and the Task Manager Command Line/Image Path data are both required before this
  session can be archived; no further code changes are proposed until that evidence
  arrives.

  NINTH round (2026-07-26T14:00:00Z, this update): both outstanding questions from the
  eighth round answered. "Completely inert" (H9 visual-reaction) eliminates H9's
  original FormClosing-cancel-and-hide framing (that mechanism implies some visible
  attempted action; zero reaction does not match it) and reframes as H9b
  (reasoning_checkpoint_10): the window's system menu has its Close (SC_CLOSE) command
  disabled via EnableMenuItem/MF_GRAYED -- a mechanism orthogonal to WS_DISABLED
  (already eliminated) that would produce exactly the confirmed pattern (X/Alt+F4/
  taskbar-Close all fail identically, Minimize unaffected, zero visual reaction, since
  DefWindowProc drops a grayed system-menu command before any app-level handling runs).
  Diagnostic-only instrumentation (GetSystemMenu/GetMenuState, new closeGrayed= log
  field, zero behavior change) added to FocusWindow to test this directly -- no
  behavior-changing fix applied without that evidence. The Task Manager Details-tab data
  revealed a genuine structural fact (5 processes named "MOZA Pit House.exe" across two
  distinct physical paths -- a root-folder exe and a \bin\-subfolder exe, the latter
  with 3 Suspended, near-zero-memory instances) which the orchestrator suggested might
  indicate RigToggle launches the wrong (\bin\) exe directly. This theory was evaluated
  (reasoning_checkpoint_11) and NOT acted on: this session's own already-confirmed H10
  rig-test evidence shows Process.Start(companionAppPath) -- whatever the actual
  configured path is -- reliably produces the correct, fully working dashboard window,
  directly contradicting the premise of a broken/non-functional launch target; code
  re-read also confirms RigToggle has no concept of "\bin\" anywhere and only ever
  issues one Process.Start call per launch. The suspended \bin\ processes are judged an
  informational, non-blocking finding (likely a Moza-internal multi-process
  architecture detail), not a RigToggle bug -- no fix applied, and the actual
  CompanionAppPath value was not requested since it would not change this conclusion.
  Session remains open: only H9b's closeGrayed= rig-test result is required before this
  session can be archived; no further code changes are proposed until that evidence
  arrives.

  TENTH round (2026-07-26T15:00:00Z): the requested closeGrayed= rig-test
  data arrived (closeGrayed=False both before and after FocusWindow on window
  0xB40E44) and FORMALLY FALSIFIES H9b per reasoning_checkpoint_10's own
  pre-registered falsification test. This exhausts every Win32-state mechanism
  (WS_DISABLED / H8, both original and residual variants; system-menu MF_GRAYED / H9b;
  FormClosing-visible-revert / H9-original) this codebase can test via read-only
  P/Invoke queries from RigToggle's own process -- all have now been eliminated.
  reasoning_checkpoint_12 evaluates whether any further RigToggle-side diagnostic could
  usefully discriminate H9's remaining candidate mechanism (Moza subclassing
  WM_NCHITTEST/WM_NCLBUTTONDOWN/WM_SYSCOMMAND in its own window procedure, upstream of
  anything a passive external Win32 query can observe) and concludes NO further
  diagnostic exists with a favorable risk/actionability tradeoff -- the one
  theoretically possible option (RigToggle injecting a synthetic WM_SYSCOMMAND(SC_CLOSE)
  to test where input is dropped) would not itself be a passive read (unlike every
  other diagnostic this session used), would not yield an actionable fix (RigToggle has
  no legitimate "close Moza for the user" feature in scope per CLAUDE.md), and a real
  fix for a WM_NCHITTEST/WM_SYSCOMMAND-level subclass would require injecting into or
  hooking Moza's own message loop -- categorically outside this project's Win32-utility
  scope and carrying real cross-process stability/security risk. H9 (close button
  becomes non-functional specifically on windows RigToggle brings to focus) is
  therefore documented as a known, likely Moza-side limitation, not fixable from
  RigToggle with the diagnostic tools available to a separate process, rather than
  pursued with an eleventh round of instrumentation. No further code changes are
  proposed for H9. H7 (original bug -- wrong/no window focused) and H10 (app would
  never relaunch once fully closed) remain the two CONFIRMED, rig-verified fixes from
  this session and are the changes proposed for commit. The zombie \bin\-process
  accumulation (now 5 total, growing by 1 per fresh-launch fallback cycle) remains an
  informational, non-blocking, Moza-internal-architecture finding (reasoning_
  checkpoint_11) -- not a RigToggle defect, not actioned. Session moved to a
  human-verify/decision checkpoint: awaiting explicit user confirmation that (a) H7 and
  H10 hold up in normal day-to-day rig use (not just the specific rig-test moments
  captured in this file), and (b) the user accepts closing this session out with H9
  documented as a known limitation rather than continuing to chase it further.

  ELEVENTH round (2026-07-26T16:00:00Z, FINAL -- session closed): checkpoint response
  received and both open questions answered. (1) "H7 and H10 hold up in normal
  day-to-day toggling" -> user confirmed YES, both solid, verified via regular use
  over time, not just the specific rig-test moments captured earlier in this file. H7
  (original bug: wrong/no window focused on the already-running path) and H10 (app
  would never relaunch after being fully closed) are therefore FINAL CONFIRMED FIXED,
  promoted from "rig-test confirmed" to "day-to-day-use confirmed" -- the highest
  verification tier this session's discipline recognizes. (2) H9 (close/X button inert
  specifically on windows RigToggle brought to focus) -> user explicitly accepted
  reasoning_checkpoint_12's conclusion: ACCEPTED as a Moza-side, not-fixable-from-
  RigToggle known limitation, not pursued further. Final disposition: H9 is
  INVESTIGATED TO THE PRACTICAL LIMIT of what a separate Win32 utility can diagnose via
  passive, read-only queries (three independent state mechanisms tested and eliminated
  across ten rounds: WS_DISABLED/H8, system-menu MF_GRAYED/H9b, FormClosing-visible-
  revert/H9-original) and ACCEPTED as a documented known limitation, likely caused by
  Moza intercepting close input at the message level (WM_NCHITTEST/WM_NCLBUTTONDOWN/
  WM_SYSCOMMAND) inside its own window procedure -- a layer no external process can
  safely observe or fix without hooking Moza's own message loop, which is categorically
  outside this project's Win32-utility scope per CLAUDE.md/STACK.md. Session archived:
  status -> resolved, file moved to .planning/debug/resolved/, STATE.md's Pending
  Todos updated to close the original entry and record H9 as a documented known
  limitation with a pointer back to this file's full investigation history. Code
  changes for H7 + H10 (and H9's diagnostic-only instrumentation, left in place --
  see files_changed and the diagnostic-logging disposition note below) remain
  UNCOMMITTED in the working tree per this project's git safety protocol -- the user
  will review and commit them directly, not this session.
files_changed:
  - src/RigToggle.Windows/NativeMethods.cs (uncommitted -- added WindowPlacement/Point structs and
    GetWindowPlacement P/Invoke declaration, on top of all prior declarations (GetWindowTextLength/
    GetWindowText/Rect/GetWindowRect/GetForegroundWindow/EnumWindows/GetWindowThreadProcessId/
    IsWindowVisible/GetWindow/SetWindowPos), all kept. THIS SESSION (H8 investigation, diagnostic
    only, no behavior change): added IsWindowEnabled and GetClassName P/Invoke declarations.)
  - src/RigToggle.Windows/WindowsAppController.cs (uncommitted -- renamed FindHiddenMainWindow to
    FindBestMainWindow, now always runs (not conditioned on MainWindowHandle==Zero) and scores
    every owner-less PID-matched candidate by caption + GetWindowPlacement-based restored area
    instead of GetWindowRect; LaunchOrFocus's already-running branch and MinimizeIfRunning both
    now call FindBestMainWindow instead of trusting Process.MainWindowHandle directly; added
    `using System.Runtime.InteropServices;`; Log() helper and all three prior FocusWindow fix
    layers (IsWindowVisible/SW_SHOW, IsIconic/SW_RESTORE, SetWindowPos TOPMOST-toggle) kept
    unchanged. THIS SESSION (H8 investigation, diagnostic only, no behavior change): logs
    enabled= (IsWindowEnabled) state in FindBestMainWindow's per-candidate scan and in
    FocusWindow's before/after state dump; logs each candidate's window class name (GetClassName)
    alongside its existing title/rect/area/iconic fields.)
  - src/RigToggle.App/Program.cs (uncommitted, unchanged this session -- TextWriterTraceListener
    wiring to %LOCALAPPDATA%\RigToggle\debug.log from the prior session, kept in place)
  - src/RigToggle.Windows/WindowsAppController.cs (uncommitted, THIS ROUND -- H10 fix):
    added AlreadyRunningWindowPollTimeout (3s) constant; LaunchOrFocus's already-running
    branch refactored into a bounded do/while recheck loop (re-enumerates
    Process.GetProcessesByName + FindBestMainWindow up to ~3s, short-circuiting the moment
    a window is found or zero processes still match) instead of a single one-shot scan;
    when the loop ends with hWnd still Zero (every matched process genuinely windowless),
    falls back to a new shared LaunchFreshAndFocus private method (extracted from the
    former inline "not running" branch body, behavior unchanged) instead of returning with
    no action. Class-level doc comment updated to describe the fallback. No changes to
    FocusWindow, FindBestMainWindow, MinimizeIfRunning, or any NativeMethods.cs P/Invoke
    surface this round.
  - src/RigToggle.Windows/NativeMethods.cs (uncommitted, THIS ROUND -- H9b diagnostic
    only, no behavior change): added GetSystemMenu and GetMenuState P/Invoke
    declarations, plus SC_CLOSE/MF_BYCOMMAND/MF_GRAYED/MF_DISABLED/MENU_ITEM_NOT_FOUND
    constants, to read (never modify) whether a window's system menu has its Close
    command grayed/disabled.
  - src/RigToggle.Windows/WindowsAppController.cs (uncommitted, THIS ROUND -- H9b
    diagnostic only, no behavior change): added a private static IsSystemCloseGrayed(hWnd)
    helper (read-only, best-effort, never throws) wrapping GetSystemMenu/GetMenuState;
    FocusWindow's existing before/after Log() calls now include a new closeGrayed= field
    alongside the existing visible=/iconic=/enabled=/foreground= fields. No changes to
    FindBestMainWindow, LaunchOrFocus, MinimizeIfRunning, or LaunchFreshAndFocus this
    round.
