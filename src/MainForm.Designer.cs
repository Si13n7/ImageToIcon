namespace ImageToIcon
{
    partial class MainForm
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
            this.ImgPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.Extended = new System.Windows.Forms.CheckBox();
            this.OpenBtn = new System.Windows.Forms.Button();
            this.SaveBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ImgPanel
            // 
            this.ImgPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.ImgPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.ImgPanel.Location = new System.Drawing.Point(0, 0);
            this.ImgPanel.Name = "ImgPanel";
            this.ImgPanel.Padding = new System.Windows.Forms.Padding(4);
            this.ImgPanel.Size = new System.Drawing.Size(336, 410);
            this.ImgPanel.TabIndex = 1;
            // 
            // Extended
            // 
            this.Extended.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Extended.AutoSize = true;
            this.Extended.Location = new System.Drawing.Point(11, 424);
            this.Extended.Name = "Extended";
            this.Extended.Size = new System.Drawing.Size(99, 17);
            this.Extended.TabIndex = 2;
            this.Extended.TabStop = false;
            this.Extended.Text = "Extended Sizes";
            this.Extended.UseVisualStyleBackColor = true;
            this.Extended.CheckedChanged += new System.EventHandler(this.Extended_CheckedChanged);
            // 
            // OpenBtn
            // 
            this.OpenBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.OpenBtn.Location = new System.Drawing.Point(116, 421);
            this.OpenBtn.Name = "OpenBtn";
            this.OpenBtn.Size = new System.Drawing.Size(101, 23);
            this.OpenBtn.TabIndex = 3;
            this.OpenBtn.TabStop = false;
            this.OpenBtn.Text = "Open Image";
            this.OpenBtn.UseVisualStyleBackColor = true;
            this.OpenBtn.Click += new System.EventHandler(this.OpenBtn_Click);
            // 
            // SaveBtn
            // 
            this.SaveBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SaveBtn.Location = new System.Drawing.Point(223, 421);
            this.SaveBtn.Name = "SaveBtn";
            this.SaveBtn.Size = new System.Drawing.Size(101, 23);
            this.SaveBtn.TabIndex = 4;
            this.SaveBtn.TabStop = false;
            this.SaveBtn.Text = "Save Icon";
            this.SaveBtn.UseVisualStyleBackColor = true;
            this.SaveBtn.Click += new System.EventHandler(this.SaveBtn_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.ClientSize = new System.Drawing.Size(336, 455);
            this.Controls.Add(this.SaveBtn);
            this.Controls.Add(this.OpenBtn);
            this.Controls.Add(this.Extended);
            this.Controls.Add(this.ImgPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(402, 494);
            this.MinimumSize = new System.Drawing.Size(352, 494);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Convert Image to Icon";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel ImgPanel;
        private System.Windows.Forms.CheckBox Extended;
        private System.Windows.Forms.Button OpenBtn;
        private System.Windows.Forms.Button SaveBtn;
    }
}

