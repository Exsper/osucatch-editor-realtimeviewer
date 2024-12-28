using Editor_Reader;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;

namespace osucatch_editor_realtimeviewer
{
    public partial class Form1 : Form
    {
        EditorReader reader = new EditorReader();
        bool Is_Osu_Running = false;
        bool Is_Editor_Running = false;
        bool Is_Editor_CTB = false;
        string osu_path = "";
        string beatmap_path = "";
        string newBeatmap = "";
        readonly string tmpFilePath = Environment.CurrentDirectory;

        public Form1()
        {
            InitializeComponent();
        }

        private string Select_Osu_Path()
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.ShowNewFolderButton = false;
            folder.RootFolder = Environment.SpecialFolder.MyComputer;
            folder.Description = "��ѡ��osu!.exe�����ļ���";
            DialogResult path = folder.ShowDialog();
            if (path == DialogResult.OK)
            {
                //check if osu!.exe is present
                if (!File.Exists(Path.Combine(folder.SelectedPath, "osu!.exe")))
                {
                    MessageBox.Show("ѡ����ļ��в�����osu!.exe", "����", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return Select_Osu_Path();
                }
            }
            return folder.SelectedPath;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            osu_path = GetOsuPath();
            if (osu_path == "") osu_path = Select_Osu_Path();

            reader_timer.Interval = 1000;
            reader_timer.Start();
            /*
            reader.FetchAll();
            Console.WriteLine(reader.ContainingFolder);
            Console.WriteLine(reader.Filename);
            Console.WriteLine("oR" + reader.objectRadius);
            Console.WriteLine("sO" + reader.stackOffset);
            Console.WriteLine("HP" + reader.HPDrainRate);
            Console.WriteLine("CS" + reader.CircleSize);
            Console.WriteLine("AR" + reader.ApproachRate);
            Console.WriteLine("OD" + reader.OverallDifficulty);
            Console.WriteLine("SV" + reader.SliderMultiplier);
            Console.WriteLine("TR" + reader.SliderTickRate);
            Console.WriteLine("CT" + reader.ComposeTool());
            Console.WriteLine("GS" + reader.GridSize());
            Console.WriteLine("BD" + reader.BeatDivisor());
            Console.WriteLine("TZ" + reader.TimelineZoom);
            Console.WriteLine("DS" + reader.DistanceSpacing());

            Console.WriteLine("Current Time:");
            Console.WriteLine(reader.EditorTime());

            Console.WriteLine("Timing Points:");
            for (int i = 0; i < reader.numControlPoints; i++)
                Console.WriteLine(reader.controlPoints[i].ToString());

            Console.WriteLine("Bookmarks:");
            for (int i = 0; i < reader.numBookmarks; i++)
                Console.WriteLine(reader.bookmarks[i]);

            Console.WriteLine("Hit Objects (selected):");
            for (int i = 0; i < reader.numObjects; i++)
                if (reader.hitObjects[i].IsSelected)
                    Console.WriteLine(reader.hitObjects[i].ToString());

            while (true)
            {
                Console.WriteLine(reader.SnapPosition());

                reader.FetchSelected();
                Console.WriteLine("Selected Hit Objects:");
                for (int i = 0; i < reader.numSelected; i++)
                    Console.WriteLine(reader.selectedObjects[i].ToString());

                reader.FetchClipboard();
                Console.WriteLine("Copied Hit Objects:");
                for (int i = 0; i < reader.numClipboard; i++)
                    Console.WriteLine(reader.clipboardObjects[i].ToString());

                Console.WriteLine("Hovered Hit Object:");
                if (reader.FetchHovered())
                    Console.WriteLine(reader.hoveredObject.ToString());

                Console.ReadLine();
            }
            */
        }

        private void reader_timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!Is_Osu_Running || reader.ProcessNeedsReload()) {
                    try
                    {
                        reader.SetProcess();
                        Is_Osu_Running = true;
                    }
                    catch {
                        this.Text = "Osu!.exeδ����";
                        Is_Osu_Running = false;
                        Is_Editor_Running = false;
                        beatmap_path = "";
                        return;
                    }
                }
                string title = reader.ProcessTitle();
                if (title == "")
                {
                    this.Text = "Osu!.exeδ����";
                    Is_Osu_Running = false;
                    Is_Editor_Running = false;
                    beatmap_path = "";
                    return;
                }
                if (!title.EndsWith(".osu") || reader.EditorNeedsReload())
                {
                    try
                    {
                        reader.SetEditor();
                        Is_Osu_Running = true;
                        Is_Editor_Running = true;
                    }
                    catch
                    {
                        this.Text = "Editorδ����";
                        Is_Editor_Running = false;
                        beatmap_path = "";
                        return;
                    }
                }
                else
                {
                    this.Text = title;
                    Is_Editor_Running = true;
                }
                reader.FetchAll();

