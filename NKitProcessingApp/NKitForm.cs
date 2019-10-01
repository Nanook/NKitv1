using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nanook.NKit
{
    public partial class NKitForm : Form
    {
        private enum State { New, Processing, Stopping, Stopped, Complete }

        private State _state;
        private ListViewItem _processingItem;
        private ListViewItem _dragDropItem;
        private int _processFilesCount;
        private int _processFileIndex;
        private Dictionary<string, string> _masks;
        private DateTime _startDate;

        public bool HasDragDropItem { get { return (lvw.Items.Count == 1 && lvw.Items[0] == _dragDropItem); } }

        public NKitForm()
        {
            _startDate = DateTime.MinValue;
            InitializeComponent();
            _masks = new Dictionary<string, string>() { { "GameCube:RedumpMatchRenameToMask", "" }, { "GameCube:CustomMatchRenameToMask", "" }, { "GameCube:MatchFailRenameToMask", "" }, { "Wii:RedumpMatchRenameToMask", "" }, { "Wii:CustomMatchRenameToMask", "" }, { "Wii:MatchFailRenameToMask", "" } };
            btnSettingsSummaryLog.Tag = txtSettingsSummaryLog;
            btnSettingsTempPath.Tag = txtSettingsTempPath;
            btnSettingsOutputPathBase.Tag = txtSettingsOutputPathBase;
            _state = State.New;
            _dragDropItem = lvw.Items[0];
        }

        private void NKitForm_Load(object sender, EventArgs e)
        {
            cboSettingsMode.SelectedIndex = 0;
            resetScreen();
            setScreenState();
        }

        private void NkitConvert_LogMessage(object sender, MessageEventArgs e)
        {
            if (_processingItem != null)
                this.Invoke((MethodInvoker)delegate { addToLog(e.Message); });
        }

        private void chkSettings_Checked(object sender, EventArgs e)
        {
            setScreenState();
        }

        private void btnSettingsMasks_Click(object sender, EventArgs e)
        {
            setPaths(txtSettingsOutputPathBase.Text);
            using (MasksForm frm = new MasksForm())
                frm.ShowDialogWithInitialise(this, _masks);
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            TextBox t = (TextBox)((Button)sender).Tag;

            try
            {
                if ((string)t.Tag == "folder")
                {
                    dlgFolder.Title = "Select Folder (Use any Filename)";
                    dlgFolder.CheckFileExists = false;
                    dlgFolder.ValidateNames = false;
                    try
                    {
                        if (!string.IsNullOrEmpty(t.Text) && Directory.Exists(t.Text))
                            dlgFolder.InitialDirectory = t.Text;
                    }
                    catch { }
                    dlgFolder.FileName = "anything";
                    if (dlgFolder.ShowDialog() == DialogResult.OK)
                        t.Text = Path.GetDirectoryName(dlgFolder.FileName);
                }
                else
                {
                    dlgFile.Title = "Select File";
                    dlgFile.CheckFileExists = false;
                    try
                    {
                        if (!string.IsNullOrEmpty(t.Text) && File.Exists(t.Text))
                            dlgFile.FileName = t.Text;
                    }
                    catch { }
                    if (dlgFile.ShowDialog() == DialogResult.OK)
                        dlgFile.FileName = t.Text;
                }
            }
            catch { }
        }

        private void lvw_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void lvw_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (SourceFile sf in SourceFiles.Scan(files, false))
                addItem(sf);
            setScreenState();
        }

        private void txt_DragEnter(object sender, DragEventArgs e)
        {
            TextBox t = (TextBox)sender;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void txt_DragDrop(object sender, DragEventArgs e)
        {
            TextBox t = (TextBox)sender;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string f in files)
            {
                try
                {
                    if ((string)t.Tag == "file" && File.Exists(f))
                    {
                        t.Text = f;
                        break;
                    }
                    else if ((string)t.Tag == "folder" && Directory.Exists(f))
                    {
                        t.Text = f;
                        if (t == txtSettingsOutputPathBase)
                            setPaths(t.Text); //update the output paths for GC and Wii
                        break;
                    }
                }
                catch { } //next item
            }
            setScreenState();
        }

        private void btnSettingsProcess_Click(object sender, EventArgs e)
        {
            ListViewItem[] items = itemsToProcess().ToArray();

            if (_state == State.New) //if not resuming
            {
                _processFileIndex = 0;
                _processFilesCount = items.Length;
            }

            setPaths(txtSettingsOutputPathBase.Text);

            _state = State.Processing;
            setScreenState();

            process(cboSettingsMode.SelectedIndex, items,
                item => //started item
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (lvw.SelectedItems.Count == 0 || lvw.SelectedItems[0] == _processingItem)
                            item.Selected = true; //move the logging on
                        _processingItem = item;
                    });
                },
                item => //completed item
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        _processFileIndex++;
                        OutputResults results = ((ProcessFile)item.Tag).Results;
                        if (results != null)
                        {
                            item.SubItems[1].Text = results.DiscType.ToString();
                            item.SubItems[2].Text = results.ValidateReadResult.ToString();
                            item.SubItems[3].Text = results.VerifyOutputResult.ToString();
                            item.SubItems[4].Text = results.OutputCrc.ToString("X8") ?? "";
                            item.SubItems[5].Text = results.OutputId4 ?? "";
                            item.SubItems[6].Text = (results.RedumpInfo?.MatchType.ToString() ?? "") + (results.IsRecoverable ? "Recoverable" : "");
                            item.SubItems[7].Text = (results.OutputSize / (double)(1024 * 1024)).ToString("#.0") + " MiB";
                            item.SubItems[8].Text = results.RedumpInfo?.MatchName ?? "";
                        }
                        else
                            item.SubItems[1].Text = "Error";
                    });
                    return _state != State.Stopping; //stop
                }).ContinueWith(
                t => //finished processing
                {
                    _state = _processFileIndex == _processFilesCount ? State.Complete : State.Stopped;
                    setScreenState();
                });
        }
        private void btnProgressResume_Click(object sender, EventArgs e)
        {
            if (_state == State.Stopping)
                _state = State.Processing;
            else if (_state == State.Stopped)
                btnSettingsProcess_Click(this, new EventArgs());
            setScreenState();
        }

        private void btnProgressStop_Click(object sender, EventArgs e)
        {
            if (_state == State.Processing)
            {
                _state = State.Stopping;
                setScreenState();
            }
            else
                btnSettingsReset_Click(btnSettingsReset, new EventArgs());
        }

        private void btnSettingsReset_Click(object sender, EventArgs e)
        {
            _state = State.New;
            lvw.Items.Clear();
            lvw.Items.Add(_dragDropItem);
            txtLog.Text = "";
            cboSettingsMode_SelectedIndexChanged(this, new EventArgs());
            setScreenState();
        }

        private void cboSettingsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (cboSettingsMode.SelectedIndex)
            {
                case 0: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"Select the conversion type (tooltip updates on change). All conversions use the NKit.dll directly, no exes are used."); 
                    break;
                case 1: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"RecoverToISO: Repair and rebuild images to match Redump as ISO");
                    break;
                case 2: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"RecoverToNKit ISO: Repair and rebuild images to match Redump and convert to nkit.iso");
                    break;
                case 3: //Recover to Nkit.gcz
                    tooltip.SetToolTip(cboSettingsMode, @"RecoverToNKit GCZ: Repair and rebuild images to match Redump and convert to nkit.gcz");
                    break;
                case 4: //Convert to .iso
                    tooltip.SetToolTip(cboSettingsMode, @"ConvertToISO: Convert anything to a full size ISO. Nothing is modified. Scrubbing and changes are preserved");
                    break;
                case 5: //Convert to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"ConvertToNKit ISO: Convert source images to NKit.iso. This removes all junk/encryption/hashes and compacts the file system. Playable in Dolphin");
                    break;
                case 6: //Convert to Nkit.gcz
                    tooltip.SetToolTip(cboSettingsMode, @"ConvertToNKit GCZ: Convert source images to nkit.gcz. This removes all junk/encryption/hashes and compacts the file system. Playable in Dolphin");
                    break;
            }
            setScreenState();
        }

        private void lvw_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!this.HasDragDropItem && lvw.SelectedItems.Count != 0)
                txtLog.Text = ((ProcessFile)lvw.SelectedItems[0].Tag).Log;
        }

        private ProcessFile addItem(SourceFile sourceFile)
        {
            ProcessFile pf = new ProcessFile() { SourceFile = sourceFile };
            ListViewItem li = new ListViewItem(pf.SourceFile.Name) { Tag = pf };
            for (int i = 0; i < lvw.Columns.Count - 1; i++)
                li.SubItems.Add("");

            if (this.HasDragDropItem)
                lvw.Items.Clear();

            lvw.Items.Add(li);
            return pf;
        }

        private IEnumerable<ListViewItem> itemsToProcess()
        {
            if (!this.HasDragDropItem)
            {
                foreach (ListViewItem li in lvw.Items)
                {
                    if (((ProcessFile)li.Tag).Results == null)
                        yield return li;
                }
            }
        }

        private void resetScreen()
        {
            Settings settings = new Settings(DiscType.GameCube);
            txtSettingsOutputPathBase.Text = settings.Path;
            txtSettingsSummaryLog.Text = settings.SummaryLog;
            txtSettingsTempPath.Text = settings.TempPath;
            chkSettingsCalculateHashes.Checked = settings.CalculateHashes;
            chkSettingsDeleteSource.Checked = settings.DeleteSource;
            chkSettingsSummaryLog.Checked = settings.EnableSummaryLog;
            chkSettingsFullVerify.Checked = settings.FullVerify;
            chkSettingsReencodeNkit.Checked = settings.NkitReencode;
            chkSettingsRemoveUpdate.Checked = settings.NkitUpdatePartitionRemoval;
            chkSettingsTestMode.Checked = settings.TestMode;
            chkSettingsUseMasks.Checked = settings.MaskRename;
            chkSettingsRecoveryMatchFailDelete.Checked = settings.RecoveryMatchFailDelete;
            setPaths(settings.Path);
        }

        private void setPaths(string path)
        {
            Settings settings = new Settings(DiscType.GameCube, path, false);
            _masks["GameCube:RedumpMatchRenameToMask"] = settings.RedumpMatchRenameToMask;
            _masks["GameCube:CustomMatchRenameToMask"] = settings.CustomMatchRenameToMask;
            _masks["GameCube:MatchFailRenameToMask"] = settings.MatchFailRenameToMask;
            settings = new Settings(DiscType.Wii, path, false);
            _masks["Wii:RedumpMatchRenameToMask"] = settings.RedumpMatchRenameToMask;
            _masks["Wii:CustomMatchRenameToMask"] = settings.CustomMatchRenameToMask;
            _masks["Wii:MatchFailRenameToMask"] = settings.MatchFailRenameToMask;
        }

        private void setScreenState()
        {
            this.Invoke((MethodInvoker)delegate
            {
                lvw.AllowDrop = _state == State.New;
                grpSettings.Visible = _state == State.New;
                pnlProgress.Visible = _state != State.New;
                btnSettingsProcess.Enabled = cboSettingsMode.SelectedIndex > 0 && itemsToProcess().Count() != 0;
                btnProgressResume.Visible = _state == State.Stopping || _state == State.Stopped;
                btnProgressStop.Enabled = _state != State.Stopping;
                string[] bn = ((string)btnProgressStop.Tag).Split('|');
                btnProgressStop.Text = bn[_state == State.Stopping ? 1 : (_state == State.Complete || _state == State.Stopped ? 2 : 0)];
                chkSettingsDeleteSource.Enabled = chkSettingsFullVerify.Checked && !chkSettingsTestMode.Checked;
                btnSettingsOutputPathBase.Enabled = chkSettingsUseMasks.Checked;
                txtSettingsOutputPathBase.Enabled = chkSettingsUseMasks.Checked;
                lblSettingsOutputPathBase.Enabled = chkSettingsUseMasks.Checked;
                btnSettingsMasks.Enabled = chkSettingsUseMasks.Checked;
                chkSettingsReencodeNkit.Enabled = cboSettingsMode.SelectedIndex != 1 && cboSettingsMode.SelectedIndex != 4;
                chkSettingsRecoveryMatchFailDelete.Enabled = cboSettingsMode.SelectedIndex >= 1 && cboSettingsMode.SelectedIndex <= 3;
                txtSettingsSummaryLog.Enabled = chkSettingsSummaryLog.Checked;
                btnSettingsSummaryLog.Enabled = chkSettingsSummaryLog.Checked;

                if (_state == State.New)
                {
                    prgProgressFiles.CustomText = "";
                    prgProgressFile.CustomText = "";
                    prgProgressStep.CustomText = "";
                }
            });
        }

        private Task process(int convertMode, ListViewItem[] items, Action<ListViewItem> startItem, Func<ListViewItem, bool> completedItem)
        {
            //get the settings on the main thread to be used on another thread
            var guiSettings = new
            {
                SummaryLog = txtSettingsSummaryLog.Text,
                TempPath = txtSettingsTempPath.Text,
                CalculateHashes = chkSettingsCalculateHashes.Checked,
                DeleteSource = chkSettingsDeleteSource.Checked,
                EnableSummaryLog = chkSettingsSummaryLog.Checked,
                FullVerify = chkSettingsFullVerify.Checked,
                NkitReencode = chkSettingsReencodeNkit.Checked,
                NkitUpdatePartitionRemoval = chkSettingsRemoveUpdate.Checked,
                TestMode = chkSettingsTestMode.Checked,
                MaskRename = chkSettingsUseMasks.Checked,
                RecoveryMatchFailDelete = chkSettingsRecoveryMatchFailDelete.Checked,
                Masks = _masks
            };

            return Task.Run(() =>
            {
                foreach (ListViewItem item in items)
                {
                    Converter nkitConvert = null;
                    ProcessFile pf = (ProcessFile)item.Tag;
                    try
                    {
                        SourceFile src = pf.SourceFile;
                        nkitConvert = new Converter(src, false);
                        startItem(item);
                        nkitConvert.Settings.SummaryLog = guiSettings.SummaryLog;
                        nkitConvert.Settings.TempPath = guiSettings.TempPath;
                        nkitConvert.Settings.CalculateHashes = guiSettings.CalculateHashes;
                        nkitConvert.Settings.DeleteSource = guiSettings.DeleteSource;
                        nkitConvert.Settings.EnableSummaryLog = guiSettings.EnableSummaryLog;
                        nkitConvert.Settings.FullVerify = guiSettings.FullVerify;
                        nkitConvert.Settings.NkitReencode = guiSettings.NkitReencode;
                        nkitConvert.Settings.NkitUpdatePartitionRemoval = guiSettings.NkitUpdatePartitionRemoval;
                        nkitConvert.Settings.TestMode = guiSettings.TestMode;
                        nkitConvert.Settings.MaskRename = guiSettings.MaskRename;
                        nkitConvert.Settings.RecoveryMatchFailDelete = guiSettings.RecoveryMatchFailDelete;

                        if (nkitConvert.Settings.DiscType == DiscType.GameCube)
                        {
                            nkitConvert.Settings.RedumpMatchRenameToMask = guiSettings.Masks["GameCube:RedumpMatchRenameToMask"];
                            nkitConvert.Settings.CustomMatchRenameToMask = guiSettings.Masks["GameCube:CustomMatchRenameToMask"];
                            nkitConvert.Settings.MatchFailRenameToMask = guiSettings.Masks["GameCube:MatchFailRenameToMask"];
                        }
                        else
                        {
                            nkitConvert.Settings.RedumpMatchRenameToMask = guiSettings.Masks["Wii:RedumpMatchRenameToMask"];
                            nkitConvert.Settings.CustomMatchRenameToMask = guiSettings.Masks["Wii:CustomMatchRenameToMask"];
                            nkitConvert.Settings.MatchFailRenameToMask = guiSettings.Masks["Wii:MatchFailRenameToMask"];
                        }

                        nkitConvert.LogMessage += NkitConvert_LogMessage;
                        nkitConvert.LogProgress += NkitConvert_LogProgress;
                        pf.Log = "";

                        switch (convertMode)
                        {
                            case 1: //Recover to ISO
                                pf.Results = nkitConvert.RecoverToIso();
                                break;
                            case 2: //Recover to Nkit.iso
                                nkitConvert.Settings.NkitFormat = NkitFormatType.Iso;
                                pf.Results = nkitConvert.RecoverToNkit();
                                break;
                            case 3: //Recover to Nkit.gcz
                                nkitConvert.Settings.NkitFormat = NkitFormatType.Gcz;
                                pf.Results = nkitConvert.RecoverToNkit();
                                break;
                            case 4: //Convert to .iso
                                pf.Results = nkitConvert.ConvertToIso();
                                break;
                            case 5: //Convert to Nkit.iso
                                nkitConvert.Settings.NkitFormat = NkitFormatType.Iso;
                                pf.Results = nkitConvert.ConvertToNkit();
                                break;
                            case 6: //Convert to Nkit.gcz
                                nkitConvert.Settings.NkitFormat = NkitFormatType.Gcz;
                                pf.Results = nkitConvert.ConvertToNkit();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        pf.Log += ex.Message;
                    }
                    finally
                    {
                        if (nkitConvert != null)
                        {
                            nkitConvert.LogMessage -= NkitConvert_LogMessage;
                            nkitConvert.LogProgress -= NkitConvert_LogProgress;
                        }
                    }
                    if (!completedItem(item))
                        break;
                }
            });
        }

        private void addToLog(string msg)
        {
            ((ProcessFile)_processingItem.Tag).Log += msg + Environment.NewLine;
            setLog(((ProcessFile)_processingItem.Tag).Log);
        }
        private void setLog(string log)
        {
            if (lvw.SelectedItems.Count != 0 && lvw.SelectedItems[0] == _processingItem)
            {
                txtLog.Text = log;
                int len = log.Length;
                txtLog.SelectionStart = log.Length;
                txtLog.SelectionLength = 0;
                txtLog.ScrollToCaret();
            }
        }

        //##### Events subscribed to the NKit convert
        private void NkitConvert_LogProgress(object sender, ProgressEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                try
                {
                    if (e.IsStart)
                    {
                        _startDate = DateTime.Now;
                        addToLog(e.StartMessage + "...");
                        prgProgressFiles.CustomText = string.Format("{0}: File {1} of {2}", ((Converter)sender).ConvertionName, (_processFileIndex + 1).ToString(), _processFilesCount.ToString());
                        prgProgressFile.CustomText = ((ProcessFile)_processingItem.Tag).SourceFile.Name;
                        prgProgressStep.CustomText = e.StartMessage;
                    }
                    if (e.IsComplete)
                    {
                        TimeSpan ts = DateTime.Now - _startDate;
                        StringBuilder sb = new StringBuilder(50);
                        sb.AppendFormat(":    1.2.3.4.5.6.7.8.9.10 ~{0,2}m {1,2:D2}s", ((int)ts.TotalMinutes).ToString(), ts.Seconds.ToString());
                        _startDate = DateTime.MinValue; //reset

                        if (e.Size != 0)
                            sb.AppendFormat("  [MiB:{0,7:#####.0}]", (e.Size / (double)(1024 * 1024)));
                        else if (e.CompleteMessage != null)
                            sb.Append("               ");

                        if (e.CompleteMessage != null)
                            sb.AppendFormat("  {0}", e.CompleteMessage);
                        ((ProcessFile)_processingItem.Tag).Log = Regex.Replace(((ProcessFile)_processingItem.Tag).Log, @"^(.*[^\.])\.\.\.(\r?\n.*?)$", string.Concat("$1", sb.ToString(), "$2"), RegexOptions.Singleline);
                        setLog(((ProcessFile)_processingItem.Tag).Log);
                    }
                    prgProgressStep.Value = Math.Min(1000, (int)(e.Progress * 1000F));
                    prgProgressFile.Value = Math.Min(1000, (int)(e.TotalProgress * 1000F));
                    prgProgressFiles.Value = Math.Min(1000, (int)((((double)_processFileIndex + e.TotalProgress) / (double)lvw.Items.Count) * 1000F));
                }
                catch { }
            });
        }

        private void btnProgressSummaryLog_Click(object sender, EventArgs e)
        {
            if (File.Exists(txtSettingsSummaryLog.Text))
            {
                try
                {
                    Process.Start(txtSettingsSummaryLog.Text);
                }
                catch { }
            }
            else
                MessageBox.Show(this, string.Format("Summary Log does not exist!{0}{0}{1}", Environment.NewLine, txtSettingsSummaryLog.Text), "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }
}
