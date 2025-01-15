﻿namespace osucatch_editor_realtimeviewer
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            numericUpDown_width.Value = app.Default.Window_Width;
            numericUpDown_height.Value = app.Default.Window_Height;
            numericUpDown_idleInterval.Value = app.Default.Idle_Interval;
            numericUpDown_drawingInterval.Value = app.Default.Drawing_Interval;
            numericUpDown_backupInterval.Value = app.Default.Backup_Interval / 1000;
            textBox_osuFolder.Text = app.Default.osu_path;
            textBox_backupFolder.Text = app.Default.Backup_Folder;
            if (textBox_backupFolder.Text == "" && textBox_osuFolder.Text != "")
            {
                textBox_backupFolder.Text = Path.Combine(textBox_osuFolder.Text, "EditorBackups");
            }
            checkBox_enableBackup.Checked = app.Default.Backup_Enabled;
            checkBox_withColor.Checked = app.Default.Combo_Colour;
            checkBox_ShowSelected.Checked = app.Default.Selected_Show;

            checkBox_showConsole.Checked = app.Default.Show_Console;
            checkBox_Log_Program.Checked = app.Default.Log_Program;
            checkBox_Log_EditorReader.Checked = app.Default.Log_EditorReader;
            checkBox_Log_BeatmapBuilder.Checked = app.Default.Log_BeatmapBuilder;
            checkBox_Log_BeatmapConverter.Checked = app.Default.Log_BeatmapConverter;
            checkBox_Log_Drawing.Checked = app.Default.Log_Drawing;
            checkBox_Log_Backup.Checked = app.Default.Log_Backup;
            checkBox_Log_Timer.Checked = app.Default.Log_Timer;
            comboBox_Log_Level.SelectedIndex = app.Default.Log_Level;
            comboBox_Log_Level.DropDownStyle = ComboBoxStyle.DropDownList;

            checkBox_TimingLine_ShowRed.Checked = app.Default.TimingLine_ShowRed;
            checkBox_TimingLine_ShowGreen.Checked = app.Default.TimingLine_ShowGreen;
            checkBox_BarLine_Show.Checked = app.Default.BarLine_Show;
        }

        private void button_width_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_width.Value = 250;
        }

        private void button_height_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_height.Value = 750;
        }

        private void button_idleInterval_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_idleInterval.Value = 1000;
        }

        private void button_drawingInterval_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_drawingInterval.Value = 20;
        }

        private void button_osuFolder_reset_Click(object sender, EventArgs e)
        {
            textBox_osuFolder.Text = Form1.GetOsuPath();
        }

        private void button_backupFolder_reset_Click(object sender, EventArgs e)
        {
            textBox_backupFolder.Text = (textBox_osuFolder.Text == "") ? "" : Path.Combine(textBox_osuFolder.Text, "EditorBackups");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            numericUpDown_backupInterval.Value = 60;
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button_apply_Click(object sender, EventArgs e)
        {
            if (!File.Exists(Path.Combine(textBox_osuFolder.Text, "osu!.exe")))
            {
                MessageBox.Show("No osu!.exe in the osu folder!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            app.Default.Window_Width = (int)numericUpDown_width.Value;
            app.Default.Window_Height = (int)numericUpDown_height.Value;
            app.Default.Idle_Interval = (int)numericUpDown_idleInterval.Value;
            app.Default.Drawing_Interval = (int)numericUpDown_drawingInterval.Value;
            app.Default.Backup_Interval = (int)numericUpDown_backupInterval.Value * 1000;
            app.Default.osu_path = textBox_osuFolder.Text;
            app.Default.Backup_Folder = textBox_backupFolder.Text;
            app.Default.Backup_Enabled = checkBox_enableBackup.Checked;
            app.Default.Combo_Colour = checkBox_withColor.Checked;
            app.Default.Selected_Show = checkBox_ShowSelected.Checked;

            app.Default.Show_Console = checkBox_showConsole.Checked;
            app.Default.Log_Program = checkBox_Log_Program.Checked;
            app.Default.Log_EditorReader = checkBox_Log_EditorReader.Checked;
            app.Default.Log_BeatmapBuilder = checkBox_Log_BeatmapBuilder.Checked;
            app.Default.Log_BeatmapConverter = checkBox_Log_BeatmapConverter.Checked;
            app.Default.Log_Drawing = checkBox_Log_Drawing.Checked;
            app.Default.Log_Backup = checkBox_Log_Backup.Checked;
            app.Default.Log_Timer = checkBox_Log_Timer.Checked;
            app.Default.Log_Level = comboBox_Log_Level.SelectedIndex;

            app.Default.TimingLine_ShowRed = checkBox_TimingLine_ShowRed.Checked;
            app.Default.TimingLine_ShowGreen = checkBox_TimingLine_ShowGreen.Checked;
            app.Default.BarLine_Show = checkBox_BarLine_Show.Checked;

            app.Default.Save();

            Form1.NeedReapplySettings = true;
            this.Close();
        }

        private void button_osuFolder_select_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            folder.RootFolder = Environment.SpecialFolder.MyComputer;
            folder.Description = "Select osu! Folder";
            DialogResult path = folder.ShowDialog();
            if (path == DialogResult.OK)
            {
                //check if osu!.exe is present
                if (!File.Exists(Path.Combine(folder.SelectedPath, "osu!.exe")))
                {
                    MessageBox.Show("No osu!.exe in this folder!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                else textBox_osuFolder.Text = folder.SelectedPath;
            }
        }

        private void button_backupFolder_select_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            folder.RootFolder = Environment.SpecialFolder.MyComputer;
            folder.Description = "Select .osu Files Backup Folder";
            DialogResult path = folder.ShowDialog();
            if (path == DialogResult.OK)
            {
                textBox_backupFolder.Text = folder.SelectedPath;
            }
        }
    }
}