                string newpath = Path.Combine(osu_path, "Songs", reader.ContainingFolder, reader.Filename);
                
                // ���ļ�
                if (beatmap_path != newpath)
                {
                    beatmap_path = newpath;
                    newBeatmap = BuildNewBeatmap(beatmap_path);
                    File.WriteAllText(Path.Combine(tmpFilePath, "temp.osu"), newBeatmap);
                }
                else
                {
                    // ������һtick�������ļ���������
                    string _newBeatmap = BuildNewBeatmap(beatmap_path);
                    if(!String.Equals(_newBeatmap.Length,newBeatmap.Length))
                    {
                        newBeatmap = _newBeatmap;
                        File.WriteAllText(Path.Combine(tmpFilePath, "temp.osu"), newBeatmap);
                    }
                }

                this.Text = reader.hitObjects.Count.ToString();
            }
            catch (Exception ex)
            {
                this.Text = ex.ToString();
                Is_Osu_Running = false;
                Is_Editor_Running = false;
            }
        }

        private static string GetOsuPath()
        {
            using (RegistryKey osureg = Registry.ClassesRoot.OpenSubKey("osu\\DefaultIcon"))
            {
                if (osureg != null)
                {
                    string osukey = osureg.GetValue(null).ToString();
                    string osupath = osukey.Remove(0, 1);
                    osupath = osupath.Remove(osupath.Length - 11);
                    return osupath;
                }
                else return "";
            }
        }

        private string BuildNewBeatmap(string orgpath)
        {
            string newfile = "";
            using (StreamReader file = File.OpenText(orgpath))
            {
                string line;
                bool isMultiLine = false;
                while ((line = file.ReadLine()) != null)
                {
                    if (isMultiLine) {
                        if (Regex.IsMatch(line, @"^\[")) {
                            isMultiLine = false;
                        }
                        else continue;
                    }

                    // ֻ�滻��Ҫ�Ķ���
                    if (Regex.IsMatch(line, "^PreviewTime:")) newfile += "PreviewTime: " + reader.PreviewTime + "\r\n";
                    else if(Regex.IsMatch(line, "^StackLeniency:")) newfile += "StackLeniency: " + reader.StackLeniency + "\r\n";

                    // ǿ��CTBģʽ
                    // if (Regex.IsMatch(line, "^Mode:")) newfile += "Mode: 2" + "\r\n";

                    else if (Regex.IsMatch(line, "^HPDrainRate:")) newfile += "HPDrainRate: " + reader.HPDrainRate + "\r\n";
                    else if (Regex.IsMatch(line, "^CircleSize:")) newfile += "CircleSize: " + reader.CircleSize + "\r\n";
                    else if (Regex.IsMatch(line, "^OverallDifficulty:")) newfile += "OverallDifficulty: " + reader.OverallDifficulty + "\r\n";
                    else if (Regex.IsMatch(line, "^ApproachRate:")) newfile += "ApproachRate: " + reader.ApproachRate + "\r\n";

                    else if (Regex.IsMatch(line, "^SliderMultiplier:")) newfile += "SliderMultiplier: " + reader.SliderMultiplier + "\r\n";
                    else if (Regex.IsMatch(line, "^SliderTickRate:")) newfile += "SliderTickRate: " + reader.SliderTickRate + "\r\n";

                    else if (Regex.IsMatch(line, @"^\[TimingPoints\]"))
                    {
                        newfile += "[TimingPoints]" + "\r\n";
                        newfile += String.Join("\r\n", reader.controlPoints) + "\r\n";
                        newfile += "\r\n";
                        isMultiLine = true;
                    }
                    else if (Regex.IsMatch(line, @"^\[HitObjects\]"))
                    {
                        newfile += "[HitObjects]" + "\r\n";
                        newfile += String.Join("\r\n", reader.hitObjects) + "\r\n";
                        newfile += "\r\n";
                        isMultiLine = true;
                    }
                    else newfile += line + "\r\n";
                }
                return newfile;
            }
        }
    }
}
