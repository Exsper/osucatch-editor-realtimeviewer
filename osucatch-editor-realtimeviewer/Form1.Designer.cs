namespace osucatch_editor_realtimeviewer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            Canvas = new Canvas();
            menuStrip1 = new MenuStrip();
            viewerToolStripMenuItem = new ToolStripMenuItem();
            converterToolStripMenuItem = new ToolStripMenuItem();
            lazerConverterToolStripMenuItem = new ToolStripMenuItem();
            stableConverterToolStripMenuItem = new ToolStripMenuItem();
            modToolStripMenuItem = new ToolStripMenuItem();
            hRToolStripMenuItem = new ToolStripMenuItem();
            eZToolStripMenuItem = new ToolStripMenuItem();
            noneToolStripMenuItem = new ToolStripMenuItem();
            cubicFittingCurveToolStripMenuItem = new ToolStripMenuItem();
            hitobjectLabelToolStripMenuItem = new ToolStripMenuItem();
            hideToolStripMenuItem = new ToolStripMenuItem();
            sameWithEditorToolStripMenuItem = new ToolStripMenuItem();
            noSliderVelocityMultiplierToolStripMenuItem = new ToolStripMenuItem();
            compareWithWalkSpeedToolStripMenuItem = new ToolStripMenuItem();
            difficultyStarsToolStripMenuItem = new ToolStripMenuItem();
            fruitCountInComboToolStripMenuItem = new ToolStripMenuItem();
            screenToolStripMenuItem = new ToolStripMenuItem();
            Screens1ToolStripMenuItem = new ToolStripMenuItem();
            Screens2ToolStripMenuItem = new ToolStripMenuItem();
            Screens3ToolStripMenuItem = new ToolStripMenuItem();
            Screens4ToolStripMenuItem = new ToolStripMenuItem();
            Screens5ToolStripMenuItem = new ToolStripMenuItem();
            Screens6ToolStripMenuItem = new ToolStripMenuItem();
            Screens7ToolStripMenuItem = new ToolStripMenuItem();
            Screens8ToolStripMenuItem = new ToolStripMenuItem();
            bookmarkToolStripMenuItem = new ToolStripMenuItem();
            bookmarkSettingsToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator7 = new ToolStripSeparator();
            loadOnlyBookmarkToolStripMenuItem = new ToolStripMenuItem();
            loadFullBookmarkToolStripMenuItem = new ToolStripMenuItem();
            saveBookmarkToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator6 = new ToolStripSeparator();
            ClearBookmarkToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator8 = new ToolStripSeparator();
            bookmarkSetStripMenuItem_1 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_2 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_3 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_4 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_5 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_6 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_7 = new ToolStripMenuItem();
            bookmarkSetStripMenuItem_8 = new ToolStripMenuItem();
            generateConversionMappingToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            settingsToolStripMenuItem = new ToolStripMenuItem();
            openSettingsFileToolStripMenuItem = new ToolStripMenuItem();
            languageToolStripMenuItem = new ToolStripMenuItem();
            defaultLanguageToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            englishLanguageToolStripMenuItem = new ToolStripMenuItem();
            zhHansLanguageToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            TopWhenEditorFocusToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            backupToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            forceResetStripMenuItem = new ToolStripMenuItem();
            restartProgramStripMenuItem = new ToolStripMenuItem();
            githubToolStripMenuItem = new ToolStripMenuItem();
            githubToolStripMenuItem1 = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            statusStrip1 = new StatusStrip();
            StateToolStripStatusLabel = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // Canvas
            // 
            resources.ApplyResources(Canvas, "Canvas");
            Canvas.BackColor = Color.Black;
            Canvas.Name = "Canvas";
            Canvas.VSync = false;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(32, 32);
            menuStrip1.Items.AddRange(new ToolStripItem[] { viewerToolStripMenuItem, settingsToolStripMenuItem, githubToolStripMenuItem, toolStripMenuItem1 });
            resources.ApplyResources(menuStrip1, "menuStrip1");
            menuStrip1.Name = "menuStrip1";
            // 
            // viewerToolStripMenuItem
            // 
            viewerToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { converterToolStripMenuItem, modToolStripMenuItem, cubicFittingCurveToolStripMenuItem, hitobjectLabelToolStripMenuItem, screenToolStripMenuItem, bookmarkToolStripMenuItem, generateConversionMappingToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
            viewerToolStripMenuItem.Name = "viewerToolStripMenuItem";
            resources.ApplyResources(viewerToolStripMenuItem, "viewerToolStripMenuItem");
            // 
            // converterToolStripMenuItem
            // 
            converterToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { lazerConverterToolStripMenuItem, stableConverterToolStripMenuItem });
            converterToolStripMenuItem.Name = "converterToolStripMenuItem";
            resources.ApplyResources(converterToolStripMenuItem, "converterToolStripMenuItem");
            // 
            // lazerConverterToolStripMenuItem
            // 
            lazerConverterToolStripMenuItem.Name = "lazerConverterToolStripMenuItem";
            resources.ApplyResources(lazerConverterToolStripMenuItem, "lazerConverterToolStripMenuItem");
            lazerConverterToolStripMenuItem.Click += lazerConverterToolStripMenuItem_Click;
            // 
            // stableConverterToolStripMenuItem
            // 
            stableConverterToolStripMenuItem.Name = "stableConverterToolStripMenuItem";
            resources.ApplyResources(stableConverterToolStripMenuItem, "stableConverterToolStripMenuItem");
            stableConverterToolStripMenuItem.Click += stableConverterToolStripMenuItem_Click;
            // 
            // modToolStripMenuItem
            // 
            modToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { hRToolStripMenuItem, eZToolStripMenuItem, noneToolStripMenuItem });
            modToolStripMenuItem.Name = "modToolStripMenuItem";
            resources.ApplyResources(modToolStripMenuItem, "modToolStripMenuItem");
            // 
            // hRToolStripMenuItem
            // 
            hRToolStripMenuItem.Name = "hRToolStripMenuItem";
            resources.ApplyResources(hRToolStripMenuItem, "hRToolStripMenuItem");
            hRToolStripMenuItem.Click += hRToolStripMenuItem_Click;
            // 
            // eZToolStripMenuItem
            // 
            eZToolStripMenuItem.Name = "eZToolStripMenuItem";
            resources.ApplyResources(eZToolStripMenuItem, "eZToolStripMenuItem");
            eZToolStripMenuItem.Click += eZToolStripMenuItem_Click;
            // 
            // noneToolStripMenuItem
            // 
            noneToolStripMenuItem.Checked = true;
            noneToolStripMenuItem.CheckState = CheckState.Checked;
            noneToolStripMenuItem.Name = "noneToolStripMenuItem";
            resources.ApplyResources(noneToolStripMenuItem, "noneToolStripMenuItem");
            noneToolStripMenuItem.Click += noneToolStripMenuItem_Click;
            // 
            // cubicFittingCurveToolStripMenuItem
            // 
            cubicFittingCurveToolStripMenuItem.Name = "cubicFittingCurveToolStripMenuItem";
            resources.ApplyResources(cubicFittingCurveToolStripMenuItem, "cubicFittingCurveToolStripMenuItem");
            cubicFittingCurveToolStripMenuItem.Click += cubicFittingCurveToolStripMenuItem_Click;
            // 
            // hitobjectLabelToolStripMenuItem
            // 
            hitobjectLabelToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { hideToolStripMenuItem, sameWithEditorToolStripMenuItem, noSliderVelocityMultiplierToolStripMenuItem, compareWithWalkSpeedToolStripMenuItem, difficultyStarsToolStripMenuItem, fruitCountInComboToolStripMenuItem });
            hitobjectLabelToolStripMenuItem.Name = "hitobjectLabelToolStripMenuItem";
            resources.ApplyResources(hitobjectLabelToolStripMenuItem, "hitobjectLabelToolStripMenuItem");
            // 
            // hideToolStripMenuItem
            // 
            hideToolStripMenuItem.Checked = true;
            hideToolStripMenuItem.CheckState = CheckState.Checked;
            hideToolStripMenuItem.Name = "hideToolStripMenuItem";
            resources.ApplyResources(hideToolStripMenuItem, "hideToolStripMenuItem");
            hideToolStripMenuItem.Click += hideToolStripMenuItem_Click;
            // 
            // sameWithEditorToolStripMenuItem
            // 
            sameWithEditorToolStripMenuItem.Name = "sameWithEditorToolStripMenuItem";
            resources.ApplyResources(sameWithEditorToolStripMenuItem, "sameWithEditorToolStripMenuItem");
            sameWithEditorToolStripMenuItem.Click += sameWithEditorToolStripMenuItem_Click;
            // 
            // noSliderVelocityMultiplierToolStripMenuItem
            // 
            noSliderVelocityMultiplierToolStripMenuItem.Name = "noSliderVelocityMultiplierToolStripMenuItem";
            resources.ApplyResources(noSliderVelocityMultiplierToolStripMenuItem, "noSliderVelocityMultiplierToolStripMenuItem");
            noSliderVelocityMultiplierToolStripMenuItem.Click += noSliderVelocityMultiplierToolStripMenuItem_Click;
            // 
            // compareWithWalkSpeedToolStripMenuItem
            // 
            compareWithWalkSpeedToolStripMenuItem.Name = "compareWithWalkSpeedToolStripMenuItem";
            resources.ApplyResources(compareWithWalkSpeedToolStripMenuItem, "compareWithWalkSpeedToolStripMenuItem");
            compareWithWalkSpeedToolStripMenuItem.Click += compareWithWalkSpeedToolStripMenuItem_Click;
            // 
            // difficultyStarsToolStripMenuItem
            // 
            difficultyStarsToolStripMenuItem.Name = "difficultyStarsToolStripMenuItem";
            resources.ApplyResources(difficultyStarsToolStripMenuItem, "difficultyStarsToolStripMenuItem");
            difficultyStarsToolStripMenuItem.Click += difficultyStarsToolStripMenuItem_Click;
            // 
            // fruitCountInComboToolStripMenuItem
            // 
            fruitCountInComboToolStripMenuItem.Name = "fruitCountInComboToolStripMenuItem";
            resources.ApplyResources(fruitCountInComboToolStripMenuItem, "fruitCountInComboToolStripMenuItem");
            fruitCountInComboToolStripMenuItem.Click += fruitCountInComboToolStripMenuItem_Click;
            // 
            // screenToolStripMenuItem
            // 
            screenToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Screens1ToolStripMenuItem, Screens2ToolStripMenuItem, Screens3ToolStripMenuItem, Screens4ToolStripMenuItem, Screens5ToolStripMenuItem, Screens6ToolStripMenuItem, Screens7ToolStripMenuItem, Screens8ToolStripMenuItem });
            screenToolStripMenuItem.Name = "screenToolStripMenuItem";
            resources.ApplyResources(screenToolStripMenuItem, "screenToolStripMenuItem");
            // 
            // Screens1ToolStripMenuItem
            // 
            Screens1ToolStripMenuItem.Name = "Screens1ToolStripMenuItem";
            resources.ApplyResources(Screens1ToolStripMenuItem, "Screens1ToolStripMenuItem");
            Screens1ToolStripMenuItem.Click += Screens1ToolStripMenuItem_Click;
            // 
            // Screens2ToolStripMenuItem
            // 
            Screens2ToolStripMenuItem.Name = "Screens2ToolStripMenuItem";
            resources.ApplyResources(Screens2ToolStripMenuItem, "Screens2ToolStripMenuItem");
            Screens2ToolStripMenuItem.Click += Screens2ToolStripMenuItem_Click;
            // 
            // Screens3ToolStripMenuItem
            // 
            Screens3ToolStripMenuItem.Name = "Screens3ToolStripMenuItem";
            resources.ApplyResources(Screens3ToolStripMenuItem, "Screens3ToolStripMenuItem");
            Screens3ToolStripMenuItem.Click += Screens3ToolStripMenuItem_Click;
            // 
            // Screens4ToolStripMenuItem
            // 
            Screens4ToolStripMenuItem.Checked = true;
            Screens4ToolStripMenuItem.CheckState = CheckState.Checked;
            Screens4ToolStripMenuItem.Name = "Screens4ToolStripMenuItem";
            resources.ApplyResources(Screens4ToolStripMenuItem, "Screens4ToolStripMenuItem");
            Screens4ToolStripMenuItem.Click += Screens4ToolStripMenuItem_Click;
            // 
            // Screens5ToolStripMenuItem
            // 
            Screens5ToolStripMenuItem.Name = "Screens5ToolStripMenuItem";
            resources.ApplyResources(Screens5ToolStripMenuItem, "Screens5ToolStripMenuItem");
            Screens5ToolStripMenuItem.Click += Screens5ToolStripMenuItem_Click;
            // 
            // Screens6ToolStripMenuItem
            // 
            Screens6ToolStripMenuItem.Name = "Screens6ToolStripMenuItem";
            resources.ApplyResources(Screens6ToolStripMenuItem, "Screens6ToolStripMenuItem");
            Screens6ToolStripMenuItem.Click += Screens6ToolStripMenuItem_Click;
            // 
            // Screens7ToolStripMenuItem
            // 
            Screens7ToolStripMenuItem.Name = "Screens7ToolStripMenuItem";
            resources.ApplyResources(Screens7ToolStripMenuItem, "Screens7ToolStripMenuItem");
            Screens7ToolStripMenuItem.Click += Screens7ToolStripMenuItem_Click;
            // 
            // Screens8ToolStripMenuItem
            // 
            Screens8ToolStripMenuItem.Name = "Screens8ToolStripMenuItem";
            resources.ApplyResources(Screens8ToolStripMenuItem, "Screens8ToolStripMenuItem");
            Screens8ToolStripMenuItem.Click += Screens8ToolStripMenuItem_Click;
            // 
            // bookmarkToolStripMenuItem
            // 
            bookmarkToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { bookmarkSettingsToolStripMenuItem, toolStripSeparator7, loadOnlyBookmarkToolStripMenuItem, loadFullBookmarkToolStripMenuItem, saveBookmarkToolStripMenuItem, toolStripSeparator6, ClearBookmarkToolStripMenuItem, toolStripSeparator8, bookmarkSetStripMenuItem_1, bookmarkSetStripMenuItem_2, bookmarkSetStripMenuItem_3, bookmarkSetStripMenuItem_4, bookmarkSetStripMenuItem_5, bookmarkSetStripMenuItem_6, bookmarkSetStripMenuItem_7, bookmarkSetStripMenuItem_8 });
            bookmarkToolStripMenuItem.Name = "bookmarkToolStripMenuItem";
            resources.ApplyResources(bookmarkToolStripMenuItem, "bookmarkToolStripMenuItem");
            // 
            // bookmarkSettingsToolStripMenuItem
            // 
            bookmarkSettingsToolStripMenuItem.Name = "bookmarkSettingsToolStripMenuItem";
            resources.ApplyResources(bookmarkSettingsToolStripMenuItem, "bookmarkSettingsToolStripMenuItem");
            bookmarkSettingsToolStripMenuItem.Click += bookmarkSettingsToolStripMenuItem_Click;
            // 
            // toolStripSeparator7
            // 
            toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(toolStripSeparator7, "toolStripSeparator7");
            // 
            // loadOnlyBookmarkToolStripMenuItem
            // 
            loadOnlyBookmarkToolStripMenuItem.Name = "loadOnlyBookmarkToolStripMenuItem";
            resources.ApplyResources(loadOnlyBookmarkToolStripMenuItem, "loadOnlyBookmarkToolStripMenuItem");
            loadOnlyBookmarkToolStripMenuItem.Click += loadOnlyBookmarkToolStripMenuItem_Click;
            // 
            // loadFullBookmarkToolStripMenuItem
            // 
            loadFullBookmarkToolStripMenuItem.Name = "loadFullBookmarkToolStripMenuItem";
            resources.ApplyResources(loadFullBookmarkToolStripMenuItem, "loadFullBookmarkToolStripMenuItem");
            loadFullBookmarkToolStripMenuItem.Click += loadFullBookmarkToolStripMenuItem_Click;
            // 
            // saveBookmarkToolStripMenuItem
            // 
            saveBookmarkToolStripMenuItem.Name = "saveBookmarkToolStripMenuItem";
            resources.ApplyResources(saveBookmarkToolStripMenuItem, "saveBookmarkToolStripMenuItem");
            saveBookmarkToolStripMenuItem.Click += saveBookmarkToolStripMenuItem_Click;
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            resources.ApplyResources(toolStripSeparator6, "toolStripSeparator6");
            // 
            // ClearBookmarkToolStripMenuItem
            // 
            ClearBookmarkToolStripMenuItem.Name = "ClearBookmarkToolStripMenuItem";
            resources.ApplyResources(ClearBookmarkToolStripMenuItem, "ClearBookmarkToolStripMenuItem");
            ClearBookmarkToolStripMenuItem.Click += ClearBookmarkToolStripMenuItem_Click;
            // 
            // toolStripSeparator8
            // 
            toolStripSeparator8.Name = "toolStripSeparator8";
            resources.ApplyResources(toolStripSeparator8, "toolStripSeparator8");
            // 
            // bookmarkSetStripMenuItem_1
            // 
            bookmarkSetStripMenuItem_1.Name = "bookmarkSetStripMenuItem_1";
            resources.ApplyResources(bookmarkSetStripMenuItem_1, "bookmarkSetStripMenuItem_1");
            bookmarkSetStripMenuItem_1.Click += bookmarkSetStripMenuItem_1_Click;
            // 
            // bookmarkSetStripMenuItem_2
            // 
            bookmarkSetStripMenuItem_2.Name = "bookmarkSetStripMenuItem_2";
            resources.ApplyResources(bookmarkSetStripMenuItem_2, "bookmarkSetStripMenuItem_2");
            bookmarkSetStripMenuItem_2.Click += bookmarkSetStripMenuItem_2_Click;
            // 
            // bookmarkSetStripMenuItem_3
            // 
            bookmarkSetStripMenuItem_3.Name = "bookmarkSetStripMenuItem_3";
            resources.ApplyResources(bookmarkSetStripMenuItem_3, "bookmarkSetStripMenuItem_3");
            bookmarkSetStripMenuItem_3.Click += bookmarkSetStripMenuItem_3_Click;
            // 
            // bookmarkSetStripMenuItem_4
            // 
            bookmarkSetStripMenuItem_4.Name = "bookmarkSetStripMenuItem_4";
            resources.ApplyResources(bookmarkSetStripMenuItem_4, "bookmarkSetStripMenuItem_4");
            bookmarkSetStripMenuItem_4.Click += bookmarkSetStripMenuItem_4_Click;
            // 
            // bookmarkSetStripMenuItem_5
            // 
            bookmarkSetStripMenuItem_5.Name = "bookmarkSetStripMenuItem_5";
            resources.ApplyResources(bookmarkSetStripMenuItem_5, "bookmarkSetStripMenuItem_5");
            bookmarkSetStripMenuItem_5.Click += bookmarkSetStripMenuItem_5_Click;
            // 
            // bookmarkSetStripMenuItem_6
            // 
            bookmarkSetStripMenuItem_6.Name = "bookmarkSetStripMenuItem_6";
            resources.ApplyResources(bookmarkSetStripMenuItem_6, "bookmarkSetStripMenuItem_6");
            bookmarkSetStripMenuItem_6.Click += bookmarkSetStripMenuItem_6_Click;
            // 
            // bookmarkSetStripMenuItem_7
            // 
            bookmarkSetStripMenuItem_7.Name = "bookmarkSetStripMenuItem_7";
            resources.ApplyResources(bookmarkSetStripMenuItem_7, "bookmarkSetStripMenuItem_7");
            bookmarkSetStripMenuItem_7.Click += bookmarkSetStripMenuItem_7_Click;
            // 
            // bookmarkSetStripMenuItem_8
            // 
            bookmarkSetStripMenuItem_8.Name = "bookmarkSetStripMenuItem_8";
            resources.ApplyResources(bookmarkSetStripMenuItem_8, "bookmarkSetStripMenuItem_8");
            bookmarkSetStripMenuItem_8.Click += bookmarkSetStripMenuItem_8_Click;
            // 
            // generateConversionMappingToolStripMenuItem
            // 
            generateConversionMappingToolStripMenuItem.Name = "generateConversionMappingToolStripMenuItem";
            resources.ApplyResources(generateConversionMappingToolStripMenuItem, "generateConversionMappingToolStripMenuItem");
            generateConversionMappingToolStripMenuItem.Click += generateConversionMappingToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(toolStripSeparator1, "toolStripSeparator1");
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            resources.ApplyResources(exitToolStripMenuItem, "exitToolStripMenuItem");
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // settingsToolStripMenuItem
            // 
            settingsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openSettingsFileToolStripMenuItem, languageToolStripMenuItem, toolStripSeparator2, TopWhenEditorFocusToolStripMenuItem, toolStripSeparator5, backupToolStripMenuItem, toolStripSeparator4, forceResetStripMenuItem, restartProgramStripMenuItem });
            settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
            resources.ApplyResources(settingsToolStripMenuItem, "settingsToolStripMenuItem");
            // 
            // openSettingsFileToolStripMenuItem
            // 
            openSettingsFileToolStripMenuItem.Name = "openSettingsFileToolStripMenuItem";
            resources.ApplyResources(openSettingsFileToolStripMenuItem, "openSettingsFileToolStripMenuItem");
            openSettingsFileToolStripMenuItem.Click += openSettingsFileToolStripMenuItem_Click;
            // 
            // languageToolStripMenuItem
            // 
            languageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { defaultLanguageToolStripMenuItem, toolStripSeparator3, englishLanguageToolStripMenuItem, zhHansLanguageToolStripMenuItem });
            languageToolStripMenuItem.Name = "languageToolStripMenuItem";
            resources.ApplyResources(languageToolStripMenuItem, "languageToolStripMenuItem");
            // 
            // defaultLanguageToolStripMenuItem
            // 
            defaultLanguageToolStripMenuItem.Name = "defaultLanguageToolStripMenuItem";
            resources.ApplyResources(defaultLanguageToolStripMenuItem, "defaultLanguageToolStripMenuItem");
            defaultLanguageToolStripMenuItem.Click += defaultLanguageToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(toolStripSeparator3, "toolStripSeparator3");
            // 
            // englishLanguageToolStripMenuItem
            // 
            englishLanguageToolStripMenuItem.Name = "englishLanguageToolStripMenuItem";
            resources.ApplyResources(englishLanguageToolStripMenuItem, "englishLanguageToolStripMenuItem");
            englishLanguageToolStripMenuItem.Click += englishLanguageToolStripMenuItem_Click;
            // 
            // zhHansLanguageToolStripMenuItem
            // 
            zhHansLanguageToolStripMenuItem.Name = "zhHansLanguageToolStripMenuItem";
            resources.ApplyResources(zhHansLanguageToolStripMenuItem, "zhHansLanguageToolStripMenuItem");
            zhHansLanguageToolStripMenuItem.Click += zhHansLanguageToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(toolStripSeparator2, "toolStripSeparator2");
            // 
            // TopWhenEditorFocusToolStripMenuItem
            // 
            TopWhenEditorFocusToolStripMenuItem.Name = "TopWhenEditorFocusToolStripMenuItem";
            resources.ApplyResources(TopWhenEditorFocusToolStripMenuItem, "TopWhenEditorFocusToolStripMenuItem");
            TopWhenEditorFocusToolStripMenuItem.Click += TopWhenEditorFocusToolStripMenuItem_Click;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            resources.ApplyResources(toolStripSeparator5, "toolStripSeparator5");
            // 
            // backupToolStripMenuItem
            // 
            backupToolStripMenuItem.Name = "backupToolStripMenuItem";
            resources.ApplyResources(backupToolStripMenuItem, "backupToolStripMenuItem");
            backupToolStripMenuItem.Click += backupToolStripMenuItem_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            resources.ApplyResources(toolStripSeparator4, "toolStripSeparator4");
            // 
            // forceResetStripMenuItem
            // 
            forceResetStripMenuItem.Name = "forceResetStripMenuItem";
            resources.ApplyResources(forceResetStripMenuItem, "forceResetStripMenuItem");
            forceResetStripMenuItem.Click += forceResetStripMenuItem_Click;
            // 
            // restartProgramStripMenuItem
            // 
            restartProgramStripMenuItem.Name = "restartProgramStripMenuItem";
            resources.ApplyResources(restartProgramStripMenuItem, "restartProgramStripMenuItem");
            restartProgramStripMenuItem.Click += restartProgramStripMenuItem_Click;
            // 
            // githubToolStripMenuItem
            // 
            githubToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { githubToolStripMenuItem1 });
            githubToolStripMenuItem.Name = "githubToolStripMenuItem";
            resources.ApplyResources(githubToolStripMenuItem, "githubToolStripMenuItem");
            // 
            // githubToolStripMenuItem1
            // 
            githubToolStripMenuItem1.Name = "githubToolStripMenuItem1";
            resources.ApplyResources(githubToolStripMenuItem1, "githubToolStripMenuItem1");
            githubToolStripMenuItem1.Click += githubToolStripMenuItem1_Click;
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(toolStripMenuItem1, "toolStripMenuItem1");
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(32, 32);
            statusStrip1.Items.AddRange(new ToolStripItem[] { StateToolStripStatusLabel });
            resources.ApplyResources(statusStrip1, "statusStrip1");
            statusStrip1.Name = "statusStrip1";
            // 
            // StateToolStripStatusLabel
            // 
            StateToolStripStatusLabel.Name = "StateToolStripStatusLabel";
            resources.ApplyResources(StateToolStripStatusLabel, "StateToolStripStatusLabel");
            // 
            // Form1
            // 
            resources.ApplyResources(this, "$this");
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(statusStrip1);
            Controls.Add(Canvas);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Canvas Canvas;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem viewerToolStripMenuItem;
        private ToolStripMenuItem modToolStripMenuItem;
        private ToolStripMenuItem noneToolStripMenuItem;
        private ToolStripMenuItem hRToolStripMenuItem;
        private ToolStripMenuItem eZToolStripMenuItem;
        private ToolStripMenuItem githubToolStripMenuItem;
        private ToolStripMenuItem githubToolStripMenuItem1;
        private ToolStripMenuItem settingsToolStripMenuItem;
        private ToolStripMenuItem openSettingsFileToolStripMenuItem;
        private ToolStripMenuItem hitobjectLabelToolStripMenuItem;
        private ToolStripMenuItem hideToolStripMenuItem;
        private ToolStripMenuItem sameWithEditorToolStripMenuItem;
        private ToolStripMenuItem noSliderVelocityMultiplierToolStripMenuItem;
        private ToolStripMenuItem compareWithWalkSpeedToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem screenToolStripMenuItem;
        private ToolStripMenuItem Screens1ToolStripMenuItem;
        private ToolStripMenuItem Screens2ToolStripMenuItem;
        private ToolStripMenuItem Screens3ToolStripMenuItem;
        private ToolStripMenuItem Screens4ToolStripMenuItem;
        private ToolStripMenuItem Screens5ToolStripMenuItem;
        private ToolStripMenuItem Screens6ToolStripMenuItem;
        private ToolStripMenuItem Screens7ToolStripMenuItem;
        private ToolStripMenuItem Screens8ToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem backupToolStripMenuItem;
        private ToolStripMenuItem languageToolStripMenuItem;
        private ToolStripMenuItem englishLanguageToolStripMenuItem;
        private ToolStripMenuItem zhHansLanguageToolStripMenuItem;
        private ToolStripMenuItem defaultLanguageToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem difficultyStarsToolStripMenuItem;
        private ToolStripMenuItem fruitCountInComboToolStripMenuItem;
        private ToolStripMenuItem forceResetStripMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem restartProgramStripMenuItem;
        private ToolStripMenuItem TopWhenEditorFocusToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel StateToolStripStatusLabel;
        private ToolStripMenuItem bookmarkToolStripMenuItem;
        private ToolStripMenuItem bookmarkSettingsToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator6;
        private ToolStripMenuItem bookmarkSetStripMenuItem_1;
        private ToolStripMenuItem bookmarkSetStripMenuItem_2;
        private ToolStripMenuItem bookmarkSetStripMenuItem_3;
        private ToolStripMenuItem bookmarkSetStripMenuItem_4;
        private ToolStripMenuItem bookmarkSetStripMenuItem_5;
        private ToolStripMenuItem bookmarkSetStripMenuItem_6;
        private ToolStripMenuItem bookmarkSetStripMenuItem_7;
        private ToolStripMenuItem bookmarkSetStripMenuItem_8;
        private ToolStripSeparator toolStripSeparator7;
        private ToolStripMenuItem loadOnlyBookmarkToolStripMenuItem;
        private ToolStripMenuItem loadFullBookmarkToolStripMenuItem;
        private ToolStripMenuItem saveBookmarkToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem ClearBookmarkToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator8;
        private ToolStripMenuItem cubicFittingCurveToolStripMenuItem;
        private ToolStripMenuItem converterToolStripMenuItem;
        private ToolStripMenuItem lazerConverterToolStripMenuItem;
        private ToolStripMenuItem stableConverterToolStripMenuItem;
        private ToolStripMenuItem generateConversionMappingToolStripMenuItem;
    }
}
