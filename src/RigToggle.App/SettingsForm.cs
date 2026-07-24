using RigToggle.Core.Abstractions;
using RigToggle.Core.Models;

namespace RigToggle.App
{
    /// <summary>
    /// Settings modal dialog (D-03): three labeled sections (Monitor, Audio Devices,
    /// Application Path — D-09) bound to real enumerated hardware (D-05) through the
    /// Core interfaces only — never instantiates concrete Windows adapters directly
    /// (02-RESEARCH.md Anti-Pattern: "Putting P/Invoke/COM calls in ... code-behind").
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly IMonitorController _monitorController;
        private readonly IAudioController _audioController;
        private readonly ISettingsStore _settingsStore;

        private AppSettings _settings = new();

        /// <summary>
        /// Display/value wrapper for ComboBox binding (DisplayMember/ValueMember) —
        /// 02-RESEARCH.md Pattern 2.
        /// </summary>
        private sealed record PickerItem(string Id, string DisplayLabel);

        public SettingsForm(IMonitorController monitorController, IAudioController audioController, ISettingsStore settingsStore)
        {
            _monitorController = monitorController ?? throw new ArgumentNullException(nameof(monitorController));
            _audioController = audioController ?? throw new ArgumentNullException(nameof(audioController));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

            InitializeComponent();

            // Esc / system close box (X) both produce DialogResult.Cancel via the
            // declarative Discard button — no extra FormClosing handler needed
            // (02-RESEARCH.md Pattern 5).
            this.AcceptButton = btnSaveSettings;
            this.CancelButton = btnDiscardChanges;

            this.Load += SettingsForm_Load;
            cboMonitor.SelectedIndexChanged += OnPickerChanged;
            cboAudioNormal.SelectedIndexChanged += OnPickerChanged;
            cboAudioRig.SelectedIndexChanged += OnPickerChanged;
        }

        private void SettingsForm_Load(object? sender, EventArgs e)
        {
            // Re-enumerate on every open — no manual Refresh control exists (D-11).
            _settings = _settingsStore.Load();
            PopulateMonitorPicker();
            PopulateAudioPickers();
            PopulateAppPathField();
            ValidateSettingsForm();
        }

        private void OnPickerChanged(object? sender, EventArgs e) => ValidateSettingsForm();

        private void PopulateMonitorPicker()
        {
            errMonitor.SetError(cboMonitor, string.Empty);
            lblMonitorWarning.Visible = false;

            IReadOnlyList<MonitorInfo> monitors;
            try
            {
                monitors = _monitorController.GetActiveMonitors();
            }
            catch (Exception)
            {
                // Defensive: enumeration should not crash Settings open; degrade to empty-state.
                monitors = Array.Empty<MonitorInfo>();
            }

            var items = monitors
                .Select(m => new PickerItem(m.DevicePath, m.IsPrimary ? $"{m.FriendlyName} (Primary)" : m.FriendlyName))
                .ToList();

            // Pitfall 1: unhook SelectedIndexChanged around DataSource assignment to avoid
            // a spurious change event firing mid-populate.
            cboMonitor.SelectedIndexChanged -= OnPickerChanged;

            if (items.Count == 0)
            {
                cboMonitor.DataSource = null;
                cboMonitor.Items.Clear();
                cboMonitor.Items.Add("No displays detected.");
                cboMonitor.SelectedIndex = -1;
                cboMonitor.Enabled = false;
            }
            else
            {
                cboMonitor.Enabled = true;
                cboMonitor.DataSource = items;
                cboMonitor.DisplayMember = nameof(PickerItem.DisplayLabel);
                cboMonitor.ValueMember = nameof(PickerItem.Id);
                cboMonitor.SelectedIndex = -1;

                string? savedId = _settings.MonitorDevicePath;
                if (savedId is not null)
                {
                    var match = items.FirstOrDefault(i => i.Id == savedId);
                    if (match is not null)
                    {
                        cboMonitor.SelectedItem = match;
                    }
                    else
                    {
                        // D-10: saved-but-not-found — unselected + inline warning.
                        // (savedId is null branch above is the distinct first-run case — no warning, Pitfall 3.)
                        ShowStaleWarning(errMonitor, cboMonitor, lblMonitorWarning, "monitor");
                    }
                }
            }

            cboMonitor.SelectedIndexChanged += OnPickerChanged;
        }

