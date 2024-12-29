﻿namespace osucatch_editor_realtimeviewer
{
    partial class SettingsForm
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
            groupBox1 = new GroupBox();
            button_height_reset = new Button();
            button_width_reset = new Button();
            numericUpDown_height = new NumericUpDown();
            numericUpDown_width = new NumericUpDown();
            label2 = new Label();
            label1 = new Label();
            groupBox2 = new GroupBox();
            button_osuFolder_reset = new Button();
            button_osuFolder_select = new Button();
            textBox_osuFolder = new TextBox();
            label3 = new Label();
            groupBox3 = new GroupBox();
            button_drawingInterval_reset = new Button();
            numericUpDown_idleInterval = new NumericUpDown();
            button_idleInterval_reset = new Button();
            label5 = new Label();
            numericUpDown_drawingInterval = new NumericUpDown();
            label4 = new Label();
            groupBox4 = new GroupBox();
            checkBox_enableBackup = new CheckBox();
            numericUpDown_backupInterval = new NumericUpDown();
            button_backupFolder_reset = new Button();
            button1 = new Button();
            textBox_backupFolder = new TextBox();
            label7 = new Label();
            label6 = new Label();
            button_backupFolder_select = new Button();
            button_apply = new Button();
            button_cancel = new Button();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_height).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_width).BeginInit();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_idleInterval).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_drawingInterval).BeginInit();
            groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_backupInterval).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(button_height_reset);
            groupBox1.Controls.Add(button_width_reset);
            groupBox1.Controls.Add(numericUpDown_height);
            groupBox1.Controls.Add(numericUpDown_width);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(label1);
            groupBox1.Location = new Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(176, 99);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Window Size";
            // 
            // button_height_reset
            // 
            button_height_reset.Location = new Point(138, 61);
            button_height_reset.Name = "button_height_reset";
            button_height_reset.Size = new Size(26, 23);
            button_height_reset.TabIndex = 5;
            button_height_reset.Text = "↺";
            button_height_reset.UseVisualStyleBackColor = true;
            button_height_reset.Click += button_height_reset_Click;
            // 
            // button_width_reset
            // 
            button_width_reset.Location = new Point(138, 27);
            button_width_reset.Name = "button_width_reset";
            button_width_reset.Size = new Size(26, 23);
            button_width_reset.TabIndex = 4;
            button_width_reset.Text = "↺";
            button_width_reset.UseVisualStyleBackColor = true;
            button_width_reset.Click += button_width_reset_Click;
            // 
            // numericUpDown_height
            // 
            numericUpDown_height.Location = new Point(54, 61);
            numericUpDown_height.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDown_height.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDown_height.Name = "numericUpDown_height";
            numericUpDown_height.Size = new Size(78, 23);
            numericUpDown_height.TabIndex = 3;
            numericUpDown_height.Value = new decimal(new int[] { 750, 0, 0, 0 });
            // 
            // numericUpDown_width
            // 
            numericUpDown_width.Location = new Point(54, 27);
            numericUpDown_width.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
            numericUpDown_width.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDown_width.Name = "numericUpDown_width";
            numericUpDown_width.Size = new Size(78, 23);
            numericUpDown_width.TabIndex = 2;
            numericUpDown_width.Value = new decimal(new int[] { 250, 0, 0, 0 });
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(6, 63);
            label2.Name = "label2";
            label2.Size = new Size(46, 17);
            label2.TabIndex = 1;
            label2.Text = "Height";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 29);
            label1.Name = "label1";
            label1.Size = new Size(42, 17);
            label1.TabIndex = 0;
            label1.Text = "Width";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(button_osuFolder_reset);
            groupBox2.Controls.Add(button_osuFolder_select);
            groupBox2.Controls.Add(textBox_osuFolder);
            groupBox2.Controls.Add(label3);
            groupBox2.Location = new Point(194, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(531, 99);
            groupBox2.TabIndex = 1;
            groupBox2.TabStop = false;
            groupBox2.Text = "osu! Folder";
            // 
            // button_osuFolder_reset
            // 
            button_osuFolder_reset.Location = new Point(491, 27);
            button_osuFolder_reset.Name = "button_osuFolder_reset";
            button_osuFolder_reset.Size = new Size(25, 23);
            button_osuFolder_reset.TabIndex = 5;
            button_osuFolder_reset.Text = "↺";
            button_osuFolder_reset.UseVisualStyleBackColor = true;
            button_osuFolder_reset.Click += button_osuFolder_reset_Click;
            // 
            // button_osuFolder_select
            // 
            button_osuFolder_select.Location = new Point(439, 26);
            button_osuFolder_select.Name = "button_osuFolder_select";
            button_osuFolder_select.Size = new Size(46, 24);
            button_osuFolder_select.TabIndex = 2;
            button_osuFolder_select.Text = "...";
            button_osuFolder_select.UseVisualStyleBackColor = true;
            button_osuFolder_select.Click += button_osuFolder_select_Click;
            // 
            // textBox_osuFolder
            // 
            textBox_osuFolder.Location = new Point(86, 26);
            textBox_osuFolder.Name = "textBox_osuFolder";
            textBox_osuFolder.Size = new Size(347, 23);
            textBox_osuFolder.TabIndex = 1;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(6, 29);
            label3.Name = "label3";
            label3.Size = new Size(74, 17);
            label3.TabIndex = 0;
            label3.Text = "osu! Folder";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(button_drawingInterval_reset);
            groupBox3.Controls.Add(numericUpDown_idleInterval);
            groupBox3.Controls.Add(button_idleInterval_reset);
            groupBox3.Controls.Add(label5);
            groupBox3.Controls.Add(numericUpDown_drawingInterval);
            groupBox3.Controls.Add(label4);
            groupBox3.Location = new Point(12, 117);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(239, 110);
            groupBox3.TabIndex = 2;
            groupBox3.TabStop = false;
            groupBox3.Text = "Interval (ms)";
            // 
            // button_drawingInterval_reset
            // 
            button_drawingInterval_reset.Location = new Point(202, 67);
            button_drawingInterval_reset.Name = "button_drawingInterval_reset";
            button_drawingInterval_reset.Size = new Size(26, 23);
            button_drawingInterval_reset.TabIndex = 11;
            button_drawingInterval_reset.Text = "↺";
            button_drawingInterval_reset.UseVisualStyleBackColor = true;
            button_drawingInterval_reset.Click += button_drawingInterval_reset_Click;
            // 
            // numericUpDown_idleInterval
            // 
            numericUpDown_idleInterval.Location = new Point(118, 32);
            numericUpDown_idleInterval.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numericUpDown_idleInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDown_idleInterval.Name = "numericUpDown_idleInterval";
            numericUpDown_idleInterval.Size = new Size(78, 23);
            numericUpDown_idleInterval.TabIndex = 8;
            numericUpDown_idleInterval.Value = new decimal(new int[] { 1000, 0, 0, 0 });
            // 
            // button_idleInterval_reset
            // 
            button_idleInterval_reset.Location = new Point(202, 32);
            button_idleInterval_reset.Name = "button_idleInterval_reset";
            button_idleInterval_reset.Size = new Size(26, 23);
            button_idleInterval_reset.TabIndex = 10;
            button_idleInterval_reset.Text = "↺";
            button_idleInterval_reset.UseVisualStyleBackColor = true;
            button_idleInterval_reset.Click += button_idleInterval_reset_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(6, 34);
            label5.Name = "label5";
            label5.Size = new Size(77, 17);
            label5.TabIndex = 6;
            label5.Text = "Idle Interval";
            // 
            // numericUpDown_drawingInterval
            // 
            numericUpDown_drawingInterval.Location = new Point(118, 67);
            numericUpDown_drawingInterval.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numericUpDown_drawingInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDown_drawingInterval.Name = "numericUpDown_drawingInterval";
            numericUpDown_drawingInterval.Size = new Size(78, 23);
            numericUpDown_drawingInterval.TabIndex = 9;
            numericUpDown_drawingInterval.Value = new decimal(new int[] { 20, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 68);
            label4.Name = "label4";
            label4.Size = new Size(103, 17);
            label4.TabIndex = 7;
            label4.Text = "Drawing Interval";
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(checkBox_enableBackup);
            groupBox4.Controls.Add(numericUpDown_backupInterval);
            groupBox4.Controls.Add(button_backupFolder_reset);
            groupBox4.Controls.Add(button1);
            groupBox4.Controls.Add(textBox_backupFolder);
            groupBox4.Controls.Add(label7);
            groupBox4.Controls.Add(label6);
            groupBox4.Controls.Add(button_backupFolder_select);
            groupBox4.Location = new Point(260, 117);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(465, 110);
            groupBox4.TabIndex = 3;
            groupBox4.TabStop = false;
            groupBox4.Text = "Backup";
            // 
            // checkBox_enableBackup
            // 
            checkBox_enableBackup.AutoSize = true;
            checkBox_enableBackup.Location = new Point(6, 22);
            checkBox_enableBackup.Name = "checkBox_enableBackup";
            checkBox_enableBackup.Size = new Size(144, 21);
            checkBox_enableBackup.TabIndex = 14;
            checkBox_enableBackup.Text = "Enable Auto Backup";
            checkBox_enableBackup.UseVisualStyleBackColor = true;
            // 
            // numericUpDown_backupInterval
            // 
            numericUpDown_backupInterval.Location = new Point(133, 80);
            numericUpDown_backupInterval.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numericUpDown_backupInterval.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDown_backupInterval.Name = "numericUpDown_backupInterval";
            numericUpDown_backupInterval.Size = new Size(78, 23);
            numericUpDown_backupInterval.TabIndex = 12;
            numericUpDown_backupInterval.Value = new decimal(new int[] { 60, 0, 0, 0 });
            // 
            // button_backupFolder_reset
            // 
            button_backupFolder_reset.Location = new Point(425, 50);
            button_backupFolder_reset.Name = "button_backupFolder_reset";
            button_backupFolder_reset.Size = new Size(25, 23);
            button_backupFolder_reset.TabIndex = 9;
            button_backupFolder_reset.Text = "↺";
            button_backupFolder_reset.UseVisualStyleBackColor = true;
            button_backupFolder_reset.Click += button_backupFolder_reset_Click;
            // 
            // button1
            // 
            button1.Location = new Point(217, 80);
            button1.Name = "button1";
            button1.Size = new Size(26, 23);
            button1.TabIndex = 13;
            button1.Text = "↺";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // textBox_backupFolder
            // 
            textBox_backupFolder.Location = new Point(109, 49);
            textBox_backupFolder.Name = "textBox_backupFolder";
            textBox_backupFolder.Size = new Size(258, 23);
            textBox_backupFolder.TabIndex = 7;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(11, 82);
            label7.Name = "label7";
            label7.Size = new Size(116, 17);
            label7.TabIndex = 11;
            label7.Text = "Backup Interval (s)";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(11, 52);
            label6.Name = "label6";
            label6.Size = new Size(92, 17);
            label6.TabIndex = 6;
            label6.Text = "Backup Folder";
            // 
            // button_backupFolder_select
            // 
            button_backupFolder_select.Location = new Point(373, 49);
            button_backupFolder_select.Name = "button_backupFolder_select";
            button_backupFolder_select.Size = new Size(46, 24);
            button_backupFolder_select.TabIndex = 8;
            button_backupFolder_select.Text = "...";
            button_backupFolder_select.UseVisualStyleBackColor = true;
            button_backupFolder_select.Click += button_backupFolder_select_Click;
            // 
            // button_apply
            // 
            button_apply.Location = new Point(491, 233);
            button_apply.Name = "button_apply";
            button_apply.Size = new Size(102, 33);
            button_apply.TabIndex = 4;
            button_apply.Text = "Apply";
            button_apply.UseVisualStyleBackColor = true;
            button_apply.Click += button_apply_Click;
            // 
            // button_cancel
            // 
            button_cancel.Location = new Point(608, 233);
            button_cancel.Name = "button_cancel";
            button_cancel.Size = new Size(102, 33);
            button_cancel.TabIndex = 5;
            button_cancel.Text = "Cancel";
            button_cancel.UseVisualStyleBackColor = true;
            button_cancel.Click += button_cancel_Click;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(737, 275);
            Controls.Add(button_cancel);
            Controls.Add(button_apply);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Name = "SettingsForm";
            Text = "Settings";
            Load += SettingsForm_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_height).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_width).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_idleInterval).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_drawingInterval).EndInit();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDown_backupInterval).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private Label label2;
        private Label label1;
        private Button button_width_reset;
        private NumericUpDown numericUpDown_height;
        private NumericUpDown numericUpDown_width;
        private Button button_height_reset;
        private GroupBox groupBox2;
        private Button button_osuFolder_select;
        private TextBox textBox_osuFolder;
        private Label label3;
        private Button button_osuFolder_reset;
        private GroupBox groupBox3;
        private Button button_drawingInterval_reset;
        private NumericUpDown numericUpDown_idleInterval;
        private Button button_idleInterval_reset;
        private Label label5;
        private NumericUpDown numericUpDown_drawingInterval;
        private Label label4;
        private GroupBox groupBox4;
        private Button button_backupFolder_reset;
        private TextBox textBox_backupFolder;
        private Label label6;
        private Button button_backupFolder_select;
        private CheckBox checkBox_enableBackup;
        private NumericUpDown numericUpDown_backupInterval;
        private Button button1;
        private Label label7;
        private Button button_apply;
        private Button button_cancel;
    }
}