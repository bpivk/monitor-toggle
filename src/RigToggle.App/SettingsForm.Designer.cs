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

            // 22-01/D-03: TableLayoutPanel scaffold for the new mode-based layout.
            // tlpRoot is the form's single root child; tlpModeColumns hosts the
            // Normal/Rig mode columns side by side; tlpNormalColumn/tlpAudioNormal
            // and tlpRigColumn/tlpAudioRig are each mode column's own table and its
            // nested audio-picker row.
            this.tlpRoot = new System.Windows.Forms.TableLayoutPanel();
            this.tlpModeColumns = new System.Windows.Forms.TableLayoutPanel();
            this.tlpNormalColumn = new System.Windows.Forms.TableLayoutPanel();
            this.tlpAudioNormal = new System.Windows.Forms.TableLayoutPanel();
            this.tlpRigColumn = new System.Windows.Forms.TableLayoutPanel();
            this.tlpAudioRig = new System.Windows.Forms.TableLayoutPanel();

            // 22-02/D-02/D-04: shared full-width settings section, its two nested
            // layout helpers (tlpAppPath, tlpHotkey), and the Phase 23 reserved slot.
            this.pnlSharedSection = new System.Windows.Forms.Panel();
            this.flpShared = new System.Windows.Forms.FlowLayoutPanel();
            this.tlpAppPath = new System.Windows.Forms.TableLayoutPanel();
            this.tlpHotkey = new System.Windows.Forms.TableLayoutPanel();
            // 23-02: pnlThemeReserved now hosts flpTheme (the System/Light/Dark radio
            // stack) and gets a Suspend/Resume pair below like every other populated
            // container -- the "no children" premise that used to justify skipping the
            // bracket no longer holds.
            this.pnlThemeReserved = new System.Windows.Forms.Panel();
            this.flpTheme = new System.Windows.Forms.FlowLayoutPanel();
            this.lblThemeCaption = new System.Windows.Forms.Label();
            this.rdoThemeSystem = new System.Windows.Forms.RadioButton();
            this.rdoThemeLight = new System.Windows.Forms.RadioButton();
            this.rdoThemeDark = new System.Windows.Forms.RadioButton();

            // 22-02/D-05/D-06: right-aligned Save/Discard button row.
            this.flpButtons = new System.Windows.Forms.FlowLayoutPanel();

            // tlpRoot's bottom button row container, wrapping flpButtons.
            this.tlpButtonRow = new System.Windows.Forms.TableLayoutPanel();

            this.pnlMonitor = new System.Windows.Forms.Panel();
            this.lblMonitorCaption = new System.Windows.Forms.Label();
            this.dgvMonitors = new System.Windows.Forms.DataGridView();
            this.colMonitorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDisable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colEnable = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.lblMonitorWarning = new System.Windows.Forms.LinkLabel();
            this.lblMonitorExplain = new System.Windows.Forms.Label();

            this.pnlMonitorNormal = new System.Windows.Forms.Panel();
            this.lblMonitorNormalCaption = new System.Windows.Forms.Label();
            this.dgvMonitorsNormal = new System.Windows.Forms.DataGridView();
            this.colMonitorNameNormal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDisableNormal = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colEnableNormal = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.lblMonitorNormalWarning = new System.Windows.Forms.LinkLabel();
            this.lblMonitorNormalExplain = new System.Windows.Forms.Label();

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
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitorsNormal)).BeginInit();
            this.pnlMonitor.SuspendLayout();
            this.pnlMonitorNormal.SuspendLayout();
            this.pnlAppPath.SuspendLayout();
            this.tlpAppPath.SuspendLayout();
            this.tlpHotkey.SuspendLayout();
            this.pnlSharedSection.SuspendLayout();
            this.flpShared.SuspendLayout();
            this.pnlThemeReserved.SuspendLayout();
            this.flpTheme.SuspendLayout();
            this.flpButtons.SuspendLayout();
            this.tlpButtonRow.SuspendLayout();
            this.tlpAudioNormal.SuspendLayout();
            this.tlpNormalColumn.SuspendLayout();
            this.tlpAudioRig.SuspendLayout();
            this.tlpRigColumn.SuspendLayout();
            this.tlpModeColumns.SuspendLayout();
            this.tlpRoot.SuspendLayout();
            this.SuspendLayout();

            //
            // pnlMonitor (THEME-05: flat bordered Panel replacing the grpMonitor GroupBox
            // bevel -- GroupBox has no flat variant, SetColorMode cannot recolor its 3D
            // border. 22-01/D-03: migrated off fixed Location/Size onto Dock=Fill inside
            // tlpModeColumns, mirroring pnlMonitorNormal's Task 1 migration. Margin is
            // zero here -- the 12px inter-column gap is contributed entirely by
            // pnlMonitorNormal's right Margin, not doubled up on this side.
            // 22-04 gap closure: this was one of only two container Panels in this
            // file that never set AutoSize (pnlSharedSection and pnlAppPath both do).
            // Its sole child, tlpRigColumn, is Dock=Fill, which contributes nothing to
            // a DefaultLayout container's own extent -- so without AutoSize this panel
            // reported the default Panel size (200x100) to its tlpModeColumns cell
            // instead of its measured content, matching the rig's Check 1 symptom
            // exactly (caption and explain visible, everything from the grid row down
            // clipped). AutoSize added so it measures its content instead.)
            //
            this.pnlMonitor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMonitor.Name = "pnlMonitor";
            this.pnlMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMonitor.AutoSize = true;
            this.pnlMonitor.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMonitor.Padding = new System.Windows.Forms.Padding(12);
            this.pnlMonitor.Margin = new System.Windows.Forms.Padding(0);
            this.pnlMonitor.TabIndex = 1;
            this.pnlMonitor.Controls.Add(this.tlpRigColumn);

            //
            // tlpRigColumn (22-01/D-01: six-row table mirroring tlpNormalColumn --
            // caption, explain, grid, warning, audio picker row, audio warning --
            // replacing pnlMonitor's four absolutely positioned children plus the
            // Rig audio picker moved in from the old shared audio-devices panel
            // (dissolved this task).)
            //
            this.tlpRigColumn.ColumnCount = 1;
            this.tlpRigColumn.RowCount = 6;
            this.tlpRigColumn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRigColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRigColumn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRigColumn.AutoSize = true;
            this.tlpRigColumn.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpRigColumn.Margin = new System.Windows.Forms.Padding(0);
            this.tlpRigColumn.Name = "tlpRigColumn";
            this.tlpRigColumn.Controls.Add(this.lblMonitorCaption, 0, 0);
            this.tlpRigColumn.Controls.Add(this.lblMonitorExplain, 0, 1);
            this.tlpRigColumn.Controls.Add(this.dgvMonitors, 0, 2);
            this.tlpRigColumn.Controls.Add(this.lblMonitorWarning, 0, 3);
            this.tlpRigColumn.Controls.Add(this.tlpAudioRig, 0, 4);
            this.tlpRigColumn.Controls.Add(this.lblAudioRigWarning, 0, 5);

            //
            // lblMonitorCaption
            //
            this.lblMonitorCaption.Text = "Rig Mode";
            this.lblMonitorCaption.AutoSize = true;
            this.lblMonitorCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMonitorCaption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
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
            this.dgvMonitors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvMonitors.MinimumSize = new System.Drawing.Size(0, 120);
            this.dgvMonitors.Margin = new System.Windows.Forms.Padding(0, 0, 20, 8);
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
            this.colDisable.HeaderText = "Off";
            this.colDisable.Name = "colDisable";
            this.colDisable.Width = 66;
            this.colDisable.ToolTipText = "Turns this monitor off when switching to Rig Mode.";

            //
            // colEnable
            //
            this.colEnable.HeaderText = "On";
            this.colEnable.Name = "colEnable";
            this.colEnable.Width = 66;
            this.colEnable.ToolTipText = "Turns this monitor on when switching to Rig Mode (for a monitor normally kept off, e.g. to save power).";

            //
            // lblMonitorExplain
            //
            this.lblMonitorExplain.AutoSize = false;
            this.lblMonitorExplain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMonitorExplain.MinimumSize = new System.Drawing.Size(0, 50);
            this.lblMonitorExplain.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblMonitorExplain.Text = "Only controls what changes when switching TO Rig Mode. A monitor not listed here is left untouched.";
            this.lblMonitorExplain.Name = "lblMonitorExplain";

            //
            // lblMonitorWarning
            //
            this.lblMonitorWarning.AutoSize = false;
            this.lblMonitorWarning.Visible = false;
            this.lblMonitorWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMonitorWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblMonitorWarning.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblMonitorWarning.Name = "lblMonitorWarning";

            //
            // tlpAudioRig (22-01/D-01: Rig's audio picker, split out of the old
            // shared audio-devices panel into its own mode column, mirroring
            // tlpAudioNormal.)
            //
            this.tlpAudioRig.ColumnCount = 2;
            this.tlpAudioRig.RowCount = 1;
            this.tlpAudioRig.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAudioRig.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpAudioRig.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAudioRig.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpAudioRig.AutoSize = true;
            this.tlpAudioRig.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpAudioRig.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.tlpAudioRig.Name = "tlpAudioRig";
            this.tlpAudioRig.Controls.Add(this.lblAudioRigCaption, 0, 0);
            this.tlpAudioRig.Controls.Add(this.cboAudioRig, 1, 0);

            //
            // lblAudioRigCaption
            //
            this.lblAudioRigCaption.Text = "Rig:";
            this.lblAudioRigCaption.AutoSize = true;
            this.lblAudioRigCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAudioRigCaption.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblAudioRigCaption.Name = "lblAudioRigCaption";

            //
            // cboAudioRig
            //
            this.cboAudioRig.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAudioRig.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.cboAudioRig.Margin = new System.Windows.Forms.Padding(0, 0, 20, 0);
            this.cboAudioRig.Name = "cboAudioRig";

            //
            // lblAudioRigWarning
            //
            this.lblAudioRigWarning.AutoSize = false;
            this.lblAudioRigWarning.Visible = false;
            this.lblAudioRigWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAudioRigWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblAudioRigWarning.Margin = new System.Windows.Forms.Padding(0);
            this.lblAudioRigWarning.Name = "lblAudioRigWarning";

            //
            // tlpModeColumns (22-01/D-01/D-03: 50/50 Percent-split container hosting
            // the Normal and Rig mode columns side by side. Normal = column 0 (left),
            // Rig = column 1 (right) -- a deliberate swap from the pre-migration
            // pnlMonitor-at-x=12/pnlMonitorNormal-at-x=420 order, per the user's own
            // framing in 22-CONTEXT.md ("One side is for normal mode... second for
            // rig mode").)
            //
            this.tlpModeColumns.ColumnCount = 2;
            this.tlpModeColumns.RowCount = 1;
            this.tlpModeColumns.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpModeColumns.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpModeColumns.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpModeColumns.AutoSize = true;
            this.tlpModeColumns.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpModeColumns.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpModeColumns.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
            // 22-04 gap closure: this MinimumSize is a deliberately mechanism-independent
            // backstop for the rig's Check 1 collapse (both mode columns showed only
            // caption + explain, with grid/warning/audio/audio-warning entirely absent).
            // Control.GetPreferredSize clamps its result to MinimumSize on the way up the
            // layout pass and Control.SetBoundsCore clamps to it on the way down, so this
            // row gets a real height in both directions regardless of which mechanism
            // caused the collapse -- whether it was the pnlMonitor*/pnlMonitorNormal
            // AutoSize fix above, the Percent-100F grid row, or something neither of us
            // identified.
            // Height arithmetic at 100% scale, per mode column: caption ~19 + 4 margin =
            // 23; explain 50 floor + 8 margin = 58; grid 120 floor + 8 margin = 128;
            // monitor warning 0 (hidden); audio row ~23 combo + 4 margin = 27; audio
            // warning 0 (hidden). Content ~236, plus the wrapper's 12px top/bottom
            // padding (24) and 1px FixedSingle border top/bottom (2) ~262, rounded up to
            // 280 for slack.
            // Width is deliberately 0 -- no horizontal floor -- so the two Percent 50F
            // columns stay free to split whatever width the window has.
            // DPI-safe: Control.ScaleControl scales MinimumSize alongside Size under
            // AutoScaleMode.Font, so this 100%-scale figure scales itself at 125%/150%.
            // Satisfies 22-RESEARCH.md Pitfall 2's condition that any MinimumSize added
            // to this form be documented with its reason.
            this.tlpModeColumns.MinimumSize = new System.Drawing.Size(0, 280);
            this.tlpModeColumns.TabIndex = 0;
            this.tlpModeColumns.Name = "tlpModeColumns";
            this.tlpModeColumns.Controls.Add(this.pnlMonitorNormal, 0, 0);
            this.tlpModeColumns.Controls.Add(this.pnlMonitor, 1, 0);

            //
            // pnlMonitorNormal (THEME-05: flat bordered Panel, unchanged bevel
            // treatment. 22-01/D-03: migrated off fixed Location/Size onto
            // Dock=Fill inside tlpModeColumns; content Padding now derives its own
            // inset instead of the old 60px top reservation, since the caption/
            // explain stack is now a table row, not an absolutely positioned
            // overlay -- 22-UI-SPEC.md Spacing exception 3.
            // 22-04 gap closure: this was one of only two container Panels in this
            // file that never set AutoSize (pnlSharedSection and pnlAppPath both do).
            // Its sole child, tlpNormalColumn, is Dock=Fill, which contributes nothing
            // to a DefaultLayout container's own extent -- so without AutoSize this
            // panel reported the default Panel size (200x100) to its tlpModeColumns
            // cell instead of its measured content, matching the rig's Check 1 symptom
            // exactly (caption and explain visible, everything from the grid row down
            // clipped). AutoSize added so it measures its content instead.)
            //
            this.pnlMonitorNormal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlMonitorNormal.Name = "pnlMonitorNormal";
            this.pnlMonitorNormal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlMonitorNormal.AutoSize = true;
            this.pnlMonitorNormal.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlMonitorNormal.Padding = new System.Windows.Forms.Padding(12);
            this.pnlMonitorNormal.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.pnlMonitorNormal.TabIndex = 0;
            this.pnlMonitorNormal.Controls.Add(this.tlpNormalColumn);

            //
            // tlpNormalColumn (22-01/D-01: six-row table -- caption, explain, grid,
            // warning, audio picker row, audio warning -- replacing pnlMonitorNormal's
            // four absolutely positioned children plus the Normal audio picker moved
            // in from the old shared audio-devices panel (dissolved this task).)
            //
            this.tlpNormalColumn.ColumnCount = 1;
            this.tlpNormalColumn.RowCount = 6;
            this.tlpNormalColumn.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpNormalColumn.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpNormalColumn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpNormalColumn.AutoSize = true;
            this.tlpNormalColumn.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpNormalColumn.Margin = new System.Windows.Forms.Padding(0);
            this.tlpNormalColumn.Name = "tlpNormalColumn";
            this.tlpNormalColumn.Controls.Add(this.lblMonitorNormalCaption, 0, 0);
            this.tlpNormalColumn.Controls.Add(this.lblMonitorNormalExplain, 0, 1);
            this.tlpNormalColumn.Controls.Add(this.dgvMonitorsNormal, 0, 2);
            this.tlpNormalColumn.Controls.Add(this.lblMonitorNormalWarning, 0, 3);
            this.tlpNormalColumn.Controls.Add(this.tlpAudioNormal, 0, 4);
            this.tlpNormalColumn.Controls.Add(this.lblAudioNormalWarning, 0, 5);

            //
            // lblMonitorNormalCaption
            //
            this.lblMonitorNormalCaption.Text = "Normal Mode";
            this.lblMonitorNormalCaption.AutoSize = true;
            this.lblMonitorNormalCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblMonitorNormalCaption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.lblMonitorNormalCaption.Name = "lblMonitorNormalCaption";

            //
            // dgvMonitorsNormal
            //
            this.dgvMonitorsNormal.AllowUserToAddRows = false;
            this.dgvMonitorsNormal.AllowUserToDeleteRows = false;
            this.dgvMonitorsNormal.AllowUserToResizeRows = false;
            this.dgvMonitorsNormal.AllowUserToResizeColumns = false;
            this.dgvMonitorsNormal.RowHeadersVisible = false;
            this.dgvMonitorsNormal.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvMonitorsNormal.MultiSelect = false;
            this.dgvMonitorsNormal.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvMonitorsNormal.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.dgvMonitorsNormal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvMonitorsNormal.MinimumSize = new System.Drawing.Size(0, 120);
            this.dgvMonitorsNormal.Margin = new System.Windows.Forms.Padding(0, 0, 20, 8);
            this.dgvMonitorsNormal.Name = "dgvMonitorsNormal";
            this.dgvMonitorsNormal.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colMonitorNameNormal,
            this.colDisableNormal,
            this.colEnableNormal});

            //
            // colMonitorNameNormal
            //
            this.colMonitorNameNormal.HeaderText = "Monitor";
            this.colMonitorNameNormal.Name = "colMonitorNameNormal";
            this.colMonitorNameNormal.ReadOnly = true;
            this.colMonitorNameNormal.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;

            //
            // colDisableNormal
            //
            this.colDisableNormal.HeaderText = "Off";
            this.colDisableNormal.Name = "colDisableNormal";
            this.colDisableNormal.Width = 66;
            this.colDisableNormal.ToolTipText = "Turns this monitor off when switching to Normal Mode.";

            //
            // colEnableNormal
            //
            this.colEnableNormal.HeaderText = "On";
            this.colEnableNormal.Name = "colEnableNormal";
            this.colEnableNormal.Width = 66;
            this.colEnableNormal.ToolTipText = "Turns this monitor on when switching to Normal Mode (for a monitor normally kept off, e.g. to save power).";

            //
            // lblMonitorNormalExplain
            //
            this.lblMonitorNormalExplain.AutoSize = false;
            this.lblMonitorNormalExplain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMonitorNormalExplain.MinimumSize = new System.Drawing.Size(0, 50);
            this.lblMonitorNormalExplain.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblMonitorNormalExplain.Text = "Only controls what changes when switching TO Normal Mode. A monitor not listed here is left untouched.";
            this.lblMonitorNormalExplain.Name = "lblMonitorNormalExplain";

            //
            // lblMonitorNormalWarning
            //
            this.lblMonitorNormalWarning.AutoSize = false;
            this.lblMonitorNormalWarning.Visible = false;
            this.lblMonitorNormalWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMonitorNormalWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblMonitorNormalWarning.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblMonitorNormalWarning.Name = "lblMonitorNormalWarning";

            //
            // tlpAudioNormal (22-01/D-01: Normal's audio picker, split out of the old
            // shared audio-devices panel (dissolved this task) into its own mode
            // column.)
            //
            this.tlpAudioNormal.ColumnCount = 2;
            this.tlpAudioNormal.RowCount = 1;
            this.tlpAudioNormal.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAudioNormal.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpAudioNormal.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAudioNormal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpAudioNormal.AutoSize = true;
            this.tlpAudioNormal.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpAudioNormal.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.tlpAudioNormal.Name = "tlpAudioNormal";
            this.tlpAudioNormal.Controls.Add(this.lblAudioNormalCaption, 0, 0);
            this.tlpAudioNormal.Controls.Add(this.cboAudioNormal, 1, 0);

            //
            // lblAudioNormalCaption
            //
            this.lblAudioNormalCaption.Text = "Normal:";
            this.lblAudioNormalCaption.AutoSize = true;
            this.lblAudioNormalCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAudioNormalCaption.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
            this.lblAudioNormalCaption.Name = "lblAudioNormalCaption";

            //
            // cboAudioNormal
            //
            this.cboAudioNormal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAudioNormal.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.cboAudioNormal.Margin = new System.Windows.Forms.Padding(0, 0, 20, 0);
            this.cboAudioNormal.Name = "cboAudioNormal";

            //
            // lblAudioNormalWarning
            //
            this.lblAudioNormalWarning.AutoSize = false;
            this.lblAudioNormalWarning.Visible = false;
            this.lblAudioNormalWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAudioNormalWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblAudioNormalWarning.Margin = new System.Windows.Forms.Padding(0);
            this.lblAudioNormalWarning.Name = "lblAudioNormalWarning";

            //
            // pnlAppPath (THEME-05: flat bordered Panel replacing the grpAppPath GroupBox
            // bevel. CRITICAL: AllowDrop plus the drag-enter/drag-drop wiring
            // (AppPath_Drag(Enter|Drop)) stay on this panel verbatim -- must not be
            // dropped (T-12-07). 22-02/D-02: migrated off fixed Location/Size onto
            // AutoSize inside flpShared; its five flat children are replaced by a single
            // nested tlpAppPath table.)
            //
            this.pnlAppPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlAppPath.Name = "pnlAppPath";
            this.pnlAppPath.AllowDrop = true;
            this.pnlAppPath.AutoSize = true;
            this.pnlAppPath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlAppPath.Padding = new System.Windows.Forms.Padding(12);
            this.pnlAppPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.pnlAppPath.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.pnlAppPath.TabIndex = 0;
            this.pnlAppPath.Controls.Add(this.tlpAppPath);
            this.pnlAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.pnlAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // tlpAppPath (22-02: internal layout for the target-app box, replacing
            // pnlAppPath's five absolutely-positioned children. CRITICAL: this table now
            // covers pnlAppPath's client area, so its own AllowDrop=true plus
            // drag-enter/drag-drop subscriptions are required -- without them the
            // panel-wide drop target (T-12-07) collapses to just the text box.)
            //
            this.tlpAppPath.ColumnCount = 3;
            this.tlpAppPath.RowCount = 3;
            this.tlpAppPath.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpAppPath.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAppPath.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAppPath.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAppPath.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAppPath.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpAppPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpAppPath.AutoSize = true;
            this.tlpAppPath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpAppPath.Margin = new System.Windows.Forms.Padding(0);
            this.tlpAppPath.Name = "tlpAppPath";
            this.tlpAppPath.AllowDrop = true;
            this.tlpAppPath.Controls.Add(this.lblAppPathCaption, 0, 0);
            this.tlpAppPath.Controls.Add(this.txtAppPath, 0, 1);
            this.tlpAppPath.Controls.Add(this.btnBrowse, 1, 1);
            this.tlpAppPath.Controls.Add(this.btnClearAppPath, 2, 1);
            this.tlpAppPath.Controls.Add(this.lblAppWarning, 0, 2);
            this.tlpAppPath.SetColumnSpan(this.lblAppPathCaption, 3);
            this.tlpAppPath.SetColumnSpan(this.lblAppWarning, 3);
            this.tlpAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.tlpAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // lblAppPathCaption
            //
            this.lblAppPathCaption.Text = "Target App";
            this.lblAppPathCaption.AutoSize = true;
            this.lblAppPathCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblAppPathCaption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.lblAppPathCaption.Name = "lblAppPathCaption";

            //
            // txtAppPath
            //
            this.txtAppPath.ReadOnly = true;
            this.txtAppPath.Text = "No app shortcut or .exe selected";
            // 22-02: tlpAppPath's column 0 is Percent 100F, so this field's width is now
            // derived from the table instead of a hand-picked pixel value (supersedes the
            // old 15-03/D-01 288->220 narrowing note, which described the deleted pixel
            // math).
            this.txtAppPath.Name = "txtAppPath";
            this.txtAppPath.AllowDrop = true;
            this.txtAppPath.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.txtAppPath.Margin = new System.Windows.Forms.Padding(0, 0, 20, 0);
            this.txtAppPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.AppPath_DragEnter);
            this.txtAppPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.AppPath_DragDrop);

            //
            // btnBrowse
            //
            this.btnBrowse.Text = "Browse…";
            this.btnBrowse.AutoSize = true;
            this.btnBrowse.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnBrowse.MinimumSize = new System.Drawing.Size(70, 25);
            this.btnBrowse.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.btnBrowse.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
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
            // PopulateAppPathField/BtnClearAppPath_Click/BtnBrowse_Click/the app-path
            // drag-drop handler in SettingsForm.cs); starts disabled here since the
            // initial state is resolved on Load from persisted settings.
            this.btnClearAppPath.Text = "Clear";
            this.btnClearAppPath.AutoSize = true;
            this.btnClearAppPath.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnClearAppPath.MinimumSize = new System.Drawing.Size(70, 25);
            this.btnClearAppPath.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.btnClearAppPath.Margin = new System.Windows.Forms.Padding(0);
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
            this.lblAppWarning.AutoSize = false;
            this.lblAppWarning.Visible = false;
            this.lblAppWarning.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAppWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblAppWarning.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.lblAppWarning.Name = "lblAppWarning";

            //
            // tlpHotkey (22-02: hotkey caption + field row -- the one shared-section item
            // that is not a single control. Two-column table: AutoSize caption, Percent
            // 100F field column.)
            //
            this.tlpHotkey.ColumnCount = 2;
            this.tlpHotkey.RowCount = 1;
            this.tlpHotkey.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpHotkey.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpHotkey.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpHotkey.AutoSize = true;
            this.tlpHotkey.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpHotkey.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.tlpHotkey.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.tlpHotkey.TabIndex = 1;
            this.tlpHotkey.Name = "tlpHotkey";
            this.tlpHotkey.Controls.Add(this.lblHotkeyCaption, 0, 0);
            this.tlpHotkey.Controls.Add(this.txtHotkey, 1, 0);

            //
            // lblHotkeyCaption
            //
            this.lblHotkeyCaption.Text = "Hotkey:";
            this.lblHotkeyCaption.AutoSize = true;
            this.lblHotkeyCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblHotkeyCaption.Margin = new System.Windows.Forms.Padding(0, 0, 4, 0);
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
            this.txtHotkey.Size = new System.Drawing.Size(200, 23);
            this.txtHotkey.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.txtHotkey.Margin = new System.Windows.Forms.Padding(0, 0, 20, 0);
            this.txtHotkey.Name = "txtHotkey";

            //
            // lblHotkeyWarning
            //
            this.lblHotkeyWarning.AutoSize = false;
            this.lblHotkeyWarning.Visible = false;
            this.lblHotkeyWarning.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.lblHotkeyWarning.MinimumSize = new System.Drawing.Size(0, 36);
            this.lblHotkeyWarning.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblHotkeyWarning.TabIndex = 2;
            this.lblHotkeyWarning.Name = "lblHotkeyWarning";

            //
            // chkEnableDebugLogging
            //
            this.chkEnableDebugLogging.Text = "Enable debug logging (writes to %LOCALAPPDATA%\\RigToggle\\debug.log)";
            this.chkEnableDebugLogging.AutoSize = true;
            this.chkEnableDebugLogging.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkEnableDebugLogging.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.chkEnableDebugLogging.TabIndex = 3;
            this.chkEnableDebugLogging.Name = "chkEnableDebugLogging";

            //
            // chkCloseMinimizesToTray
            //
            this.chkCloseMinimizesToTray.Text = "Closing the window (X) minimizes to tray";
            this.chkCloseMinimizesToTray.AutoSize = true;
            this.chkCloseMinimizesToTray.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkCloseMinimizesToTray.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.chkCloseMinimizesToTray.TabIndex = 4;
            this.chkCloseMinimizesToTray.Name = "chkCloseMinimizesToTray";

            //
            // chkMinimizeToTray
            //
            this.chkMinimizeToTray.Text = "Minimizing the window also sends it to tray";
            this.chkMinimizeToTray.AutoSize = true;
            this.chkMinimizeToTray.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkMinimizeToTray.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.chkMinimizeToTray.TabIndex = 5;
            this.chkMinimizeToTray.Name = "chkMinimizeToTray";

            //
            // chkStartWithWindows
            //
            this.chkStartWithWindows.Text = "Start with Windows";
            this.chkStartWithWindows.AutoSize = true;
            this.chkStartWithWindows.Anchor = System.Windows.Forms.AnchorStyles.Left;
            // 20px right Margin reserves room for errAutostart.SetError's icon (T-22-07).
            this.chkStartWithWindows.Margin = new System.Windows.Forms.Padding(0, 0, 20, 8);
            this.chkStartWithWindows.TabIndex = 6;
            this.chkStartWithWindows.Name = "chkStartWithWindows";

            //
            // lblAutostartWarning
            //
            this.lblAutostartWarning.AutoSize = false;
            this.lblAutostartWarning.Visible = false;
            this.lblAutostartWarning.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.lblAutostartWarning.MinimumSize = new System.Drawing.Size(0, 20);
            this.lblAutostartWarning.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.lblAutostartWarning.TabIndex = 7;
            this.lblAutostartWarning.Name = "lblAutostartWarning";

            //
            // pnlThemeReserved (D-04/THEME-09: 23-02 fills the insertion point Phase 22
            // reserved with the System/Light/Dark radio group. The slot's own layout
            // contract -- Margin, Anchor, AutoSize, AutoSizeMode, TabIndex, Name -- is
            // untouched from Phase 22, so nothing above it in flpShared reflows; only its
            // children are new. The plain Size = (0, 0) below is still left in place from
            // Phase 22 -- a bare Panel serializes a 200x100 default Size, and stating
            // (0, 0) explicitly guarantees the slot starts zero-size before the first
            // AutoSize/GrowAndShrink layout pass recomputes it against its new children;
            // this remains one of only two permitted plain .Size = assignments left in
            // this file (the other is txtHotkey's Size(200, 23)).)
            //
            this.pnlThemeReserved.AutoSize = true;
            this.pnlThemeReserved.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlThemeReserved.Size = new System.Drawing.Size(0, 0);
            this.pnlThemeReserved.Margin = new System.Windows.Forms.Padding(0);
            this.pnlThemeReserved.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.pnlThemeReserved.TabIndex = 8;
            this.pnlThemeReserved.Name = "pnlThemeReserved";
            this.pnlThemeReserved.Controls.Add(this.flpTheme);

            //
            // flpTheme (23-02: inner flow container mirroring flpShared's own proven
            // property set -- a plain Panel positions children absolutely, so this inner
            // FlowLayoutPanel, not Location arithmetic, is what stacks the caption and the
            // three radio buttons top-down and keeps the group DPI-safe under
            // AutoScaleMode.Font.)
            //
            this.flpTheme.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpTheme.WrapContents = false;
            this.flpTheme.AutoSize = true;
            this.flpTheme.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpTheme.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpTheme.Margin = new System.Windows.Forms.Padding(0);
            this.flpTheme.Padding = new System.Windows.Forms.Padding(0);
            this.flpTheme.TabIndex = 0;
            this.flpTheme.Name = "flpTheme";
            this.flpTheme.Controls.Add(this.lblThemeCaption);
            this.flpTheme.Controls.Add(this.rdoThemeSystem);
            this.flpTheme.Controls.Add(this.rdoThemeLight);
            this.flpTheme.Controls.Add(this.rdoThemeDark);

            //
            // lblThemeCaption
            //
            this.lblThemeCaption.Text = "Theme:";
            this.lblThemeCaption.AutoSize = true;
            this.lblThemeCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblThemeCaption.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.lblThemeCaption.TabIndex = 0;
            this.lblThemeCaption.Name = "lblThemeCaption";

            //
            // rdoThemeSystem (D-07: pre-selected -- the structural guarantee that the
            // group is never rendered with all three options unselected; SettingsForm_Load
            // still re-derives the selection from persisted settings.)
            //
            this.rdoThemeSystem.Text = "System (default)";
            this.rdoThemeSystem.Checked = true;
            this.rdoThemeSystem.AutoSize = true;
            this.rdoThemeSystem.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rdoThemeSystem.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.rdoThemeSystem.TabIndex = 1;
            this.rdoThemeSystem.Name = "rdoThemeSystem";

            //
            // rdoThemeLight
            //
            this.rdoThemeLight.Text = "Light";
            this.rdoThemeLight.AutoSize = true;
            this.rdoThemeLight.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rdoThemeLight.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.rdoThemeLight.TabIndex = 2;
            this.rdoThemeLight.Name = "rdoThemeLight";

            //
            // rdoThemeDark
            //
            this.rdoThemeDark.Text = "Dark";
            this.rdoThemeDark.AutoSize = true;
            this.rdoThemeDark.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.rdoThemeDark.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.rdoThemeDark.TabIndex = 3;
            this.rdoThemeDark.Name = "rdoThemeDark";

            //
            // pnlSharedSection (D-02/THEME-05: full-width shared settings section --
            // everything that is not mode-specific (target app path, hotkey capture,
            // debug logging, and the three tray/autostart checkboxes) lives here as one
            // flat, bordered stack below the two mode columns. Same THEME-05
            // flat-bordered-Panel-as-GroupBox pattern as pnlMonitor/pnlMonitorNormal
            // (Phase 12) -- ThemeApplier/SetColorMode cannot recolor a GroupBox bevel.)
            //
            this.pnlSharedSection.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlSharedSection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlSharedSection.AutoSize = true;
            this.pnlSharedSection.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlSharedSection.Padding = new System.Windows.Forms.Padding(12);
            this.pnlSharedSection.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
            this.pnlSharedSection.TabIndex = 1;
            this.pnlSharedSection.Name = "pnlSharedSection";
            this.pnlSharedSection.Controls.Add(this.flpShared);

            //
            // flpShared (D-02: one flat top-down stack, no sub-grouping. Add order below
            // is also the Tab order (Pitfall 5) -- keep add order and each child's
            // TabIndex in sync. FlowLayoutPanel does not honour Dock on its children
            // (22-RESEARCH.md Pattern 2) -- every child below uses Anchor, never Dock.)
            //
            this.flpShared.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flpShared.WrapContents = false;
            this.flpShared.AutoSize = true;
            this.flpShared.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpShared.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpShared.Margin = new System.Windows.Forms.Padding(0);
            this.flpShared.Padding = new System.Windows.Forms.Padding(0);
            this.flpShared.TabIndex = 0;
            this.flpShared.Name = "flpShared";
            this.flpShared.Controls.Add(this.pnlAppPath);
            this.flpShared.Controls.Add(this.tlpHotkey);
            this.flpShared.Controls.Add(this.lblHotkeyWarning);
            this.flpShared.Controls.Add(this.chkEnableDebugLogging);
            this.flpShared.Controls.Add(this.chkCloseMinimizesToTray);
            this.flpShared.Controls.Add(this.chkMinimizeToTray);
            this.flpShared.Controls.Add(this.chkStartWithWindows);
            this.flpShared.Controls.Add(this.lblAutostartWarning);
            this.flpShared.Controls.Add(this.pnlThemeReserved);

            //
            // flpButtons (D-05: Save/Discard right-aligned in their own tlpRoot row.
            // RightToLeft flow lands the first-added child rightmost, so
            // btnDiscardChanges is added first, then btnSaveSettings -- reproducing
            // today's left-to-right reading order of Save then Discard, x=180/x=298.)
            //
            this.flpButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flpButtons.WrapContents = false;
            this.flpButtons.AutoSize = true;
            this.flpButtons.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flpButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpButtons.Margin = new System.Windows.Forms.Padding(0);
            this.flpButtons.Padding = new System.Windows.Forms.Padding(0);
            this.flpButtons.TabIndex = 2;
            this.flpButtons.Name = "flpButtons";
            this.flpButtons.Controls.Add(this.btnDiscardChanges);
            this.flpButtons.Controls.Add(this.btnSaveSettings);

            //
            // btnDiscardChanges
            //
            this.btnDiscardChanges.Text = "Discard Changes";
            this.btnDiscardChanges.AutoSize = true;
            this.btnDiscardChanges.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnDiscardChanges.MinimumSize = new System.Drawing.Size(110, 32);
            this.btnDiscardChanges.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.btnDiscardChanges.TabIndex = 1;
            this.btnDiscardChanges.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnDiscardChanges.Name = "btnDiscardChanges";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): see btnBrowse's comment above for the
            // full rig-finding + #13897 rationale.
            this.btnDiscardChanges.FlatStyle = System.Windows.Forms.FlatStyle.Flat;

            //
            // btnSaveSettings
            //
            this.btnSaveSettings.Text = "Save Settings";
            this.btnSaveSettings.AutoSize = true;
            this.btnSaveSettings.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSaveSettings.MinimumSize = new System.Drawing.Size(110, 32);
            this.btnSaveSettings.Margin = new System.Windows.Forms.Padding(0);
            this.btnSaveSettings.TabIndex = 0;
            this.btnSaveSettings.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnSaveSettings.Name = "btnSaveSettings";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): see btnBrowse's comment above for the
            // full rig-finding + #13897 rationale.
            this.btnSaveSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveSettings.Click += new System.EventHandler(this.BtnSaveSettings_Click);

            //
            // tlpButtonRow (single-column container wrapping the unchanged
            // right-aligned flpButtons -- quick-260829-ga9 removed the secondary
            // update-check action button that previously occupied a second,
            // left-hand column here.)
            //
            this.tlpButtonRow.ColumnCount = 1;
            this.tlpButtonRow.RowCount = 1;
            this.tlpButtonRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpButtonRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpButtonRow.AutoSize = true;
            this.tlpButtonRow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpButtonRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpButtonRow.Margin = new System.Windows.Forms.Padding(0);
            this.tlpButtonRow.Padding = new System.Windows.Forms.Padding(0);
            this.tlpButtonRow.TabIndex = 2;
            this.tlpButtonRow.Name = "tlpButtonRow";
            this.tlpButtonRow.Controls.Add(this.flpButtons, 0, 0);

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
            // tlpRoot (22-01/D-03: the form's single root child container. Row 0
            // (mode columns) is Percent 100F rather than AutoSize -- under D-06's
            // eventual Sizable border (Plan 02), vertical space gained by dragging
            // the bottom edge has to land somewhere, and the grids are the only
            // element that benefits from growing; an all-AutoSize row set would
            // leave an unexplained empty gap, which Plan 03's rig check 7(d) treats
            // as a FAIL. Rows 1/2 (shared section, button row) stay AutoSize --
            // Plan 02 fills them.)
            //
            this.tlpRoot.ColumnCount = 1;
            this.tlpRoot.RowCount = 3;
            this.tlpRoot.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.tlpRoot.AutoSize = true;
            this.tlpRoot.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpRoot.Padding = new System.Windows.Forms.Padding(16);
            this.tlpRoot.Margin = new System.Windows.Forms.Padding(0);
            this.tlpRoot.TabIndex = 0;
            this.tlpRoot.Name = "tlpRoot";
            this.tlpRoot.Controls.Add(this.tlpModeColumns, 0, 0);
            this.tlpRoot.Controls.Add(this.pnlSharedSection, 0, 1);
            this.tlpRoot.Controls.Add(this.tlpButtonRow, 0, 2);

            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // 22-02/D-05's original comment here claimed AutoSize "does not fight a
            // user-driven edge drag." The rig's Check 3 disproved that: dragging the
            // window's edge showed a flickering resize preview that then vanished, with
            // no actual resize. A Form with AutoSize on rewrites its own bounds to its
            // computed preferred size on every layout pass, and a user's edge drag
            // (WM_SIZING/WM_SIZE) triggers exactly such a pass -- the drag is immediately
            // overwritten. 22-04 gap closure disables AutoSize at the Form level only
            // (this is the remedy 22-03-PLAN.md Task 2 pre-authorised: "disabling
            // Form.AutoSize after first show, keeping the container AutoSize intact").
            // tlpRoot and every container below it keep their own AutoSize/AutoSizeMode
            // exactly as they were -- only this Form-level property is turned off.
            // D-05's content-driven initial size is no longer produced by this property;
            // it is now computed explicitly by SettingsForm.cs's OnLoad override, which
            // reads tlpRoot.PreferredSize once at load time and applies it to the
            // window's client area.
            this.AutoSize = false;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = false;
            // MinimizeBox stays false -- not a leftover default. This dialog is shown via
            // ShowDialog() with ShowInTaskbar = false; minimizing that exact combination
            // can leave the window unreachable (no taskbar entry to restore from) or close
            // the dialog outright (22-RESEARCH.md Pitfall 3).
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Rig Toggle — Settings";
            this.Name = "SettingsForm";

            this.Controls.Add(this.tlpRoot);

            ((System.ComponentModel.ISupportInitialize)(this.errMonitor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioNormal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAudioRig)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errApp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errAutostart)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.errHotkey)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitors)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMonitorsNormal)).EndInit();
            this.pnlMonitor.ResumeLayout(false);
            this.pnlMonitorNormal.ResumeLayout(false);
            this.pnlAppPath.ResumeLayout(false);
            this.pnlAppPath.PerformLayout();
            this.tlpAppPath.ResumeLayout(false);
            this.tlpAppPath.PerformLayout();
            this.tlpHotkey.ResumeLayout(false);
            this.tlpHotkey.PerformLayout();
            this.flpShared.ResumeLayout(false);
            this.flpShared.PerformLayout();
            this.flpTheme.ResumeLayout(false);
            this.flpTheme.PerformLayout();
            this.pnlThemeReserved.ResumeLayout(false);
            this.pnlThemeReserved.PerformLayout();
            this.pnlSharedSection.ResumeLayout(false);
            this.pnlSharedSection.PerformLayout();
            this.flpButtons.ResumeLayout(false);
            this.flpButtons.PerformLayout();
            this.tlpButtonRow.ResumeLayout(false);
            this.tlpButtonRow.PerformLayout();
            this.tlpAudioRig.ResumeLayout(false);
            this.tlpAudioRig.PerformLayout();
            this.tlpRigColumn.ResumeLayout(false);
            this.tlpRigColumn.PerformLayout();
            this.tlpAudioNormal.ResumeLayout(false);
            this.tlpAudioNormal.PerformLayout();
            this.tlpNormalColumn.ResumeLayout(false);
            this.tlpNormalColumn.PerformLayout();
            this.tlpModeColumns.ResumeLayout(false);
            this.tlpModeColumns.PerformLayout();
            this.tlpRoot.ResumeLayout(false);
            this.tlpRoot.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tlpRoot;
        private System.Windows.Forms.TableLayoutPanel tlpModeColumns;
        private System.Windows.Forms.TableLayoutPanel tlpNormalColumn;
        private System.Windows.Forms.TableLayoutPanel tlpAudioNormal;
        private System.Windows.Forms.TableLayoutPanel tlpRigColumn;
        private System.Windows.Forms.TableLayoutPanel tlpAudioRig;

        private System.Windows.Forms.Panel pnlSharedSection;
        private System.Windows.Forms.FlowLayoutPanel flpShared;
        private System.Windows.Forms.TableLayoutPanel tlpAppPath;
        private System.Windows.Forms.TableLayoutPanel tlpHotkey;
        private System.Windows.Forms.Panel pnlThemeReserved;
        private System.Windows.Forms.FlowLayoutPanel flpTheme;
        private System.Windows.Forms.Label lblThemeCaption;
        private System.Windows.Forms.RadioButton rdoThemeSystem;
        private System.Windows.Forms.RadioButton rdoThemeLight;
        private System.Windows.Forms.RadioButton rdoThemeDark;
        private System.Windows.Forms.FlowLayoutPanel flpButtons;
        private System.Windows.Forms.TableLayoutPanel tlpButtonRow;

        private System.Windows.Forms.Panel pnlMonitor;
        private System.Windows.Forms.Label lblMonitorCaption;
        private System.Windows.Forms.DataGridView dgvMonitors;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonitorName;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colDisable;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEnable;
        private System.Windows.Forms.LinkLabel lblMonitorWarning;
        private System.Windows.Forms.Label lblMonitorExplain;

        private System.Windows.Forms.Panel pnlMonitorNormal;
        private System.Windows.Forms.Label lblMonitorNormalCaption;
        private System.Windows.Forms.DataGridView dgvMonitorsNormal;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMonitorNameNormal;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colDisableNormal;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colEnableNormal;
        private System.Windows.Forms.LinkLabel lblMonitorNormalWarning;
        private System.Windows.Forms.Label lblMonitorNormalExplain;

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
