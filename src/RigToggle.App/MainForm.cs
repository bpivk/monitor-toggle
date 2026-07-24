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
        private readonly Func<SettingsForm> _settingsFormFactory;

        public MainForm(
            ToggleService toggleService,
            IAppController appController,
            ISettingsStore settingsStore,
            Func<SettingsForm> settingsFormFactory)
        {
            _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
            _appController = appController ?? throw new ArgumentNullException(nameof(appController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
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
                    _toggleService.ToggleToRigMode();
                }

                RefreshUi();
            }
            catch (Exception)
            {
                // Basic guard only (D-13/T-02-FAKEFAIL) — full per-step CORE-04 partial-failure
                // reporting is out of scope until Phase 5.
                MessageBox.Show(
                    this,
                    "Something went wrong while toggling. No changes were applied. Try again, or check Settings.",
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
