namespace RigToggle.App
{
    partial class MainForm
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

            // WR-03 (code review): _normalIcon/_rigIcon (MainForm.cs) are plain fields,
            // not components-owned, so they need explicit deterministic disposal here
            // rather than relying on Icon's finalizer.
            if (disposing)
            {
                _normalIcon?.Dispose();
                _rigIcon?.Dispose();

                // 12-02/T-12-05: unsubscribe from the app-lifetime theme provider so this
                // form (which the provider otherwise long outlives) does not leak a
                // handler reference.
                _themeProvider.ThemeChanged -= OnThemeChanged;

                // TILE-06/19-RESEARCH.md Pitfall 2: REAL-PROCESS-EXIT backstop only,
                // mirroring the theme unsubscribe above. The DisplaySettingsChanged
                // subscription itself (MainForm.cs constructor) is held for the whole
                // app lifetime and must NEVER be gated on Hide()/Show()/visibility --
                // MainForm is hidden-not-closed during tray-resident operation, unlike
                // a closable Form, whose subscribe-in-ctor /
                // unsubscribe-when-the-window-closes pattern must not be copied here.
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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
            // TRAY-01..05/D-04: components was previously declared but never instantiated
            // (dead field) -- it now owns NotifyIcon/ContextMenuStrip so Dispose(bool)'s
            // existing components?.Dispose() call above becomes a genuine defensive
            // backstop against the well-known "ghost tray icon" WinForms bug, on top of
            // the explicit notifyIcon.Visible = false calls in FormClosing/Exit below.
            this.components = new System.ComponentModel.Container();

            this.toggleSwitch = new RigToggle.App.Controls.ToggleSwitch();
            this.btnSettings = new System.Windows.Forms.Button();
            this.tileStrip = new System.Windows.Forms.FlowLayoutPanel();
            this.lblNoMonitors = new System.Windows.Forms.Label();
            this.btnIdentify = new System.Windows.Forms.Button();
            // quick-260829-fnt/UPDATE-06: menuStrip is a Control (unlike
            // notifyIcon/trayContextMenu below), so it is added to this.Controls and
            // disposed with the form -- do NOT pass this.components to its
            // constructor.
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.helpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpAboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tileToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.trayToggleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySeparator = new System.Windows.Forms.ToolStripSeparator();
            this.trayExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            this.SuspendLayout();

            //
            // toggleSwitch
            //
            // THEME-08/D-01 -- this replaces the former 288x40 stock-Button toggle
            // control with a compact Settings-style toggle row (label left, pill
            // switch right); the control is owner-drawn and code-only
            // (Controls/ToggleSwitch.cs), so unlike the button it replaces it needs no
            // FlatStyle/FlatAppearance workaround -- the dotnet/winforms#13897
            // rationale that used to live here now applies only to the remaining stock
            // Buttons and is documented on ThemeApplier.ThemeButton. Location/Size
            // below are PLACEHOLDER defaults -- LayoutDashboard() (MainForm.cs)
            // overwrites both on every population and hotplug, same as
            // btnIdentify/tileStrip above; do not "fix" these Designer coordinates.
            this.toggleSwitch.Name = "toggleSwitch";
            this.toggleSwitch.Size = new System.Drawing.Size(288, 32);
            this.toggleSwitch.Location = new System.Drawing.Point(16, 148);
            this.toggleSwitch.ActionRequested += new System.EventHandler(this.ToggleSwitch_ActionRequested);

            //
            // btnSettings
            //
            // MAIN-02/D-10: icon-only, de-emphasized gear button. The instance, field,
            // and Click wiring stay identical to the pre-19-02 form -- only its visual
            // identity (no visible Text, small square size, bottom-right position)
            // changes here. The visible label and its "…" ellipsis are deliberately
            // dropped rather than shrunk (no longer competing with the toggle/tiles for
            // attention); AccessibleName + tileToolTip-style tooltip carry the meaning
            // instead. Location below is a PLACEHOLDER -- LayoutDashboard() (MainForm.cs)
            // recomputes it on every population/hotplug from a font-derived scale
            // factor; do not "fix" these Designer coordinates.
            this.btnSettings.Text = string.Empty;
            this.btnSettings.Location = new System.Drawing.Point(272, 252);
            this.btnSettings.Size = new System.Drawing.Size(32, 32);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettings.AccessibleName = "Settings";
            this.btnSettings.TabStop = true;
            this.btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);
            this.btnSettings.Paint += new System.Windows.Forms.PaintEventHandler(this.BtnSettings_Paint);
            this.btnSettings.MouseEnter += new System.EventHandler(this.BtnSettings_MouseEnter);
            this.btnSettings.MouseLeave += new System.EventHandler(this.BtnSettings_MouseLeave);
            this.btnSettings.MouseDown += new System.Windows.Forms.MouseEventHandler(this.BtnSettings_MouseDown);
            this.btnSettings.MouseUp += new System.Windows.Forms.MouseEventHandler(this.BtnSettings_MouseUp);
            this.btnSettings.Enter += new System.EventHandler(this.BtnSettings_Enter);
            this.btnSettings.Leave += new System.EventHandler(this.BtnSettings_Leave);
            this.tileToolTip.SetToolTip(this.btnSettings, "Settings");

