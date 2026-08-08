using System.Reflection;

namespace osucatch_editor_realtimeviewer
{
    public partial class BookmarkSettingsForm : Form
    {
        public BookmarkSettingsForm()
        {
            InitializeComponent();
        }

        private void button_saveFolder_select_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            folder.RootFolder = Environment.SpecialFolder.MyComputer;
            folder.Description = "Select Save Folder";
            DialogResult path = folder.ShowDialog();
            if (path == DialogResult.OK)
            {
                textBox_saveFolder.Text = folder.SelectedPath;
            }
        }

        private void button_saveFolder_reset_Click(object sender, EventArgs e)
        {
            textBox_saveFolder.Text = (app.Default.osu_path == "") ? "" : Path.Combine(app.Default.osu_path, "BookmarkPlus");
        }

        private void BookmarkSettingsForm_Load(object sender, EventArgs e)
        {
            textBox_saveFolder.Text = app.Default.Bookmark_FolderPath;
            if (textBox_saveFolder.Text == "" && app.Default.osu_path != "")
            {
                textBox_saveFolder.Text = Path.Combine(app.Default.osu_path, "BookmarkPlus");
            }

            autoLoadSaveCheckBox.Checked = app.Default.Bookmark_AutoLoadSave;
            loadWithStyleCheckBox.Checked = app.Default.Bookmark_AutoLoadWithStyle;
            RegisterHotKeyCheckBox.Checked = app.Default.Bookmark_RegisterHotKey;

            SetBookmarkStyleSettingsFromConfig();
        }

