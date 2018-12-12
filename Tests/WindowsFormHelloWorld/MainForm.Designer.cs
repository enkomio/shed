using System.Diagnostics;

namespace WindowsFormHelloWorld
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
            this.Hello = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Hello
            // 
            this.Hello.AutoSize = true;
            this.Hello.Location = new System.Drawing.Point(39, 44);
            this.Hello.Name = "Hello";
            this.Hello.Size = new System.Drawing.Size(0, 13);
            this.Hello.TabIndex = 0;

            var id = Process.GetCurrentProcess().Id;
            this.Hello.Text = "Hello World, from process: " + id.ToString();

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(235, 128);
            this.Controls.Add(this.Hello);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label Hello;
    }
}