            //
            // tileStrip
            //
            // TILE-01: hosts one MonitorTile per detected monitor (MainForm.cs
            // RefreshMonitorTiles). AutoSize is deliberately FALSE -- LayoutDashboard()
            // computes this panel's exact Size arithmetically from tile count and a
            // font-derived scale factor instead of relying on
            // FlowLayoutPanel.AutoSize/Form.AutoSize, because 19-RESEARCH.md Open
            // Question 2 flags that no form in this codebase has ever used AutoSize and
            // its layout-pass timing under the --tray hidden-start path (where
            // InitializeTrayState(), not OnLoad/OnShown, does the population work) is
            // unproven on this runtime. WrapContents still does the row-wrapping within
            // that computed width. Do not "simplify" this back to AutoSize = true.
            // Location/Size below are PLACEHOLDER defaults -- LayoutDashboard()
            // overwrites both on every population and hotplug.
            this.tileStrip.Name = "tileStrip";
            this.tileStrip.AutoSize = false;
            this.tileStrip.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tileStrip.WrapContents = true;
            this.tileStrip.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.tileStrip.Padding = new System.Windows.Forms.Padding(0);
            this.tileStrip.Margin = new System.Windows.Forms.Padding(0);
            this.tileStrip.Location = new System.Drawing.Point(16, 44);
            this.tileStrip.Size = new System.Drawing.Size(288, 88);

            //
            // lblNoMonitors
            //
            // Defensive empty state (GetAllMonitors() returning zero rows is expected
            // unreachable under normal Windows operation) -- occupies the tile strip's
            // rectangle and is shown in its place. Location/Size are PLACEHOLDER
            // defaults -- LayoutDashboard() overwrites both.
            this.lblNoMonitors.Name = "lblNoMonitors";
            this.lblNoMonitors.Text = "No monitors detected.";
            this.lblNoMonitors.AutoSize = false;
            this.lblNoMonitors.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblNoMonitors.Visible = false;
            this.lblNoMonitors.Location = new System.Drawing.Point(16, 44);
            this.lblNoMonitors.Size = new System.Drawing.Size(288, 40);

            //
            // btnIdentify
            //
            // TILE-04: single shared Identify action, with Owner retargeted to
            // MainForm (MainForm.cs, BtnIdentify_Click). Location/Size below are
            // PLACEHOLDER defaults -- LayoutDashboard() overwrites both on every
            // population/hotplug.
            this.btnIdentify.Name = "btnIdentify";
            this.btnIdentify.Text = "Identify";
            this.btnIdentify.Size = new System.Drawing.Size(100, 32);
            this.btnIdentify.Location = new System.Drawing.Point(16, 148);
            this.btnIdentify.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnIdentify.Click += new System.EventHandler(this.BtnIdentify_Click);
            this.btnIdentify.Paint += new System.Windows.Forms.PaintEventHandler(this.BtnIdentify_Paint);
            this.btnIdentify.MouseEnter += new System.EventHandler(this.BtnIdentify_MouseEnter);
            this.btnIdentify.MouseLeave += new System.EventHandler(this.BtnIdentify_MouseLeave);
            this.btnIdentify.MouseDown += new System.Windows.Forms.MouseEventHandler(this.BtnIdentify_MouseDown);
            this.btnIdentify.MouseUp += new System.Windows.Forms.MouseEventHandler(this.BtnIdentify_MouseUp);
            this.btnIdentify.Enter += new System.EventHandler(this.BtnIdentify_Enter);
            this.btnIdentify.Leave += new System.EventHandler(this.BtnIdentify_Leave);

            //
            // helpAboutMenuItem
            //
            // UPDATE-06/quick-260829-ga9: the sole manual "Check for Updates" entry
            // point (via the About dialog it opens) -- the tray item and the
            // Settings button were removed as redundant, silently-broken surfaces.
            this.helpAboutMenuItem.Text = "About";
            this.helpAboutMenuItem.Name = "helpAboutMenuItem";
            this.helpAboutMenuItem.Click += new System.EventHandler(this.HelpAboutMenuItem_Click);

