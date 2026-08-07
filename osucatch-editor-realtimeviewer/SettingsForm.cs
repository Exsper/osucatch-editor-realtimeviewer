namespace osucatch_editor_realtimeviewer
{
    public partial class SettingsForm : Form
    {
        private ComboBox? comboBox_BarLineSubdivide;
        private CheckBox? checkBox_ShowDistanceHelper;
        private Label? label_WhiteSpeed;
        private NumericUpDown? numericUpDown_WhiteSpeed;
        private Label? label_RedSpeed;
        private NumericUpDown? numericUpDown_RedSpeed;

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

            // 小节线细分选择（程序内创建，避免改动 Designer/资源布局）
            comboBox_BarLineSubdivide = new ComboBox();
            comboBox_BarLineSubdivide.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox_BarLineSubdivide.Location = new Point(127, 76);
            comboBox_BarLineSubdivide.Size = new Size(110, 25);
            System.ComponentModel.ComponentResourceManager rm = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            comboBox_BarLineSubdivide.Items.Add(rm.GetString("BarLineSubdivide_Item_None") ?? "显示到小节");
            comboBox_BarLineSubdivide.Items.Add(rm.GetString("BarLineSubdivide_Item_Half") ?? "显示到2拍");
            comboBox_BarLineSubdivide.Items.Add(rm.GetString("BarLineSubdivide_Item_Quarter") ?? "显示到拍");
            comboBox_BarLineSubdivide.SelectedIndex = Math.Clamp(app.Default.BarLine_Subdivide, 0, 2);
            groupBox7.Controls.Add(comboBox_BarLineSubdivide);

            // 距离辅助线开关
            checkBox_ShowDistanceHelper = new CheckBox();
            checkBox_ShowDistanceHelper.Location = new Point(6, 103);
            checkBox_ShowDistanceHelper.Size = new Size(220, 21);
            checkBox_ShowDistanceHelper.Text = rm.GetString("ShowDistanceHelper_Checkbox") ?? "显示距离辅助线";
            checkBox_ShowDistanceHelper.Checked = app.Default.Show_Distance_Helper;
            groupBox7.Controls.Add(checkBox_ShowDistanceHelper);

            // 白线/红线速度倍率（SameWithEditor 语义：1x = 与编辑器滑条速度一致）
            label_WhiteSpeed = new Label();
            label_WhiteSpeed.Location = new Point(6, 130);
            label_WhiteSpeed.Size = new Size(150, 21);
            label_WhiteSpeed.Text = rm.GetString("DistanceHelper_WhiteSpeed_Label") ?? "白线速度倍率";
            numericUpDown_WhiteSpeed = new NumericUpDown();
            numericUpDown_WhiteSpeed.Location = new Point(160, 128);
            numericUpDown_WhiteSpeed.Size = new Size(60, 23);
            numericUpDown_WhiteSpeed.Minimum = 0.1m;
            numericUpDown_WhiteSpeed.Maximum = 10m;
            numericUpDown_WhiteSpeed.DecimalPlaces = 2;
            numericUpDown_WhiteSpeed.Increment = 0.25m;
            numericUpDown_WhiteSpeed.Value = Math.Clamp((decimal)app.Default.Distance_Helper_White_Speed, numericUpDown_WhiteSpeed.Minimum, numericUpDown_WhiteSpeed.Maximum);
            groupBox7.Controls.Add(label_WhiteSpeed);
            groupBox7.Controls.Add(numericUpDown_WhiteSpeed);

            label_RedSpeed = new Label();
            label_RedSpeed.Location = new Point(6, 157);
            label_RedSpeed.Size = new Size(150, 21);
            label_RedSpeed.Text = rm.GetString("DistanceHelper_RedSpeed_Label") ?? "红线速度倍率";
            numericUpDown_RedSpeed = new NumericUpDown();
            numericUpDown_RedSpeed.Location = new Point(160, 155);
            numericUpDown_RedSpeed.Size = new Size(60, 23);
            numericUpDown_RedSpeed.Minimum = 0.1m;
            numericUpDown_RedSpeed.Maximum = 10m;
            numericUpDown_RedSpeed.DecimalPlaces = 2;
            numericUpDown_RedSpeed.Increment = 0.25m;
            numericUpDown_RedSpeed.Value = Math.Clamp((decimal)app.Default.Distance_Helper_Red_Speed, numericUpDown_RedSpeed.Minimum, numericUpDown_RedSpeed.Maximum);
            groupBox7.Controls.Add(label_RedSpeed);
            groupBox7.Controls.Add(numericUpDown_RedSpeed);

            checkBox_FilterNearbyHitObjects.Checked = app.Default.FilterNearbyHitObjects;
            numericUpDown_timeOut.Value = app.Default.WorkCancelAfter;

            button_Label_Color.BackColor = app.Default.Color_HitObject_Label;

            CurveWidthComboBox.SelectedIndex = app.Default.Curve_Width - 1;
            CurveDashStyleComboBox.SelectedIndex = app.Default.Curve_LineStyle;
            CurveColorButton.BackColor = app.Default.Curve_Color;
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
            app.Default.BarLine_Subdivide = (comboBox_BarLineSubdivide != null) ? comboBox_BarLineSubdivide.SelectedIndex : 0;
            app.Default.Show_Distance_Helper = (checkBox_ShowDistanceHelper != null) && checkBox_ShowDistanceHelper.Checked;
            app.Default.Distance_Helper_White_Speed = (numericUpDown_WhiteSpeed != null) ? (double)numericUpDown_WhiteSpeed.Value : 1.0;
            app.Default.Distance_Helper_Red_Speed = (numericUpDown_RedSpeed != null) ? (double)numericUpDown_RedSpeed.Value : 2.0;

            app.Default.FilterNearbyHitObjects = checkBox_FilterNearbyHitObjects.Checked;
            app.Default.WorkCancelAfter = (int)numericUpDown_timeOut.Value;

            app.Default.Color_HitObject_Label = button_Label_Color.BackColor;

            app.Default.Curve_Width = CurveWidthComboBox.SelectedIndex + 1;
            app.Default.Curve_LineStyle = CurveDashStyleComboBox.SelectedIndex;
            app.Default.Curve_Color = CurveColorButton.BackColor;

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
    }
}
