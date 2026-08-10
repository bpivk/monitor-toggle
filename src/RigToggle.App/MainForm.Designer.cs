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

            this.lblMode = new System.Windows.Forms.Label();
            this.btnToggle = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();
            this.tileStrip = new System.Windows.Forms.FlowLayoutPanel();
            this.lblNoMonitors = new System.Windows.Forms.Label();
            this.btnIdentify = new System.Windows.Forms.Button();
            this.tileToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.trayContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.trayToggleMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySettingsMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.traySeparator = new System.Windows.Forms.ToolStripSeparator();
            this.trayExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            this.SuspendLayout();

            //
            // lblMode
            //
            this.lblMode.Text = "Mode: Normal";
            this.lblMode.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblMode.Location = new System.Drawing.Point(16, 16);
            this.lblMode.Size = new System.Drawing.Size(288, 20);
            this.lblMode.AutoSize = false;
            this.lblMode.Name = "lblMode";

            //
            // btnToggle
            //
            this.btnToggle.Text = "Switch to Rig Mode";
            this.btnToggle.Location = new System.Drawing.Point(16, 60);
            this.btnToggle.Size = new System.Drawing.Size(288, 40);
            this.btnToggle.Name = "btnToggle";
            // 12-05/THEME-05 (12-REVIEW.md CR-02): FlatStyle.Flat, not .System -- the
            // Windows 11 rig proved FlatStyle.System buttons do NOT pick up dark-mode
            // coloring on this runtime. ThemeApplier.ThemeButton (called at load and on
            // every live flip) re-asserts Flat + explicit palette colors, working around
            // dotnet/winforms#13897's unreliable FlatAppearance auto-apply pipeline via
            // BorderSize=0 + explicit hover/pressed overrides. This Designer assignment
            // is the declarative default; the runtime call is belt-and-suspenders.
            this.btnToggle.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);

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
            // trayToggleMenuItem
            //
            // Text left at a safe default; RefreshUi() overrides it every call with
            // btnToggle.Text so the tray menu and the GUI button never drift (D-04).
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
            // separator -> Exit. This reverts to the pre-Phase-17 three-item order now
            // that the standalone panel's tray entry is retired (TILE-07/Plan 19-04)
            // -- the tile dashboard on MainForm itself has fully absorbed that
            // capability.
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
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            // D-09 reading order: tile row first, then Identify, then the Rig/Normal
            // toggle, then the de-emphasized Settings gear last. lblMode stays above
            // the tile row (see LayoutDashboard()'s own comment) so it is still first
            // in this Controls.Add sequence.
            this.Controls.Add(this.lblMode);
            this.Controls.Add(this.tileStrip);
            this.Controls.Add(this.lblNoMonitors);
            this.Controls.Add(this.btnIdentify);
            this.Controls.Add(this.btnToggle);
            this.Controls.Add(this.btnSettings);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblMode;
        private System.Windows.Forms.Button btnToggle;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.FlowLayoutPanel tileStrip;
        private System.Windows.Forms.Label lblNoMonitors;
        private System.Windows.Forms.Button btnIdentify;
        private System.Windows.Forms.ToolTip tileToolTip;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayContextMenu;
        private System.Windows.Forms.ToolStripMenuItem trayToggleMenuItem;
        private System.Windows.Forms.ToolStripMenuItem traySettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator traySeparator;
        private System.Windows.Forms.ToolStripMenuItem trayExitMenuItem;
    }
}
