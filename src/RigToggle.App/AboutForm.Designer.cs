namespace RigToggle.App
{
    partial class AboutForm
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

            // Deterministic backstop unsubscribe, mirroring UpdatePromptDialog's
            // Dispose(bool) pattern -- the constructor's FormClosed-based unsubscribe
            // covers the normal ShowDialog-then-close path; this covers an abnormal
            // dispose that never fires FormClosed (e.g. an exception between
            // construction and ShowDialog returning).
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
            this.lblAppName = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblUpdateStatus = new System.Windows.Forms.Label();
            this.btnCheckForUpdates = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();

            this.SuspendLayout();

            //
            // lblAppName
            //
            this.lblAppName.Location = new System.Drawing.Point(12, 12);
            this.lblAppName.Size = new System.Drawing.Size(336, 26);
            this.lblAppName.AutoSize = false;
            this.lblAppName.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblAppName.Text = "Rig Toggle";
            this.lblAppName.Name = "lblAppName";

            //
            // lblVersion
            //
            this.lblVersion.Location = new System.Drawing.Point(12, 44);
            this.lblVersion.Size = new System.Drawing.Size(336, 20);
            this.lblVersion.AutoSize = false;
            this.lblVersion.Name = "lblVersion";

            //
            // lblUpdateStatus
            //
            // Quick-260829-ga9: always-visible inline status line for manual-check
            // outcomes (D-06/D-07). AutoEllipsis lets a long exception message wrap
            // to ~two lines then ellipsis rather than overflow this fixed-size
            // dialog. No per-label theming call -- it inherits the form surface
            // exactly like lblAppName/lblVersion (ThemeApplier has no label API).
            this.lblUpdateStatus.Location = new System.Drawing.Point(12, 70);
            this.lblUpdateStatus.Size = new System.Drawing.Size(336, 34);
            this.lblUpdateStatus.AutoSize = false;
            this.lblUpdateStatus.AutoEllipsis = true;
            this.lblUpdateStatus.Text = string.Empty;
            this.lblUpdateStatus.Name = "lblUpdateStatus";

            //
            // btnCheckForUpdates
            //
            this.btnCheckForUpdates.Text = "Check for Updates";
            this.btnCheckForUpdates.Location = new System.Drawing.Point(12, 108);
            this.btnCheckForUpdates.MinimumSize = new System.Drawing.Size(150, 32);
            this.btnCheckForUpdates.Height = 32;
            this.btnCheckForUpdates.AutoSize = true;
            this.btnCheckForUpdates.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnCheckForUpdates.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCheckForUpdates.Name = "btnCheckForUpdates";

            //
            // btnClose
            //
            this.btnClose.Text = "Close";
            this.btnClose.Location = new System.Drawing.Point(258, 108);
            this.btnClose.MinimumSize = new System.Drawing.Size(90, 32);
            this.btnClose.Height = 32;
            this.btnClose.AutoSize = true;
            this.btnClose.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Name = "btnClose";

            //
            // AboutForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(360, 152);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About Rig Toggle";
            this.Name = "AboutForm";

            this.Controls.Add(this.lblAppName);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblUpdateStatus);
            this.Controls.Add(this.btnCheckForUpdates);
            this.Controls.Add(this.btnClose);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblAppName;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblUpdateStatus;
        private System.Windows.Forms.Button btnCheckForUpdates;
        private System.Windows.Forms.Button btnClose;
    }
}
