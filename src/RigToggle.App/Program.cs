using System.Diagnostics;
using System.Net.Http;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Diagnostics;
using RigToggle.Core.Models;
using RigToggle.Core.Persistence;
using RigToggle.Windows;

namespace RigToggle.App
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        /// <remarks>
        /// Composition root (02-RESEARCH.md "Recommended Project Structure" / Pattern 4
        /// Open Question #1 resolution): the ONE place real Windows adapters and Json
        /// stores are constructed and wired together. MainForm/SettingsForm never `new`
        /// a concrete adapter or store themselves (Anti-Pattern 2). Deliberately no
        /// elevation manifest of any kind is added anywhere in this project — the
        /// default non-elevated execution level is preserved (Pitfall 6) so Phase 3's
        /// cross-process window-focus call against the non-elevated companion app is
        /// not broken by UIPI.
        ///
        /// 08-03: also takes the process `args` so `StartupArgs.ShouldStartHidden` can
        /// select a hidden vs. visible startup (D-06). `WindowsAutostartConfigurator` is
        /// constructed here alongside the other Windows adapters and injected into
        /// `SettingsForm` — the checkbox never touches the registry directly. Before
        /// either `Application.Run` branch, `mainForm.InitializeTrayState()` is called
        /// unconditionally (08-RESEARCH.md Pitfall 6): under `--tray`, `Form.Load` never
        /// fires because the form is never shown, so the tray icon/menu must be primed
        /// here instead of relying on the form's own `Load` handler.
        ///
        /// 25-02/UPDATE-07: Main()'s very first branch after the two position-sensitive
        /// calls above parses the `--apply-update` internal relaunch flag and, if
        /// present, transfers control to `UpdateApplyEntryPoint.Run` and returns
        /// before the single-instance guard below is ever touched — see that type's
        /// doc comment for why this must precede the guard.
        /// </remarks>
        [STAThread]
        static void Main(string[] args)
        {
            // 12-02/D-04: must be the very first executable statement of Main(), before
            // ApplicationConfiguration.Initialize() and before any Form/control is
            // constructed (Pitfall 1) -- constructing UI before this call risks a
            // visible title-bar flash as SetColorMode's own internal DWM call fires
            // after the window has already painted once in the default light chrome.
            System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            //
            // 25-03 Task 3 operator checkpoint: CONFIRMED real-usage crash (Windows
            // Event Viewer detail plus a real 2-launch repro on real hardware -- an
            // ordinary "app already running, user double-clicks it again" scenario,
            // NOT an artificial test-harness burst). Application.SetColorMode(System)
            // above subscribes to Microsoft.Win32.SystemEvents.UserPreferenceChanged
            // (needed for live OS theme-follow), whose first-use initialization
            // lazily spins up a dedicated background thread that creates a native
            // window asynchronously relative to this thread (confirmed directly from
            // the actual .NET 10.0.10 binaries' own symbols -- SystemEvents.dll's
            // s_windowThread/WindowThreadProc/CreateWindowExW, not guessed).
            // ApplicationConfiguration.Initialize()'s SetCompatibleTextRenderingDefault
            // call asserts no window has been created yet in this process; under
            // ordinary scheduling contention from an already-running instance's own
            // message pump/GC (reproduced with as few as two total processes on the
            // machine, no artificial concurrent burst needed), that background thread
            // can occasionally win the race and create its window first, throwing
            // InvalidOperationException and crashing the whole process before it ever
            // reaches the single-instance guard below -- which is what made a real
            // duplicate launch look like a broken restore path: the process crashed
            // before WaitForInstanceReady/BroadcastActivation ever ran.
            //
            // Catching InvalidOperationException narrowly around this one call
            // converts that crash into a graceful continuation. This is safe because
            // EnableVisualStyles()/SetHighDpiMode() -- the two calls the generated
            // Initialize() method makes before SetCompatibleTextRenderingDefault --
            // have already completed successfully by the time this specific exception
            // can be thrown (execution reached the LAST statement), so this app's DPI
            // awareness and visual-styles configuration are unaffected either way.
            // Only SetCompatibleTextRenderingDefault's own setting (false) might not
            // get explicitly (re-)asserted in the rare case this race is lost -- and
            // .NET's own WinForms runtime already defaults new controls to
            // UseCompatibleTextRendering=false without this call, so the practical
            // effect of skipping it here is negligible. Deliberately NOT retried
            // (calling ApplicationConfiguration.Initialize() again would re-invoke
            // SetHighDpiMode(), which MSDN documents as throwing if called a second
            // time -- turning this exception into a different, guaranteed one) and
            // deliberately NOT fixed by reordering SetColorMode after this call
            // instead (Pitfall 1's title-bar-flash fix, above, is Phase 12's own
            // rig-verified ordering; reordering it without a real Windows visual
            // re-check risks silently reintroducing a previously-fixed bug that no
            // build-time gate can catch). This is a narrow, defensive catch around
            // one documented SDK bootstrap call, not a broad swallow-all -- any other
            // exception from this call (which this codebase does not otherwise expect
            // to throw) still propagates and crashes the process exactly as before.
            bool applicationConfigurationRaceHit = false;
            try
            {
                ApplicationConfiguration.Initialize();
            }
            catch (InvalidOperationException)
            {
                applicationConfigurationRaceHit = true;
            }

            // UPDATE-07/D-03/D-04 (ARCHITECTURE.md Pattern 1): the --apply-update
            // bypass must be the very first branch in Main(), strictly above the
            // single-instance guard acquisition below and above every other
            // bootstrap step. Letting this helper through the guard first would make
            // its brief existence observable to and blockable by the very mechanism
            // it exists to bypass -- Phase 26's internal update-apply relaunch must
            // never be blocked by a guard the process it is replacing still holds.
            // Main is `static void`, hence the exit code is published through the
            // process-wide exit-code property rather than a returned value.
            if (StartupArgs.TryGetApplyUpdateArgs(args, out var applyUpdateArgs))
            {
                Environment.ExitCode = UpdateApplyEntryPoint.Run(applyUpdateArgs);
                return;
            }

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigToggle");

            var settingsStore = new JsonSettingsStore(Path.Combine(basePath, "settings.json"));

            // 25-03 Task 3 operator checkpoint (INSTANCE-02 restore investigation):
            // settings-loading and the trace-listener wiring below were originally
            // positioned AFTER the single-instance guard's early-return branch (further
            // down this file, past the point where a duplicate/loser process returns).
            // That meant a duplicate launch -- exactly the path this investigation needs
            // to observe (WaitForInstanceReady, BroadcastActivation) -- could NEVER write
            // to debug.log, in any build, regardless of AppSettings.EnableDebugLogging.
            // Moved here, above the guard acquisition, so BOTH the primary and duplicate
            // paths can log. Still strictly AFTER the --apply-update bypass branch above
            // (D-03's contract: the bypass runs before settings/mode-store bootstrap of
            // any kind, before the guard, before anything else -- moving this any earlier
            // would violate that one-way, Phase-26-consumed ordering guarantee). Settings
            // loading itself is unchanged: still best-effort, still defaults to
            // EnableDebugLogging=false on a read failure rather than blocking startup or
            // guessing "on" -- this is purely a reordering, not a behavior change to D-01/
            // D-02 (still zero visible feedback on a duplicate launch, still exactly one
            // code path regardless of launch reason).
            AppSettings settings;
            try
            {
                settings = settingsStore.Load();
            }
            catch
            {
                settings = new AppSettings();
            }

            // Debug session .planning/debug/moza-foreground-focus.md: Trace.WriteLine calls
            // already existed in ToggleService's best-effort catch blocks, and
            // WindowsAppController now has diagnostic logging around its window-focus P/Invoke
            // sequence, but nothing previously persisted Trace output anywhere a user running
            // the self-contained .exe (no attached debugger) could read it. Wiring a file trace
            // listener — best-effort, must never block startup — is the minimal change that
            // makes existing and new Trace.WriteLine calls actually observable on the rig.
            //
            // Debug session monitor-enable-reactivates-others-again, round 4: this used to be
            // an inline `if (settings.EnableDebugLogging) { ... wire listener ... }` block,
            // wired exactly ONCE, here, at process startup (including the FileShare.ReadWrite
            // fix from the 25-03 Task 3 investigation, now inside DebugLog.Configure below).
            // Round 3's rig trial came back with a COMPLETELY empty debug.log despite the bug
            // still reproducing — traced (by static reading; this sandbox has no Windows
            // runtime to reproduce on) to that one-time wiring: this app runs tray-resident for
            // long stretches, and SettingsForm.cs's Save handler wrote the new
            // EnableDebugLogging value to settings.json but never told THIS already-running
            // process to start listening. Enabling the checkbox mid-session (the natural
            // workflow: open the already-running app, flip the box on, reproduce immediately)
            // silently produced zero log output, no restart having occurred. DebugLog.Configure
            // (RigToggle.Core.Diagnostics) is now the single, live-reconfigurable wiring point
            // — called here at startup AND from SettingsForm.cs on every Save, so the checkbox
            // takes effect immediately, no restart required, permanently closing this gap
            // rather than just working around it once. WriteStartupBanner runs
            // unconditionally, before Configure, so a future empty-log report is immediately
            // diagnosable from the log itself: it records this process's exe path/on-disk
            // build timestamp (stale-build check), the resolved log path (wrong-location
            // check), and EnableDebugLogging as loaded (off-by-mistake check) — all
            // independent of whether Configure below actually wires anything.
            DebugLog.WriteStartupBanner(settings.EnableDebugLogging);
            DebugLog.Configure(settings.EnableDebugLogging);

            // Logged here (not at the actual catch site above) so it is observable at
            // all -- the trace listener isn't wired up yet at the point the race can
            // occur. See the ApplicationConfiguration.Initialize() comment above for
            // the full explanation of what this means and why it's safe to continue past.
            if (applicationConfigurationRaceHit)
            {
                TryLog("Program.Main: ApplicationConfiguration.Initialize() threw InvalidOperationException (SetColorMode/SystemEvents race, 25-03 Task 3) -- continued startup without crashing.");
            }

            // INSTANCE-01/D-02: the single-instance guard is acquired here, above every
            // other bootstrap step except settings-loading/trace-listener wiring above --
            // before modeStore, before any Form is constructed. This is deliberately NOT
            // wrapped in this file's best-effort swallow-and-continue try/catch idiom (see
            // the trace-listener block above): a second instance that got as far as, say,
            // hotkey registration would hard-fail RegisterHotKey and surface a spurious
            // user-facing error on every accidental double-launch (STACK.md §2 row 4),
            // so failing this check must short-circuit everything after it, not degrade
            // gracefully and continue. `using var` means the compiler emits the
            // try/finally that covers everything below including Application.Run --
            // rewriting this to a bare local or a static field would drop that finally
            // and lose deterministic mutex release on the exception path.
            using var guard = SingleInstanceGuard.Acquire();

            // D-02: exactly one duplicate-launch branch, handled identically regardless
            // of why the second launch happened (accidental double-click, autostart
            // racing a manual launch, tray relaunch) -- no reason-based sub-case. D-01:
            // the only action is waiting for readiness then broadcasting the activation
            // signal; no toast, no dialog, no notification of any kind is raised here or
            // anywhere else on this path. The Trace.WriteLine calls below are new
            // (25-03 Task 3 investigation) and purely diagnostic -- individually
            // try/caught so a logging failure can never turn a clean duplicate-process
            // exit into an unhandled-exception exit code.
            if (!guard.IsPrimaryInstance)
            {
                TryLog("Program.Main: duplicate launch detected (not primary instance) -- waiting for existing instance's readiness signal.");

                // Pitfall 8: wait for the primary instance's readiness handle before
                // broadcasting -- but broadcast either way, since a false result here is
                // not proof no window exists (it may just mean the wait timed out or the
                // handle opened but was never signalled in time). ActivationSignal itself
                // also retries multiple times, giving this a second layer of tolerance
                // against a startup race.
                bool becameReady = SingleInstanceGuard.WaitForInstanceReady(SingleInstanceGuard.DefaultReadyWaitTimeout);
                TryLog($"Program.Main: WaitForInstanceReady returned {becameReady}.");

                ActivationSignal.BroadcastActivation();
                TryLog("Program.Main: ActivationSignal.BroadcastActivation completed; duplicate process exiting.");
                return;
            }

            string legacyStateJsonPath = Path.Combine(basePath, "state.json");
            var modeStore = new JsonModeStore(Path.Combine(basePath, "mode.json"));
            var markerStore = new JsonToggleInProgressStore(Path.Combine(basePath, "toggle-in-progress.json"));

            // 16-RESEARCH.md Pattern 1 (CRITICAL, one-time only): seed mode.json from
            // legacy snapshot presence exactly once, when it does not yet exist. Never
            // unconditionally default to Normal here — that would silently flip every
            // existing Rig-mode user's true state on their first v2.0 launch and also
            // trigger a spurious mode-corruption dialog below for every healthy install.
            if (!modeStore.Exists())
            {
                modeStore.Save(File.Exists(legacyStateJsonPath) ? ToggleMode.Rig : ToggleMode.Normal);
            }

            // Pattern 3 (16-RESEARCH.md): the two blocking startup checks (mode
            // corruption, crash-recovery) run after the bootstrap above and before any
            // toggle-capable object is constructed or the tray-safe timing point below
            // — on both the visible and --tray startup paths. Deliberately NOT wrapped
            // in a try/catch: these are the one deliberate exception to this file's
            // best-effort-swallow startup idiom (D-06/D-07).
            StartupRecoveryChecker.Run(modeStore, markerStore);

            var monitorController = new WindowsMonitorController();
            var audioController = new WindowsAudioController();
            var appController = new WindowsAppController();
            var autostartConfigurator = new WindowsAutostartConfigurator();

            // UPDATE-01..04: composition-root-only construction (Anti-Pattern 2), same
            // flat sequential-var style as the Windows adapters immediately above. No
            // new NuGet packages -- HttpClient + System.Text.Json only (STACK.md §1).
            var updateHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var releaseFeed = new GitHubReleaseFeed(updateHttpClient, "bpivk", "monitor-toggle");
            var updateApplier = new WindowsUpdateApplier(updateHttpClient);

            // The entry assembly is RigToggle.App -- the only project carrying
            // <Version> (UPDATE-01) -- which is why the running version is resolved
            // here, at the composition root, and injected into UpdateOrchestrator
            // rather than read inside Core (RigToggle.Core.csproj carries no version
            // of its own that would mean anything for this comparison).
            var runningVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            var updateOrchestrator = new UpdateOrchestrator(releaseFeed, updateApplier, runningVersion);

            // 12-02/THEME-01/02/03: composition-root-only construction (Anti-Pattern 2)
            // -- this is the ONE and only place the theme provider adapter is
            // constructed anywhere in the solution. App-lifetime object; intentionally
            // never disposed before Application.Run.
            var innerThemeProvider = new WindowsThemeProvider();

            // THEME-09/D-04/Pitfall 6: wrapping the raw OS-signal provider in the
            // decorator here -- and passing this single `themeProvider` local
            // everywhere below -- is what gives all three of the codebase's
            // independent IsDark/IsDarkTheme copies (MainForm, SettingsForm,
            // MonitorConfirmDialog) override awareness with zero per-form edit.
            // Declared via `var` (concrete OverridableThemeProvider), not
            // IThemeProvider, since Plan 23-02 needs SetPreviewOverride/
            // RefreshOverride as method-group arguments from this same local.
            var themeProvider = new OverridableThemeProvider(innerThemeProvider, settingsStore);

            // THEME-09/Task 2: pins the process-wide application color mode to the
            // effective theme immediately -- prevents a startup flash of OS-themed
            // native controls when a Light or Dark override is persisted and Windows
            // is currently on the other theme. Still safely before any Form or
            // control is constructed (Pitfall 1's actual constraint) -- mainForm
            // below is the first one.
            ThemeApplier.ApplyEffectiveColorMode(themeProvider.CurrentTheme == AppTheme.Dark);

            var toggleService = new ToggleService(
                settingsStore,
                modeStore,
                monitorController,
                audioController,
                appController);

            // 07-01: every toggle trigger (this GUI button today; tray/hotkey/CLI in
            // Phases 8-10) must call through ToggleOrchestrator, never ToggleService
            // directly — it wraps ToggleService with the CORE-06 reentrancy guard.
            // 16-04: also threads markerStore through so RunGuarded can save/clear the
            // DISPLAY-13 crash-in-progress marker around every guarded toggle.
            var toggleOrchestrator = new ToggleOrchestrator(toggleService, markerStore);

            // TRIG-01: mainForm and SettingsFormFactory are mutually dependent (the
            // factory needs mainForm.TryRegisterConfiguredHotkey; MainForm's constructor
            // needs the factory). C# local-variable scope is strictly textual — a local
            // function cannot reference a variable declared later in the same block, even
            // though it's only invoked after that variable is assigned (CS0841/CS0165,
            // caught on rig build). Pre-declaring mainForm with null! and assigning it
            // after the factory is defined breaks the cycle: the factory captures
            // mainForm by reference, and by the time it's actually invoked (Settings is
            // opened), mainForm already holds the real instance.
            MainForm mainForm = null!;
            SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore, autostartConfigurator, themeProvider, mainForm.TryRegisterConfiguredHotkey, mainForm.ApplyTrayVisibility, themeProvider.SetPreviewOverride, themeProvider.RefreshOverride);

            mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, themeProvider, updateOrchestrator);

            // Pitfall 6: prime the tray icon/menu BEFORE either Run branch — under
            // --tray the form's own Load event never fires since the form is never
            // shown, so tray state must not depend on it.
            mainForm.InitializeTrayState();

            // TRIG-01/D-04: registers the configured global hotkey unconditionally for
            // BOTH the visible and --tray startup paths, exactly mirroring how
            // InitializeTrayState() above must run before either Application.Run branch.
            // RegisterHotkeyAtStartup is best-effort (traces + toasts on failure, never
            // throws) so it cannot block startup — same posture as the trace-listener
            // block above.
            mainForm.RegisterHotkeyAtStartup();

            // Pitfall 8: publish this instance's readiness signal only now -- after
            // InitializeTrayState() above, which calls ApplyDwmChrome(), which reads
            // Handle, which is what forces the window handle into existence on BOTH the
            // visible and --tray startup paths (MainForm.cs's ApplyDwmChrome doc comment
            // states this explicitly). A broadcast window message can only reach a
            // window that already exists, so signalling readiness any earlier would
            // reopen exactly the race this call closes. A message posted between handle
            // creation and Application.Run below is not lost -- it queues on this
            // thread's message queue and is dispatched once the pump starts.
            guard.MarkReady();

            // UPDATE-02: best-effort, fire-and-forget on-launch update check -- must
            // never block or delay startup (PITFALLS.md UX Pitfalls table). The
            // handle already exists at this point because InitializeTrayState() above
            // forces its creation on BOTH the visible and --tray startup paths
            // (MainForm.cs's ApplyDwmChrome doc comment states this explicitly) --
            // ARCHITECTURE.md flags mainForm.BeginInvoke's pre-Application.Run safety
            // under --tray as an open verification question; the documented fallback
            // if it proves unsafe on real hardware is a one-shot
            // System.Windows.Forms.Timer instead of BeginInvoke.
            try
            {
                mainForm.BeginInvoke(new Action(() => _ = mainForm.RunAutomaticUpdateCheckAsync()));
            }
            catch (Exception ex)
            {
                TryLog($"Program.Main: failed to schedule RunAutomaticUpdateCheckAsync: {ex.GetType().Name}: {ex.Message}");
            }

            // D-06, rig-corrected: 08-RESEARCH.md's original theory — that
            // `new ApplicationContext(mainForm)` (passing the form in) suppresses the
            // Show() call — was rig-tested and found FALSE for this runtime: the window
            // still appeared under `--tray`. The actual documented/working pattern for a
            // start-hidden WinForms tray app is to give ApplicationContext NO main form
            // at all (`new ApplicationContext()`), so nothing in the framework ever
            // shows a window; mainForm exists only as an object reference the tray
            // icon's left-click/Settings/Exit handlers already hold, and is Show()'n for
            // the first time only when the user actually requests it via the tray icon.
            // Application.Exit() from the tray icon still terminates the message loop
            // correctly with this parameterless ApplicationContext (Exit() closes every
            // form and message loop on the thread; it does not depend on
            // ApplicationContext.MainForm being set).
            if (StartupArgs.ShouldStartHidden(args))
            {
                Application.Run(new ApplicationContext());
            }
            else
            {
                Application.Run(mainForm);
            }
        }

        /// <summary>
        /// 25-03 Task 3 operator checkpoint: best-effort diagnostic logging for the
        /// duplicate-launch branch, matching this codebase's established
        /// Trace.WriteLine-is-diagnostic-only convention (WindowsAppController.cs,
        /// WindowsThemeProvider.cs, WindowsAutostartConfigurator.cs). Individually
        /// try/caught -- a Trace write failure (e.g. a disk error mid-write) must
        /// never turn a clean duplicate-process exit (D-01/D-02: exit code 0, no
        /// visible feedback) into an unhandled-exception exit, which would silently
        /// change this file's most correctness-sensitive path for a logging reason.
        /// </summary>
        private static void TryLog(string message)
        {
            try
            {
                Trace.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Program: {message}");
            }
            catch
            {
                // Logging is diagnostic-only; never let it affect startup/exit behavior.
            }
        }
    }
}
