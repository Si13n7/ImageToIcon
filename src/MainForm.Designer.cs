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
            this.components = new System.ComponentModel.Container();
            this.ImgPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.Extended = new System.Windows.Forms.CheckBox();
            this.OpenBtn = new System.Windows.Forms.Button();
            this.SaveBtn = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.LayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.ControlPanel = new System.Windows.Forms.Panel();
            this.LayoutPanel.SuspendLayout();
            this.ControlPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // ImgPanel
            // 
            this.ImgPanel.BackColor = System.Drawing.Color.Transparent;
            this.ImgPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ImgPanel.Location = new System.Drawing.Point(3, 3);
            this.ImgPanel.Name = "ImgPanel";
            this.ImgPanel.Padding = new System.Windows.Forms.Padding(4);
            this.ImgPanel.Size = new System.Drawing.Size(606, 275);
            this.ImgPanel.TabIndex = 1;
            // 
            // Extended
            // 
            this.Extended.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Extended.AutoSize = true;
            this.Extended.BackColor = System.Drawing.Color.Transparent;
            this.Extended.Location = new System.Drawing.Point(55, 9);
            this.Extended.Name = "Extended";
            this.Extended.Size = new System.Drawing.Size(126, 17);
            this.Extended.TabIndex = 2;
            this.Extended.TabStop = false;
            this.Extended.Text = "Show extended sizes";
            this.Extended.UseVisualStyleBackColor = false;
            this.Extended.CheckedChanged += new System.EventHandler(this.Extended_CheckedChanged);
            // 
            // OpenBtn
            // 
            this.OpenBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.OpenBtn.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.OpenBtn.Location = new System.Drawing.Point(39, 32);
            this.OpenBtn.Name = "OpenBtn";
            this.OpenBtn.Size = new System.Drawing.Size(200, 30);
            this.OpenBtn.TabIndex = 3;
            this.OpenBtn.TabStop = false;
            this.OpenBtn.Text = "Open Image";
            this.OpenBtn.UseVisualStyleBackColor = true;
            this.OpenBtn.Click += new System.EventHandler(this.OpenBtn_Click);
            // 
            // SaveBtn
            // 
            this.SaveBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SaveBtn.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.SaveBtn.Location = new System.Drawing.Point(367, 32);
            this.SaveBtn.Name = "SaveBtn";
            this.SaveBtn.Size = new System.Drawing.Size(200, 30);
            this.SaveBtn.TabIndex = 4;
            this.SaveBtn.TabStop = false;
            this.SaveBtn.Text = "Save Icon";
            this.SaveBtn.UseVisualStyleBackColor = true;
            this.SaveBtn.Click += new System.EventHandler(this.SaveBtn_Click);
            // 
            // LayoutPanel
            // 
            this.LayoutPanel.BackColor = System.Drawing.Color.Transparent;
            this.LayoutPanel.ColumnCount = 1;
            this.LayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.LayoutPanel.Controls.Add(this.ImgPanel, 0, 0);
            this.LayoutPanel.Controls.Add(this.ControlPanel, 0, 1);
            this.LayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.LayoutPanel.Name = "LayoutPanel";
            this.LayoutPanel.RowCount = 2;
            this.LayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.LayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.LayoutPanel.Size = new System.Drawing.Size(612, 361);
            this.LayoutPanel.TabIndex = 5;
            // 
            // ControlPanel
            // 
            this.ControlPanel.Controls.Add(this.Extended);
            this.ControlPanel.Controls.Add(this.OpenBtn);
            this.ControlPanel.Controls.Add(this.SaveBtn);
            this.ControlPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ControlPanel.Location = new System.Drawing.Point(3, 284);
            this.ControlPanel.Name = "ControlPanel";
            this.ControlPanel.Size = new System.Drawing.Size(606, 74);
            this.ControlPanel.TabIndex = 2;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(243)))), ((int)(((byte)(243)))));
            this.ClientSize = new System.Drawing.Size(612, 361);
            this.Controls.Add(this.LayoutPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(628, 400);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Image to Icon";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.LayoutPanel.ResumeLayout(false);
            this.ControlPanel.ResumeLayout(false);
            this.ControlPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel ImgPanel;
        private System.Windows.Forms.CheckBox Extended;
        private System.Windows.Forms.Button OpenBtn;
        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.TableLayoutPanel LayoutPanel;
        private System.Windows.Forms.Panel ControlPanel;
    }
}

