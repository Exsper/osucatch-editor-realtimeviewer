﻿namespace osucatch_editor_realtimeviewer
{
    partial class Form1
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
            components = new System.ComponentModel.Container();
            reader_timer = new System.Windows.Forms.Timer(components);
            Canvas = new Canvas();
            menuStrip1 = new MenuStrip();
            modToolStripMenuItem = new ToolStripMenuItem();
            noneToolStripMenuItem = new ToolStripMenuItem();
            hRToolStripMenuItem = new ToolStripMenuItem();
            eZToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            openSettingsFileToolStripMenuItem = new ToolStripMenuItem();
            githubToolStripMenuItem = new ToolStripMenuItem();
            githubToolStripMenuItem1 = new ToolStripMenuItem();
            backup_timer = new System.Windows.Forms.Timer(components);
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // reader_timer
            // 
            reader_timer.Interval = 1000;
            reader_timer.Tick += reader_timer_Tick;
            // 
            // Canvas
            // 
            Canvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Canvas.BackColor = Color.Black;
            Canvas.CatcherAreaHeight = 0F;
            Canvas.Location = new Point(0, 28);
            Canvas.Margin = new Padding(4, 5, 4, 5);
            Canvas.Name = "Canvas";
            Canvas.Size = new Size(234, 683);
            Canvas.TabIndex = 33;
            Canvas.VSync = false;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { modToolStripMenuItem, settingsToolStripMenuItem, githubToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(234, 25);
            menuStrip1.TabIndex = 34;
            menuStrip1.Text = "menuStrip1";
            // 
            // modToolStripMenuItem
            // 
            modToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { noneToolStripMenuItem, hRToolStripMenuItem, eZToolStripMenuItem });
            modToolStripMenuItem.Name = "modToolStripMenuItem";
            modToolStripMenuItem.Size = new Size(48, 21);
            modToolStripMenuItem.Text = "Mod";
            // 
            // noneToolStripMenuItem
            // 
            noneToolStripMenuItem.Checked = true;
            noneToolStripMenuItem.CheckState = CheckState.Checked;
            noneToolStripMenuItem.Name = "noneToolStripMenuItem";
            noneToolStripMenuItem.Size = new Size(180, 22);
            noneToolStripMenuItem.Text = "None";
            noneToolStripMenuItem.Click += noneToolStripMenuItem_Click;
            // 
            // hRToolStripMenuItem
            // 
            hRToolStripMenuItem.Name = "hRToolStripMenuItem";
            hRToolStripMenuItem.Size = new Size(180, 22);
            hRToolStripMenuItem.Text = "HR";
            hRToolStripMenuItem.Click += hRToolStripMenuItem_Click;
            // 
            // eZToolStripMenuItem
            // 
            eZToolStripMenuItem.Name = "eZToolStripMenuItem";
            eZToolStripMenuItem.Size = new Size(180, 22);
            eZToolStripMenuItem.Text = "EZ";
            eZToolStripMenuItem.Click += eZToolStripMenuItem_Click;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openSettingsFileToolStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            settingsToolStripMenuItem.Size = new Size(66, 21);
            settingsToolStripMenuItem.Text = "Settings";
            // 
            // openSettingsFileToolStripMenuItem
            // 
            openSettingsFileToolStripMenuItem.Name = "openSettingsFileToolStripMenuItem";
            openSettingsFileToolStripMenuItem.Size = new Size(180, 22);
            openSettingsFileToolStripMenuItem.Text = "Open Settings";
            openSettingsFileToolStripMenuItem.Click += openSettingsFileToolStripMenuItem_Click;
            // 
            // githubToolStripMenuItem
            // 
            githubToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { githubToolStripMenuItem1 });
            githubToolStripMenuItem.Name = "githubToolStripMenuItem";
            githubToolStripMenuItem.Size = new Size(55, 21);
            githubToolStripMenuItem.Text = "About";
            // 
            // githubToolStripMenuItem1
            // 
            githubToolStripMenuItem1.Name = "githubToolStripMenuItem1";
            githubToolStripMenuItem1.Size = new Size(114, 22);
            githubToolStripMenuItem1.Text = "Github";
            githubToolStripMenuItem1.Click += githubToolStripMenuItem1_Click;
            // 
            // backup_timer
            // 
            backup_timer.Interval = 60000;
            backup_timer.Tick += backup_timer_Tick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(234, 711);
            Controls.Add(Canvas);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "OsuCatch Editor Realtime Viewer";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Timer reader_timer;
        private Canvas Canvas;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem modToolStripMenuItem;
        private ToolStripMenuItem noneToolStripMenuItem;
        private ToolStripMenuItem hRToolStripMenuItem;
        private ToolStripMenuItem eZToolStripMenuItem;
        private ToolStripMenuItem githubToolStripMenuItem;
        private ToolStripMenuItem githubToolStripMenuItem1;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem openSettingsFileToolStripMenuItem;
        private System.Windows.Forms.Timer backup_timer;
    }
}
