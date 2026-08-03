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
            // 12-02/THEME-05: FlatStyle.System (never .Flat, dotnet/winforms#13897) --
            // renders a flat, theme-aware button using the OS's own visual-styles
            // renderer instead of WinForms' 3D-bevel default.
            this.btnToggle.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);

            //
            // btnSettings
            //
            this.btnSettings.Text = "Settings…";
            this.btnSettings.Location = new System.Drawing.Point(16, 108);
            this.btnSettings.Size = new System.Drawing.Size(288, 32);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);

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
            // Exact order per TRAY-03/08-UI-SPEC.md: Switch mode -> Settings -> separator -> Exit.
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
            this.ClientSize = new System.Drawing.Size(320, 200);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Rig Toggle";
            this.Name = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Resize += new System.EventHandler(this.MainForm_Resize);

            this.Controls.Add(this.lblMode);
            this.Controls.Add(this.btnToggle);
            this.Controls.Add(this.btnSettings);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblMode;
        private System.Windows.Forms.Button btnToggle;
        private System.Windows.Forms.Button btnSettings;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip trayContextMenu;
        private System.Windows.Forms.ToolStripMenuItem trayToggleMenuItem;
        private System.Windows.Forms.ToolStripMenuItem traySettingsMenuItem;
        private System.Windows.Forms.ToolStripSeparator traySeparator;
        private System.Windows.Forms.ToolStripMenuItem trayExitMenuItem;
    }
}
