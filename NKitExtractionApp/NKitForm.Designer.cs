namespace Nanook.NKit
{
    partial class NKitForm
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.ListViewItem listViewItem1 = new System.Windows.Forms.ListViewItem("Drag and Drop files here...");
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NKitForm));
            this.splitter = new System.Windows.Forms.SplitContainer();
            this.lvw = new System.Windows.Forms.ListView();
            this.colSource = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSystem = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colHeaderTitle = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colRegion = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblLog = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.btnSettingsRegex = new System.Windows.Forms.Button();
            this.lblSettingsTooltips = new System.Windows.Forms.Label();
            this.lblSettingsMode = new System.Windows.Forms.Label();
            this.cboSettingsMode = new System.Windows.Forms.ComboBox();
            this.lblSettingsRegex = new System.Windows.Forms.Label();
            this.txtSettingsRegex = new System.Windows.Forms.TextBox();
            this.btnSettingsPath = new System.Windows.Forms.Button();
            this.lblSettingsPath = new System.Windows.Forms.Label();
            this.txtSettingsPath = new System.Windows.Forms.TextBox();
            this.btnSettingsProcess = new System.Windows.Forms.Button();
            this.grpProgress = new System.Windows.Forms.GroupBox();
            this.btnProgressCopy = new System.Windows.Forms.Button();
            this.lblProgressStatus = new System.Windows.Forms.Label();
            this.btnProgressResume = new System.Windows.Forms.Button();
            this.btnProgressStop = new System.Windows.Forms.Button();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.dlgFolder = new System.Windows.Forms.OpenFileDialog();
            this.mnuSettingsRegex = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnuSettingsPresets = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.chkSettingsSystemFiles = new System.Windows.Forms.CheckBox();
            this.prgProgressFiles = new Nanook.NKit.TextProgressBar();
            ((System.ComponentModel.ISupportInitialize)(this.splitter)).BeginInit();
            this.splitter.Panel1.SuspendLayout();
            this.splitter.Panel2.SuspendLayout();
            this.splitter.SuspendLayout();
            this.grpSettings.SuspendLayout();
            this.grpProgress.SuspendLayout();
            this.mnuSettingsRegex.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitter
            // 
            this.splitter.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitter.Location = new System.Drawing.Point(1, 2);
            this.splitter.Name = "splitter";
            // 
            // splitter.Panel1
            // 
            this.splitter.Panel1.Controls.Add(this.lvw);
            // 
            // splitter.Panel2
            // 
            this.splitter.Panel2.Controls.Add(this.lblLog);
            this.splitter.Panel2.Controls.Add(this.txtLog);
            this.splitter.Panel2.Controls.Add(this.grpSettings);
            this.splitter.Panel2.Controls.Add(this.grpProgress);
            this.splitter.Size = new System.Drawing.Size(792, 449);
            this.splitter.SplitterDistance = 470;
            this.splitter.TabIndex = 4;
            // 
            // lvw
            // 
            this.lvw.AllowDrop = true;
            this.lvw.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lvw.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colSource,
            this.colSystem,
            this.colId,
            this.colHeaderTitle,
            this.colRegion});
            this.lvw.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lvw.FullRowSelect = true;
            this.lvw.HideSelection = false;
            listViewItem1.Tag = "Dummy";
            this.lvw.Items.AddRange(new System.Windows.Forms.ListViewItem[] {
            listViewItem1});
            this.lvw.Location = new System.Drawing.Point(0, 0);
            this.lvw.MultiSelect = false;
            this.lvw.Name = "lvw";
            this.lvw.ShowItemToolTips = true;
            this.lvw.Size = new System.Drawing.Size(469, 448);
            this.lvw.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvw.TabIndex = 0;
            this.lvw.UseCompatibleStateImageBehavior = false;
            this.lvw.View = System.Windows.Forms.View.Details;
            this.lvw.SelectedIndexChanged += new System.EventHandler(this.lvw_SelectedIndexChanged);
            this.lvw.DragDrop += new System.Windows.Forms.DragEventHandler(this.lvw_DragDrop);
            this.lvw.DragEnter += new System.Windows.Forms.DragEventHandler(this.lvw_DragEnter);
            // 
            // colSource
            // 
            this.colSource.Text = "Source";
            this.colSource.Width = 230;
            // 
            // colSystem
            // 
            this.colSystem.Text = "System";
            // 
            // colId
            // 
            this.colId.Text = "ID";
            this.colId.Width = 68;
            // 
            // colHeaderTitle
            // 
            this.colHeaderTitle.Text = "Header Title";
            this.colHeaderTitle.Width = 81;
            // 
            // colRegion
            // 
            this.colRegion.Text = "Region";
            this.colRegion.Width = 80;
            // 
            // lblLog
            // 
            this.lblLog.AutoSize = true;
            this.lblLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLog.Location = new System.Drawing.Point(9, 182);
            this.lblLog.Name = "lblLog";
            this.lblLog.Size = new System.Drawing.Size(28, 13);
            this.lblLog.TabIndex = 1;
            this.lblLog.Text = "Log";
            // 
            // txtLog
            // 
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.BackColor = System.Drawing.SystemColors.Window;
            this.txtLog.Font = new System.Drawing.Font("Lucida Console", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtLog.Location = new System.Drawing.Point(2, 197);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLog.Size = new System.Drawing.Size(314, 251);
            this.txtLog.TabIndex = 2;
            this.txtLog.WordWrap = false;
            // 
            // grpSettings
            // 
            this.grpSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSettings.BackColor = System.Drawing.SystemColors.Control;
            this.grpSettings.Controls.Add(this.chkSettingsSystemFiles);
            this.grpSettings.Controls.Add(this.btnSettingsRegex);
            this.grpSettings.Controls.Add(this.lblSettingsTooltips);
            this.grpSettings.Controls.Add(this.lblSettingsMode);
            this.grpSettings.Controls.Add(this.cboSettingsMode);
            this.grpSettings.Controls.Add(this.lblSettingsRegex);
            this.grpSettings.Controls.Add(this.txtSettingsRegex);
            this.grpSettings.Controls.Add(this.btnSettingsPath);
            this.grpSettings.Controls.Add(this.lblSettingsPath);
            this.grpSettings.Controls.Add(this.txtSettingsPath);
            this.grpSettings.Controls.Add(this.btnSettingsProcess);
            this.grpSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpSettings.Location = new System.Drawing.Point(2, 3);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(311, 174);
            this.grpSettings.TabIndex = 0;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";
            // 
            // btnSettingsRegex
            // 
            this.btnSettingsRegex.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsRegex.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsRegex.Location = new System.Drawing.Point(279, 70);
            this.btnSettingsRegex.Name = "btnSettingsRegex";
            this.btnSettingsRegex.Size = new System.Drawing.Size(25, 23);
            this.btnSettingsRegex.TabIndex = 5;
            this.btnSettingsRegex.Text = "...";
            this.btnSettingsRegex.UseVisualStyleBackColor = true;
            this.btnSettingsRegex.Click += new System.EventHandler(this.btnSettingsRegex_Click);
            // 
            // lblSettingsTooltips
            // 
            this.lblSettingsTooltips.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSettingsTooltips.AutoSize = true;
            this.lblSettingsTooltips.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsTooltips.Location = new System.Drawing.Point(212, 35);
            this.lblSettingsTooltips.Name = "lblSettingsTooltips";
            this.lblSettingsTooltips.Size = new System.Drawing.Size(95, 13);
            this.lblSettingsTooltips.TabIndex = 2;
            this.lblSettingsTooltips.Text = "(Tooltips on hover)";
            // 
            // lblSettingsMode
            // 
            this.lblSettingsMode.AutoSize = true;
            this.lblSettingsMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsMode.Location = new System.Drawing.Point(7, 16);
            this.lblSettingsMode.Name = "lblSettingsMode";
            this.lblSettingsMode.Size = new System.Drawing.Size(34, 13);
            this.lblSettingsMode.TabIndex = 0;
            this.lblSettingsMode.Text = "Mode";
            // 
            // cboSettingsMode
            // 
            this.cboSettingsMode.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboSettingsMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSettingsMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboSettingsMode.FormattingEnabled = true;
            this.cboSettingsMode.Items.AddRange(new object[] {
            "<Select Mode>",
            "Recovery Items",
            "Extract All Files",
            "Scan List Info Only"});
            this.cboSettingsMode.Location = new System.Drawing.Point(9, 32);
            this.cboSettingsMode.Name = "cboSettingsMode";
            this.cboSettingsMode.Size = new System.Drawing.Size(197, 21);
            this.cboSettingsMode.TabIndex = 1;
            this.cboSettingsMode.SelectedIndexChanged += new System.EventHandler(this.cboSettingsMode_SelectedIndexChanged);
            // 
            // lblSettingsRegex
            // 
            this.lblSettingsRegex.AutoSize = true;
            this.lblSettingsRegex.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsRegex.Location = new System.Drawing.Point(7, 56);
            this.lblSettingsRegex.Name = "lblSettingsRegex";
            this.lblSettingsRegex.Size = new System.Drawing.Size(63, 13);
            this.lblSettingsRegex.TabIndex = 0;
            this.lblSettingsRegex.Text = "Filter Regex";
            this.tooltip.SetToolTip(this.lblSettingsRegex, "Only extract files that match this Regular Expression");
            // 
            // txtSettingsRegex
            // 
            this.txtSettingsRegex.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSettingsRegex.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSettingsRegex.Location = new System.Drawing.Point(9, 72);
            this.txtSettingsRegex.Name = "txtSettingsRegex";
            this.txtSettingsRegex.Size = new System.Drawing.Size(265, 20);
            this.txtSettingsRegex.TabIndex = 4;
            this.tooltip.SetToolTip(this.txtSettingsRegex, "Only extract files that match this Regular Expression");
            // 
            // btnSettingsPath
            // 
            this.btnSettingsPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsPath.Location = new System.Drawing.Point(279, 109);
            this.btnSettingsPath.Name = "btnSettingsPath";
            this.btnSettingsPath.Size = new System.Drawing.Size(25, 23);
            this.btnSettingsPath.TabIndex = 8;
            this.btnSettingsPath.Text = "...";
            this.btnSettingsPath.UseVisualStyleBackColor = true;
            this.btnSettingsPath.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // lblSettingsPath
            // 
            this.lblSettingsPath.AutoSize = true;
            this.lblSettingsPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsPath.Location = new System.Drawing.Point(7, 95);
            this.lblSettingsPath.Name = "lblSettingsPath";
            this.lblSettingsPath.Size = new System.Drawing.Size(64, 13);
            this.lblSettingsPath.TabIndex = 6;
            this.lblSettingsPath.Text = "Output Path";
            this.tooltip.SetToolTip(this.lblSettingsPath, "(Drag and Drop Folder) Root path to store files extracted from the scanned images" +
        "");
            // 
            // txtSettingsPath
            // 
            this.txtSettingsPath.AllowDrop = true;
            this.txtSettingsPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSettingsPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSettingsPath.Location = new System.Drawing.Point(10, 111);
            this.txtSettingsPath.Name = "txtSettingsPath";
            this.txtSettingsPath.Size = new System.Drawing.Size(264, 20);
            this.txtSettingsPath.TabIndex = 7;
            this.txtSettingsPath.Tag = "folder";
            this.tooltip.SetToolTip(this.txtSettingsPath, "(Drag and Drop Folder) Ensure this is the same drive as the output location for b" +
        "est performance");
            this.txtSettingsPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.txt_DragDrop);
            this.txtSettingsPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.txt_DragEnter);
            // 
            // btnSettingsProcess
            // 
            this.btnSettingsProcess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsProcess.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsProcess.Location = new System.Drawing.Point(223, 143);
            this.btnSettingsProcess.Name = "btnSettingsProcess";
            this.btnSettingsProcess.Size = new System.Drawing.Size(75, 23);
            this.btnSettingsProcess.TabIndex = 9;
            this.btnSettingsProcess.Text = "&Process";
            this.btnSettingsProcess.UseVisualStyleBackColor = true;
            this.btnSettingsProcess.Click += new System.EventHandler(this.btnSettingsProcess_Click);
            // 
            // grpProgress
            // 
            this.grpProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpProgress.Controls.Add(this.btnProgressCopy);
            this.grpProgress.Controls.Add(this.lblProgressStatus);
            this.grpProgress.Controls.Add(this.btnProgressResume);
            this.grpProgress.Controls.Add(this.btnProgressStop);
            this.grpProgress.Controls.Add(this.prgProgressFiles);
            this.grpProgress.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpProgress.Location = new System.Drawing.Point(2, 3);
            this.grpProgress.Name = "grpProgress";
            this.grpProgress.Size = new System.Drawing.Size(311, 174);
            this.grpProgress.TabIndex = 1;
            this.grpProgress.TabStop = false;
            this.grpProgress.Text = "Progress";
            this.grpProgress.Visible = false;
            // 
            // btnProgressCopy
            // 
            this.btnProgressCopy.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnProgressCopy.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProgressCopy.Location = new System.Drawing.Point(61, 143);
            this.btnProgressCopy.Name = "btnProgressCopy";
            this.btnProgressCopy.Size = new System.Drawing.Size(75, 23);
            this.btnProgressCopy.TabIndex = 1;
            this.btnProgressCopy.Text = "&Copy";
            this.tooltip.SetToolTip(this.btnProgressCopy, "Copy Results to Clipboard");
            this.btnProgressCopy.UseVisualStyleBackColor = true;
            this.btnProgressCopy.Click += new System.EventHandler(this.btnProgressCopy_Click);
            // 
            // lblProgressStatus
            // 
            this.lblProgressStatus.AutoSize = true;
            this.lblProgressStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblProgressStatus.Location = new System.Drawing.Point(3, 53);
            this.lblProgressStatus.Name = "lblProgressStatus";
            this.lblProgressStatus.Size = new System.Drawing.Size(37, 13);
            this.lblProgressStatus.TabIndex = 5;
            this.lblProgressStatus.Tag = "Status|Extracting Recovery Files|Extracting Files|Scanning";
            this.lblProgressStatus.Text = "Status";
            // 
            // btnProgressResume
            // 
            this.btnProgressResume.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnProgressResume.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProgressResume.Location = new System.Drawing.Point(142, 143);
            this.btnProgressResume.Name = "btnProgressResume";
            this.btnProgressResume.Size = new System.Drawing.Size(75, 23);
            this.btnProgressResume.TabIndex = 2;
            this.btnProgressResume.Tag = "";
            this.btnProgressResume.Text = "&Resume";
            this.btnProgressResume.UseVisualStyleBackColor = true;
            this.btnProgressResume.Visible = false;
            this.btnProgressResume.Click += new System.EventHandler(this.btnProgressResume_Click);
            // 
            // btnProgressStop
            // 
            this.btnProgressStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnProgressStop.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProgressStop.Location = new System.Drawing.Point(223, 143);
            this.btnProgressStop.Name = "btnProgressStop";
            this.btnProgressStop.Size = new System.Drawing.Size(75, 23);
            this.btnProgressStop.TabIndex = 3;
            this.btnProgressStop.Tag = "&Stop|&Stopping|&Reset";
            this.btnProgressStop.Text = "&Stop";
            this.btnProgressStop.UseVisualStyleBackColor = true;
            this.btnProgressStop.Click += new System.EventHandler(this.btnProgressStop_Click);
            // 
            // dlgFolder
            // 
            this.dlgFolder.FileName = "openFileDialog1";
            // 
            // mnuSettingsRegex
            // 
            this.mnuSettingsRegex.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuSettingsPresets,
            this.toolStripMenuItem1});
            this.mnuSettingsRegex.Name = "contextMenuStrip1";
            this.mnuSettingsRegex.Size = new System.Drawing.Size(125, 32);
            // 
            // mnuSettingsPresets
            // 
            this.mnuSettingsPresets.Enabled = false;
            this.mnuSettingsPresets.Name = "mnuSettingsPresets";
            this.mnuSettingsPresets.Size = new System.Drawing.Size(124, 22);
            this.mnuSettingsPresets.Text = "Examples";
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(121, 6);
            // 
            // chkSettingsSystemFiles
            // 
            this.chkSettingsSystemFiles.AutoSize = true;
            this.chkSettingsSystemFiles.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsSystemFiles.Location = new System.Drawing.Point(10, 143);
            this.chkSettingsSystemFiles.Name = "chkSettingsSystemFiles";
            this.chkSettingsSystemFiles.Size = new System.Drawing.Size(108, 17);
            this.chkSettingsSystemFiles.TabIndex = 10;
            this.chkSettingsSystemFiles.Text = "Only System Files";
            this.tooltip.SetToolTip(this.chkSettingsSystemFiles, "Only extract system files (including disc and partition headers)");
            this.chkSettingsSystemFiles.UseVisualStyleBackColor = true;
            this.chkSettingsSystemFiles.CheckedChanged += new System.EventHandler(this.chkSettingsSystemFiles_CheckedChanged);
            // 
            // prgProgressFiles
            // 
            this.prgProgressFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.prgProgressFiles.CustomText = null;
            this.prgProgressFiles.DisplayStyle = Nanook.NKit.ProgressBarDisplayText.CustomText;
            this.prgProgressFiles.Location = new System.Drawing.Point(6, 69);
            this.prgProgressFiles.Maximum = 1000;
            this.prgProgressFiles.Name = "prgProgressFiles";
            this.prgProgressFiles.Size = new System.Drawing.Size(299, 23);
            this.prgProgressFiles.Step = 1;
            this.prgProgressFiles.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.prgProgressFiles.TabIndex = 0;
            // 
            // NKitForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(794, 453);
            this.Controls.Add(this.splitter);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "NKitForm";
            this.Text = "NKit Extraction App";
            this.Load += new System.EventHandler(this.NKitForm_Load);
            this.splitter.Panel1.ResumeLayout(false);
            this.splitter.Panel2.ResumeLayout(false);
            this.splitter.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitter)).EndInit();
            this.splitter.ResumeLayout(false);
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            this.grpProgress.ResumeLayout(false);
            this.grpProgress.PerformLayout();
            this.mnuSettingsRegex.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitter;
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Button btnSettingsProcess;
        private System.Windows.Forms.GroupBox grpProgress;
        private System.Windows.Forms.Button btnProgressStop;
        private TextProgressBar prgProgressFiles;
        private System.Windows.Forms.Label lblLog;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.ListView lvw;
        private System.Windows.Forms.ColumnHeader colSource;
        private System.Windows.Forms.ColumnHeader colHeaderTitle;
        private System.Windows.Forms.ColumnHeader colRegion;
        private System.Windows.Forms.ColumnHeader colId;
        private System.Windows.Forms.Button btnProgressResume;
        private System.Windows.Forms.ColumnHeader colSystem;
        private System.Windows.Forms.Label lblSettingsMode;
        private System.Windows.Forms.ComboBox cboSettingsMode;
        private System.Windows.Forms.Label lblSettingsRegex;
        private System.Windows.Forms.TextBox txtSettingsRegex;
        private System.Windows.Forms.Button btnSettingsPath;
        private System.Windows.Forms.Label lblSettingsPath;
        private System.Windows.Forms.TextBox txtSettingsPath;
        private System.Windows.Forms.Label lblProgressStatus;
        private System.Windows.Forms.ToolTip tooltip;
        private System.Windows.Forms.OpenFileDialog dlgFolder;
        private System.Windows.Forms.Label lblSettingsTooltips;
        private System.Windows.Forms.Button btnSettingsRegex;
        private System.Windows.Forms.ContextMenuStrip mnuSettingsRegex;
        private System.Windows.Forms.ToolStripMenuItem mnuSettingsPresets;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.Button btnProgressCopy;
        private System.Windows.Forms.CheckBox chkSettingsSystemFiles;
    }
}