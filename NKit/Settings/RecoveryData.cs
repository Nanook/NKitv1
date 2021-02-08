using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Nanook.NKit
{
    public class RecoveryData
    {
        public List<FileItem> GcBinFiles { get; set; }
        public List<FileItem> GcNewBinFiles { get; set; }

        public List<Tuple<string, string, string, uint>> WiiUPartsData { get; private set; }
        public List<Tuple<string, string, string, uint>> WiiUOtherPartsData { get; private set; }
        public List<Tuple<string, string, string, uint>> WiiChanData { get; private set; }
        public List<Tuple<string, string, string, uint>> WiiOtherChanData { get; private set; }
        public List<Tuple<string, string, string, uint>> WiiDuplicatePartitions { get; private set; }

        public List<JunkRedumpPatch> JunkPatches { get; private set; }

        internal RecoveryData(Settings settings, ILog log, bool isGameCube, string id8)
        {
            JunkRedumpPatch[] junkPatches = settings.JunkRedumpPatches.Where(a => a.Id8 == id8).OrderBy(a => a.Offset).ToArray();

            log?.Log("RECOVERY DATA");
            log?.Log("-------------------------------------------------------------------------------");

            if (junkPatches != null)
            {
                JunkPatches = junkPatches.ToList();
                //if (JunkPatches.Count != 0)
                //    log?.Log(string.Format("{0, 4} Redump Junk patch{1} loaded", JunkPatches.Count.ToString(), JunkPatches.Count == 1 ? "" : "es"));
            }
            else
                JunkPatches = new List<JunkRedumpPatch>();

            if (!isGameCube)
            {
                WiiUPartsData = new List<Tuple<string, string, string, uint>>();
                WiiChanData = new List<Tuple<string, string, string, uint>>();
                WiiUOtherPartsData = new List<Tuple<string, string, string, uint>>();
                WiiOtherChanData = new List<Tuple<string, string, string, uint>>();

                int movedFiles = populatePartitions(settings.RecoveryFilesPath, WiiUPartsData, WiiChanData, settings.RedumpUpdateCrcs.Union(settings.RedumpChannelCrcs).ToArray(), settings.OtherRecoveryFilesPath);

                log?.Log(string.Format("[{0,4} Redump ] {1}", (WiiUPartsData.Count + WiiChanData.Count).ToString(), settings.RecoveryFilesPath));
                if (settings.OtherRecoveryFilesPath != null)
                {
                    populatePartitions(settings.OtherRecoveryFilesPath, WiiUOtherPartsData, WiiOtherChanData, new uint[0], null);
                    log?.Log(string.Format("[{0,4} Other  ] {1}", (WiiUOtherPartsData.Count + WiiOtherChanData.Count).ToString(), settings.OtherRecoveryFilesPath));
                }
                if (movedFiles != 0)
                {
                    log?.LogBlank();
                    log?.Log(string.Format("!! {0} file{1} in the Redump recovery folder moved to Other - check they are valid", movedFiles.ToString(), movedFiles == 1 ? "" : "s"));
                }
                else if (WiiUPartsData.Count + WiiChanData.Count + WiiUPartsData.Count + WiiOtherChanData.Count == 0)
                {
                    log?.LogBlank();
                    log?.Log("!! Add Recovery Partitions to help recover images");
                }
            }
            else
            {
                GcBinFiles = new List<FileItem>();
                int movedFiles = populateGcBinFiles(settings.RecoveryFilesPath, GcBinFiles, settings.RedumpFstCrcs.Union(settings.RedumpAppldrCrcs).ToArray(), settings.OtherRecoveryFilesPath);
                log?.Log(string.Format("[{0,4} Redump] {1}", (GcBinFiles.Count(a => a is ApploaderFileItem) + GcBinFiles.Count(a => a is FstFileItem)).ToString(), settings.RecoveryFilesPath));
                if (settings.OtherRecoveryFilesPath != null)
                {
                    GcNewBinFiles = new List<FileItem>();
                    populateGcBinFiles(settings.OtherRecoveryFilesPath, GcNewBinFiles, new uint[0], null);
                    log?.Log(string.Format("[{0,4} Other ] {1}",( GcNewBinFiles.Count(a => a is ApploaderFileItem) + GcNewBinFiles.Count(a => a is FstFileItem)).ToString(), settings.OtherRecoveryFilesPath));
                }
                if (movedFiles != 0)
                {
                    log?.LogBlank();
                    log?.Log(string.Format("!! {0} file{1} in the Redump recovery folder moved to Other - check they are valid", movedFiles.ToString(), movedFiles == 1 ? "" : "s"));
                }
                else if (GcBinFiles.Count + GcNewBinFiles.Count == 0)
                {
                    log?.LogBlank();
                    log?.Log("!! Add Recovery files to help recover images - GameCube relies on them heavily!");
                }
            }
            log?.LogBlank();
        }

        public static string GetUpdatePartition(Settings settings, uint crc)
        {
            Regex m = new Regex(@"[/|\\]([A-Z0-9]{40})_([A-Z]+)_" + crc.ToString("X8") + "$", RegexOptions.IgnoreCase);
            string fn = null;
            if (Directory.Exists(settings.RecoveryFilesPath) && (fn = Directory.GetFiles(settings.RecoveryFilesPath).FirstOrDefault(a => m.IsMatch(a))) != null)
                return fn;
            else if (Directory.Exists(settings.NkitRecoveryFilesPath) && (fn = Directory.GetFiles(settings.NkitRecoveryFilesPath).FirstOrDefault(a => m.IsMatch(a))) != null)
                return fn;
            else if (Directory.Exists(settings.OtherRecoveryFilesPath) && (fn = Directory.GetFiles(settings.OtherRecoveryFilesPath).FirstOrDefault(a => m.IsMatch(a))) != null)
                return fn;
            return fn;
        }

        private int populatePartitions(string partitionsPath, List<Tuple<string, string, string, uint>> uParts, List<Tuple<string, string, string, uint>> uChans, uint[] keepList, string moveFolder)
        {
            int movedFiles = 0;
            try
            {
                if (Directory.Exists(partitionsPath))
                {
                    Match m;
                    foreach (string f in Directory.EnumerateFiles(partitionsPath))
                    {
                        string fn = Path.GetFileName(f);
                        m = Regex.Match(fn, @"^([A-Z0-9_]{10})_([0-9]{2})_([A-Z0-9_]{4})_[A-Z]+_([A-Z0-9]{8})$", RegexOptions.IgnoreCase);
                        if (m.Success)
                        {
                            bool keep = keepList.Any(a => a == uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber));
                            bool dupe = uChans.Any(a => a.Item1.StartsWith(string.Format("{0}_{1}_{2}_", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)));

                            if (keep && !dupe)
                                uChans.Add(new Tuple<string, string, string, uint>(fn, m.Groups[1].Value, m.Groups[3].Value, uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber)));
                            else if (moveFolder != null)
                                movedFiles += moveToOther(f, moveFolder) ? 1 : 0;
                        }
                        else
                        {
                            m = Regex.Match(fn, @"^([A-Z0-9]{40})_([A-Z]+)_([A-Z0-9]{8})$", RegexOptions.IgnoreCase);
                            if (m.Success)
                            {
                                bool keep = keepList.Any(a => a == uint.Parse(m.Groups[3].Value, NumberStyles.HexNumber));
                                if (keep || moveFolder == null)
                                    uParts.Add(new Tuple<string, string, string, uint>(fn, m.Groups[1].Value, m.Groups[2].Value, uint.Parse(m.Groups[3].Value, NumberStyles.HexNumber)));
                                else if (moveFolder != null)
                                    movedFiles += moveToOther(f, moveFolder) ? 1 : 0;
                            }
                            else if (moveFolder != null)
                                movedFiles += moveToOther(f, moveFolder) ? 1 : 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "RecoveryData.populatePartitions");
            }
            return movedFiles;
        }

        private int populateGcBinFiles(string path, List<FileItem> files, uint[] keepList, string moveFolder)
        {
            int movedFiles = 0;

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return movedFiles;

            foreach (FileInfo fi in (new DirectoryInfo(path)).EnumerateFiles())
            {
                Match m = Regex.Match(fi.Name, @"^fst\[(.{10})\]\[(.{8})\]\[(.{8})\]\[(.{8})\]\.bin$");
                if (m.Success)
                {
                    bool keep = keepList.Any(a => a == uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber));
                    if (keep || moveFolder == null)
                        files.Add(new FstFileItem(fi.FullName, fi.Length, uint.Parse(m.Groups[3].Value, NumberStyles.HexNumber), m.Groups[1].Value, uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber), uint.Parse(m.Groups[4].Value, NumberStyles.HexNumber)));
                    else if (moveFolder != null)
                        movedFiles += moveToOther(fi.FullName, moveFolder) ? 1 : 0;
                }
                else
                {
                    m = Regex.Match(fi.Name, @"^appldr\[(.{8})\]\[(.{8})\].bin$");
                    if (m.Success)
                    {
                        bool keep = keepList.Any(a => a == uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber));
                        if (keep || moveFolder == null)
                            files.Add(new ApploaderFileItem(fi.FullName, fi.Length, uint.Parse(m.Groups[2].Value, NumberStyles.HexNumber)));
                        else if (moveFolder != null)
                            movedFiles += moveToOther(fi.FullName, moveFolder) ? 1 : 0;
                    }
                }
            }
            return movedFiles;
        }

        private bool moveToOther(string src, string dst)
        {
            try
            {
                dst = Path.Combine(dst, Path.GetFileName(src));
                File.Move(src, SourceFiles.GetUniqueName(dst));
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