            //
            // helpMenuItem
            //
            this.helpMenuItem.Text = "Help";
            this.helpMenuItem.Name = "helpMenuItem";
            this.helpMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.helpAboutMenuItem});

            //
            // menuStrip
            //
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Dock = System.Windows.Forms.DockStyle.Top;
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.helpMenuItem});

            //
            // trayToggleMenuItem
            //
            // Text left at a safe default; RefreshUi() computes this text directly from
            // the same isInRigMode branch that sets the switch's state, so the tray
            // menu and the switch can never drift (D-04) -- the old toggle button no
            // longer exists.
            this.trayToggleMenuItem.Text = "Switch to Rig Mode";
            this.trayToggleMenuItem.Name = "trayToggleMenuItem";
            this.trayToggleMenuItem.Click += new System.EventHandler(this.TrayToggleMenuItem_Click);

            //
            // traySettingsMenuItem
            //
            this.traySettingsMenuItem.Text = "Settings";
            this.traySettingsMenuItem.Name = "traySettingsMenuItem";
            this.traySettingsMenuItem.Click += new System.EventHandler(this.TraySettingsMenuItem_Click);

            //
            // traySeparator
            //
            this.traySeparator.Name = "traySeparator";

            // 12-02/D-03 rationale (trayContextMenu/traySeparator, THEME-03 scope note):
            // the ToolStrip separator and dropdown-arrow glyph keep their pre-flip color
            // after a live theme change (dotnet/winforms#12027, no clean first-party
            // fix) -- this is an ACCEPTED, known WinForms limitation for this milestone,
            // not a bug to fix. Do NOT expand scope into rebuilding/re-creating
            // trayContextMenu on every ThemeChanged just to chase this cosmetic,
            // rarely-visible stale-color glitch.

            //
            // trayExitMenuItem
            //
            this.trayExitMenuItem.Text = "Exit";
            this.trayExitMenuItem.Name = "trayExitMenuItem";
            this.trayExitMenuItem.Click += new System.EventHandler(this.TrayExitMenuItem_Click);

            //
            // trayContextMenu
            //
            // Exact order per TRAY-03/08-UI-SPEC.md: Switch mode -> Settings ->
            // separator -> Exit.
            this.trayContextMenu.Name = "trayContextMenu";
            this.trayContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.trayToggleMenuItem,
                this.traySettingsMenuItem,
                this.traySeparator,
                this.trayExitMenuItem});

            //
            // notifyIcon
            //
            // NOT added to this.Controls -- NotifyIcon is not a Control. Icon/Text are set
            // from RefreshUi()/InitializeTrayState() (MainForm.cs), never left at a default
            // here, since the correct mode-reflecting glyph must be current-on-first-paint
            // even under --tray startup (D-01, 08-RESEARCH.md Pitfall 6). Visible defaults
            // to false (Phase 11/D-08/D-11): actual visibility is derived by
            // ApplyTrayVisibility() at startup from CloseMinimizesToTray || MinimizeToTray,
            // not hardcoded on -- this prevents a ghost tray icon flashing on a
            // both-settings-off launch before the derived rule runs.
            this.notifyIcon.ContextMenuStrip = this.trayContextMenu;
            this.notifyIcon.Visible = false;
            this.notifyIcon.MouseClick += new System.Windows.Forms.MouseEventHandler(this.NotifyIcon_MouseClick);

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // D-04/D-06: this is only the pre-layout default for the designer/first
            // construction -- LayoutDashboard() (MainForm.cs) overwrites ClientSize on
            // every population and hotplug, so this is NOT the real window size and
            // must not be treated as one.
            this.ClientSize = new System.Drawing.Size(320, 300);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rig Toggle";
            this.Name = "MainForm";
            this.MainMenuStrip = this.menuStrip;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            // D-09 reading order: tile row first, then Identify, then the Rig/Normal
            // toggle, then the de-emphasized Settings gear last. The old mode-text
            // label is deleted this phase (D-06) -- the toggle row is now MainForm's
            // sole mode readout, so the tile row starts this Controls.Add sequence.
            this.Controls.Add(this.tileStrip);
            this.Controls.Add(this.lblNoMonitors);
            this.Controls.Add(this.btnIdentify);
            this.Controls.Add(this.toggleSwitch);
            this.Controls.Add(this.btnSettings);

            // quick-260829-fnt/UPDATE-06: menuStrip is appended LAST, deliberately,
            // so the D-09 reading/tab order above (tiles, Identify, toggle, Settings
            // gear) is left byte-for-byte untouched -- Controls.Add order, not
            // Dock/Location, is what determines that order.
            this.Controls.Add(this.menuStrip);

            this.ResumeLayout(false);
        }

        #endregion

        private RigToggle.App.Controls.ToggleSwitch toggleSwitch;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.FlowLayoutPanel tileStrip;
        private System.Windows.Forms.Label lblNoMonitors;
        private System.Windows.Forms.Button btnIdentify;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem helpMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpAboutMenuItem;
        private System.Windows.Forms.ToolTip tileToolTip;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayContextMenu;
        private System.Windows.Forms.ToolStripMenuItem trayToggleMenuItem;
        private System.Windows.Forms.ToolStripMenuItem traySettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator traySeparator;
        private System.Windows.Forms.ToolStripMenuItem trayExitMenuItem;
    }
}
