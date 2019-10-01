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
            this.colReadResult = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVerifyResult = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colCrc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colMatch = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.lblSettingsTooltips = new System.Windows.Forms.Label();
            this.grpSettingsOutput = new System.Windows.Forms.GroupBox();
            this.chkSettingsRecoveryMatchFailDelete = new System.Windows.Forms.CheckBox();
            this.btnSettingsMasks = new System.Windows.Forms.Button();
            this.lblSettingsTempPath = new System.Windows.Forms.Label();
            this.btnSettingsTempPath = new System.Windows.Forms.Button();
            this.txtSettingsTempPath = new System.Windows.Forms.TextBox();
            this.lblSettingsOutputPathBase = new System.Windows.Forms.Label();
            this.chkSettingsUseMasks = new System.Windows.Forms.CheckBox();
            this.btnSettingsOutputPathBase = new System.Windows.Forms.Button();
            this.txtSettingsOutputPathBase = new System.Windows.Forms.TextBox();
            this.grpSettingsSummaryLog = new System.Windows.Forms.GroupBox();
            this.btnSettingsSummaryLog = new System.Windows.Forms.Button();
            this.txtSettingsSummaryLog = new System.Windows.Forms.TextBox();
            this.chkSettingsSummaryLog = new System.Windows.Forms.CheckBox();
            this.chkSettingsCalculateHashes = new System.Windows.Forms.CheckBox();
            this.chkSettingsDeleteSource = new System.Windows.Forms.CheckBox();
            this.chkSettingsFullVerify = new System.Windows.Forms.CheckBox();
            this.chkSettingsReencodeNkit = new System.Windows.Forms.CheckBox();
            this.chkSettingsTestMode = new System.Windows.Forms.CheckBox();
            this.chkSettingsRemoveUpdate = new System.Windows.Forms.CheckBox();
            this.cboSettingsMode = new System.Windows.Forms.ComboBox();
            this.btnSettingsReset = new System.Windows.Forms.Button();
            this.btnSettingsProcess = new System.Windows.Forms.Button();
            this.pnlProgress = new System.Windows.Forms.Panel();
            this.grpProgress = new System.Windows.Forms.GroupBox();
            this.btnProgressSummaryLog = new System.Windows.Forms.Button();
            this.btnProgressResume = new System.Windows.Forms.Button();
            this.btnProgressStop = new System.Windows.Forms.Button();
            this.lblLog = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.dlgFile = new System.Windows.Forms.SaveFileDialog();
            this.dlgFolder = new System.Windows.Forms.OpenFileDialog();
            this.prgProgressStep = new Nanook.NKit.TextProgressBar();
            this.prgProgressFile = new Nanook.NKit.TextProgressBar();
            this.prgProgressFiles = new Nanook.NKit.TextProgressBar();
            ((System.ComponentModel.ISupportInitialize)(this.splitter)).BeginInit();
            this.splitter.Panel1.SuspendLayout();
            this.splitter.Panel2.SuspendLayout();
            this.splitter.SuspendLayout();
            this.grpSettings.SuspendLayout();
            this.grpSettingsOutput.SuspendLayout();
            this.grpSettingsSummaryLog.SuspendLayout();
            this.pnlProgress.SuspendLayout();
            this.grpProgress.SuspendLayout();
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
            this.splitter.Panel2.Controls.Add(this.grpSettings);
            this.splitter.Panel2.Controls.Add(this.pnlProgress);
            this.splitter.Size = new System.Drawing.Size(792, 461);
            this.splitter.SplitterDistance = 458;
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
            this.colReadResult,
            this.colVerifyResult,
            this.colCrc,
            this.colId,
            this.colMatch,
            this.colSize,
            this.colName});
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
            this.lvw.Size = new System.Drawing.Size(455, 460);
            this.lvw.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvw.TabIndex = 0;
            this.tooltip.SetToolTip(this.lvw, "Right-Click to edit when not processing");
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
            // colReadResult
            // 
            this.colReadResult.Text = "Read Valid";
            this.colReadResult.Width = 72;
            // 
            // colVerifyResult
            // 
            this.colVerifyResult.Text = "Verify Valid";
            this.colVerifyResult.Width = 71;
            // 
            // colCrc
            // 
            this.colCrc.Text = "CRC";
            this.colCrc.Width = 85;
            // 
            // colId
            // 
            this.colId.Text = "ID";
            this.colId.Width = 68;
            // 
            // colMatch
            // 
            this.colMatch.Text = "Match";
            this.colMatch.Width = 81;
            // 
            // colSize
            // 
            this.colSize.Text = "Size";
            this.colSize.Width = 80;
            // 
            // colName
            // 
            this.colName.Text = "Match Name";
            // 
            // grpSettings
            // 
            this.grpSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSettings.BackColor = System.Drawing.SystemColors.Control;
            this.grpSettings.Controls.Add(this.lblSettingsTooltips);
            this.grpSettings.Controls.Add(this.grpSettingsOutput);
            this.grpSettings.Controls.Add(this.grpSettingsSummaryLog);
            this.grpSettings.Controls.Add(this.chkSettingsCalculateHashes);
            this.grpSettings.Controls.Add(this.chkSettingsDeleteSource);
            this.grpSettings.Controls.Add(this.chkSettingsFullVerify);
            this.grpSettings.Controls.Add(this.chkSettingsReencodeNkit);
            this.grpSettings.Controls.Add(this.chkSettingsTestMode);
            this.grpSettings.Controls.Add(this.chkSettingsRemoveUpdate);
            this.grpSettings.Controls.Add(this.cboSettingsMode);
            this.grpSettings.Controls.Add(this.btnSettingsReset);
            this.grpSettings.Controls.Add(this.btnSettingsProcess);
            this.grpSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpSettings.Location = new System.Drawing.Point(3, 2);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(324, 458);
            this.grpSettings.TabIndex = 0;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";
            // 
            // lblSettingsTooltips
            // 
            this.lblSettingsTooltips.AutoSize = true;
            this.lblSettingsTooltips.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsTooltips.Location = new System.Drawing.Point(217, 31);
            this.lblSettingsTooltips.Name = "lblSettingsTooltips";
            this.lblSettingsTooltips.Size = new System.Drawing.Size(95, 13);
            this.lblSettingsTooltips.TabIndex = 11;
            this.lblSettingsTooltips.Text = "(Tooltips on hover)";
            // 
            // grpSettingsOutput
            // 
            this.grpSettingsOutput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSettingsOutput.Controls.Add(this.chkSettingsRecoveryMatchFailDelete);
            this.grpSettingsOutput.Controls.Add(this.btnSettingsMasks);
            this.grpSettingsOutput.Controls.Add(this.lblSettingsTempPath);
            this.grpSettingsOutput.Controls.Add(this.btnSettingsTempPath);
            this.grpSettingsOutput.Controls.Add(this.txtSettingsTempPath);
            this.grpSettingsOutput.Controls.Add(this.lblSettingsOutputPathBase);
            this.grpSettingsOutput.Controls.Add(this.chkSettingsUseMasks);
            this.grpSettingsOutput.Controls.Add(this.btnSettingsOutputPathBase);
            this.grpSettingsOutput.Controls.Add(this.txtSettingsOutputPathBase);
            this.grpSettingsOutput.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpSettingsOutput.Location = new System.Drawing.Point(7, 55);
            this.grpSettingsOutput.Name = "grpSettingsOutput";
            this.grpSettingsOutput.Size = new System.Drawing.Size(312, 161);
            this.grpSettingsOutput.TabIndex = 2;
            this.grpSettingsOutput.TabStop = false;
            this.grpSettingsOutput.Text = "Output";
            // 
            // chkSettingsRecoveryMatchFailDelete
            // 
            this.chkSettingsRecoveryMatchFailDelete.AutoSize = true;
            this.chkSettingsRecoveryMatchFailDelete.Checked = true;
            this.chkSettingsRecoveryMatchFailDelete.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSettingsRecoveryMatchFailDelete.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsRecoveryMatchFailDelete.Location = new System.Drawing.Point(7, 130);
            this.chkSettingsRecoveryMatchFailDelete.Name = "chkSettingsRecoveryMatchFailDelete";
            this.chkSettingsRecoveryMatchFailDelete.Size = new System.Drawing.Size(178, 17);
            this.chkSettingsRecoveryMatchFailDelete.TabIndex = 7;
            this.chkSettingsRecoveryMatchFailDelete.Text = "Delete Recovery Match Failures";
            this.tooltip.SetToolTip(this.chkSettingsRecoveryMatchFailDelete, "When Recovering, delete items that fail to match a Dat entry");
            this.chkSettingsRecoveryMatchFailDelete.UseVisualStyleBackColor = true;
            this.chkSettingsRecoveryMatchFailDelete.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // btnSettingsMasks
            // 
            this.btnSettingsMasks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsMasks.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsMasks.Location = new System.Drawing.Point(234, 115);
            this.btnSettingsMasks.Name = "btnSettingsMasks";
            this.btnSettingsMasks.Size = new System.Drawing.Size(68, 23);
            this.btnSettingsMasks.TabIndex = 8;
            this.btnSettingsMasks.Text = "Masks...";
            this.btnSettingsMasks.UseVisualStyleBackColor = true;
            this.btnSettingsMasks.Click += new System.EventHandler(this.btnSettingsMasks_Click);
            // 
            // lblSettingsTempPath
            // 
            this.lblSettingsTempPath.AutoSize = true;
            this.lblSettingsTempPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsTempPath.Location = new System.Drawing.Point(6, 63);
            this.lblSettingsTempPath.Name = "lblSettingsTempPath";
            this.lblSettingsTempPath.Size = new System.Drawing.Size(59, 13);
            this.lblSettingsTempPath.TabIndex = 3;
            this.lblSettingsTempPath.Text = "Temp Path";
            this.tooltip.SetToolTip(this.lblSettingsTempPath, "(Drag and Drop Folder) Ensure this is the same drive as the output location for b" +
        "est performance");
            // 
            // btnSettingsTempPath
            // 
            this.btnSettingsTempPath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsTempPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsTempPath.Location = new System.Drawing.Point(278, 76);
            this.btnSettingsTempPath.Name = "btnSettingsTempPath";
            this.btnSettingsTempPath.Size = new System.Drawing.Size(24, 23);
            this.btnSettingsTempPath.TabIndex = 5;
            this.btnSettingsTempPath.Text = "...";
            this.btnSettingsTempPath.UseVisualStyleBackColor = true;
            this.btnSettingsTempPath.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // txtSettingsTempPath
            // 
            this.txtSettingsTempPath.AllowDrop = true;
            this.txtSettingsTempPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSettingsTempPath.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSettingsTempPath.Location = new System.Drawing.Point(7, 78);
            this.txtSettingsTempPath.Name = "txtSettingsTempPath";
            this.txtSettingsTempPath.Size = new System.Drawing.Size(265, 20);
            this.txtSettingsTempPath.TabIndex = 4;
            this.txtSettingsTempPath.Tag = "folder";
            this.tooltip.SetToolTip(this.txtSettingsTempPath, "(Drag and Drop Folder) Ensure this is the same drive as the output location for b" +
        "est performance");
            this.txtSettingsTempPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.txt_DragDrop);
            this.txtSettingsTempPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.txt_DragEnter);
            // 
            // lblSettingsOutputPathBase
            // 
            this.lblSettingsOutputPathBase.AutoSize = true;
            this.lblSettingsOutputPathBase.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSettingsOutputPathBase.Location = new System.Drawing.Point(6, 24);
            this.lblSettingsOutputPathBase.Name = "lblSettingsOutputPathBase";
            this.lblSettingsOutputPathBase.Size = new System.Drawing.Size(218, 13);
            this.lblSettingsOutputPathBase.TabIndex = 0;
            this.lblSettingsOutputPathBase.Text = "Config %pth Value (Only for Mask Renaming)";
            this.tooltip.SetToolTip(this.lblSettingsOutputPathBase, "(Drag and Drop Folder) Ensure the config masks use this in their path to output t" +
        "o this location");
            // 
            // chkSettingsUseMasks
            // 
            this.chkSettingsUseMasks.AutoSize = true;
            this.chkSettingsUseMasks.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsUseMasks.Location = new System.Drawing.Point(7, 109);
            this.chkSettingsUseMasks.Name = "chkSettingsUseMasks";
            this.chkSettingsUseMasks.Size = new System.Drawing.Size(181, 17);
            this.chkSettingsUseMasks.TabIndex = 6;
            this.chkSettingsUseMasks.Text = "Mask Rename (Use source if off)";
            this.tooltip.SetToolTip(this.chkSettingsUseMasks, "Turn off Mask renaming and output to the source path and name");
            this.chkSettingsUseMasks.UseVisualStyleBackColor = true;
            this.chkSettingsUseMasks.Click += new System.EventHandler(this.chkSettings_Checked);
            // 
            // btnSettingsOutputPathBase
            // 
            this.btnSettingsOutputPathBase.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsOutputPathBase.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsOutputPathBase.Location = new System.Drawing.Point(278, 37);
            this.btnSettingsOutputPathBase.Name = "btnSettingsOutputPathBase";
            this.btnSettingsOutputPathBase.Size = new System.Drawing.Size(24, 23);
            this.btnSettingsOutputPathBase.TabIndex = 2;
            this.btnSettingsOutputPathBase.Text = "...";
            this.btnSettingsOutputPathBase.UseVisualStyleBackColor = true;
            this.btnSettingsOutputPathBase.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // txtSettingsOutputPathBase
            // 
            this.txtSettingsOutputPathBase.AllowDrop = true;
            this.txtSettingsOutputPathBase.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSettingsOutputPathBase.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSettingsOutputPathBase.Location = new System.Drawing.Point(7, 39);
            this.txtSettingsOutputPathBase.Name = "txtSettingsOutputPathBase";
            this.txtSettingsOutputPathBase.Size = new System.Drawing.Size(265, 20);
            this.txtSettingsOutputPathBase.TabIndex = 1;
            this.txtSettingsOutputPathBase.Tag = "folder";
            this.tooltip.SetToolTip(this.txtSettingsOutputPathBase, "(Drag and Drop Folder) Ensure the config masks use this in their path to output t" +
        "o this location");
            this.txtSettingsOutputPathBase.DragDrop += new System.Windows.Forms.DragEventHandler(this.txt_DragDrop);
            this.txtSettingsOutputPathBase.DragEnter += new System.Windows.Forms.DragEventHandler(this.txt_DragEnter);
            // 
            // grpSettingsSummaryLog
            // 
            this.grpSettingsSummaryLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpSettingsSummaryLog.Controls.Add(this.btnSettingsSummaryLog);
            this.grpSettingsSummaryLog.Controls.Add(this.txtSettingsSummaryLog);
            this.grpSettingsSummaryLog.Controls.Add(this.chkSettingsSummaryLog);
            this.grpSettingsSummaryLog.Location = new System.Drawing.Point(6, 364);
            this.grpSettingsSummaryLog.Name = "grpSettingsSummaryLog";
            this.grpSettingsSummaryLog.Size = new System.Drawing.Size(312, 52);
            this.grpSettingsSummaryLog.TabIndex = 10;
            this.grpSettingsSummaryLog.TabStop = false;
            // 
            // btnSettingsSummaryLog
            // 
            this.btnSettingsSummaryLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsSummaryLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsSummaryLog.Location = new System.Drawing.Point(278, 19);
            this.btnSettingsSummaryLog.Name = "btnSettingsSummaryLog";
            this.btnSettingsSummaryLog.Size = new System.Drawing.Size(24, 23);
            this.btnSettingsSummaryLog.TabIndex = 2;
            this.btnSettingsSummaryLog.Text = "...";
            this.btnSettingsSummaryLog.UseVisualStyleBackColor = true;
            this.btnSettingsSummaryLog.Click += new System.EventHandler(this.btnSettings_Click);
            // 
            // txtSettingsSummaryLog
            // 
            this.txtSettingsSummaryLog.AllowDrop = true;
            this.txtSettingsSummaryLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSettingsSummaryLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtSettingsSummaryLog.Location = new System.Drawing.Point(7, 21);
            this.txtSettingsSummaryLog.Name = "txtSettingsSummaryLog";
            this.txtSettingsSummaryLog.Size = new System.Drawing.Size(265, 20);
            this.txtSettingsSummaryLog.TabIndex = 1;
            this.txtSettingsSummaryLog.Tag = "file";
            this.tooltip.SetToolTip(this.txtSettingsSummaryLog, "(Drag and Drop Folder) Ensure this is the same drive as the output location for b" +
        "est performance");
            this.txtSettingsSummaryLog.DragDrop += new System.Windows.Forms.DragEventHandler(this.txt_DragDrop);
            this.txtSettingsSummaryLog.DragEnter += new System.Windows.Forms.DragEventHandler(this.txt_DragEnter);
            // 
            // chkSettingsSummaryLog
            // 
            this.chkSettingsSummaryLog.AutoSize = true;
            this.chkSettingsSummaryLog.Checked = true;
            this.chkSettingsSummaryLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSettingsSummaryLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsSummaryLog.Location = new System.Drawing.Point(7, 0);
            this.chkSettingsSummaryLog.Name = "chkSettingsSummaryLog";
            this.chkSettingsSummaryLog.Size = new System.Drawing.Size(90, 17);
            this.chkSettingsSummaryLog.TabIndex = 0;
            this.chkSettingsSummaryLog.Text = "Summary Log";
            this.tooltip.SetToolTip(this.chkSettingsSummaryLog, "Enable writing to the Summary Log. (Drag and Drop Folder) Ensure this is the same" +
        " drive as the output location for best performance");
            this.chkSettingsSummaryLog.UseVisualStyleBackColor = true;
            this.chkSettingsSummaryLog.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsCalculateHashes
            // 
            this.chkSettingsCalculateHashes.AutoSize = true;
            this.chkSettingsCalculateHashes.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsCalculateHashes.Location = new System.Drawing.Point(13, 268);
            this.chkSettingsCalculateHashes.Name = "chkSettingsCalculateHashes";
            this.chkSettingsCalculateHashes.Size = new System.Drawing.Size(109, 17);
            this.chkSettingsCalculateHashes.TabIndex = 5;
            this.chkSettingsCalculateHashes.Text = "Calculate Hashes";
            this.tooltip.SetToolTip(this.chkSettingsCalculateHashes, "Calculate the MD5 and SHA1 of the output file so it can be used in the match mask" +
        "s and Summary Log.");
            this.chkSettingsCalculateHashes.UseVisualStyleBackColor = true;
            this.chkSettingsCalculateHashes.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsDeleteSource
            // 
            this.chkSettingsDeleteSource.AutoSize = true;
            this.chkSettingsDeleteSource.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsDeleteSource.Location = new System.Drawing.Point(13, 337);
            this.chkSettingsDeleteSource.Name = "chkSettingsDeleteSource";
            this.chkSettingsDeleteSource.Size = new System.Drawing.Size(285, 17);
            this.chkSettingsDeleteSource.TabIndex = 8;
            this.chkSettingsDeleteSource.Text = "Delete Source (Requires Full Verify and No Test Mode)";
            this.tooltip.SetToolTip(this.chkSettingsDeleteSource, "Delete the source file(s) on successful verification");
            this.chkSettingsDeleteSource.UseVisualStyleBackColor = true;
            this.chkSettingsDeleteSource.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsFullVerify
            // 
            this.chkSettingsFullVerify.AutoSize = true;
            this.chkSettingsFullVerify.Checked = true;
            this.chkSettingsFullVerify.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkSettingsFullVerify.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsFullVerify.Location = new System.Drawing.Point(13, 291);
            this.chkSettingsFullVerify.Name = "chkSettingsFullVerify";
            this.chkSettingsFullVerify.Size = new System.Drawing.Size(71, 17);
            this.chkSettingsFullVerify.TabIndex = 6;
            this.chkSettingsFullVerify.Text = "Full Verify";
            this.tooltip.SetToolTip(this.chkSettingsFullVerify, "Check the CRC of the output file matches. For NKit files this will rebuild the fi" +
        "le in memory.");
            this.chkSettingsFullVerify.UseVisualStyleBackColor = true;
            this.chkSettingsFullVerify.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsReencodeNkit
            // 
            this.chkSettingsReencodeNkit.AutoSize = true;
            this.chkSettingsReencodeNkit.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsReencodeNkit.Location = new System.Drawing.Point(13, 245);
            this.chkSettingsReencodeNkit.Name = "chkSettingsReencodeNkit";
            this.chkSettingsReencodeNkit.Size = new System.Drawing.Size(99, 17);
            this.chkSettingsReencodeNkit.TabIndex = 4;
            this.chkSettingsReencodeNkit.Text = "Reencode NKit";
            this.tooltip.SetToolTip(this.chkSettingsReencodeNkit, "Reconvert the NKit format when converting nkit [iso/gcz] <-> nkit [iso/gcz]");
            this.chkSettingsReencodeNkit.UseVisualStyleBackColor = true;
            this.chkSettingsReencodeNkit.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsTestMode
            // 
            this.chkSettingsTestMode.AutoSize = true;
            this.chkSettingsTestMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsTestMode.Location = new System.Drawing.Point(13, 314);
            this.chkSettingsTestMode.Name = "chkSettingsTestMode";
            this.chkSettingsTestMode.Size = new System.Drawing.Size(77, 17);
            this.chkSettingsTestMode.TabIndex = 7;
            this.chkSettingsTestMode.Text = "Test Mode";
            this.tooltip.SetToolTip(this.chkSettingsTestMode, "Convert then delete the output. The Summary Log is still written. Useful for audi" +
        "ting");
            this.chkSettingsTestMode.UseVisualStyleBackColor = true;
            this.chkSettingsTestMode.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // chkSettingsRemoveUpdate
            // 
            this.chkSettingsRemoveUpdate.AutoSize = true;
            this.chkSettingsRemoveUpdate.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chkSettingsRemoveUpdate.Location = new System.Drawing.Point(13, 222);
            this.chkSettingsRemoveUpdate.Name = "chkSettingsRemoveUpdate";
            this.chkSettingsRemoveUpdate.Size = new System.Drawing.Size(234, 17);
            this.chkSettingsRemoveUpdate.TabIndex = 3;
            this.chkSettingsRemoveUpdate.Text = "Remove and Preserve Wii Update Partitions";
            this.tooltip.SetToolTip(this.chkSettingsRemoveUpdate, "Controlled removal of Wii Partitions to save more space. Removed items are stored" +
        " in the Recovery files folders");
            this.chkSettingsRemoveUpdate.UseVisualStyleBackColor = true;
            this.chkSettingsRemoveUpdate.CheckedChanged += new System.EventHandler(this.chkSettings_Checked);
            // 
            // cboSettingsMode
            // 
            this.cboSettingsMode.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.cboSettingsMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSettingsMode.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboSettingsMode.FormattingEnabled = true;
            this.cboSettingsMode.Items.AddRange(new object[] {
            "<Select Mode>",
            "Recover to ISO",
            "Recover to NKit.iso",
            "Recover to NKit.gcz",
            "Convert to ISO",
            "Convert to NKit.iso",
            "Convert to NKit.gcz"});
            this.cboSettingsMode.Location = new System.Drawing.Point(13, 28);
            this.cboSettingsMode.Name = "cboSettingsMode";
            this.cboSettingsMode.Size = new System.Drawing.Size(198, 21);
            this.cboSettingsMode.TabIndex = 0;
            this.cboSettingsMode.SelectedIndexChanged += new System.EventHandler(this.cboSettingsMode_SelectedIndexChanged);
            // 
            // btnSettingsReset
            // 
            this.btnSettingsReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSettingsReset.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsReset.Location = new System.Drawing.Point(13, 425);
            this.btnSettingsReset.Name = "btnSettingsReset";
            this.btnSettingsReset.Size = new System.Drawing.Size(75, 23);
            this.btnSettingsReset.TabIndex = 0;
            this.btnSettingsReset.Text = "Reset";
            this.btnSettingsReset.UseVisualStyleBackColor = true;
            this.btnSettingsReset.Click += new System.EventHandler(this.btnSettingsReset_Click);
            // 
            // btnSettingsProcess
            // 
            this.btnSettingsProcess.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSettingsProcess.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSettingsProcess.Location = new System.Drawing.Point(241, 425);
            this.btnSettingsProcess.Name = "btnSettingsProcess";
            this.btnSettingsProcess.Size = new System.Drawing.Size(75, 23);
            this.btnSettingsProcess.TabIndex = 1;
            this.btnSettingsProcess.Text = "&Process";
            this.btnSettingsProcess.UseVisualStyleBackColor = true;
            this.btnSettingsProcess.Click += new System.EventHandler(this.btnSettingsProcess_Click);
            // 
            // pnlProgress
            // 
            this.pnlProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlProgress.Controls.Add(this.grpProgress);
            this.pnlProgress.Controls.Add(this.lblLog);
            this.pnlProgress.Controls.Add(this.txtLog);
            this.pnlProgress.Location = new System.Drawing.Point(4, 2);
            this.pnlProgress.Name = "pnlProgress";
            this.pnlProgress.Size = new System.Drawing.Size(323, 457);
            this.pnlProgress.TabIndex = 4;
            this.pnlProgress.Visible = false;
            // 
            // grpProgress
            // 
            this.grpProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpProgress.Controls.Add(this.btnProgressSummaryLog);
            this.grpProgress.Controls.Add(this.btnProgressResume);
            this.grpProgress.Controls.Add(this.btnProgressStop);
            this.grpProgress.Controls.Add(this.prgProgressStep);
            this.grpProgress.Controls.Add(this.prgProgressFile);
            this.grpProgress.Controls.Add(this.prgProgressFiles);
            this.grpProgress.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpProgress.Location = new System.Drawing.Point(0, 282);
            this.grpProgress.Name = "grpProgress";
            this.grpProgress.Size = new System.Drawing.Size(322, 174);
            this.grpProgress.TabIndex = 0;
            this.grpProgress.TabStop = false;
            this.grpProgress.Text = "Progress";
            // 
            // btnProgressSummaryLog
            // 
            this.btnProgressSummaryLog.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnProgressSummaryLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProgressSummaryLog.Location = new System.Drawing.Point(6, 143);
            this.btnProgressSummaryLog.Name = "btnProgressSummaryLog";
            this.btnProgressSummaryLog.Size = new System.Drawing.Size(75, 23);
            this.btnProgressSummaryLog.TabIndex = 5;
            this.btnProgressSummaryLog.Text = "&Open Log";
            this.tooltip.SetToolTip(this.btnProgressSummaryLog, "Open the Summary Log (Uses associated Windows App)");
            this.btnProgressSummaryLog.UseVisualStyleBackColor = true;
            this.btnProgressSummaryLog.Click += new System.EventHandler(this.btnProgressSummaryLog_Click);
            // 
            // btnProgressResume
            // 
            this.btnProgressResume.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnProgressResume.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnProgressResume.Location = new System.Drawing.Point(144, 143);
            this.btnProgressResume.Name = "btnProgressResume";
            this.btnProgressResume.Size = new System.Drawing.Size(75, 23);
            this.btnProgressResume.TabIndex = 3;
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
            this.btnProgressStop.Location = new System.Drawing.Point(240, 143);
            this.btnProgressStop.Name = "btnProgressStop";
            this.btnProgressStop.Size = new System.Drawing.Size(75, 23);
            this.btnProgressStop.TabIndex = 4;
            this.btnProgressStop.Tag = "&Stop|&Stopping|&Reset";
            this.btnProgressStop.Text = "&Stop";
            this.btnProgressStop.UseVisualStyleBackColor = true;
            this.btnProgressStop.Click += new System.EventHandler(this.btnProgressStop_Click);
            // 
            // lblLog
            // 
            this.lblLog.AutoSize = true;
            this.lblLog.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLog.Location = new System.Drawing.Point(-1, 3);
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
            this.txtLog.Location = new System.Drawing.Point(0, 19);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLog.Size = new System.Drawing.Size(322, 258);
            this.txtLog.TabIndex = 2;
            this.txtLog.WordWrap = false;
            // 
            // dlgFolder
            // 
            this.dlgFolder.FileName = "openFileDialog1";
            // 
            // prgProgressStep
            // 
            this.prgProgressStep.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.prgProgressStep.CustomText = null;
            this.prgProgressStep.DisplayStyle = Nanook.NKit.ProgressBarDisplayText.CustomText;
            this.prgProgressStep.Location = new System.Drawing.Point(6, 99);
            this.prgProgressStep.Maximum = 1000;
            this.prgProgressStep.Name = "prgProgressStep";
            this.prgProgressStep.Size = new System.Drawing.Size(311, 23);
            this.prgProgressStep.Step = 1;
            this.prgProgressStep.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.prgProgressStep.TabIndex = 2;
            // 
            // prgProgressFile
            // 
            this.prgProgressFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.prgProgressFile.CustomText = null;
            this.prgProgressFile.DisplayStyle = Nanook.NKit.ProgressBarDisplayText.CustomText;
            this.prgProgressFile.Location = new System.Drawing.Point(6, 64);
            this.prgProgressFile.Maximum = 1000;
            this.prgProgressFile.Name = "prgProgressFile";
            this.prgProgressFile.Size = new System.Drawing.Size(310, 23);
            this.prgProgressFile.Step = 1;
            this.prgProgressFile.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.prgProgressFile.TabIndex = 1;
            // 
            // prgProgressFiles
            // 
            this.prgProgressFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.prgProgressFiles.CustomText = null;
            this.prgProgressFiles.DisplayStyle = Nanook.NKit.ProgressBarDisplayText.CustomText;
            this.prgProgressFiles.Location = new System.Drawing.Point(6, 29);
            this.prgProgressFiles.Maximum = 1000;
            this.prgProgressFiles.Name = "prgProgressFiles";
            this.prgProgressFiles.Size = new System.Drawing.Size(310, 23);
            this.prgProgressFiles.Step = 1;
            this.prgProgressFiles.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.prgProgressFiles.TabIndex = 0;
            // 
            // NKitForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(794, 465);
            this.Controls.Add(this.splitter);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "NKitForm";
            this.Text = "NKit Processing App";
            this.Load += new System.EventHandler(this.NKitForm_Load);
            this.splitter.Panel1.ResumeLayout(false);
            this.splitter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitter)).EndInit();
            this.splitter.ResumeLayout(false);
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            this.grpSettingsOutput.ResumeLayout(false);
            this.grpSettingsOutput.PerformLayout();
            this.grpSettingsSummaryLog.ResumeLayout(false);
            this.grpSettingsSummaryLog.PerformLayout();
            this.pnlProgress.ResumeLayout(false);
            this.pnlProgress.PerformLayout();
            this.grpProgress.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitter;
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Button btnSettingsProcess;
        private System.Windows.Forms.CheckBox chkSettingsFullVerify;
        private System.Windows.Forms.CheckBox chkSettingsReencodeNkit;
        private System.Windows.Forms.CheckBox chkSettingsTestMode;
        private System.Windows.Forms.CheckBox chkSettingsRemoveUpdate;
        private System.Windows.Forms.ComboBox cboSettingsMode;
        private System.Windows.Forms.ListView lvw;
        private System.Windows.Forms.ColumnHeader colSource;
        private System.Windows.Forms.ColumnHeader colMatch;
        private System.Windows.Forms.ColumnHeader colSize;
        private System.Windows.Forms.ColumnHeader colId;
        private System.Windows.Forms.ColumnHeader colCrc;
        private System.Windows.Forms.ColumnHeader colReadResult;
        private System.Windows.Forms.ColumnHeader colVerifyResult;
        private System.Windows.Forms.ColumnHeader colName;
        private System.Windows.Forms.ColumnHeader colSystem;
        private System.Windows.Forms.Panel pnlProgress;
        private System.Windows.Forms.GroupBox grpProgress;
        private System.Windows.Forms.Button btnProgressResume;
        private System.Windows.Forms.Button btnProgressStop;
        private TextProgressBar prgProgressStep;
        private TextProgressBar prgProgressFile;
        private TextProgressBar prgProgressFiles;
        private System.Windows.Forms.Label lblLog;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.CheckBox chkSettingsDeleteSource;
        private System.Windows.Forms.ToolTip tooltip;
        private System.Windows.Forms.CheckBox chkSettingsCalculateHashes;
        private System.Windows.Forms.GroupBox grpSettingsSummaryLog;
        private System.Windows.Forms.Button btnSettingsSummaryLog;
        private System.Windows.Forms.TextBox txtSettingsSummaryLog;
        private System.Windows.Forms.CheckBox chkSettingsSummaryLog;
        private System.Windows.Forms.SaveFileDialog dlgFile;
        private System.Windows.Forms.GroupBox grpSettingsOutput;
        private System.Windows.Forms.Button btnSettingsMasks;
        private System.Windows.Forms.Label lblSettingsTempPath;
        private System.Windows.Forms.Button btnSettingsTempPath;
        private System.Windows.Forms.TextBox txtSettingsTempPath;
        private System.Windows.Forms.Label lblSettingsOutputPathBase;
        private System.Windows.Forms.CheckBox chkSettingsUseMasks;
        private System.Windows.Forms.Button btnSettingsOutputPathBase;
        private System.Windows.Forms.TextBox txtSettingsOutputPathBase;
        private System.Windows.Forms.CheckBox chkSettingsRecoveryMatchFailDelete;
        private System.Windows.Forms.Button btnProgressSummaryLog;
        private System.Windows.Forms.Button btnSettingsReset;
        private System.Windows.Forms.OpenFileDialog dlgFolder;
        private System.Windows.Forms.Label lblSettingsTooltips;
    }
}