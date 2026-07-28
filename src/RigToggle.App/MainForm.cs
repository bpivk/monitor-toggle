using System.Linq;
using RigToggle.Core;
using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.App
{
    /// <summary>
    /// Main window (D-13): mode indicator, Toggle button, Settings launch. Never
    /// instantiates a concrete Windows adapter or Json store directly — everything
    /// is injected by the composition root (Program.cs, Anti-Pattern 2 in
    /// 02-RESEARCH.md). Mode is derived from ToggleService.IsInRigMode(), which
    /// itself derives from snapshot-file presence (D-14) — correct on startup even
    /// after a crash while in Rig mode.
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly ToggleService _toggleService;
        private readonly ISettingsStore _settingsStore;
        private readonly IMonitorController _monitorController;
        private readonly Func<SettingsForm> _settingsFormFactory;

        public MainForm(
            ToggleService toggleService,
            ISettingsStore settingsStore,
            IMonitorController monitorController,
            Func<SettingsForm> settingsFormFactory)
        {
            _toggleService = toggleService ?? throw new ArgumentNullException(nameof(toggleService));
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
        /// Re-derives the mode indicator (from snapshot-file presence, D-14). Called
        /// on startup and after every toggle/Settings-dialog close.
        /// </summary>
        private void RefreshUi()
        {
            bool isInRigMode = _toggleService.IsInRigMode();
            lblMode.Text = isInRigMode ? "Mode: Rig" : "Mode: Normal";
            btnToggle.Text = isInRigMode ? "Switch to Normal Mode" : "Switch to Rig Mode";
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            ToggleResult? result = null;

            try
            {
                if (_toggleService.IsInRigMode())
                {
                    result = _toggleService.ToggleToNormalMode();
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
                            "Please finish configuring Settings (at least one monitor to disable or enable, both audio devices, and the companion app) before switching to Rig Mode.",
                            "Rig Toggle",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    // DISPLAY-07 / D-06: informed-consent confirmation naming every
                    // monitor being disabled AND every monitor being enabled, durably
                    // suppressible via "don't ask again" and reset whenever Settings
                    // changes either configured set (04-RESEARCH.md Pattern 5, generalized
                    // Phase 6). Names are resolved via GetAllMonitors() (not
                    // GetActiveMonitors()) because an enable-set monitor is inactive at
                    // confirm-time and would otherwise fail to resolve.
                    var settings = _settingsStore.Load();
                    if (!settings.SkipMonitorConfirmation)
                    {
                        var disablePaths = settings.MonitorsToDisable ?? new List<string>();
                        var enablePaths = settings.MonitorsToEnable ?? new List<string>();

                        IReadOnlyList<MonitorInfo> allMonitors;
                        try
                        {
                            allMonitors = _monitorController.GetAllMonitors();
                        }
                        catch
                        {
                            // Defensive fallback: an enumeration hiccup must never block the
                            // confirmation — fall back to raw device paths as names.
                            allMonitors = Array.Empty<MonitorInfo>();
                        }

                        string ResolveName(string devicePath) =>
                            allMonitors.FirstOrDefault(m => m.DevicePath == devicePath)?.FriendlyName ?? devicePath;

                        var disableNames = disablePaths.Select(ResolveName).ToList();
                        var enableNames = enablePaths.Select(ResolveName).ToList();

                        using var confirmDialog = new MonitorConfirmDialog(disableNames, enableNames);
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

                    result = _toggleService.ToggleToRigMode();
                }

                RefreshUi();

                if (result is not null && !result.Success)
                {
                    // CORE-04: per-step checklist for a partial failure. State may have
                    // partially changed (e.g. monitor disabled OK but audio failed), which
                    // is why RefreshUi() above always runs before this dialog is shown.
                    MessageBox.Show(
                        this,
                        $"The toggle did not fully complete:\n\n{FormatChecklist(result)}",
                        "Rig Toggle",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                // Basic guard only (D-13/T-02-FAKEFAIL) — this catch is the fallback for the
                // exception-based preflight/corrupted-snapshot guards (unconfigured settings,
                // missing companion app path, corrupted monitor snapshot), which are NOT part
                // of the ToggleResult contract (see Plan 01). Per-step CORE-04 partial-failure
                // reporting for the three mutation steps (Monitor/Audio/App) happens via the
                // ToggleResult checklist above. Exception detail is included (not just a
                // generic message) because this is a single-user diagnostic tool, not a
                // hardened multi-user app — surfacing the real error is more useful than
                // hiding it, especially for CCD-mutation failures that are otherwise
                // unreproducible without rig hardware.
                MessageBox.Show(
                    this,
                    $"Something went wrong while toggling:\n\n{ex.GetType().Name}: {ex.Message}\n\nTry again, or check Settings.",
                    "Rig Toggle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Maps a ToggleResult's per-step outcomes to a human-readable checklist for the
        /// partial-failure MessageBox (CORE-04). One line per step, in step order.
        /// </summary>
        private static string FormatChecklist(ToggleResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.Steps.Select(step => step.Outcome switch
                {
                    ToggleStepOutcome.Succeeded => $"{step.StepName}: OK",
                    ToggleStepOutcome.Failed => $"{step.StepName}: FAILED ({step.Reason})",
                    ToggleStepOutcome.NotAttempted => $"{step.StepName}: not attempted",
                    _ => $"{step.StepName}: unknown",
                }));
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
