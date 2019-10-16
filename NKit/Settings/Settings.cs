using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public enum NkitFormatType { Iso, Gcz }

    public class Settings
    {
        private static string _exePath;

        public static string ExeName { get; private set; }

        public static bool ConfigFileFound { get; private set; }
        public static string HomePath { get; private set; }
        public string Path { get; set; }
        public string TempPath { get; set; }
        public bool EnableSummaryLog { get; set; }
        public string SummaryLog { get; set; }
        public bool FullVerify { get; set; }
        public bool CalculateHashes { get; set; }
        public bool DeleteSource { get; set; }
        public int OutputLevel { get; set; }
        public bool TestMode { get; set; }
        public bool MaskRename { get; set; }
        public NkitFormatType NkitFormat { get; set; }
        public bool NkitReencode { get; set; }
        public bool NkitUpdatePartitionRemoval { get; set; }

        public bool RecoveryMatchFailDelete { get; set; }
        public string MatchFailRenameToMask { get; set; }

        public string DatPathRedumpMask { get; set; }
        public string DatPathCustomMask { get; set; }
        public string DatPathNameGameTdbMask { get; set; }
        public string DatPathRedump { get; set; }
        public string DatPathCustom { get; set; }
        public string DatPathNameGameTdb { get; set; }

        public string RecoveryFilesPath { get; set; }
        public string OtherRecoveryFilesPath { get; set; }
        public string NkitRecoveryFilesPath { get; set; }
        public string RedumpMatchRenameToMask { get; set; }
        public string CustomMatchRenameToMask { get; set; }

        public uint[] RedumpFstCrcs { get; set; }
        public uint[] RedumpAppldrCrcs { get; set; }
        public uint[] RedumpUpdateCrcs { get; set; }
        public uint[] RedumpChannelCrcs { get; set; }
        public Tuple<string, int>[] RedumpChannels { get; set; }
        public Tuple<byte[], int[]>[] RedumpRegionData { get; set; }

        public JunkIdSubstitution[] JunkIdSubstitutions { get; set; }
        public JunkStartOffset[] JunkStartOffsets { get; set; }
        public JunkRedumpPatch[] JunkRedumpPatches { get; set; }
        public Tuple<string, long>[] PreserveFstFileAlignment { get; set; }

        private static AppSettingsSection _appSettings;

        public DiscType DiscType { get; }

        public void CreatePaths()
        {
            createPaths(DatPathNameGameTdbMask, DatPathRedumpMask, DatPathCustomMask);
        }

        private void createPaths(params string[] paths)
        {
            foreach (string p in paths)
                createPath(p, true);
            createPath(this.TempPath, false);
            createPath(this.RecoveryFilesPath, false);
            createPath(this.OtherRecoveryFilesPath, false);
        }

        private void createPath(string path, bool removeName)
        {
            if (path == null)
                return;
            try
            {
                path = removeName ? System.IO.Path.GetDirectoryName(path) : path;
                if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch { }
        }

        static Settings()
        {
            try
            {
                try
                {
                    HomePath = (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                            ? Environment.GetEnvironmentVariable("HOME") : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                }
                catch { }

                string exe = Assembly.GetEntryAssembly().Location;
                _exePath = System.IO.Path.GetDirectoryName(exe);
                ExeName = System.IO.Path.GetFileNameWithoutExtension(exe); //get correct filename casing
                try
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                    ConfigFileFound = config.HasFile;
                }
                catch { }
            }
            catch { }
        }

        private Settings()
        {
        }

        public static string Read(string name, string defValue)
        {
            try
            {
                if (_appSettings == null)
                {
                    Configuration config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                    ConfigFileFound = config.HasFile;
                    //string sectionName = System.IO.Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                    string key = config?.Sections?.Keys?.Cast<string>().FirstOrDefault(a => string.Compare(a, "appSettings" /*ExeName*/, true) == 0);
                    _appSettings = key == null ? null : (AppSettingsSection)config?.GetSection(key);
                }
                if (_appSettings != null)
                    defValue = _appSettings?.Settings[name]?.Value ?? defValue;
            }
            catch { }
            return pathFix(defValue)?.Replace(" % exe", _exePath);
        }

        public Settings(DiscType type) : this(type, null, true)
        {
        }

        public Settings(DiscType type, string overridePath, bool createPaths)
        {
            this.DiscType = type;
            string setting = "Opening File";
            Configuration config;
            try
            {
                config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                AppSettingsSection s = config == null ? null : (AppSettingsSection)config.GetSection("appSettings");

                string path = overridePath ?? get(s, setting = "Path", true, @"%exe\Processed");
                if (string.IsNullOrEmpty(path))
                    Path = Environment.CurrentDirectory;
                else
                    Path = (new DirectoryInfo(path)).FullName;

                int ol;
                if (!int.TryParse(get(s, setting = "OutputLevel", false, "2"), out ol))
                    ol = 1;
                OutputLevel = ol;

                EnableSummaryLog = get(s, setting = "EnableSummaryLog", false, ConfigFileFound ? "true" : "false") == "true";
                RecoveryMatchFailDelete = get(s, setting = "RecoveryMatchFailDelete", false, ConfigFileFound ? "true" : "false") == "true";
                FullVerify = get(s, setting = "FullVerify", false, "true") == "true";
                CalculateHashes = get(s, setting = "CalculateHashes", false, "false") == "true";
                DeleteSource = get(s, setting = "DeleteSource", false, "false") == "true";
                TestMode = get(s, setting = "TestMode", false, "false") == "true";
                MaskRename = get(s, setting = "MaskRename", false, "true") == "true";
                string nkitFormat = get(s, setting = "NkitFormat", false, "iso"); //nkit format is experimental - undocumented
                NkitFormat = nkitFormat == "gcz" ? NkitFormatType.Gcz : NkitFormatType.Iso;

                NkitReencode = get(s, setting = "NkitReencode", false, "false") == "true";

                s = get(config, "junkIdSubstitutions");
                JunkIdSubstitutions = get(s, () => s?.Settings?.AllKeys?.Select(a => s.Settings[a])?.Select(a => new JunkIdSubstitution(a.Key, a.Value))?.ToArray(), new JunkIdSubstitution[0]);

                s = get(config, "junkStartOffset");
                JunkStartOffsets = get(s, () => s?.Settings?.AllKeys?.Select(a => s.Settings[a])?.Select(a => new JunkStartOffset(a.Key, long.Parse(a.Value, NumberStyles.HexNumber)))?.ToArray(), new JunkStartOffset[0]);

                s = get(config, "junkRedumpPatch");
                JunkRedumpPatches = get(s, () => s?.Settings?.AllKeys?.Select(a => s.Settings[a])?.Select(a => new JunkRedumpPatch(a.Key.Split('_')[0], long.Parse(a.Key.Split('_')[1], NumberStyles.HexNumber), Enumerable.Range(0, a.Value.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(a.Value.Substring(x, 2), 16)).ToArray()))?.ToArray(), new JunkRedumpPatch[0]);

                s = get(config, "preserveFstFileAlignment");
                PreserveFstFileAlignment = get(s, () => s?.Settings?.AllKeys?.Select(a => s.Settings[a])?.Select(a => new Tuple<string, long>(a.Key, long.Parse(a.Value, NumberStyles.HexNumber))).ToArray(), new Tuple<string, long>[0]);

                if (this.DiscType == DiscType.GameCube)
                {
                    s = get(config, "gamecube");
                    RedumpFstCrcs = get(s, setting = "RedumpFstCrcs", false, "")?.Split(',')?.Where(a => !string.IsNullOrEmpty(a)).Select(a => uint.Parse(a, NumberStyles.HexNumber)).ToArray() ?? new uint[0];
                    RedumpAppldrCrcs = get(s, setting = "RedumpAppldrCrcs", false, "")?.Split(',')?.Where(a => !string.IsNullOrEmpty(a)).Select(a => uint.Parse(a, NumberStyles.HexNumber)).ToArray() ?? new uint[0];
                }
                else
                {
                    s = get(config, "wii");
                    RedumpUpdateCrcs = get(s, setting = "RedumpUpdateCrcs", false, "")?.Split(',')?.Where(a => !string.IsNullOrEmpty(a))?.Select(a => uint.Parse(a, NumberStyles.HexNumber)).ToArray() ?? new uint[0];
                    RedumpChannelCrcs = get(s, setting = "RedumpChannelCrcs", false, "")?.Split(',')?.Where(a => !string.IsNullOrEmpty(a))?.Select(a => uint.Parse(a, NumberStyles.HexNumber)).ToArray() ?? new uint[0];
                    RedumpChannels = get(s, setting = "RedumpChannels", false, "")?.Split(',')?.Where(a => a != null)?.Where(a => !string.IsNullOrEmpty(a))?.Select(a => a.Split('_')).Select(a => new Tuple<string, int>(a[0], int.Parse(a[1]))).ToArray() ?? new Tuple<string, int>[0];
                    RedumpRegionData = get(s, setting = "RedumpRegionData", false, "")?.Split(',')?.Where(a => !string.IsNullOrEmpty(a))?.Select(a => a.Split('_'))?.Select(a => new Tuple<byte[], int[]>(Enumerable.Range(0, a[0].Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(a[0].Substring(x, 2), 16)).ToArray(), a[1].Split(':').Select(b => int.Parse(b)).ToArray()))?.ToArray() ?? new Tuple<byte[], int[]>[0];

                    NkitUpdatePartitionRemoval = get(s, setting = "NkitUpdatePartitionRemoval", false, "false") == "true";
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "Settings - Error loading setting '{0}'", setting);
            }
            setPath(this.Path, config);
            if (createPaths)
                this.CreatePaths();
        }

        public void SetPath(string path)
        {
            if (path != this.Path)
            {
                string setting = "Opening File";
                Configuration config;
                try
                {
                    config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
                    AppSettingsSection s = config == null ? null : (AppSettingsSection)config.GetSection("appSettings");
                }
                catch (Exception ex)
                {
                    throw new HandledException(ex, "Settings - Error loading setting '{0}'", setting);
                }
                setPath(path, config);
            }
        }

        /// <summary>
        /// Causes a reload of the settings containing paths with a new %pth substitution
        /// </summary>
        public void setPath(string path, Configuration config)
        {
            AppSettingsSection s = config == null ? null : (AppSettingsSection)config.GetSection("appSettings");
            string setting = "Set Path";
            try
            {
                if (path == null)
                    path = get(s, setting = "Path", true, @"%exe\Processed");
                if (string.IsNullOrEmpty(path))
                    Path = Environment.CurrentDirectory;
                else
                    Path = (new DirectoryInfo(path)).FullName;

                TempPath = get(s, setting = "TempPath", true, @"%pth\Temp");
                SummaryLog = get(s, setting = "SummaryLog", true, @"%pth\NKitSummary.txt");
                DatPathNameGameTdbMask = get(s, setting = "DatPathNameGameTdbMask", true, @"%exe\Dats\GameTdb\*.txt");

                if (this.DiscType == DiscType.GameCube)
                {
                    s = get(config, "gamecube");

                    DatPathRedumpMask = get(s, setting = "DatPathRedumpMask", true, @"%exe\Dats\GameCube_Redump\*.dat");
                    DatPathCustomMask = get(s, setting = "DatPathCustomMask", true, @"%exe\Dats\GameCube_Custom\*.dat");

                    RecoveryFilesPath = get(s, setting = "RecoveryFilesPath", true, @"%exe\Recovery\GameCube");
                    OtherRecoveryFilesPath = get(s, setting = "OtherRecoveryFilesPath", true, @"%exe\Recovery\GameCube_Other");

                    RedumpMatchRenameToMask = get(s, setting = "RedumpMatchRenameToMask", true, @"%pth\Restored\GameCube\%nmm.%ext");
                    CustomMatchRenameToMask = get(s, setting = "CustomMatchRenameToMask", true, @"%pth\Restored\GameCube_Custom\%nmm.%ext");
                    MatchFailRenameToMask = get(s, setting = "MatchFailRenameToMask", true, @"%pth\Restored\GameCube_MatchFail\%nmg %id6 [b].%ext");
                }
                else
                {
                    s = get(config, "wii");

                    DatPathRedumpMask = get(s, setting = "DatPathRedumpMask", true, @"%exe\Dats\Wii_Redump\*.dat");
                    DatPathCustomMask = get(s, setting = "DatPathCustomMask", true, @"%exe\Dats\Wii_Custom\*.dat");

                    RecoveryFilesPath = get(s, setting = "RecoveryFilesPath", true, @"%exe\Recovery\Wii");
                    OtherRecoveryFilesPath = get(s, setting = "OtherRecoveryFilesPath", true, @"%exe\Recovery\Wii_Other");
                    NkitRecoveryFilesPath = get(s, setting = "NkitRecoveryFilesPath", true, @"%exe\Recovery\Wii_NkitExtracted");

                    RedumpMatchRenameToMask = get(s, setting = "RedumpMatchRenameToMask", true, @"%pth\Restored\Wii\%nmm.%ext");
                    CustomMatchRenameToMask = get(s, setting = "CustomMatchRenameToMask", true, @"%pth\Restored\Wii_Custom\%nmm.%ext");
                    MatchFailRenameToMask = get(s, setting = "MatchFailRenameToMask", true, @"%pth\Restored\Wii_MatchFail\%nmg %id6 [b].%ext");
                }

                DatPathNameGameTdb = getLatestFile(DatPathNameGameTdbMask);
                DatPathRedump = getLatestFile(DatPathRedumpMask);
                DatPathCustom = getLatestFile(DatPathCustomMask);
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "Settings - Error loading setting '{0}'", setting);
            }
        }

        private string getLatestFile(string mask)
        {
            string x = System.IO.Path.GetDirectoryName(mask);
            if (Directory.Exists(x))
            {
                DirectoryInfo di = new DirectoryInfo(x);
                return di.GetFiles(System.IO.Path.GetFileName(mask))?.OrderByDescending(a => a.LastWriteTime)?.FirstOrDefault()?.FullName;
            }
            return null;
        }

        private bool validateExHandle(ILog l, Action test, string msg)
        {
            return validateExHandle(l, () => { test(); return true; }, msg);
        }

        private bool validateExHandle(ILog l, Func<bool> test, string msg)
        {
            string err = null;
            try
            {
                if (!test())
                    err = msg;
            }
            catch (Exception ex)
            {
                err = msg + ": " + ex.Message;
                return false;
            }

            try
            {
                if (err != null)
                    l?.LogDetail(err);
            }
            catch { }
            return err == null; //okay
        }

        private static string pathFix(string path)
        {
            return SourceFiles.PathFix(path);
        }

        private AppSettingsSection get(Configuration config, string sectionName)
        {
            try
            {
                if (config != null)
                    return (AppSettingsSection)config.GetSection(sectionName);
            }
            catch { }
            return null;
        }

        private T get<T>(AppSettingsSection s, Func<T> getVals, T defValue)
        {
            try
            {
                if (s != null)
                    return getVals();
            }
            catch { }
            return defValue;
        }

        private string get(AppSettingsSection s, string name, bool pathReplace, string defValue)
        {
            try
            {
                string m = (s?.Settings[name]?.Value) ?? defValue;
                if (pathReplace)
                {
                    if (m.StartsWith("~") && !string.IsNullOrEmpty(HomePath))
                        m = HomePath + (m.Length == 1 ? "" : m.Substring(1));
                    m = pathFix(m).Replace("%pth", this.Path)?.Replace("%exe", _exePath);
                }
                return m;
            }
            catch { }
            return null;
        }

        public static Version LibraryVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
    }
}
