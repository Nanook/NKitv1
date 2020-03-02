using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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
        private int _processFilesCount;
        private int _processFileIndex;
        private ListViewItem _dragDropItem;
        public bool HasDragDropItem { get { return (lvw.Items.Count == 1 && lvw.Items[0] == _dragDropItem); } }

        public NKitForm()
        {
            InitializeComponent();
            _state = State.New;
            _dragDropItem = lvw.Items[0];
            btnSettingsPath.Tag = txtSettingsPath;

            var ms = new[]
            {
                new { Name = "Everything", Regex = @".*" },
                new { Name = "All opening banners", Regex = @"opening\.bnr$" },
                new { Name = "All dol files", Regex = @"\.dol$" },
                new { Name = "Everything in a root folder (video)", Regex = @"^/video/.*" },
                new { Name = "Multiple file types (bnr and bik files)", Regex = @"\.(bnr|bik)$" }
            };
            foreach (var m in ms)
            {
                ToolStripMenuItem mi = new ToolStripMenuItem(m.Name) { Tag = m.Regex };
                mnuSettingsRegex.Items.Add(mi);
                mi.Click += (s, e) => { txtSettingsRegex.Text = (string)((ToolStripMenuItem)s).Tag; };
            }
        }

        private void NKitForm_Load(object sender, EventArgs e)
        {
            cboSettingsMode.SelectedIndex = 0;
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
                        break;
                    }
                }
                catch { } //next item
            }
            setScreenState();
        }
        private void cboSettingsMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            lblProgressStatus.Text = ((string)lblProgressStatus.Tag).Split('|')[cboSettingsMode.SelectedIndex];

            switch (cboSettingsMode.SelectedIndex)
            {
                case 0: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"Select the mode (tooltip updates on change). All conversions use the NKit.dll directly, no exes are used.");
                    break;
                case 1: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"Extracts recovery files used when Recovering (repair and rebuild) images to match Redump.");
                    break;
                case 2: //Recover to Nkit.iso
                    tooltip.SetToolTip(cboSettingsMode, @"Extracts image file system files to the Output Path. Output files can be filtered with a Regex.");
                    break;
                case 3: //Recover to Nkit.gcz
                    tooltip.SetToolTip(cboSettingsMode, @"Scan the info to populate the details in the file list");
                    break;
            }
            setScreenState();
        }

        private void chkSettingsSystemFiles_CheckedChanged(object sender, EventArgs e)
        {
            setScreenState();
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
                    //dlgFile.Title = "Select File";
                    //dlgFile.CheckFileExists = false;
                    //try
                    //{
                    //    if (!string.IsNullOrEmpty(t.Text) && File.Exists(t.Text))
                    //        dlgFile.FileName = t.Text;
                    //}
                    //catch { }
                    //if (dlgFile.ShowDialog() == DialogResult.OK)
                    //    dlgFile.FileName = t.Text;
                }
            }
            catch { }
        }
        private void btnSettingsProcess_Click(object sender, EventArgs e)
        {
            if (txtSettingsRegex.Enabled && txtSettingsRegex.Text != "")
            {
                try
                {
                    Regex rx = new Regex(txtSettingsRegex.Text, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Invalid Filter Regex: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }

            ListViewItem[] items = itemsToProcess().ToArray();

            if (_state == State.New) //if not resuming
            {
                _processFileIndex = 0;
                _processFilesCount = items.Length;
                prgProgressFiles.Maximum = _processFilesCount;
            }

            _state = State.Processing;
            setScreenState();

            process(cboSettingsMode.SelectedIndex, txtSettingsRegex.Text, txtSettingsPath.Text, items,
                item => //started item
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        if (lvw.SelectedItems.Count == 0 || lvw.SelectedItems[0] == _processingItem)
                            item.Selected = true; //move the logging on
                        _processingItem = item;
                        prgProgressFiles.CustomText = ((ProcessFile)item.Tag).SourceFile.Name;
                        prgProgressFiles.Refresh();
                    });
                },
                item => //completed item
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        _processFileIndex++;
                        prgProgressFiles.Value = _processFileIndex;
                        ExtractResult results = ((ProcessFile)item.Tag).Results;
                        if (results != null)
                        {
                            item.SubItems[1].Text = results.DiscType.ToString();
                            item.SubItems[2].Text = results.Id?.ToString();
                            item.SubItems[3].Text = results.Title?.ToString();
                            item.SubItems[4].Text = results.Region.ToString() ?? "";
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
                _state = State.Stopping;
            else
            {
                _state = State.New;
                lvw.Items.Clear();
                lvw.Items.Add(_dragDropItem);
                txtLog.Text = "";
            }
            setScreenState();
        }

        private void lvw_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvw.SelectedItems.Count != 0)
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
            if (lvw.Items.Count != 1 || lvw.Items[0] != _dragDropItem)
            {
                foreach (ListViewItem li in lvw.Items)
                {
                    if (((ProcessFile)li.Tag).Results == null)
                        yield return li;
                }
            }
        }

        private void setScreenState()
        {
            this.Invoke((MethodInvoker)delegate
            {
                lvw.AllowDrop = _state == State.New;
                grpSettings.Visible = _state == State.New;
                grpProgress.Visible = _state != State.New;
                btnSettingsProcess.Enabled = cboSettingsMode.SelectedIndex > 0 && itemsToProcess().Count() != 0;
                btnProgressResume.Visible = _state == State.Stopping || _state == State.Stopped;
                btnProgressStop.Enabled = _state != State.Stopping;
                txtSettingsPath.Enabled = _state == State.New && cboSettingsMode.SelectedIndex == 2;
                btnSettingsPath.Enabled = txtSettingsPath.Enabled;
                txtSettingsRegex.Enabled = txtSettingsPath.Enabled && !chkSettingsSystemFiles.Checked;
                btnSettingsRegex.Enabled = txtSettingsRegex.Enabled;
                chkSettingsSystemFiles.Enabled = txtSettingsPath.Enabled;
                btnProgressCopy.Enabled = !this.HasDragDropItem;
                string[] bn = ((string)btnProgressStop.Tag).Split('|');
                btnProgressStop.Text = bn[_state == State.Stopping ? 1 : (_state == State.Complete || _state == State.Stopped ? 2 : 0)];
                if (_state == State.New)
                    prgProgressFiles.CustomText = "";
            });
        }

        private Task process(int mode, string regex, string path, ListViewItem[] items, Action<ListViewItem> startItem, Func<ListViewItem, bool> completedItem)
        {
            var guiSettings = new
            {
                Regex = txtSettingsRegex.Text,
                SystemOnly = chkSettingsSystemFiles.Checked
            };

            return Task.Run(() =>
            {
                foreach (ListViewItem item in items)
                {
                    ProcessFile pf = (ProcessFile)item.Tag;
                    SourceFile src = pf.SourceFile;
                    Converter nkitConvert = new Converter(src, false);
                    startItem(item);

                    try
                    {
                        nkitConvert.LogMessage += NkitConvert_LogMessage;

                        int fileTotalLen = src.TotalFiles.ToString().Length;

                        using (NDisc dx = new NDisc(nkitConvert, src.Name))
                        {
                            if (dx != null)
                            {
                                try
                                {
                                    switch (mode)
                                    {
                                        case 1:
                                            pf.Results = dx.ExtractRecoveryFiles();
                                            break;
                                        case 2:
                                            Regex rx = new Regex(guiSettings.Regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                            pf.Results = dx.ExtractFiles(
                                                /*test*/   f => (guiSettings.SystemOnly && (f.Type == ExtractedFileType.System || f.Type == ExtractedFileType.WiiDiscItem)) || (!guiSettings.SystemOnly && rx.IsMatch(string.Format("{0}/{1}", f.Path, f.Name))),
                                                /*extract*/(s, f) => saveFile(path, pf.SourceFile.Name, f, s));
                                            break;
                                        case 3:
                                            pf.Results = dx.ExtractBasicInfo();
                                            break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    this.Invoke((MethodInvoker)delegate { addToLog(ex.Message); });
                                }
                                finally
                                {
                                }
                            }
                        }
                    }
                    finally
                    {
                        nkitConvert.LogMessage -= NkitConvert_LogMessage;
                    }
                    if (!completedItem(item))
                        break;
                }
            });
        }

        private void saveFile(string path, string imageName, ExtractedFile f, Stream s)
        {

            path = Path.Combine(path, f.DiscType.ToString(), imageName + "_" + f.DiscId8);
            if (f.PartitionId != null)
                path = Path.Combine(path, f.PartitionId);
            path = Path.Combine(path, SourceFiles.PathFix(f.Path.TrimStart('/')));
            Directory.CreateDirectory(path);

            using (Stream b = File.OpenWrite(Path.Combine(path, f.Name)))
                s.Copy(b, f.Length);
        }

        private void addToLog(string msg)
        {
            ((ProcessFile)_processingItem.Tag).Log += msg + Environment.NewLine;
            if (lvw.SelectedItems.Count != 0 && lvw.SelectedItems[0] == _processingItem)
            {
                txtLog.Text = ((ProcessFile)_processingItem.Tag).Log;
                int len = txtLog.Text.Length;
                txtLog.SelectionStart = txtLog.Text.Length;
                txtLog.SelectionLength = 0;
                txtLog.ScrollToCaret();
            }
        }

        private void NkitConvert_LogMessage(object sender, MessageEventArgs e)
        {
            if (_processingItem != null)
                this.Invoke((MethodInvoker)delegate { addToLog(e.Message); });
        }

        private void btnSettingsRegex_Click(object sender, EventArgs e)
        {
            Point screenPoint = btnSettingsRegex.PointToScreen(new Point(btnSettingsRegex.Left, btnSettingsRegex.Bottom));
            if (screenPoint.Y + mnuSettingsRegex.Size.Height > Screen.PrimaryScreen.WorkingArea.Height)
                mnuSettingsRegex.Show(btnSettingsRegex, new Point(0, -mnuSettingsRegex.Size.Height));
            else
                mnuSettingsRegex.Show(btnSettingsRegex, new Point(0, btnSettingsRegex.Height));
        }

        private void btnProgressCopy_Click(object sender, EventArgs e)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                if (!this.HasDragDropItem)
                {
                    for (int i = 0; i < lvw.Columns.Count; i++)
                        sb.Append((i == 0 ? "" : "\t") + lvw.Columns[i].Text);
                    sb.AppendLine();
                    foreach (ListViewItem li in lvw.Items)
                    {
                        for (int i = 0; i < li.SubItems.Count; i++)
                            sb.Append((i == 0 ? "" : "\t") + li.SubItems[i].Text);
                        sb.AppendLine();
                    }
                    Clipboard.SetText(sb.ToString());
                    MessageBox.Show(this, "Result List Copied to Clipboard (Tab Separated)", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
        }
    }
}
