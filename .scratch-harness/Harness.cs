using System;
using System.Drawing;
using System.Windows.Forms;

namespace osucatch_editor_realtimeviewer
{
    // Diagnostic harness: build the quick toggle bar in a real WinForms environment,
    // replay Form1's creation + startup-restore order, then report layout + overflow state.
    internal static class HarnessProgram
    {
        /// <summary>ToolStrip.OverflowButtonRectangle 是 protected，这里用反射读出来判断角标是否真的存在。</summary>
        private static Rectangle OverflowButtonRect(QuickToggleBar bar)
        {
            var prop = typeof(ToolStrip).GetProperty("OverflowButtonRectangle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return prop?.GetValue(bar) is Rectangle r ? r : Rectangle.Empty;
        }
        private static void BuildAll(QuickToggleBar bar)
        {
            bar.AddStandaloneToggle("FreezePreviewTime", "F");

            QuickToggleBar.QuickToggleGroup mod = bar.AddGroup("Mod", "");
            mod.AddToggle("Mod_None", "NM", true);
            mod.AddToggle("Mod_EZ", "EZ");
            mod.AddToggle("Mod_HR", "HR");

            QuickToggleBar.QuickToggleGroup label = bar.AddGroup("HitObjectLabel", "FruitLabels");
            label.AddToggle("Label_Hidden", "Hide", true);
            label.AddToggle("Label_Distance", "Dist");
            label.AddToggle("Label_IgnoreSVM", "DistSVM");
            label.AddToggle("Label_Stars", "Stars");

            QuickToggleBar.QuickToggleGroup barLine = bar.AddGroup("BarLine", "BarLines");
            barLine.AddSlider("BarLineMode", 0, 10, 1, 1);
            barLine.AddLabel("BarLineMode_Text", "BarLines: every 2 beats");
        }

        private static void Report(QuickToggleBar bar, QuickToggleDockRow row, string stage)
        {
            int placed = 0, overflow = 0, clipped = 0;
            ToolStripItem? slider = null;
            foreach (ToolStripItem item in bar.Items)
            {
                if (item.Placement == ToolStripItemPlacement.Main) placed++;
                else if (item.Placement == ToolStripItemPlacement.Overflow) overflow++;
                if (item.Bounds.Bottom > bar.Height && item.Placement == ToolStripItemPlacement.Main) clipped++;
                if (item is ToolStripControlHost) slider = item;
            }

            Rectangle chevron = OverflowButtonRect(bar);
            bool hasChevron = !chevron.IsEmpty;
            Console.WriteLine($"{stage,-34} bar={bar.Width}x{bar.Height} row.Height={row.Height} items={bar.Items.Count} main={placed} overflow={overflow} clipped={clipped} chevron={(hasChevron ? chevron.ToString() : "none")} slider={(slider == null ? "MISSING" : (slider.Placement == ToolStripItemPlacement.Main ? "on bar" : "in overflow menu"))}");
        }

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form form = new Form { Width = 785, Height = 400, Text = "harness" };
            MenuStrip menu = new MenuStrip { Name = "menuStrip1", Dock = DockStyle.Top };
            QuickToggleDockRow dockRow = new QuickToggleDockRow();
            QuickToggleBar bar = new QuickToggleBar { Name = "quickToggleBar" };

            QuickToggleDocking docking = new QuickToggleDocking(form, bar, dockRow, menu);
            form.Controls.Add(menu);
            form.Controls.Add(dockRow);
            form.Controls.SetChildIndex(dockRow, form.Controls.GetChildIndex(menu));

            form.Show();
            Application.DoEvents();
            Console.WriteLine($"--- empty: row.Height={dockRow.Height} bar={bar.Width}x{bar.Height} ---");

            BuildAll(bar);
            Application.DoEvents();

            bar.ApplyHiddenGroups("");
            bar.ApplyCheckedStates("FreezePreviewTime=True;Mod_None=True;Mod_EZ=False;Mod_HR=False;Label_Hidden=True;Label_Distance=False;Label_IgnoreSVM=False;Label_Stars=False");
            bar.SetGroupSliderValue("BarLine", "BarLineMode", 1);
            bar.SetGroupLabelText("BarLine", "BarLineMode_Text", "BarLines: every 4 beats");
            Application.DoEvents();

            Console.WriteLine("--- startup state (no user interaction) ---");
            Report(bar, dockRow, "startup");

            foreach (int width in new[] { 785, 600, 450, 350, 250, 180 })
            {
                form.Width = width;
                Application.DoEvents();
                Report(bar, dockRow, $"window={width}px");
            }

            // 手动收起/展开拍线功能区后仍然正常
            bar.SetGroupVisible("BarLine", false);
            Application.DoEvents();
            bar.SetGroupVisible("BarLine", true);
            Application.DoEvents();
            Report(bar, dockRow, "after group off/on");

            docking.Dispose();
            form.Close();
        }
    }
}
