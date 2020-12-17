namespace ImageViewer
{
    partial class Form1
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
            this.pickImgBtn = new System.Windows.Forms.Button();
            this.leftBtn = new System.Windows.Forms.Button();
            this.rightBtn = new System.Windows.Forms.Button();
            this.resetZoomBtn = new System.Windows.Forms.Button();
            this.editButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // pickImgBtn
            // 
            this.pickImgBtn.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.pickImgBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.pickImgBtn.Location = new System.Drawing.Point(406, 303);
            this.pickImgBtn.Name = "pickImgBtn";
            this.pickImgBtn.Size = new System.Drawing.Size(96, 32);
            this.pickImgBtn.TabIndex = 0;
            this.pickImgBtn.Text = "Pick Folder";
            this.pickImgBtn.UseVisualStyleBackColor = true;
            this.pickImgBtn.Click += new System.EventHandler(this.pickImgBtn_Click);
            // 
            // leftBtn
            // 
            this.leftBtn.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.leftBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.leftBtn.Location = new System.Drawing.Point(508, 303);
            this.leftBtn.Name = "leftBtn";
            this.leftBtn.Size = new System.Drawing.Size(30, 32);
            this.leftBtn.TabIndex = 1;
            this.leftBtn.Text = "<";
            this.leftBtn.UseVisualStyleBackColor = true;
            this.leftBtn.Click += new System.EventHandler(this.leftBtn_Click);
            // 
            // rightBtn
            // 
            this.rightBtn.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.rightBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rightBtn.Location = new System.Drawing.Point(544, 303);
            this.rightBtn.Name = "rightBtn";
            this.rightBtn.Size = new System.Drawing.Size(30, 32);
            this.rightBtn.TabIndex = 2;
            this.rightBtn.Text = ">";
            this.rightBtn.UseVisualStyleBackColor = true;
            this.rightBtn.Click += new System.EventHandler(this.rightBtn_Click);
            // 
            // resetZoomBtn
            // 
            this.resetZoomBtn.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.resetZoomBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.resetZoomBtn.Location = new System.Drawing.Point(0, 303);
            this.resetZoomBtn.Name = "resetZoomBtn";
            this.resetZoomBtn.Size = new System.Drawing.Size(36, 32);
            this.resetZoomBtn.TabIndex = 3;
            this.resetZoomBtn.Text = "1:1";
            this.resetZoomBtn.UseVisualStyleBackColor = true;
            this.resetZoomBtn.Click += new System.EventHandler(this.resetZoomBtn_Click);
            // 
            // editButton
            // 
            this.editButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.editButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.editButton.Location = new System.Drawing.Point(42, 303);
            this.editButton.Name = "editButton";
            this.editButton.Size = new System.Drawing.Size(36, 32);
            this.editButton.TabIndex = 4;
            this.editButton.Text = "Edit";
            this.editButton.UseVisualStyleBackColor = true;
            this.editButton.Click += new System.EventHandler(this.editButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.exportButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.exportButton.Location = new System.Drawing.Point(84, 303);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(52, 32);
            this.exportButton.TabIndex = 5;
            this.exportButton.Text = "Export";
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(574, 335);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.editButton);
            this.Controls.Add(this.resetZoomBtn);
            this.Controls.Add(this.rightBtn);
            this.Controls.Add(this.leftBtn);
            this.Controls.Add(this.pickImgBtn);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(380, 240);
            this.Name = "Form1";
            this.Text = "Loading...";
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button exportButton;

        private System.Windows.Forms.Button editButton;

        private System.Windows.Forms.Button resetZoomBtn;

        private System.Windows.Forms.Button leftBtn;

        private System.Windows.Forms.Button rightBtn;

        private System.Windows.Forms.Button pickImgBtn;

        #endregion
    }
}