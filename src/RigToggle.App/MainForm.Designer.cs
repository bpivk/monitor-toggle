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
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblMode = new System.Windows.Forms.Label();
            this.btnToggle = new System.Windows.Forms.Button();
            this.btnSettings = new System.Windows.Forms.Button();

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
            this.btnToggle.Click += new System.EventHandler(this.BtnToggle_Click);

            //
            // btnSettings
            //
            this.btnSettings.Text = "Settings…";
            this.btnSettings.Location = new System.Drawing.Point(16, 108);
            this.btnSettings.Size = new System.Drawing.Size(288, 32);
            this.btnSettings.Name = "btnSettings";
            this.btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);

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

            this.Controls.Add(this.lblMode);
            this.Controls.Add(this.btnToggle);
            this.Controls.Add(this.btnSettings);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblMode;
        private System.Windows.Forms.Button btnToggle;
        private System.Windows.Forms.Button btnSettings;
    }
}
