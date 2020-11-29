namespace PuppetMasterGUI
{
    partial class CommandsPage
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ServerIdTextbox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.CrashButton = new System.Windows.Forms.Button();
            this.FreezeButton = new System.Windows.Forms.Button();
            this.UnfreezeButton = new System.Windows.Forms.Button();
            this.resultLabel = new System.Windows.Forms.Label();
            this.StatusButton = new System.Windows.Forms.Button();
            this.ServerList = new System.Windows.Forms.ListBox();
            this.serverListLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // ServerIdTextbox
            // 
            this.ServerIdTextbox.Location = new System.Drawing.Point(73, 156);
            this.ServerIdTextbox.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.ServerIdTextbox.Name = "ServerIdTextbox";
            this.ServerIdTextbox.Size = new System.Drawing.Size(241, 27);
            this.ServerIdTextbox.TabIndex = 0;
            this.ServerIdTextbox.TextChanged += new System.EventHandler(this.ServerIdTextbox_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(131, 81);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(156, 41);
            this.label1.TabIndex = 1;
            this.label1.Text = "SERVER ID";
            // 
            // CrashButton
            // 
            this.CrashButton.Enabled = false;
            this.CrashButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.CrashButton.Location = new System.Drawing.Point(31, 251);
            this.CrashButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.CrashButton.Name = "CrashButton";
            this.CrashButton.Size = new System.Drawing.Size(94, 45);
            this.CrashButton.TabIndex = 2;
            this.CrashButton.Text = "CRASH";
            this.CrashButton.UseVisualStyleBackColor = true;
            this.CrashButton.Click += new System.EventHandler(this.CrashButton_Click);
            // 
            // FreezeButton
            // 
            this.FreezeButton.Enabled = false;
            this.FreezeButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.FreezeButton.Location = new System.Drawing.Point(131, 251);
            this.FreezeButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.FreezeButton.Name = "FreezeButton";
            this.FreezeButton.Size = new System.Drawing.Size(99, 45);
            this.FreezeButton.TabIndex = 3;
            this.FreezeButton.Text = "FREEZE";
            this.FreezeButton.UseVisualStyleBackColor = true;
            this.FreezeButton.Click += new System.EventHandler(this.FreezeButton_Click);
            // 
            // UnfreezeButton
            // 
            this.UnfreezeButton.Enabled = false;
            this.UnfreezeButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.UnfreezeButton.Location = new System.Drawing.Point(236, 251);
            this.UnfreezeButton.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.UnfreezeButton.Name = "UnfreezeButton";
            this.UnfreezeButton.Size = new System.Drawing.Size(111, 45);
            this.UnfreezeButton.TabIndex = 4;
            this.UnfreezeButton.Text = "UNFREEZE";
            this.UnfreezeButton.UseVisualStyleBackColor = true;
            this.UnfreezeButton.Click += new System.EventHandler(this.UnfreezeButton_Click);
            // 
            // resultLabel
            // 
            this.resultLabel.AutoSize = true;
            this.resultLabel.Location = new System.Drawing.Point(48, 340);
            this.resultLabel.Name = "resultLabel";
            this.resultLabel.Size = new System.Drawing.Size(0, 20);
            this.resultLabel.TabIndex = 5;
            // 
            // StatusButton
            // 
            this.StatusButton.Location = new System.Drawing.Point(384, 267);
            this.StatusButton.Name = "StatusButton";
            this.StatusButton.Size = new System.Drawing.Size(107, 29);
            this.StatusButton.TabIndex = 6;
            this.StatusButton.Text = "STATUS";
            this.StatusButton.UseVisualStyleBackColor = true;
            this.StatusButton.Click += new System.EventHandler(this.StatusButton_Click);
            // 
            // ServerList
            // 
            this.ServerList.FormattingEnabled = true;
            this.ServerList.ItemHeight = 20;
            this.ServerList.Location = new System.Drawing.Point(384, 83);
            this.ServerList.Name = "ServerList";
            this.ServerList.Size = new System.Drawing.Size(107, 164);
            this.ServerList.TabIndex = 7;
            // 
            // serverListLabel
            // 
            this.serverListLabel.AutoSize = true;
            this.serverListLabel.Font = new System.Drawing.Font("Segoe UI", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.serverListLabel.Location = new System.Drawing.Point(384, 40);
            this.serverListLabel.Name = "serverListLabel";
            this.serverListLabel.Size = new System.Drawing.Size(109, 25);
            this.serverListLabel.TabIndex = 8;
            this.serverListLabel.Text = "Servers List";
            // 
            // CommandsPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(518, 323);
            this.Controls.Add(this.serverListLabel);
            this.Controls.Add(this.ServerList);
            this.Controls.Add(this.StatusButton);
            this.Controls.Add(this.resultLabel);
            this.Controls.Add(this.UnfreezeButton);
            this.Controls.Add(this.FreezeButton);
            this.Controls.Add(this.CrashButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ServerIdTextbox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "CommandsPage";
            this.Text = "CommandsPage";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox ServerIdTextbox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button CrashButton;
        private System.Windows.Forms.Button FreezeButton;
        private System.Windows.Forms.Button UnfreezeButton;
        private System.Windows.Forms.Label resultLabel;
        private System.Windows.Forms.Button StatusButton;
        private System.Windows.Forms.ListBox ServerList;
        private System.Windows.Forms.Label serverListLabel;
    }
}