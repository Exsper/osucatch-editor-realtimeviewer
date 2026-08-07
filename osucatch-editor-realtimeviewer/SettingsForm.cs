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
        private GroupBox? groupBox_Template;
        private Label? label_TemplateColor;
        private Button? button_TemplateColor;
        private Label? label_TemplateAlpha;
        private NumericUpDown? numericUpDown_TemplateAlpha;
        private Label? label_FullReadInterval;
        private NumericUpDown? numericUpDown_FullReadInterval;
        private Label? label_LowFreqReadInterval;
        private NumericUpDown? numericUpDown_LowFreqReadInterval;

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

            // 全量/低频读取间隔（程序内创建；groupBox3 增高并下移 groupBox6 腾出空间）
            groupBox3.Height = 176;
            groupBox6.Top += 66;

            label_FullReadInterval = new Label();
            label_FullReadInterval.Location = new Point(6, 95);
            label_FullReadInterval.Size = new Size(103, 17);
            label_FullReadInterval.Text = rm.GetString("ReadInterval_Full_Label") ?? "Full read";
            numericUpDown_FullReadInterval = new NumericUpDown();
            numericUpDown_FullReadInterval.Location = new Point(118, 93);
            numericUpDown_FullReadInterval.Size = new Size(78, 23);
            numericUpDown_FullReadInterval.Minimum = 5m;
            numericUpDown_FullReadInterval.Maximum = 10000m;
            numericUpDown_FullReadInterval.Increment = 5m;
            numericUpDown_FullReadInterval.Value = Math.Clamp(app.Default.FullRead_Interval, 5, 10000);
            groupBox3.Controls.Add(label_FullReadInterval);
            groupBox3.Controls.Add(numericUpDown_FullReadInterval);

            label_LowFreqReadInterval = new Label();
            label_LowFreqReadInterval.Location = new Point(6, 130);
            label_LowFreqReadInterval.Size = new Size(103, 17);
            label_LowFreqReadInterval.Text = rm.GetString("ReadInterval_LowFreq_Label") ?? "Low-freq read";
            numericUpDown_LowFreqReadInterval = new NumericUpDown();
            numericUpDown_LowFreqReadInterval.Location = new Point(118, 128);
            numericUpDown_LowFreqReadInterval.Size = new Size(78, 23);
            numericUpDown_LowFreqReadInterval.Minimum = 5m;
            numericUpDown_LowFreqReadInterval.Maximum = 10000m;
            numericUpDown_LowFreqReadInterval.Increment = 5m;
            numericUpDown_LowFreqReadInterval.Value = Math.Clamp(app.Default.LowFreqRead_Interval, 5, 10000);
            groupBox3.Controls.Add(label_LowFreqReadInterval);
            groupBox3.Controls.Add(numericUpDown_LowFreqReadInterval);

            // 模板谱面虚线圆轮廓的颜色/透明度（程序内创建，避免改动 Designer/资源布局）
            groupBox_Template = new GroupBox();
            groupBox_Template.Text = rm.GetString("Template_GroupBox") ?? "Template";
            groupBox_Template.Location = new Point(516, 408);
            groupBox_Template.Size = new Size(209, 92);
            groupBox_Template.TabStop = false;

            label_TemplateColor = new Label();
            label_TemplateColor.Location = new Point(6, 24);
            label_TemplateColor.Size = new Size(100, 20);
            label_TemplateColor.Text = rm.GetString("Template_Color_Label") ?? "Outline color";
            button_TemplateColor = new Button();
            button_TemplateColor.Location = new Point(110, 20);
            button_TemplateColor.Size = new Size(80, 25);
            button_TemplateColor.BackColor = app.Default.Template_Color;
            button_TemplateColor.Click += button_TemplateColor_Click;

            label_TemplateAlpha = new Label();
            label_TemplateAlpha.Location = new Point(6, 56);
            label_TemplateAlpha.Size = new Size(100, 20);
            label_TemplateAlpha.Text = rm.GetString("Template_Alpha_Label") ?? "Opacity (%)";
            numericUpDown_TemplateAlpha = new NumericUpDown();
            numericUpDown_TemplateAlpha.Location = new Point(110, 54);
            numericUpDown_TemplateAlpha.Size = new Size(60, 23);
            numericUpDown_TemplateAlpha.Minimum = 0m;
            numericUpDown_TemplateAlpha.Maximum = 100m;
            numericUpDown_TemplateAlpha.Increment = 5m;
            numericUpDown_TemplateAlpha.Value = Math.Clamp(app.Default.Template_Alpha, 0, 100);

            groupBox_Template.Controls.Add(label_TemplateColor);
            groupBox_Template.Controls.Add(button_TemplateColor);
            groupBox_Template.Controls.Add(label_TemplateAlpha);
            groupBox_Template.Controls.Add(numericUpDown_TemplateAlpha);
            this.Controls.Add(groupBox_Template);
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

            if (button_TemplateColor != null) app.Default.Template_Color = button_TemplateColor.BackColor;
            app.Default.Template_Alpha = (numericUpDown_TemplateAlpha != null) ? (int)numericUpDown_TemplateAlpha.Value : 35;

            app.Default.FullRead_Interval = (numericUpDown_FullReadInterval != null) ? (int)numericUpDown_FullReadInterval.Value : 20;
            app.Default.LowFreqRead_Interval = (numericUpDown_LowFreqReadInterval != null) ? (int)numericUpDown_LowFreqReadInterval.Value : 100;

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
    }
}
