using System.Collections.Generic;
using System.Linq;

namespace RigToggle.App
{
    /// <summary>
    /// DISPLAY-07/D-06 safety confirmation: shown before any configured monitor
    /// disable/enable is applied, naming every monitor in both sets by friendly
    /// name (full, comma-separated, never truncated). Pure display data — no Core
    /// interface is injected (04-RESEARCH.md Pattern 5) — the caller (MainForm)
    /// resolves both sets' friendly names via IMonitorController.GetAllMonitors()
    /// before constructing this (an enable-set monitor is inactive at confirm-time,
    /// so GetActiveMonitors() cannot resolve it).
    /// </summary>
    public partial class MonitorConfirmDialog : Form
    {
        public bool DontAskAgain => chkDontAskAgain.Checked;

        public MonitorConfirmDialog(IReadOnlyList<string> disableNames, IReadOnlyList<string> enableNames)
        {
            InitializeComponent();

            var clauses = new List<string>();
            if (disableNames.Count > 0) clauses.Add($"disable {FormatNames(disableNames)}");
            if (enableNames.Count > 0) clauses.Add($"enable {FormatNames(enableNames)}");
            lblMessage.Text = $"This will {string.Join(" and ", clauses)}. Continue?";

            this.AcceptButton = btnContinue;
            this.CancelButton = btnCancel;
        }

        private static string FormatNames(IReadOnlyList<string> names) =>
            string.Join(", ", names.Select(n => $"\"{n}\""));
    }
}