        private void button_apply_Click(object sender, EventArgs e)
        {
            if (autoLoadSaveCheckBox.Checked && textBox_saveFolder.Text == "")
            {
                MessageBox.Show("Please select a folder for Bookmark+ auto load/save.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            app.Default.Bookmark_FolderPath = textBox_saveFolder.Text;
            app.Default.Bookmark_AutoLoadSave = autoLoadSaveCheckBox.Checked;
            app.Default.Bookmark_AutoLoadWithStyle = loadWithStyleCheckBox.Checked;
            app.Default.Bookmark_RegisterHotKey = RegisterHotKeyCheckBox.Checked;

            SaveBookmarkStyleSettings();

            app.Default.Save();

            Form1.NeedReapplyBookmarkStyles = true;

            this.Close();
        }

        private void button_default_Click(object sender, EventArgs e)
        {
            if (app.Default.osu_path != "") textBox_saveFolder.Text = Path.Combine(app.Default.osu_path, "BookmarkPlus");
            else textBox_saveFolder.Text = "";

            autoLoadSaveCheckBox.Checked = false;
            loadWithStyleCheckBox.Checked = false;

            SetBookmarkStyleSettingsFromBookmarkData(BookmarkPlus.defaultBookmarkStyles);
        }

        private void SetBookmarkStyleSettingsFromConfig()
        {
            List<int> Settings_Style = new List<int>{
                app.Default.Bookmark_LineStyle_1,
                app.Default.Bookmark_LineStyle_2,
                app.Default.Bookmark_LineStyle_3,
                app.Default.Bookmark_LineStyle_4,
                app.Default.Bookmark_LineStyle_5,
                app.Default.Bookmark_LineStyle_6,
                app.Default.Bookmark_LineStyle_7,
                app.Default.Bookmark_LineStyle_8,
            };

            List<int> Settings_Width = new List<int>{
                app.Default.Bookmark_Width_1,
                app.Default.Bookmark_Width_2,
                app.Default.Bookmark_Width_3,
                app.Default.Bookmark_Width_4,
                app.Default.Bookmark_Width_5,
                app.Default.Bookmark_Width_6,
                app.Default.Bookmark_Width_7,
                app.Default.Bookmark_Width_8,
            };

            List<Color> Settings_Color = new List<Color>{
                app.Default.Bookmark_Color_1,
                app.Default.Bookmark_Color_2,
                app.Default.Bookmark_Color_3,
                app.Default.Bookmark_Color_4,
                app.Default.Bookmark_Color_5,
                app.Default.Bookmark_Color_6,
                app.Default.Bookmark_Color_7,
                app.Default.Bookmark_Color_8,
            };

            List<string> Settings_Comment = new List<string>{
                app.Default.Bookmark_Comment_1,
                app.Default.Bookmark_Comment_2,
                app.Default.Bookmark_Comment_3,
                app.Default.Bookmark_Comment_4,
                app.Default.Bookmark_Comment_5,
                app.Default.Bookmark_Comment_6,
                app.Default.Bookmark_Comment_7,
                app.Default.Bookmark_Comment_8,
            };

            List<ComboBox> Element_Style = new List<ComboBox>{
                dashStyleComboBox_1,
                dashStyleComboBox_2,
                dashStyleComboBox_3,
                dashStyleComboBox_4,
                dashStyleComboBox_5,
                dashStyleComboBox_6,
                dashStyleComboBox_7,
                dashStyleComboBox_8,
            };

            List<ComboBox> Element_Width = new List<ComboBox>{
                lineWidthComboBox_1,
                lineWidthComboBox_2,
                lineWidthComboBox_3,
                lineWidthComboBox_4,
                lineWidthComboBox_5,
                lineWidthComboBox_6,
                lineWidthComboBox_7,
                lineWidthComboBox_8,
            };

            List<Button> Element_Color = new List<Button>{
                colorbutton_1,
                colorbutton_2,
                colorbutton_3,
                colorbutton_4,
                colorbutton_5,
                colorbutton_6,
                colorbutton_7,
                colorbutton_8,
            };

            List<TextBox> Element_Comment = new List<TextBox>{
                commentTextBox_1,
                commentTextBox_2,
                commentTextBox_3,
                commentTextBox_4,
                commentTextBox_5,
                commentTextBox_6,
                commentTextBox_7,
                commentTextBox_8,
            };

            for (int i = 0; i < 8; i++)
            {
                Element_Style[i].SelectedIndex = Settings_Style[i];
                Element_Width[i].SelectedIndex = Settings_Width[i] - 1;
                Element_Color[i].BackColor = Settings_Color[i];
                Element_Comment[i].Text = Settings_Comment[i];
            }
        }

        private void SetBookmarkStyleSettingsFromBookmarkData(BookmarkData bd)
        {
            List<ComboBox> Element_Style = new List<ComboBox>{
                dashStyleComboBox_1,
                dashStyleComboBox_2,
                dashStyleComboBox_3,
                dashStyleComboBox_4,
                dashStyleComboBox_5,
                dashStyleComboBox_6,
                dashStyleComboBox_7,
                dashStyleComboBox_8,
            };

            List<ComboBox> Element_Width = new List<ComboBox>{
                lineWidthComboBox_1,
                lineWidthComboBox_2,
                lineWidthComboBox_3,
                lineWidthComboBox_4,
                lineWidthComboBox_5,
                lineWidthComboBox_6,
                lineWidthComboBox_7,
                lineWidthComboBox_8,
            };

            List<Button> Element_Color = new List<Button>{
                colorbutton_1,
                colorbutton_2,
                colorbutton_3,
                colorbutton_4,
                colorbutton_5,
                colorbutton_6,
                colorbutton_7,
                colorbutton_8,
            };

            List<TextBox> Element_Comment = new List<TextBox>{
                commentTextBox_1,
                commentTextBox_2,
                commentTextBox_3,
                commentTextBox_4,
                commentTextBox_5,
                commentTextBox_6,
                commentTextBox_7,
                commentTextBox_8,
            };

            for (int i = 0; i < bd.LineStyles.Count; i++)
            {
                Element_Style[i].SelectedIndex = (int)bd.LineStyles[i].Style;
                Element_Width[i].SelectedIndex = bd.LineStyles[i].Width - 1;
                Element_Color[i].BackColor = bd.LineStyles[i].Color;
                Element_Comment[i].Text = bd.LineStyles[i].Comment;
            }
        }

        private void SaveBookmarkStyleSettings()
        {
            List<ComboBox> Element_Style = new List<ComboBox>{
                dashStyleComboBox_1,
                dashStyleComboBox_2,
                dashStyleComboBox_3,
                dashStyleComboBox_4,
                dashStyleComboBox_5,
                dashStyleComboBox_6,
                dashStyleComboBox_7,
                dashStyleComboBox_8,
            };

            List<ComboBox> Element_Width = new List<ComboBox>{
                lineWidthComboBox_1,
                lineWidthComboBox_2,
                lineWidthComboBox_3,
                lineWidthComboBox_4,
                lineWidthComboBox_5,
                lineWidthComboBox_6,
                lineWidthComboBox_7,
                lineWidthComboBox_8,
            };

            List<Button> Element_Color = new List<Button>{
                colorbutton_1,
                colorbutton_2,
                colorbutton_3,
                colorbutton_4,
                colorbutton_5,
                colorbutton_6,
                colorbutton_7,
                colorbutton_8,
            };

            List<TextBox> Element_Comment = new List<TextBox>{
                commentTextBox_1,
                commentTextBox_2,
                commentTextBox_3,
                commentTextBox_4,
                commentTextBox_5,
                commentTextBox_6,
                commentTextBox_7,
                commentTextBox_8,
            };

            for (int i = 0; i < 8; i++)
            {
                string propertyName1 = "Bookmark_LineStyle_" + (i + 1);
                PropertyInfo? prop1 = typeof(app).GetProperty(propertyName1);
                prop1?.SetValue(app.Default, Element_Style[i].SelectedIndex);

                string propertyName2 = "Bookmark_Width_" + (i + 1);
                PropertyInfo? prop2 = typeof(app).GetProperty(propertyName2);
                prop2?.SetValue(app.Default, Element_Width[i].SelectedIndex + 1);

                string propertyName3 = "Bookmark_Color_" + (i + 1);
                PropertyInfo? prop3 = typeof(app).GetProperty(propertyName3);
                prop3?.SetValue(app.Default, Element_Color[i].BackColor);

                string propertyName4 = "Bookmark_Comment_" + (i + 1);
                PropertyInfo? prop4 = typeof(app).GetProperty(propertyName4);
                prop4?.SetValue(app.Default, Element_Comment[i].Text);
            }
        }

        private void button_loadStyle_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Select Bookmark file";
                openFileDialog.Filter = "BookmarkPlus File (*.bps)|*.bps";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    try
                    {
                        BookmarkData bookmarkData = BookmarkPlus.LoadBookmarkData(filePath);
                        SetBookmarkStyleSettingsFromBookmarkData(bookmarkData);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred while reading the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Log.ConsoleLog("Read BookmarkPlus from file failed.\n" + ex, Log.LogType.Bookmark, Log.LogLevel.Error);
                        return;
                    }
                }
            }
        }

        private void button_saveStyle_Click(object sender, EventArgs e)
        {

            BookmarkData currentBookmarkStyles = new BookmarkData
            {
                LineStyles =
                {
                    new LineStyle { Id = 1, Style = (LineType)dashStyleComboBox_1.SelectedIndex, Width = lineWidthComboBox_1.SelectedIndex + 1, Color = colorbutton_1.BackColor, Comment = commentTextBox_1.Text },
                    new LineStyle { Id = 2, Style = (LineType)dashStyleComboBox_2.SelectedIndex, Width = lineWidthComboBox_2.SelectedIndex + 1, Color = colorbutton_2.BackColor, Comment = commentTextBox_2.Text },
                    new LineStyle { Id = 3, Style = (LineType)dashStyleComboBox_3.SelectedIndex, Width = lineWidthComboBox_3.SelectedIndex + 1, Color = colorbutton_3.BackColor, Comment = commentTextBox_3.Text },
                    new LineStyle { Id = 4, Style = (LineType)dashStyleComboBox_4.SelectedIndex, Width = lineWidthComboBox_4.SelectedIndex + 1, Color = colorbutton_4.BackColor, Comment = commentTextBox_4.Text },
                    new LineStyle { Id = 5, Style = (LineType)dashStyleComboBox_5.SelectedIndex, Width = lineWidthComboBox_5.SelectedIndex + 1, Color = colorbutton_5.BackColor, Comment = commentTextBox_5.Text },
                    new LineStyle { Id = 6, Style = (LineType)dashStyleComboBox_6.SelectedIndex, Width = lineWidthComboBox_6.SelectedIndex + 1, Color = colorbutton_6.BackColor, Comment = commentTextBox_6.Text },
                    new LineStyle { Id = 7, Style = (LineType)dashStyleComboBox_7.SelectedIndex, Width = lineWidthComboBox_7.SelectedIndex + 1, Color = colorbutton_7.BackColor, Comment = commentTextBox_7.Text },
                    new LineStyle { Id = 8, Style = (LineType)dashStyleComboBox_8.SelectedIndex, Width = lineWidthComboBox_8.SelectedIndex + 1, Color = colorbutton_8.BackColor, Comment = commentTextBox_8.Text },
                },
                Bookmarks =
                {
                }
            };

            // 创建SaveFileDialog实例
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Title = "Save Bookmark Styles";
            saveFileDialog.Filter = "BookmarkPlus File (*.bps)|*.bps"; // 文件类型过滤器
            saveFileDialog.FileName = "Bookmark Styles"; // 默认文件名
            saveFileDialog.DefaultExt = "bps"; // 默认扩展名

            // 显示对话框并获取结果
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 获取选中的文件路径
                string filePath = saveFileDialog.FileName;
                try
                {
                    // 写入文件
                    BookmarkPlus.SaveBookmarkData(filePath, currentBookmarkStyles);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred while saving the file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log.ConsoleLog("Save BookmarkPlus to file failed.\n" + ex, Log.LogType.Bookmark, Log.LogLevel.Error);
                    return;
                }
            }
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void colorbutton_1_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_1.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_2_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_2.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_3_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_3.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_4_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_4.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_5_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_5.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_6_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_6.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_7_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_7.BackColor = colorDialog.Color;
            }
        }

        private void colorbutton_8_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                colorbutton_8.BackColor = colorDialog.Color;
            }
        }
    }
}
