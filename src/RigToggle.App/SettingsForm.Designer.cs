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

            this.grpMonitor = new System.Windows.Forms.GroupBox();
            this.dgvMonitors = new System.Windows.Forms.DataGridView();
            this.colMonitorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDisable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colEnable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.lblMonitorWarning = new System.Windows.Forms.Label();

            this.grpAudioDevices = new System.Windows.Forms.GroupBox();
            this.lblAudioNormalCaption = new System.Windows.Forms.Label();
            this.cboAudioNormal = new System.Windows.Forms.ComboBox();
            this.lblAudioNormalWarning = new System.Windows.Forms.Label();
            this.lblAudioRigCaption = new System.Windows.Forms.Label();
            this.cboAudioRig = new System.Windows.Forms.ComboBox();
            this.lblAudioRigWarning = new System.Windows.Forms.Label();

            this.grpAppPath = new System.Windows.Forms.GroupBox();
            this.txtAppPath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblAppWarning = new System.Windows.Forms.Label();

            this.chkEnableDebugLogging = new System.Windows.Forms.CheckBox();

            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.btnDiscardChanges = new System.Windows.Forms.Button();

            this.errMonitor = new System.Windows.Forms.ErrorProvider(this.components);
            this.errAudioNormal = new System.Windows.Forms.ErrorProvider(this.components);
            this.errAudioRig = new System.Windows.Forms.ErrorProvider(this.components);
            this.errApp = new System.Windows.Forms.ErrorProvider(this.components);
            this.dlgOpenExe = new System.Windows.Forms.OpenFileDialog();

            ((System.ComponentModel.ISupportInitialize)(this.errMonitor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioNormal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioRig)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.errApp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitors)).BeginInit();
            this.grpMonitor.SuspendLayout();
            this.grpAudioDevices.SuspendLayout();
            this.grpAppPath.SuspendLayout();
            this.SuspendLayout();

            //
            // grpMonitor
            //
            this.grpMonitor.Location = new System.Drawing.Point(12, 12);
            this.grpMonitor.Size = new System.Drawing.Size(396, 176);
            this.grpMonitor.TabStop = false;
            this.grpMonitor.Text = "Monitor";
            this.grpMonitor.Controls.Add(this.dgvMonitors);
            this.grpMonitor.Controls.Add(this.lblMonitorWarning);

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
            this.dgvMonitors.Location = new System.Drawing.Point(12, 22);
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
            this.colDisable.HeaderText = "Disable";
            this.colDisable.Name = "colDisable";
            this.colDisable.Width = 60;

            //
            // colEnable
            //
            this.colEnable.HeaderText = "Enable";
            this.colEnable.Name = "colEnable";
            this.colEnable.Width = 60;

            //
            // lblMonitorWarning
            //
            this.lblMonitorWarning.Location = new System.Drawing.Point(12, 148);
            this.lblMonitorWarning.Size = new System.Drawing.Size(372, 20);
            this.lblMonitorWarning.AutoSize = false;
            this.lblMonitorWarning.Visible = false;
            this.lblMonitorWarning.Name = "lblMonitorWarning";

            //
            // grpAudioDevices
            //
            this.grpAudioDevices.Location = new System.Drawing.Point(12, 200);
            this.grpAudioDevices.Size = new System.Drawing.Size(396, 132);
            this.grpAudioDevices.TabStop = false;
            this.grpAudioDevices.Text = "Audio Devices";
            this.grpAudioDevices.Controls.Add(this.lblAudioNormalCaption);
            this.grpAudioDevices.Controls.Add(this.cboAudioNormal);
            this.grpAudioDevices.Controls.Add(this.lblAudioNormalWarning);
            this.grpAudioDevices.Controls.Add(this.lblAudioRigCaption);
            this.grpAudioDevices.Controls.Add(this.cboAudioRig);
            this.grpAudioDevices.Controls.Add(this.lblAudioRigWarning);

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
            // grpAppPath
            //
            this.grpAppPath.Location = new System.Drawing.Point(12, 344);
            this.grpAppPath.Size = new System.Drawing.Size(396, 76);
            this.grpAppPath.TabStop = false;
            this.grpAppPath.Text = "Target App";
            this.grpAppPath.AllowDrop = true;
            this.grpAppPath.Controls.Add(this.txtAppPath);
            this.grpAppPath.Controls.Add(this.btnBrowse);
            this.grpAppPath.Controls.Add(this.lblAppWarning);
            this.grpAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.grpAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // txtAppPath
            //
            this.txtAppPath.ReadOnly = true;
            this.txtAppPath.Text = "No app shortcut or .exe selected";
            this.txtAppPath.Location = new System.Drawing.Point(12, 22);
            this.txtAppPath.Size = new System.Drawing.Size(288, 23);
            this.txtAppPath.Name = "txtAppPath";
            this.txtAppPath.AllowDrop = true;
            this.txtAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.txtAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // btnBrowse
            //
            this.btnBrowse.Text = "Browse…";
            this.btnBrowse.Location = new System.Drawing.Point(306, 21);
            this.btnBrowse.Size = new System.Drawing.Size(78, 25);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Click += new System.EventHandler(this.BtnBrowse_Click);

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
            this.chkEnableDebugLogging.Location = new System.Drawing.Point(12, 426);
            this.chkEnableDebugLogging.Size = new System.Drawing.Size(396, 40);
            this.chkEnableDebugLogging.AutoSize = false;
            this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";

            //
            // btnSaveSettings
            //
            this.btnSaveSettings.Text = "Save Settings";
            this.btnSaveSettings.Location = new System.Drawing.Point(180, 476);
            this.btnSaveSettings.Size = new System.Drawing.Size(110, 32);
            this.btnSaveSettings.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveSettings.Name = "btnSaveSettings";
            this.btnSaveSettings.Click += new System.EventHandler(this.BtnSaveSettings_Click);

            //
            // btnDiscardChanges
            //
            this.btnDiscardChanges.Text = "Discard Changes";
            this.btnDiscardChanges.Location = new System.Drawing.Point(298, 476);
            this.btnDiscardChanges.Size = new System.Drawing.Size(110, 32);
            this.btnDiscardChanges.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnDiscardChanges.Name = "btnDiscardChanges";

            //
            // dlgOpenExe
            //
            this.dlgOpenExe.Filter = "App shortcuts and executables (*.lnk;*.exe)|*.lnk;*.exe";
            this.dlgOpenExe.CheckFileExists = true;
            this.dlgOpenExe.Title = "Select Target App";

            //
            // errMonitor / errAudioNormal / errAudioRig / errApp
            //
            this.errMonitor.ContainerControl = this;
            this.errAudioNormal.ContainerControl = this;
            this.errAudioRig.ContainerControl = this;
            this.errApp.ContainerControl = this;

            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 524);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Rig Toggle — Settings";
            this.Name = "SettingsForm";

            this.Controls.Add(this.grpMonitor);
            this.Controls.Add(this.grpAudioDevices);
            this.Controls.Add(this.grpAppPath);
            this.Controls.Add(this.chkEnableDebugLogging);
            this.Controls.Add(this.btnSaveSettings);
            this.Controls.Add(this.btnDiscardChanges);

            ((System.ComponentModel.ISupportInitialize)(this.errMonitor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioNormal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioRig)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errApp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitors)).EndInit();
            this.grpMonitor.ResumeLayout(false);
            this.grpAudioDevices.ResumeLayout(false);
            this.grpAppPath.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox grpMonitor;
        private System.Windows.Forms.DataGridView dgvMonitors;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonitorName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colDisable;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEnable;
        private System.Windows.Forms.Label lblMonitorWarning;

        private System.Windows.Forms.GroupBox grpAudioDevices;
        private System.Windows.Forms.Label lblAudioNormalCaption;
        private System.Windows.Forms.ComboBox cboAudioNormal;
        private System.Windows.Forms.Label lblAudioNormalWarning;
        private System.Windows.Forms.Label lblAudioRigCaption;
        private System.Windows.Forms.ComboBox cboAudioRig;
        private System.Windows.Forms.Label lblAudioRigWarning;

        private System.Windows.Forms.GroupBox grpAppPath;
        private System.Windows.Forms.TextBox txtAppPath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblAppWarning;

        private System.Windows.Forms.CheckBox chkEnableDebugLogging;

        private System.Windows.Forms.Button btnSaveSettings;
        private System.Windows.Forms.Button btnDiscardChanges;

        private System.Windows.Forms.ErrorProvider errMonitor;
        private System.Windows.Forms.ErrorProvider errAudioNormal;
        private System.Windows.Forms.ErrorProvider errAudioRig;
        private System.Windows.Forms.ErrorProvider errApp;
        private System.Windows.Forms.OpenFileDialog dlgOpenExe;
    }
}
