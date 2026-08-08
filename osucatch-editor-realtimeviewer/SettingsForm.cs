namespace osucatch_editor_realtimeviewer
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
            checkBox_Log_BookmarkPlus.Checked = app.Default.Log_Bookmark;
            comboBox_Log_Level.SelectedIndex = app.Default.Log_Level;
            comboBox_Log_Level.DropDownStyle = ComboBoxStyle.DropDownList;

            checkBox_TimingLine_ShowRed.Checked = app.Default.TimingLine_ShowRed;
            checkBox_TimingLine_ShowGreen.Checked = app.Default.TimingLine_ShowGreen;
            checkBox_BarLine_Show.Checked = app.Default.BarLine_Show;

            // 小节线细分选择（下拉项由 Designer 从 resx 填充）
            comboBox_BarLineSubdivide.SelectedIndex = Math.Clamp(app.Default.BarLine_Subdivide, 0, 2);

            // 距离辅助线开关
            checkBox_ShowDistanceHelper.Checked = app.Default.Show_Distance_Helper;

            // 白线/红线速度倍率（SameWithEditor 语义：1x = 与编辑器滑条速度一致）
            numericUpDown_WhiteSpeed.Value = Math.Clamp((decimal)app.Default.Distance_Helper_White_Speed, numericUpDown_WhiteSpeed.Minimum, numericUpDown_WhiteSpeed.Maximum);

            numericUpDown_RedSpeed.Value = Math.Clamp((decimal)app.Default.Distance_Helper_Red_Speed, numericUpDown_RedSpeed.Minimum, numericUpDown_RedSpeed.Maximum);

            checkBox_FilterNearbyHitObjects.Checked = app.Default.FilterNearbyHitObjects;
            numericUpDown_timeOut.Value = app.Default.WorkCancelAfter;

            button_Label_Color.BackColor = app.Default.Color_HitObject_Label;

            CurveWidthComboBox.SelectedIndex = app.Default.Curve_Width - 1;
            CurveDashStyleComboBox.SelectedIndex = app.Default.Curve_LineStyle;
            CurveColorButton.BackColor = app.Default.Curve_Color;

            // 全量/低频读取间隔（标签文本由 resx 提供）
            numericUpDown_FullReadInterval.Value = Math.Clamp(app.Default.FullRead_Interval, 5, 10000);

            numericUpDown_LowFreqReadInterval.Value = Math.Clamp(app.Default.LowFreqRead_Interval, 5, 10000);

            // 模板谱面虚线圆轮廓的颜色/透明度（标签文本由 resx 提供）
            button_TemplateColor.BackColor = app.Default.Template_Color;
            numericUpDown_TemplateAlpha.Value = Math.Clamp(app.Default.Template_Alpha, 0, 100);
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

        private void button_backupInterval_reset_Click(object sender, EventArgs e)
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
            app.Default.Log_Bookmark = checkBox_Log_BookmarkPlus.Checked;
            app.Default.Log_Level = comboBox_Log_Level.SelectedIndex;

            app.Default.TimingLine_ShowRed = checkBox_TimingLine_ShowRed.Checked;
            app.Default.TimingLine_ShowGreen = checkBox_TimingLine_ShowGreen.Checked;
            app.Default.BarLine_Show = checkBox_BarLine_Show.Checked;
            app.Default.BarLine_Subdivide = comboBox_BarLineSubdivide.SelectedIndex;
            app.Default.Show_Distance_Helper = checkBox_ShowDistanceHelper.Checked;
            app.Default.Distance_Helper_White_Speed = (double)numericUpDown_WhiteSpeed.Value;
            app.Default.Distance_Helper_Red_Speed = (double)numericUpDown_RedSpeed.Value;

            app.Default.FilterNearbyHitObjects = checkBox_FilterNearbyHitObjects.Checked;
            app.Default.WorkCancelAfter = (int)numericUpDown_timeOut.Value;

            app.Default.Color_HitObject_Label = button_Label_Color.BackColor;

            app.Default.Curve_Width = CurveWidthComboBox.SelectedIndex + 1;
            app.Default.Curve_LineStyle = CurveDashStyleComboBox.SelectedIndex;
            app.Default.Curve_Color = CurveColorButton.BackColor;

            app.Default.Template_Color = button_TemplateColor.BackColor;
            app.Default.Template_Alpha = (int)numericUpDown_TemplateAlpha.Value;

            app.Default.FullRead_Interval = (int)numericUpDown_FullReadInterval.Value;
            app.Default.LowFreqRead_Interval = (int)numericUpDown_LowFreqReadInterval.Value;

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

        private void checkBox_FilterNearbyHitObjects_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox_FilterNearbyHitObjects.Checked)
            {
                checkBox_enableBackup.Enabled = false;
                textBox_backupFolder.Enabled = false;
                button_backupFolder_select.Enabled = false;
                numericUpDown_backupInterval.Enabled = false;
                label6.Enabled = false;
                label7.Enabled = false;
            }
            else
            {
                checkBox_enableBackup.Enabled = true;
                textBox_backupFolder.Enabled = true;
                button_backupFolder_select.Enabled = true;
                numericUpDown_backupInterval.Enabled = true;
                label6.Enabled = true;
                label7.Enabled = true;
            }
        }

        private void button_timeOut_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_timeOut.Value = 3000;
        }

        private void button_Label_Color_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                button_Label_Color.BackColor = colorDialog.Color;
            }
        }

        private void CurveColorButton_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                CurveColorButton.BackColor = colorDialog.Color;
            }
        }

        private void button_TemplateColor_Click(object? sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                button_TemplateColor!.BackColor = colorDialog.Color;
            }
        }

        private void button_LowFreqReadInterval_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_LowFreqReadInterval.Value = 100;
        }

        private void button_FullReadInterval_reset_Click(object sender, EventArgs e)
        {
            numericUpDown_FullReadInterval.Value = 20;
        }
    }
}
