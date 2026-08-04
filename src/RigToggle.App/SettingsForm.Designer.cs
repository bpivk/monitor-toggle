namespace RigToggle.App
{
    partial class SettingsForm
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

            // WR-01 (12-REVIEW.md): deterministic backstop unsubscribe, mirroring
            // MainForm's Dispose(bool) pattern. The constructor's FormClosed-based
            // unsubscribe (SettingsForm.cs) covers the normal ShowDialog-then-close
            // path; this covers an abnormal dispose that never fires FormClosed (e.g.
            // an exception between construction and ShowDialog returning) so a disposed
            // instance can never leak a handler onto the app-lifetime
            // WindowsThemeProvider (T-12-05). "-=" on an already-removed handler is a
            // safe no-op, so double-unsubscribe on the normal close path is harmless.
            if (disposing)
            {
                _themeProvider.ThemeChanged -= OnThemeChanged;
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

            this.pnlMonitor = new System.Windows.Forms.Panel();
            this.lblMonitorCaption = new System.Windows.Forms.Label();
            this.dgvMonitors = new System.Windows.Forms.DataGridView();
            this.colMonitorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDisable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colEnable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.lblMonitorWarning = new System.Windows.Forms.Label();
            this.lblMonitorExplain = new System.Windows.Forms.Label();

            this.pnlAudioDevices = new System.Windows.Forms.Panel();
            this.lblAudioDevicesCaption = new System.Windows.Forms.Label();
            this.lblAudioNormalCaption = new System.Windows.Forms.Label();
            this.cboAudioNormal = new System.Windows.Forms.ComboBox();
            this.lblAudioNormalWarning = new System.Windows.Forms.Label();
            this.lblAudioRigCaption = new System.Windows.Forms.Label();
            this.cboAudioRig = new System.Windows.Forms.ComboBox();
            this.lblAudioRigWarning = new System.Windows.Forms.Label();

            this.pnlAppPath = new System.Windows.Forms.Panel();
            this.lblAppPathCaption = new System.Windows.Forms.Label();
            this.txtAppPath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnClearAppPath = new System.Windows.Forms.Button();
            this.lblAppWarning = new System.Windows.Forms.Label();

            this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();

            this.lblHotkeyCaption = new System.Windows.Forms.Label();
            this.txtHotkey = new System.Windows.Forms.TextBox();
            this.lblHotkeyWarning = new System.Windows.Forms.Label();

            this.chkCloseMinimizesToTray = new System.Windows.Forms.CheckBox();
            this.chkMinimizeToTray = new System.Windows.Forms.CheckBox();

            this.chkStartWithWindows = new System.Windows.Forms.CheckBox();
            this.lblAutostartWarning = new System.Windows.Forms.Label();

            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.btnDiscardChanges = new System.Windows.Forms.Button();

            this.errMonitor = new System.Windows.Forms.ErrorProvider(this.components);
            this.errAudioNormal = new System.Windows.Forms.ErrorProvider(this.components);
            this.errAudioRig = new System.Windows.Forms.ErrorProvider(this.components);
            this.errApp = new System.Windows.Forms.ErrorProvider(this.components);
            this.errAutostart = new System.Windows.Forms.ErrorProvider(this.components);
            this.errHotkey = new System.Windows.Forms.ErrorProvider(this.components);
            this.dlgOpenExe = new System.Windows.Forms.OpenFileDialog();

            ((System.ComponentModel.ISupportInitialize)(this.errMonitor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioNormal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioRig)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errApp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAutostart)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errHotkey)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitors)).BeginInit();
            this.pnlMonitor.SuspendLayout();
            this.pnlAudioDevices.SuspendLayout();
            this.pnlAppPath.SuspendLayout();
            this.SuspendLayout();

            //
            // pnlMonitor (THEME-05: flat bordered Panel replacing the grpMonitor GroupBox
            // bevel -- GroupBox has no flat variant, SetColorMode cannot recolor its 3D
            // border. Same Location/Size as the original GroupBox, zero layout drift.)
            //
            this.pnlMonitor.Location = new System.Drawing.Point(12, 12);
            this.pnlMonitor.Size = new System.Drawing.Size(396, 234);
            this.pnlMonitor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMonitor.Name = "pnlMonitor";
            this.pnlMonitor.Controls.Add(this.lblMonitorCaption);
            this.pnlMonitor.Controls.Add(this.lblMonitorExplain);
            this.pnlMonitor.Controls.Add(this.dgvMonitors);
            this.pnlMonitor.Controls.Add(this.lblMonitorWarning);

            //
            // lblMonitorCaption
            //
            // Positioned at the GroupBox's native caption inset (~9px from the panel's
            // top-left) so every re-parented child below keeps its existing Location
            // unchanged (UI-SPEC Spacing contract -- pixel-parity, not a new token).
            this.lblMonitorCaption.Text = "Monitor";
            this.lblMonitorCaption.Location = new System.Drawing.Point(9, 9);
            this.lblMonitorCaption.AutoSize = true;
            this.lblMonitorCaption.Name = "lblMonitorCaption";

            //
            // dgvMonitors
            //
            this.dgvMonitors.AllowUserToAddRows = false;
            this.dgvMonitors.AllowUserToDeleteRows = false;
            this.dgvMonitors.AllowUserToResizeRows = false;
            this.dgvMonitors.AllowUserToResizeColumns = false;
            this.dgvMonitors.RowHeadersVisible = false;
            this.dgvMonitors.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvMonitors.MultiSelect = false;
            this.dgvMonitors.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvMonitors.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvMonitors.Location = new System.Drawing.Point(12, 80);
            this.dgvMonitors.Size = new System.Drawing.Size(372, 120);
            this.dgvMonitors.Name = "dgvMonitors";
            this.dgvMonitors.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMonitorName,
            this.colDisable,
            this.colEnable});

            //
            // colMonitorName
            //
            this.colMonitorName.HeaderText = "Monitor";
            this.colMonitorName.Name = "colMonitorName";
            this.colMonitorName.ReadOnly = true;
            this.colMonitorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;

            //
            // colDisable
            //
            this.colDisable.HeaderText = "Off (Rig)";
            this.colDisable.Name = "colDisable";
            this.colDisable.Width = 66;
            this.colDisable.ToolTipText = "Turns this monitor off when switching to Rig Mode. Restored automatically when switching back to Normal Mode.";

            //
            // colEnable
            //
            this.colEnable.HeaderText = "On (Rig)";
            this.colEnable.Name = "colEnable";
            this.colEnable.Width = 66;
            this.colEnable.ToolTipText = "Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power). Turned off again automatically when switching back to Normal Mode.";

            //
            // lblMonitorExplain
            //
            this.lblMonitorExplain.Location = new System.Drawing.Point(12, 22);
            this.lblMonitorExplain.Size = new System.Drawing.Size(372, 50);
            this.lblMonitorExplain.AutoSize = false;
            this.lblMonitorExplain.Text = "Only controls what changes when switching TO Rig Mode. Normal Mode is always restored exactly as it was before — nothing to set up separately.";
            this.lblMonitorExplain.Name = "lblMonitorExplain";

            //
            // lblMonitorWarning
            //
            this.lblMonitorWarning.Location = new System.Drawing.Point(12, 206);
            this.lblMonitorWarning.Size = new System.Drawing.Size(372, 20);
            this.lblMonitorWarning.AutoSize = false;
            this.lblMonitorWarning.Visible = false;
            this.lblMonitorWarning.Name = "lblMonitorWarning";

            //
            // pnlAudioDevices (THEME-05: flat bordered Panel replacing the grpAudioDevices
            // GroupBox bevel. Same Location/Size as the original GroupBox.)
            //
            this.pnlAudioDevices.Location = new System.Drawing.Point(12, 258);
            this.pnlAudioDevices.Size = new System.Drawing.Size(396, 132);
            this.pnlAudioDevices.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlAudioDevices.Name = "pnlAudioDevices";
            this.pnlAudioDevices.Controls.Add(this.lblAudioDevicesCaption);
            this.pnlAudioDevices.Controls.Add(this.lblAudioNormalCaption);
            this.pnlAudioDevices.Controls.Add(this.cboAudioNormal);
            this.pnlAudioDevices.Controls.Add(this.lblAudioNormalWarning);
            this.pnlAudioDevices.Controls.Add(this.lblAudioRigCaption);
            this.pnlAudioDevices.Controls.Add(this.cboAudioRig);
            this.pnlAudioDevices.Controls.Add(this.lblAudioRigWarning);

            //
            // lblAudioDevicesCaption
            //
            this.lblAudioDevicesCaption.Text = "Audio Devices";
            this.lblAudioDevicesCaption.Location = new System.Drawing.Point(9, 9);
            this.lblAudioDevicesCaption.AutoSize = true;
            this.lblAudioDevicesCaption.Name = "lblAudioDevicesCaption";

            //
            // lblAudioNormalCaption
            //
            this.lblAudioNormalCaption.Text = "Normal:";
            this.lblAudioNormalCaption.Location = new System.Drawing.Point(12, 25);
            this.lblAudioNormalCaption.Size = new System.Drawing.Size(48, 20);
            this.lblAudioNormalCaption.Name = "lblAudioNormalCaption";

            //
            // cboAudioNormal
            //
            this.cboAudioNormal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAudioNormal.Location = new System.Drawing.Point(64, 22);
            this.cboAudioNormal.Size = new System.Drawing.Size(320, 23);
            this.cboAudioNormal.Name = "cboAudioNormal";

            //
            // lblAudioNormalWarning
            //
            this.lblAudioNormalWarning.Location = new System.Drawing.Point(64, 48);
            this.lblAudioNormalWarning.Size = new System.Drawing.Size(320, 20);
            this.lblAudioNormalWarning.AutoSize = false;
            this.lblAudioNormalWarning.Visible = false;
            this.lblAudioNormalWarning.Name = "lblAudioNormalWarning";

            //
            // lblAudioRigCaption
            //
            this.lblAudioRigCaption.Text = "Rig:";
            this.lblAudioRigCaption.Location = new System.Drawing.Point(12, 77);
            this.lblAudioRigCaption.Size = new System.Drawing.Size(48, 20);
            this.lblAudioRigCaption.Name = "lblAudioRigCaption";

            //
            // cboAudioRig
            //
            this.cboAudioRig.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAudioRig.Location = new System.Drawing.Point(64, 74);
            this.cboAudioRig.Size = new System.Drawing.Size(320, 23);
            this.cboAudioRig.Name = "cboAudioRig";

            //
            // lblAudioRigWarning
            //
            this.lblAudioRigWarning.Location = new System.Drawing.Point(64, 100);
            this.lblAudioRigWarning.Size = new System.Drawing.Size(320, 20);
            this.lblAudioRigWarning.AutoSize = false;
            this.lblAudioRigWarning.Visible = false;
            this.lblAudioRigWarning.Name = "lblAudioRigWarning";

            //
            // pnlAppPath (THEME-05: flat bordered Panel replacing the grpAppPath GroupBox
            // bevel. Same Location/Size as the original GroupBox. CRITICAL: AllowDrop and
            // the AppPath_DragEnter/AppPath_DragDrop wiring move here from the old
            // GroupBox verbatim -- must not be dropped (T-12-07).)
            //
            this.pnlAppPath.Location = new System.Drawing.Point(12, 402);
            this.pnlAppPath.Size = new System.Drawing.Size(396, 76);
            this.pnlAppPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlAppPath.Name = "pnlAppPath";
            this.pnlAppPath.AllowDrop = true;
            this.pnlAppPath.Controls.Add(this.lblAppPathCaption);
            this.pnlAppPath.Controls.Add(this.txtAppPath);
            this.pnlAppPath.Controls.Add(this.btnBrowse);
            this.pnlAppPath.Controls.Add(this.btnClearAppPath);
            this.pnlAppPath.Controls.Add(this.lblAppWarning);
            this.pnlAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.pnlAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // lblAppPathCaption
            //
            this.lblAppPathCaption.Text = "Target App";
            this.lblAppPathCaption.Location = new System.Drawing.Point(9, 9);
            this.lblAppPathCaption.AutoSize = true;
            this.lblAppPathCaption.Name = "lblAppPathCaption";

            //
            // txtAppPath
            //
            this.txtAppPath.ReadOnly = true;
            this.txtAppPath.Text = "No app shortcut or .exe selected";
            this.txtAppPath.Location = new System.Drawing.Point(12, 22);
            // 15-03/D-01: narrowed from 288 to 220 to make room for btnClearAppPath on
            // the same row without touching panel height or downstream control Y
            // positions -- txtAppPath's range (x=12..232) stays a strict subset of the
            // original x=12..300 span, so it never overlaps either sibling button.
            this.txtAppPath.Size = new System.Drawing.Size(220, 23);
            this.txtAppPath.Name = "txtAppPath";
            this.txtAppPath.AllowDrop = true;
            this.txtAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.txtAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // btnBrowse
            //
            this.btnBrowse.Text = "Browse…";
            // 15-03/D-01: narrowed and moved left (from x=306,width=78) to make room for
            // btnClearAppPath at x=314..384 on the same row.
            this.btnBrowse.Location = new System.Drawing.Point(238, 21);
            this.btnBrowse.Size = new System.Drawing.Size(70, 25);
            this.btnBrowse.Name = "btnBrowse";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): FlatStyle.Flat, not .System -- the
            // Windows 11 rig proved FlatStyle.System buttons do NOT pick up dark-mode
            // coloring on this runtime. ThemeApplier.ThemeButton (called from
            // SettingsForm_Load and OnThemeChanged) re-asserts Flat + explicit palette
            // colors, working around dotnet/winforms#13897's unreliable FlatAppearance
            // auto-apply pipeline via BorderSize=0 + explicit hover/pressed overrides.
            this.btnBrowse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);

            //
            // btnClearAppPath
            //
            // 15-03/D-01: explicit Clear affordance -- txtAppPath is ReadOnly (see below),
            // so there is no other way for the user to unset a previously-configured app
            // path. Enabled only when a path is currently set (toggled from
            // PopulateAppPathField/BtnClearAppPath_Click/BtnBrowse_Click/AppPath_DragDrop
            // in SettingsForm.cs); starts disabled here since the initial state is
            // resolved on Load from persisted settings.
            this.btnClearAppPath.Text = "Clear";
            this.btnClearAppPath.Location = new System.Drawing.Point(314, 21);
            this.btnClearAppPath.Size = new System.Drawing.Size(70, 25);
            this.btnClearAppPath.Name = "btnClearAppPath";
            this.btnClearAppPath.Enabled = false;
            // 12-05/THEME-05 (12-REVIEW.md CR-02): FlatStyle.Flat, not .System -- see
            // btnBrowse's comment above for the full dotnet/winforms#13897 rationale.
            // This applies identically to every themed button in this dialog.
            this.btnClearAppPath.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClearAppPath.Click += new System.EventHandler(this.BtnClearAppPath_Click);

            //
            // lblAppWarning
            //
            this.lblAppWarning.Location = new System.Drawing.Point(12, 48);
            this.lblAppWarning.Size = new System.Drawing.Size(372, 20);
            this.lblAppWarning.AutoSize = false;
            this.lblAppWarning.Visible = false;
            this.lblAppWarning.Name = "lblAppWarning";

            //
            // chkEnableDebugLogging
            //
            this.chkEnableDebugLogging.Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)";
            this.chkEnableDebugLogging.Location = new System.Drawing.Point(12, 484);
            this.chkEnableDebugLogging.Size = new System.Drawing.Size(396, 40);
            this.chkEnableDebugLogging.AutoSize = false;
            this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";

            //
            // lblHotkeyCaption
            //
            this.lblHotkeyCaption.Text = "Hotkey:";
            this.lblHotkeyCaption.Location = new System.Drawing.Point(12, 532);
            this.lblHotkeyCaption.Size = new System.Drawing.Size(60, 20);
            this.lblHotkeyCaption.AutoSize = false;
            this.lblHotkeyCaption.Name = "lblHotkeyCaption";

            //
            // txtHotkey
            //
            // D-01/UI-SPEC: ReadOnly + TabStop=false are load-bearing, not an oversight --
            // if this field were reachable via Tab-cycling, landing on it via Tab and then
            // pressing Tab/Enter/Escape to continue normal dialog navigation would be
            // indistinguishable from "the user is recording those exact keys as their
            // hotkey." Capture mode must only ever begin via an explicit mouse click.
            // Cursor=Hand (not the default I-beam) reinforces click-to-activate, not a
            // free-text field. Font intentionally left unset (inherits form default).
            this.txtHotkey.ReadOnly = true;
            this.txtHotkey.TabStop = false;
            this.txtHotkey.Cursor = System.Windows.Forms.Cursors.Hand;
            this.txtHotkey.Location = new System.Drawing.Point(76, 529);
            this.txtHotkey.Size = new System.Drawing.Size(200, 23);
            this.txtHotkey.Name = "txtHotkey";

            //
            // lblHotkeyWarning
            //
            this.lblHotkeyWarning.Location = new System.Drawing.Point(12, 556);
            this.lblHotkeyWarning.Size = new System.Drawing.Size(396, 36);
            this.lblHotkeyWarning.AutoSize = false;
            this.lblHotkeyWarning.Visible = false;
            this.lblHotkeyWarning.Name = "lblHotkeyWarning";

            //
            // chkCloseMinimizesToTray
            //
            this.chkCloseMinimizesToTray.Text = "Closing the window (X) minimizes to tray";
            this.chkCloseMinimizesToTray.Location = new System.Drawing.Point(12, 600);
            this.chkCloseMinimizesToTray.Size = new System.Drawing.Size(396, 24);
            this.chkCloseMinimizesToTray.AutoSize = false;
            this.chkCloseMinimizesToTray.Name = "chkCloseMinimizesToTray";

            //
            // chkMinimizeToTray
            //
            this.chkMinimizeToTray.Text = "Minimizing the window also sends it to tray";
            this.chkMinimizeToTray.Location = new System.Drawing.Point(12, 632);
            this.chkMinimizeToTray.Size = new System.Drawing.Size(396, 24);
            this.chkMinimizeToTray.AutoSize = false;
            this.chkMinimizeToTray.Name = "chkMinimizeToTray";

            //
            // chkStartWithWindows
            //
            this.chkStartWithWindows.Text = "Start with Windows";
            this.chkStartWithWindows.Location = new System.Drawing.Point(12, 664);
            this.chkStartWithWindows.Size = new System.Drawing.Size(396, 24);
            this.chkStartWithWindows.AutoSize = false;
            this.chkStartWithWindows.Name = "chkStartWithWindows";

            //
            // lblAutostartWarning
            //
            this.lblAutostartWarning.Location = new System.Drawing.Point(12, 688);
            this.lblAutostartWarning.Size = new System.Drawing.Size(396, 20);
            this.lblAutostartWarning.AutoSize = false;
            this.lblAutostartWarning.Visible = false;
            this.lblAutostartWarning.Name = "lblAutostartWarning";

            //
            // btnSaveSettings
            //
            this.btnSaveSettings.Text = "Save Settings";
            this.btnSaveSettings.Location = new System.Drawing.Point(180, 720);
            this.btnSaveSettings.Size = new System.Drawing.Size(110, 32);
            this.btnSaveSettings.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveSettings.Name = "btnSaveSettings";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): see btnBrowse's comment above for the
            // full rig-finding + #13897 rationale.
            this.btnSaveSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveSettings.Click += new System.EventHandler(this.BtnSaveSettings_Click);

            //
            // btnDiscardChanges
            //
            this.btnDiscardChanges.Text = "Discard Changes";
            this.btnDiscardChanges.Location = new System.Drawing.Point(298, 720);
            this.btnDiscardChanges.Size = new System.Drawing.Size(110, 32);
            this.btnDiscardChanges.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnDiscardChanges.Name = "btnDiscardChanges";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): see btnBrowse's comment above for the
            // full rig-finding + #13897 rationale.
            this.btnDiscardChanges.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            //
            // dlgOpenExe
            //
            this.dlgOpenExe.Filter = "App shortcuts and executables (*.lnk;*.exe)|*.lnk;*.exe";
            this.dlgOpenExe.CheckFileExists = true;
            this.dlgOpenExe.Title = "Select Target App";

            //
            // errMonitor / errAudioNormal / errAudioRig / errApp / errAutostart / errHotkey
            //
            this.errMonitor.ContainerControl = this;
            this.errAudioNormal.ContainerControl = this;
            this.errAudioRig.ContainerControl = this;
            this.errApp.ContainerControl = this;
            this.errAutostart.ContainerControl = this;
            this.errHotkey.ContainerControl = this;

            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 768);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Rig Toggle — Settings";
            this.Name = "SettingsForm";

            this.Controls.Add(this.pnlMonitor);
            this.Controls.Add(this.pnlAudioDevices);
            this.Controls.Add(this.pnlAppPath);
            this.Controls.Add(this.chkEnableDebugLogging);
            this.Controls.Add(this.lblHotkeyCaption);
            this.Controls.Add(this.txtHotkey);
            this.Controls.Add(this.lblHotkeyWarning);
            this.Controls.Add(this.chkCloseMinimizesToTray);
            this.Controls.Add(this.chkMinimizeToTray);
            this.Controls.Add(this.chkStartWithWindows);
            this.Controls.Add(this.lblAutostartWarning);
            this.Controls.Add(this.btnSaveSettings);
            this.Controls.Add(this.btnDiscardChanges);

            ((System.ComponentModel.ISupportInitialize)(this.errMonitor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioNormal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioRig)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errApp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAutostart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errHotkey)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitors)).EndInit();
            this.pnlMonitor.ResumeLayout(false);
            this.pnlAudioDevices.ResumeLayout(false);
            this.pnlAppPath.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel pnlMonitor;
        private System.Windows.Forms.Label lblMonitorCaption;
        private System.Windows.Forms.DataGridView dgvMonitors;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonitorName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colDisable;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEnable;
        private System.Windows.Forms.Label lblMonitorWarning;
        private System.Windows.Forms.Label lblMonitorExplain;

        private System.Windows.Forms.Panel pnlAudioDevices;
        private System.Windows.Forms.Label lblAudioDevicesCaption;
        private System.Windows.Forms.Label lblAudioNormalCaption;
        private System.Windows.Forms.ComboBox cboAudioNormal;
        private System.Windows.Forms.Label lblAudioNormalWarning;
        private System.Windows.Forms.Label lblAudioRigCaption;
        private System.Windows.Forms.ComboBox cboAudioRig;
        private System.Windows.Forms.Label lblAudioRigWarning;

        private System.Windows.Forms.Panel pnlAppPath;
        private System.Windows.Forms.Label lblAppPathCaption;
        private System.Windows.Forms.TextBox txtAppPath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Button btnClearAppPath;
        private System.Windows.Forms.Label lblAppWarning;

        private System.Windows.Forms.CheckBox chkEnableDebugLogging;

        private System.Windows.Forms.Label lblHotkeyCaption;
        private System.Windows.Forms.TextBox txtHotkey;
        private System.Windows.Forms.Label lblHotkeyWarning;

        private System.Windows.Forms.CheckBox chkCloseMinimizesToTray;
        private System.Windows.Forms.CheckBox chkMinimizeToTray;

        private System.Windows.Forms.CheckBox chkStartWithWindows;
        private System.Windows.Forms.Label lblAutostartWarning;

        private System.Windows.Forms.Button btnSaveSettings;
        private System.Windows.Forms.Button btnDiscardChanges;

        private System.Windows.Forms.ErrorProvider errMonitor;
        private System.Windows.Forms.ErrorProvider errAudioNormal;
        private System.Windows.Forms.ErrorProvider errAudioRig;
        private System.Windows.Forms.ErrorProvider errApp;
        private System.Windows.Forms.ErrorProvider errAutostart;
        private System.Windows.Forms.ErrorProvider errHotkey;
        private System.Windows.Forms.OpenFileDialog dlgOpenExe;
    }
}
