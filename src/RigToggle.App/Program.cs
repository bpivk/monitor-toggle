using RigToggle.Core;
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

            SettingsForm SettingsFormFactory() => new SettingsForm(monitorController, audioController, settingsStore);

            var mainForm = new MainForm(toggleService, appController, settingsStore, monitorController, SettingsFormFactory);

            Application.Run(mainForm);
        }
    }
}
