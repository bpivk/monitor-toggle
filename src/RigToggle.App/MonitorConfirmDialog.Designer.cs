namespace RigToggle.App
{
    partial class MonitorConfirmDialog
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
            this.lblMessage = new System.Windows.Forms.Label();
            this.chkDontAskAgain = new System.Windows.Forms.CheckBox();
            this.btnContinue = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();

            this.SuspendLayout();

            //
            // lblMessage
            //
            this.lblMessage.Location = new System.Drawing.Point(12, 12);
            this.lblMessage.Size = new System.Drawing.Size(360, 72);
            this.lblMessage.AutoSize = false;
            this.lblMessage.Name = "lblMessage";

            //
            // chkDontAskAgain
            //
            this.chkDontAskAgain.Text = "Don't ask again";
            this.chkDontAskAgain.Location = new System.Drawing.Point(12, 88);
            this.chkDontAskAgain.Size = new System.Drawing.Size(200, 24);
            this.chkDontAskAgain.Name = "chkDontAskAgain";

            //
            // btnContinue
            //
            this.btnContinue.Text = "Continue";
            this.btnContinue.Location = new System.Drawing.Point(184, 124);
            this.btnContinue.Size = new System.Drawing.Size(90, 32);
            this.btnContinue.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnContinue.Name = "btnContinue";
            // 12-02/THEME-05: FlatStyle.System (never .Flat, dotnet/winforms#13897).
            this.btnContinue.FlatStyle = System.Windows.Forms.FlatStyle.System;

            //
            // btnCancel
            //
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new System.Drawing.Point(282, 124);
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.System;

            //
            // MonitorConfirmDialog
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 168);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Confirm Monitor Changes?";
            this.Name = "MonitorConfirmDialog";

            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.chkDontAskAgain);
            this.Controls.Add(this.btnContinue);
            this.Controls.Add(this.btnCancel);

            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.CheckBox chkDontAskAgain;
        private System.Windows.Forms.Button btnContinue;
        private System.Windows.Forms.Button btnCancel;
    }
}
