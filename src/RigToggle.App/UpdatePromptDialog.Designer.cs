namespace RigToggle.App
{
    partial class UpdatePromptDialog
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

            // Deterministic backstop unsubscribe, mirroring MonitorConfirmDialog's
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
            this.lblHeadline = new System.Windows.Forms.Label();
            this.rtbReleaseNotes = new System.Windows.Forms.RichTextBox();
            this.btnSkip = new System.Windows.Forms.Button();
            this.btnLater = new System.Windows.Forms.Button();
            this.btnUpdateNow = new System.Windows.Forms.Button();

            this.SuspendLayout();

            //
            // lblHeadline
            //
            this.lblHeadline.Location = new System.Drawing.Point(12, 12);
            this.lblHeadline.Size = new System.Drawing.Size(416, 24);
            this.lblHeadline.AutoSize = false;
            this.lblHeadline.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.lblHeadline.Name = "lblHeadline";

            //
            // rtbReleaseNotes
            //
            this.rtbReleaseNotes.Location = new System.Drawing.Point(12, 44);
            this.rtbReleaseNotes.Size = new System.Drawing.Size(416, 280);
            this.rtbReleaseNotes.ReadOnly = true;
            this.rtbReleaseNotes.WordWrap = true;
            this.rtbReleaseNotes.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtbReleaseNotes.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rtbReleaseNotes.TabStop = false;
            this.rtbReleaseNotes.Name = "rtbReleaseNotes";

            //
            // btnSkip
            //
            this.btnSkip.Text = "Skip this version";
            this.btnSkip.Location = new System.Drawing.Point(12, 424);
            this.btnSkip.MinimumSize = new System.Drawing.Size(130, 32);
            this.btnSkip.Height = 32;
            this.btnSkip.AutoSize = true;
            this.btnSkip.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSkip.DialogResult = System.Windows.Forms.DialogResult.Ignore;
            this.btnSkip.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSkip.Name = "btnSkip";

            //
            // btnLater
            //
            this.btnLater.Text = "Later";
            this.btnLater.Location = new System.Drawing.Point(220, 424);
            this.btnLater.MinimumSize = new System.Drawing.Size(90, 32);
            this.btnLater.Height = 32;
            this.btnLater.AutoSize = true;
            this.btnLater.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnLater.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnLater.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLater.Name = "btnLater";

            //
            // btnUpdateNow
            //
            this.btnUpdateNow.Text = "Update Now";
            this.btnUpdateNow.Location = new System.Drawing.Point(318, 424);
            this.btnUpdateNow.MinimumSize = new System.Drawing.Size(110, 32);
            this.btnUpdateNow.Height = 32;
            this.btnUpdateNow.AutoSize = true;
            this.btnUpdateNow.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnUpdateNow.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnUpdateNow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnUpdateNow.Name = "btnUpdateNow";

            //
            // UpdatePromptDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(440, 460);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Update Available";
            this.Name = "UpdatePromptDialog";

            this.Controls.Add(this.lblHeadline);
            this.Controls.Add(this.rtbReleaseNotes);
            this.Controls.Add(this.btnSkip);
            this.Controls.Add(this.btnLater);
            this.Controls.Add(this.btnUpdateNow);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblHeadline;
        private System.Windows.Forms.RichTextBox rtbReleaseNotes;
        private System.Windows.Forms.Button btnSkip;
        private System.Windows.Forms.Button btnLater;
        private System.Windows.Forms.Button btnUpdateNow;
    }
}
