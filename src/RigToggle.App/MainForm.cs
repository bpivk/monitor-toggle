using System.Linq;
using RigToggle.Core;
using RigToggle.Core.Abstractions;

namespace RigToggle.App
{
    /// <summary>
    /// Main window (D-13): mode indicator, Toggle button, Settings launch, and the
    /// companion-app status line (D-15). Never instantiates a concrete Windows
    /// adapter or Json store directly — everything is injected by the composition
    /// root (Program.cs, Anti-Pattern 2 in 02-RESEARCH.md). Mode is derived from
    /// ToggleService.IsInRigMode(), which itself derives from snapshot-file
    /// presence (D-14) — correct on startup even after a crash while in Rig mode.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ToggleService _toggleService;
        private readonly IAppController _appController;
        private readonly ISettingsStore _settingsStore;
        private readonly IMonitorController _monitorController;
        private readonly Func<SettingsForm> _settingsFormFactory;

        public MainForm(
            ToggleService toggleService,
            IAppController appController,
            ISettingsStore settingsStore,
            IMonitorController monitorController,
            Func<SettingsForm> settingsFormFactory)
        {
            _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
            _appController = appController ?? throw new ArgumentNullException(nameof(appController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _settingsFormFactory = settingsFormFactory ?? throw new ArgumentNullException(nameof(settingsFormFactory));

            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RefreshUi();
        }

        /// <summary>
        /// Re-derives the mode indicator (from snapshot-file presence, D-14) and the
        /// companion-app status line (from real Process.GetProcessesByName detection,
        /// D-07/D-15). Called on startup and after every toggle/Settings-dialog close.
        /// </summary>
        private void RefreshUi()
        {
            bool isInRigMode = _toggleService.IsInRigMode();
            lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
            btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";

            var settings = _settingsStore.Load();
            bool companionRunning = !string.IsNullOrEmpty(settings.CompanionAppPath)
                && _appController.IsRunning(settings.CompanionAppPath);
            lblCompanionStatus.Text = companionRunning
                ? "Moza Companion: Running"
                : "Moza Companion: Not running";
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_toggleService.IsInRigMode())
                {
                    _toggleService.ToggleToNormalMode();
                }
                else
                {
                    if (!_toggleService.IsSettingsConfigured())
                    {
                        // WR-01: don't let an incomplete Settings state reach ToggleToRigMode
                        // at all — redirect to Settings instead of persisting a garbage
                        // snapshot and flipping the mode indicator to "Rig".
                        MessageBox.Show(
                            this,
                            "Please finish configuring Settings (monitor, both audio devices, and the companion app) before switching to Rig Mode.",
                            "Rig Toggle",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    // DISPLAY-03 / D-01 / D-02: informed-consent confirmation naming the
                    // monitor about to be disabled, durably suppressible via "don't ask
                    // again" and reset whenever Settings changes the configured monitor
                    // (04-RESEARCH.md Pattern 5).
                    var settings = _settingsStore.Load();
                    if (!settings.SkipMonitorConfirmation)
                    {
                        var monitor = _monitorController.GetActiveMonitors()
                            .FirstOrDefault(m => m.DevicePath == settings.MonitorDevicePath);
                        string name = monitor?.FriendlyName ?? "the configured monitor";

                        using var confirmDialog = new MonitorConfirmDialog(name);
                        if (confirmDialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return; // user cancelled — nothing mutated
                        }

                        if (confirmDialog.DontAskAgain)
                        {
                            settings.SkipMonitorConfirmation = true;
                            _settingsStore.Save(settings);
                        }
                    }

                    _toggleService.ToggleToRigMode();
                }

                RefreshUi();
            }
            catch (Exception ex)
            {
                // Basic guard only (D-13/T-02-FAKEFAIL) — full per-step CORE-04 partial-failure
                // reporting is out of scope until Phase 5. Exception detail is included
                // (not just a generic message) because this is a single-user diagnostic
                // tool, not a hardened multi-user app — surfacing the real error is more
                // useful than hiding it, especially for CCD-mutation failures that are
                // otherwise unreproducible without rig hardware.
                MessageBox.Show(
                    this,
                    $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            // Modal (D-03): blocks Main until closed. New settings apply on the NEXT
            // toggle, not mid-flight — RefreshUi() below only updates the status line,
            // it does not re-run any toggle logic.
            using var settingsForm = _settingsFormFactory();
            settingsForm.ShowDialog(this);
            RefreshUi();
        }
    }
}