        private void PopulateAudioPickers()
        {
            IReadOnlyList<AudioDeviceInfo> devices;
            try
            {
                devices = _audioController.GetPlaybackDevices();
            }
            catch (Exception)
            {
                devices = Array.Empty<AudioDeviceInfo>();
            }

            var items = devices.Select(d => new PickerItem(d.Id, d.FriendlyName)).ToList();

            PopulateAudioCombo(cboAudioNormal, errAudioNormal, lblAudioNormalWarning, items, _settings.NormalAudioDeviceId);
            PopulateAudioCombo(cboAudioRig, errAudioRig, lblAudioRigWarning, items, _settings.RigAudioDeviceId);
        }

        private void PopulateAudioCombo(ComboBox combo, ErrorProvider errProvider, Label warningLabel, List<PickerItem> items, string? savedId)
        {
            errProvider.SetError(combo, string.Empty);
            warningLabel.Visible = false;

            combo.SelectedIndexChanged -= OnPickerChanged;

            if (items.Count == 0)
            {
                combo.DataSource = null;
                combo.Items.Clear();
                combo.Items.Add("No audio devices detected.");
                combo.SelectedIndex = -1;
                combo.Enabled = false;
            }
            else
            {
                combo.Enabled = true;
                // Fresh List instance per combo (items.ToList()) — binding the exact same
                // list object to two ComboBoxes would share a CurrencyManager position.
                combo.DataSource = items.ToList();
                combo.DisplayMember = nameof(PickerItem.DisplayLabel);
                combo.ValueMember = nameof(PickerItem.Id);
                combo.SelectedIndex = -1;

                if (savedId is not null)
                {
                    var match = items.FirstOrDefault(i => i.Id == savedId);
                    if (match is not null)
                    {
                        combo.SelectedItem = new PickerItem(match.Id, match.DisplayLabel);
                    }
                    else
                    {
                        ShowStaleWarning(errProvider, combo, warningLabel, "audio device");
                    }
                }
            }

            combo.SelectedIndexChanged += OnPickerChanged;
        }

        private void PopulateAppPathField()
        {
            errApp.SetError(txtAppPath, string.Empty);
            lblAppWarning.Visible = false;

            string? savedPath = _settings.CompanionAppPath;
            if (savedPath is null)
            {
                // First-ever run: no warning (Pitfall 3).
                txtAppPath.Text = "No file selected";
            }
            else
            {
                txtAppPath.Text = savedPath;
                if (!IsValidExePath(savedPath))
                {
                    // D-10: previously configured, but no longer resolves — inline warning.
                    // The field itself can't be "unselected" like a ComboBox; leaving the
                    // stale path visible alongside the warning lets the user see what to fix.
                    ShowStaleWarning(errApp, txtAppPath, lblAppWarning, "application");
                }
            }
        }

        private static void ShowStaleWarning(ErrorProvider errProvider, Control control, Label warningLabel, string noun)
        {
            string message = $"Previously selected {noun} not found — please reselect.";
            errProvider.SetError(control, message);
            warningLabel.Text = message;
            warningLabel.Visible = true;
        }

        private void ValidateSettingsForm()
        {
            bool monitorOk = cboMonitor.SelectedItem is PickerItem;
            bool audioNormalOk = cboAudioNormal.SelectedItem is PickerItem;
            bool audioRigOk = cboAudioRig.SelectedItem is PickerItem;
            bool appPathOk = IsValidExePath(txtAppPath.Text);

            btnSaveSettings.Enabled = monitorOk && audioNormalOk && audioRigOk && appPathOk;
        }

        private static bool IsValidExePath(string path)
            => !string.IsNullOrEmpty(path)
               && File.Exists(path)
               && string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);

        // Browse (D-06) and Save/persist (SETTINGS-03) wiring is implemented in
        // the Task 3 portion of this file — see the region below.

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            // Filled in by Task 3.
        }

        private void BtnSaveSettings_Click(object? sender, EventArgs e)
        {
            // Filled in by Task 3.
        }
    }
}
