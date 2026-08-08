namespace RigToggle.App
{
    partial class MonitorPanelForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            // WR-01 (12-REVIEW.md pattern, reused here): deterministic backstop
            // unsubscribe/dispose, mirroring SettingsForm/MonitorConfirmDialog's
            // Dispose(bool) pattern. The constructor's FormClosed-based unsubscribe
            // (MonitorPanelForm.cs) covers the normal open-then-close path; this
            // covers an abnormal dispose that never fires FormClosed, so a disposed
            // instance can never leak a handler onto the app-lifetime
            // WindowsThemeProvider or the process-lifetime SystemEvents handler
            // list. "-=" on an already-removed handler is a safe no-op, so
            // double-unsubscribe on the normal close path is harmless. _dotActive/
            // _dotInactive are shared per-instance Bitmaps (Task 2) created once in
            // the constructor -- they must be disposed here too, or a long session
            // that opens/closes this non-modal panel repeatedly would leak GDI
            // handles (T-17-10).
            if (disposing)
            {
                _themeProvider.ThemeChanged -= OnThemeChanged;
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
                _dotActive?.Dispose();
                _dotInactive?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            this.dgvMonitorPanel = new System.Windows.Forms.DataGridView();
            this.colStatus = new System.Windows.Forms.DataGridViewImageColumn();
            this.colMonitorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colAction = new System.Windows.Forms.DataGridViewButtonColumn();
            this.lblEmptyState = new System.Windows.Forms.Label();
            this.btnIdentify = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitorPanel)).BeginInit();
            this.SuspendLayout();

            //
            // dgvMonitorPanel
            //
            this.dgvMonitorPanel.AllowUserToAddRows = false;
            this.dgvMonitorPanel.AllowUserToDeleteRows = false;
            this.dgvMonitorPanel.AllowUserToResizeRows = false;
            this.dgvMonitorPanel.AllowUserToResizeColumns = false;
            this.dgvMonitorPanel.RowHeadersVisible = false;
            this.dgvMonitorPanel.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvMonitorPanel.MultiSelect = false;
            // 17-RESEARCH.md Pitfall 5: a DataGridViewButtonColumn cell still raises
            // CellClick even when the grid is ReadOnly -- read-only mode structurally
            // rules out the checkbox dirty-state commit-lag quirk
            // SettingsForm.DgvMonitors_CurrentCellDirtyStateChanged works around, since
            // there is no editable cell state to commit at all. Do not convert colAction
            // into a checkbox column.
            this.dgvMonitorPanel.ReadOnly = true;
            this.dgvMonitorPanel.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvMonitorPanel.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvMonitorPanel.Location = new System.Drawing.Point(16, 16);
            this.dgvMonitorPanel.Size = new System.Drawing.Size(388, 200);
            this.dgvMonitorPanel.Name = "dgvMonitorPanel";
            this.dgvMonitorPanel.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStatus,
            this.colMonitorName,
            this.colAction});
            this.dgvMonitorPanel.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DgvMonitorPanel_CellClick);

            //
            // colStatus
            //
            this.colStatus.HeaderText = string.Empty;
            this.colStatus.Width = 32;
            this.colStatus.ImageLayout = System.Windows.Forms.DataGridViewImageCellLayout.Normal;
            this.colStatus.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colStatus.DefaultCellStyle.Padding = new System.Windows.Forms.Padding(4);
            this.colStatus.Name = "colStatus";

            //
            // colMonitorName
            //
            this.colMonitorName.HeaderText = "Monitor";
            this.colMonitorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMonitorName.Name = "colMonitorName";

            //
            // colAction
            //
            // Label is per-row ("Disable"/"Enable"), set during row population
            // (MonitorPanelForm.cs PopulateMonitorGrid) -- never a fixed column-wide
            // caption.
            this.colAction.HeaderText = string.Empty;
            this.colAction.Width = 80;
            this.colAction.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.colAction.UseColumnTextForButtonValue = false;
            this.colAction.Name = "colAction";

            //
            // lblEmptyState
            //
            // Occupies the grid's own rectangle and is shown in its place
            // (dgvMonitorPanel.Visible = false) when GetAllMonitors() returns zero
            // rows -- a defensive fallback, not an expected user-facing state
            // (17-UI-SPEC.md Copywriting Contract).
            this.lblEmptyState.Location = new System.Drawing.Point(16, 16);
            this.lblEmptyState.Size = new System.Drawing.Size(388, 40);
            this.lblEmptyState.AutoSize = false;
            this.lblEmptyState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblEmptyState.Visible = false;
            this.lblEmptyState.Text = "No monitors detected. Reconnect a display and reopen this panel.";
            this.lblEmptyState.Name = "lblEmptyState";

            //
            // btnIdentify
            //
            // 17-UI-SPEC.md Spacing Scale: 24px (lg) gap below the grid's bottom edge
            // (16 + 200 = 216, + 24 = 240).
            this.btnIdentify.Location = new System.Drawing.Point(16, 240);
            this.btnIdentify.Size = new System.Drawing.Size(100, 32);
            this.btnIdentify.Text = "Identify";
            this.btnIdentify.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnIdentify.Name = "btnIdentify";
            this.btnIdentify.Click += new System.EventHandler(this.BtnIdentify_Click);

            //
            // MonitorPanelForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 288);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.ShowInTaskbar = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rig Toggle — Monitors";
            this.Name = "MonitorPanelForm";

            this.Controls.Add(this.dgvMonitorPanel);
            this.Controls.Add(this.lblEmptyState);
            this.Controls.Add(this.btnIdentify);

            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitorPanel)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dgvMonitorPanel;
        private System.Windows.Forms.DataGridViewImageColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonitorName;
        private System.Windows.Forms.DataGridViewButtonColumn colAction;
        private System.Windows.Forms.Label lblEmptyState;
        private System.Windows.Forms.Button btnIdentify;
    }
}
