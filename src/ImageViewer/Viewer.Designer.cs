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
            this.SuspendLayout();
            // 
            // pickImgBtn
            // 
            this.pickImgBtn.Location = new System.Drawing.Point(12, 12);
            this.pickImgBtn.Name = "pickImgBtn";
            this.pickImgBtn.Size = new System.Drawing.Size(96, 32);
            this.pickImgBtn.TabIndex = 0;
            this.pickImgBtn.Text = "Pick Folder";
            this.pickImgBtn.UseVisualStyleBackColor = true;
            this.pickImgBtn.Click += new System.EventHandler(this.pickImgBtn_Click);
            // 
            // leftBtn
            // 
            this.leftBtn.Location = new System.Drawing.Point(114, 12);
            this.leftBtn.Name = "leftBtn";
            this.leftBtn.Size = new System.Drawing.Size(30, 32);
            this.leftBtn.TabIndex = 1;
            this.leftBtn.Text = "<";
            this.leftBtn.UseVisualStyleBackColor = true;
            this.leftBtn.Click += new System.EventHandler(this.leftBtn_Click);
            // 
            // rightBtn
            // 
            this.rightBtn.Location = new System.Drawing.Point(150, 12);
            this.rightBtn.Name = "rightBtn";
            this.rightBtn.Size = new System.Drawing.Size(30, 32);
            this.rightBtn.TabIndex = 2;
            this.rightBtn.Text = ">";
            this.rightBtn.UseVisualStyleBackColor = true;
            this.rightBtn.Click += new System.EventHandler(this.rightBtn_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 378);
            this.Controls.Add(this.rightBtn);
            this.Controls.Add(this.leftBtn);
            this.Controls.Add(this.pickImgBtn);
            this.DoubleBuffered = true;
            this.MinimumSize = new System.Drawing.Size(380, 240);
            this.Name = "Form1";
            this.Text = "Loading...";
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseUp);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button leftBtn;

        private System.Windows.Forms.Button rightBtn;

        private System.Windows.Forms.Button pickImgBtn;

        #endregion
    }
}