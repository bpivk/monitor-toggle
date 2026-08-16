using System.Diagnostics;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
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
            ApplicationConfiguration.Initialize();

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigToggle");

            var settingsStore = new JsonSettingsStore(Path.Combine(basePath, "settings.json"));

            // Settings must be loaded before the trace-listener block below so the
            // EnableDebugLogging flag can gate it. Best-effort: if Load throws, default to
            // logging OFF rather than blocking startup or falling back to "on".
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
            // the self-contained .exe (no attached debugger) could read it. Wiring a
            // TextWriterTraceListener here — best-effort, must never block startup — is the
            // minimal change that makes existing and new Trace.WriteLine calls actually
            // observable on the rig. Gated behind AppSettings.EnableDebugLogging (off by
            // default) so the app does not write debug.log unconditionally on every run.
            if (settings.EnableDebugLogging)
            {
                try
                {
                    Directory.CreateDirectory(basePath);
                    var traceWriter = new StreamWriter(Path.Combine(basePath, "debug.log"), append: true)
                    {
                        AutoFlush = true,
                    };
                    Trace.Listeners.Add(new TextWriterTraceListener(traceWriter));
                }
                catch
                {
                    // Diagnostic logging is best-effort only — never let it prevent startup.
                }
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

            mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory, themeProvider);

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
    }
}
