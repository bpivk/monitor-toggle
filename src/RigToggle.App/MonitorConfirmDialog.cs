namespace RigToggle.App
{
    /// <summary>
    /// DISPLAY-03 safety confirmation: shown before the primary monitor is disabled,
    /// naming the configured monitor by friendly name. Pure display data — no Core
    /// interface is injected (04-RESEARCH.md Pattern 5) — the caller (MainForm)
    /// resolves the friendly name via IMonitorController before constructing this.
    /// </summary>
    public partial class MonitorConfirmDialog : Form
    {
        public bool DontAskAgain => chkDontAskAgain.Checked;

        public MonitorConfirmDialog(string monitorFriendlyName)
        {
            InitializeComponent();

            lblMessage.Text = $"This will disable \"{monitorFriendlyName}\" (primary). Continue?";
            this.AcceptButton = btnContinue;
            this.CancelButton = btnCancel;
        }
    }
}
