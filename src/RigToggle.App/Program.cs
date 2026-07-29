using System.Diagnostics;
using RigToggle.Core;
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
        /// </remarks>
        [STAThread]
        static void Main()
        {
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

            var snapshotStore = new JsonSnapshotStore(Path.Combine(basePath, "state.json"));

            var monitorController = new WindowsMonitorController();
            var audioController = new WindowsAudioController();
            var appController = new WindowsAppController();

            var toggleService = new ToggleService(
                settingsStore,
                snapshotStore,
                monitorController,
                audioController,
                appController);

            // 07-01: every toggle trigger (this GUI button today; tray/hotkey/CLI in
            // Phases 8-10) must call through ToggleOrchestrator, never ToggleService
            // directly — it wraps ToggleService with the CORE-06 reentrancy guard.
            var toggleOrchestrator = new ToggleOrchestrator(toggleService);

            SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore);

            var mainForm = new MainForm(toggleOrchestrator, settingsStore, monitorController, SettingsFormFactory);

            Application.Run(mainForm);
        }
    }
}
