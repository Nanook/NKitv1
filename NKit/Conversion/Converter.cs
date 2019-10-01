using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class Converter : ILog
    {
        public event EventHandler<MessageEventArgs> LogMessage;
        public event EventHandler<ProgressEventArgs> LogProgress;

        private List<Tuple<string, LogMessageType>> _logCache; //cache messages if progress is being printed
        private object _logCacheLock;
        private bool _inProgress;
        private int _completedPasses;
        private int _totalPasses;
        private bool _forcedWiiFullNkitRebuild; //only true when converting to nkit and reencode is false, remove update partition is true but the source image is missing it.

        private Context _context;
        private SourceFile _sourceFile;
        private bool _cacheLogsWhileProcessing; //so if console is outputting progress it is saved until complete
        private Settings _settings;
        private NStream _nstream;
        private int _detailLinesOutput;

        internal NStream NStream { get { return _nstream; } }
        public Settings Settings { get { return _settings; } }

        public string ConvertionName { get; private set; }

        public Converter(SourceFile sourceFile, bool cacheLogsWhileProcessing)
        {
            _inProgress = false;
            _cacheLogsWhileProcessing = cacheLogsWhileProcessing;
            _logCache = null;
            _logCacheLock = new Object();
            _sourceFile = sourceFile;
            _nstream = sourceFile.OpenNStream();
            _detailLinesOutput = 0;
            _settings = new Settings(_nstream.IsGameCube ? DiscType.GameCube : DiscType.Wii);
        }

        public OutputResults ConvertToNkit(bool renameWithMasks, NkitFormatType nkitFormat, bool fullVerify, bool calcHashes, bool testMode)
        {
            return process("ConvertToNKit", _sourceFile, renameWithMasks, true, nkitFormat, false, fullVerify, calcHashes, testMode, ns =>
            {
                List<Processor> p = new List<Processor>();
                if (ns.IsGameCube)
                {
                    if (ns.IsNkit) //ToNKit from GC - NKit
                    {
                        if (_context.Settings.NkitReencode || _forcedWiiFullNkitRebuild)
                        {
                            p.Add(new Processor(new NkitReaderGc(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                            p.Add(new Processor(new IsoReader(), new NkitWriterGc(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                            p[0].Reader.RequireValidationCrc = true;
                            p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                            p[1].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                        }
                        else if (nkitFormat == NkitFormatType.Iso) //don't reencode nkit, useful when wanting to just compress and/or verify
                        {
                            p.Add(new Processor(new IsoReader(), new IsoWriter(), "Copy NKit", this, true, false, ProcessorSizeMode.Stream));
                            p[0].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified (Will use src nkit crc from header)
                        }
                    }
                    else //ToNKit from GC - ISO, ISO.DEC
                    {
                        p.Add(new Processor(new IsoReader(), new NkitWriterGc(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                    }
                }
                else
                {
                    if (ns.IsNkit) //ToNKit from Wii - NKit
                    {
                        _forcedWiiFullNkitRebuild = !_context.Settings.NkitReencode && ns.IsNkitUpdateRemoved != _context.Settings.NkitUpdatePartitionRemoval;
                        if (_context.Settings.NkitReencode || _forcedWiiFullNkitRebuild)
                        {
                            p.Add(new Processor(new NkitReaderWii(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                            p.Add(new Processor(new IsoReader(), new NkitWriterWii(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                            p[0].Reader.RequireValidationCrc = true;
                            p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                            p[1].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                        }
                        else if (nkitFormat == NkitFormatType.Iso) //don't reencode nkit, useful when wanting to just compress and/or verify
                        {
                            p.Add(new Processor(new IsoReader(), new IsoWriter(), "Copy NKit", this, true, false, ProcessorSizeMode.Stream));
                            p[0].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified (Will use src nkit crc from header)
                        }
                    }
                    else //ToNKit from Wii - ISO, ISO.DEC, WBFS
                    {
                        p.Add(new Processor(new IsoReader() { EncryptWiiPartitions = ns.IsIsoDec }, new NkitWriterWii(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                    }
                }
                return p;
            });
        }

        public OutputResults ConvertToIso(bool renameWithMasks, bool fullVerify, bool calcHashes, bool testMode)
        {
            return process("ConvertToISO", _sourceFile, renameWithMasks, false, NkitFormatType.Iso, false, fullVerify, calcHashes, testMode, ns =>
            {
                List<Processor> p = new List<Processor>();

                if (ns.IsGameCube)
                {
                    if (ns.IsNkit) //ToIso from GC - NKit
                    {
                        p.Add(new Processor(new NkitReaderGc(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                        p[0].Reader.RequireValidationCrc = true;
                        p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                        p[0].Reader.VerifyIsWrite = false; //read verify
                    }
                    else //ToIso from GC - ISO, ISO.DEC
                    {
                        p.Add(new Processor(new IsoReader(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                else
                {
                    if (ns.IsNkit) //ToIso from Wii - NKit
                    {
                        p.Add(new Processor(new NkitReaderWii(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                        p[0].Reader.RequireValidationCrc = true;
                        p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                        p[0].Reader.VerifyIsWrite = false; //read verify
                    }
                    else //ToIso from Wii - ISO, ISO.DEC, WBFS
                    {
                        p.Add(new Processor(new IsoReader() { EncryptWiiPartitions = ns.IsIsoDec }, new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                return p;
            });
        }

        public OutputResults RecoverToNkit(bool renameWithMasks, NkitFormatType nkitFormat, bool fullVerify, bool calcHashes, bool testMode)
        {
            return process("RecoverToNKit", _sourceFile, renameWithMasks, true, nkitFormat, true, fullVerify, calcHashes, testMode, ns =>
            {
                List<Processor> p = new List<Processor>();

                if (ns.IsGameCube)
                {
                    if (ns.IsNkit) //RecoverToNKit from GC - NKit
                    {
                        p.Add(new Processor(new RecoverReaderGc(), new NkitWriterGc(), "Recover NKit", this, true, true, ProcessorSizeMode.Recover)); //restore gc can read nkit and has no patching for GC
                        p[0].Reader.RequireValidationCrc = true;
                    }
                    else //RecoverToNKit from GC - ISO, ISO.DEC
                    {
                        p.Add(new Processor(new RecoverReaderGc(), new NkitWriterGc(), "Recover Nkit", this, true, true, ProcessorSizeMode.Recover));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                else
                {
                    if (ns.IsNkit) //RecoverToNKit from Wii - NKit
                    {
                        p.Add(new Processor(new NkitReaderWii(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                        p.Add(new Processor(new RecoverReaderWii(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover));
                        p.Add(new Processor(new IsoReader(), new NkitWriterWii(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true;
                        p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                        p[0].Reader.VerifyIsWrite = false; //read verify
                        p[1].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                    }
                    else //RecoverToNKit from Wii - ISO, ISO.DEC, WBFS
                    {
                        p.Add(new Processor(new RecoverReaderWii(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover));
                        p.Add(new Processor(new IsoReader(), new NkitWriterWii(), "To NKit", this, false, true, ProcessorSizeMode.Stream));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                return p;
            });
        }

        public OutputResults RecoverToIso(bool renameWithMasks, bool fullVerify, bool calcHashes, bool testMode)
        {
            return process("RecoverToISO", _sourceFile, renameWithMasks, false, NkitFormatType.Iso, true, fullVerify, calcHashes, testMode, ns =>
            {
                List<Processor> p = new List<Processor>();


                if (ns.IsGameCube)
                {
                    if (ns.IsNkit) //RecoverToIso from GC - NKit
                    {
                        p.Add(new Processor(new RecoverReaderGc(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover)); //restore gc can read nkit
                        p[0].Reader.RequireValidationCrc = true;
                    }
                    else //RecoverToIso from GC - ISO, ISO.DEC
                    {
                        p.Add(new Processor(new RecoverReaderGc(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                else
                {
                    if (ns.IsNkit) //RecoverToIso from Wii - NKit
                    {
                        p.Add(new Processor(new NkitReaderWii(), new IsoWriter(), "To ISO", this, true, false, ProcessorSizeMode.Image));
                        p.Add(new Processor(new RecoverReaderWii(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover));
                        p[0].Reader.RequireValidationCrc = true;
                        p[0].Reader.RequireVerifyCrc = true; //verify the nkit read
                        p[0].Reader.VerifyIsWrite = false; //read verify
                        p[1].Reader.RequireValidationCrc = true; //IsoReader to set the CRC to be verified
                    }
                    else //RecoverToIso from Wii - ISO, ISO.DEC, WBFS
                    {
                        p.Add(new Processor(new RecoverReaderWii(), new IsoWriter(), "Recover ISO", this, true, false, ProcessorSizeMode.Recover));
                        p[0].Reader.RequireValidationCrc = true;
                    }
                }
                return p;
            });
        }

        public OutputResults ConvertToNkit()
        {
            return ConvertToNkit(_settings.MaskRename, _settings.NkitFormat, _settings.FullVerify, _settings.CalculateHashes, _settings.TestMode);
        }

        public OutputResults ConvertToIso()
        {
            return ConvertToIso(_settings.MaskRename, _settings.FullVerify, _settings.CalculateHashes, _settings.TestMode);
        }

        public OutputResults RecoverToNkit()
        {
            return RecoverToNkit(_settings.MaskRename, _settings.NkitFormat, _settings.FullVerify, _settings.CalculateHashes, _settings.TestMode);
        }

        public OutputResults RecoverToIso()
        {
            return RecoverToIso(_settings.MaskRename, _settings.FullVerify, _settings.CalculateHashes, _settings.TestMode);
        }

        private OutputResults process(string conversion, SourceFile sourceFile, bool renameWithMasks, bool toNkit, NkitFormatType nkitFormat, bool isRecovery, bool fullVerify, bool calcHashes, bool testMode, Func<NStream, IEnumerable<Processor>> passes)
        {
            OutputResults results = null;
            NStream nstream = _nstream;
            string lastTmp = null;
            string tmp = null;

            this.ConvertionName = conversion;

            try
            {

                SourceFile sf = null;
                long sourceSize = nstream.SourceSize;

                _context = new Context();
                _context.Initialise(this.ConvertionName, sourceFile, _settings, true, isRecovery, nstream.IsGameCube, nstream.Id8, this);

                List<Processor> processors = passes(nstream).Where(a => a != null).ToList();
                if (nkitFormat == NkitFormatType.Gcz)
                    processors.Add(new Processor(new IsoReader(), new GczWriter(), "To GCZ", this, false, true, ProcessorSizeMode.Stream));
                if (calcHashes)
                    processors.Add(new Processor(new IsoReader(), new HashWriter(), "Calc Hashes", this, false, true, ProcessorSizeMode.Stream));
                if (fullVerify)
                {
                    if (!toNkit)
                        processors.Add(new Processor(new IsoReader(), new VerifyWriter(), "Full Verify", this, false, true, ProcessorSizeMode.Stream));
                    else
                        processors.Add(new Processor(nstream.IsGameCube ? new NkitReaderGc() : (IReader)new NkitReaderWii(), new VerifyWriter(), "NKit Verify", this, false, true, ProcessorSizeMode.Image));
                    processors.Last().Writer.RequireVerifyCrc = true;
                    processors.Last().Writer.VerifyIsWrite = true; //read verify
                }
                _totalPasses = processors.Count();

                if (processors.Count == 0)
                    return null;

                DateTime dt = DateTime.Now;
                _completedPasses = 0;
                
                Log("PROCESSING" + (testMode ? " [TEST MODE]" : ((_context.Settings.DeleteSource ? " [DELETE SOURCE]" : ""))));
                Log("-------------------------------------------------------------------------------");
                if (_forcedWiiFullNkitRebuild)
                {
                    LogBlank();
                    Log(string.Format("Nkit Reencode forced: NkitUpdatePartitionRemoval is {0} and source image has {1} Update Partition", _context.Settings.NkitUpdatePartitionRemoval.ToString(), nstream.IsNkitUpdateRemoved ? "no" : "an"));
                    LogBlank();
                }
                Log(string.Format("{0} [{1}]  {2}  [MiB:{3,2:#.0}]", friendly(nstream.Title), friendly(nstream.Id), nstream.IsGameCube ? "GameCube" : "Wii", (sourceSize / (double)(1024 * 1024))));
                LogBlank();
                string passesText = getPassesLine(nstream, processors);
                Log(passesText);

                int i = 1;
                foreach (Processor pro in processors.Where(pro => pro != null))
                    LogDebug(string.Format("Pass {0}: {1}", (i++).ToString(), pro.ToString()));
                LogBlank();

                foreach (Processor pro in processors)
                {
                    //sort out temp file and open input as nstream each time
                    //raise progress and output messages from processors

                    if (sf != null)
                    {
                        nstream = sf.OpenNStream(!(pro.Writer is HashWriter)); //if hashWriter then read as raw file
                        sf = null;
                    }

                    tmp = null;
                    FileStream tmpFs = null;

                    try
                    {

                        if (pro.HasWriteStream)
                        {
                            tmp = Path.Combine(_context.Settings.TempPath, Path.GetFileName(Path.GetTempFileName()));
                            if (!Directory.Exists(_context.Settings.TempPath))
                                Directory.CreateDirectory(_context.Settings.TempPath);
                            tmpFs = File.Create(tmp, 0x400 * 0x400 * 4, FileOptions.SequentialScan);
                        }

                        _logCache = new List<Tuple<string, LogMessageType>>();
                        OutputResults nr = pro.Process(_context, nstream, tmpFs ?? Stream.Null);
                        _logCache = null;

                        if (results == null)
                        {
                            results = nr;
                            results.DiscType = nstream.IsGameCube ? DiscType.GameCube : DiscType.Wii;
                            results.InputFileName = sourceFile.AllFiles.Length != 0 ? sourceFile.AllFiles[0] : sourceFile.FilePath;
                            results.InputDiscNo = nstream.DiscNo;
                            results.InputDiscVersion = nstream.Version;
                            results.InputTitle = nstream.Title;
                            results.InputId8 = nstream.Id8;
                            results.InputSize = sourceSize;
                            results.FullSize = nstream.ImageSize;
                            results.Passes = passesText;
                            if (pro.IsVerify)
                                results.OutputSize = results.InputSize;
                        }
                        else if (pro.Writer is HashWriter) //hash writer gives no meaningful info back other than md5 and sha1 (the crc is forced when nkit, so ignore)
                        {
                            results.OutputMd5 = nr.OutputMd5;
                            results.OutputSha1 = nr.OutputSha1;
                        }
                        else
                        {
                            if (nr.AliasJunkId != null)
                                results.AliasJunkId = nr.AliasJunkId;
                            if (nr.OutputTitle != null)
                            {
                                results.OutputDiscNo = nr.OutputDiscNo;
                                results.OutputDiscVersion = nr.OutputDiscVersion;
                                results.OutputTitle = nr.OutputTitle;
                            }
                            results.OutputId8 = nr.OutputId8;
                            results.OutputCrc = nr.OutputCrc;
                            results.OutputPrePatchCrc = nr.OutputPrePatchCrc;
                            results.FullSize = nstream.ImageSize;
                            if (tmp != null)
                                results.OutputSize = nr.OutputSize;

                            if (nr.ValidationCrc != 0 && results.VerifyCrc != 0)
                                results.VerifyCrc = 0; //blank verify if a new ValidationCrc is set - verification happened when both != 0

                            if (nr.VerifyCrc != 0)
                                results.VerifyCrc = nr.VerifyCrc;
                            if (nr.ValidationCrc != 0)
                                results.ValidationCrc = nr.ValidationCrc;
                            if (nr.ValidateReadResult != VerifyResult.Unverified)
                                results.ValidateReadResult = nr.ValidateReadResult;
                            if (nr.VerifyOutputResult != VerifyResult.Unverified)
                            {
                                if (results.ValidateReadResult == VerifyResult.Unverified && nstream.IsNkit)
                                    results.ValidateReadResult = nr.VerifyOutputResult;
                                results.VerifyOutputResult = nr.VerifyOutputResult;
                            }
                            if (nr.IsRecoverable)
                                results.IsRecoverable = nr.IsRecoverable;
                        }
                    }
                    finally
                    {
                        if (tmpFs != null)
                            tmpFs.Dispose();

                        nstream.Close();
                    }

                    if (lastTmp != null && tmp != null)
                        File.Delete(lastTmp);

                    //handle failures well
                    if (results.ValidateReadResult == VerifyResult.VerifyFailed || results.VerifyOutputResult == VerifyResult.VerifyFailed)
                    {
                        lastTmp = tmp; //keep post loop happy
                        break;
                    }

                    if (_completedPasses != _totalPasses) //last item
                        sf = SourceFiles.OpenFile(tmp ?? lastTmp);

                    if (tmp != null)
                        lastTmp = tmp;
                }

                TimeSpan ts = DateTime.Now - dt;
                results.ProcessingTime = ts;

                //FAIL
                if (results.ValidateReadResult == VerifyResult.VerifyFailed || results.VerifyOutputResult == VerifyResult.VerifyFailed)
                {
                    LogBlank();
                    Log(string.Format("Verification Failed Crc:{0} - Failed Test Crc:{1}", results.OutputCrc.ToString("X8"), results.ValidationCrc.ToString("X8")));

                    if (lastTmp != null) //only null when verify only
                    {
                        Log("Deleting Output" + (Settings.OutputLevel != 3 ? "" : " (Skipped as OutputLevel is 3:Debug)"));
                        results.OutputFileName = null;
                        if (Settings.OutputLevel != 3)
                            File.Delete(lastTmp);

                        LogBlank();
                    }
                }
                else //SUCCESS
                {

                    LogBlank();
                    Log(string.Format("Completed ~ {0}m {1}s  [MiB:{2:#.0}]", ((int)ts.TotalMinutes).ToString(), ts.Seconds.ToString(), (results.OutputSize / (double)(1024 * 1024))));
                    LogBlank();
                    Log("RESULTS");
                    Log("-------------------------------------------------------------------------------");

                    uint finalCrc = results.ValidationCrc != 0 ? results.ValidationCrc : results.OutputCrc;
                    string mask = _context.Settings.MatchFailRenameToMask;
                    results.OutputFileExt = "." + SourceFiles.ExtensionString(false, false, toNkit, nkitFormat == NkitFormatType.Gcz).ToLower();
                    results.RedumpInfo = _context.Dats.GetRedumpEntry(_context.Settings, _context.Dats, finalCrc);
                    if (results.RedumpInfo.MatchType == MatchType.Redump || results.RedumpInfo.MatchType == MatchType.Custom)
                    {
                        Log(string.Format("{0} [{1} Name]", results.RedumpInfo.MatchName, results.RedumpInfo.MatchType.ToString()));
                        if (results.IsRecoverable)
                            Log(string.Format("Missing Recovery data is required to correctly restore this image!", results.RedumpInfo.MatchName, results.RedumpInfo.MatchType.ToString()));
                        mask = results.RedumpInfo.MatchType == MatchType.Custom ? _context.Settings.CustomMatchRenameToMask : _context.Settings.RedumpMatchRenameToMask;
                    }
                    else
                        Log(string.Format("CRC {0} not in Redump or Custom Dat", finalCrc.ToString("X8")));
                    LogBlank();


                    outputResults(results);


                    if (lastTmp != null) //only null when verify only
                    {
                        if (testMode)
                        {
                            Log("TestMode: Deleting Output");
                            results.OutputFileName = null;
                            if (File.Exists(lastTmp))
                                File.Delete(lastTmp);
                        }
                        else if (isRecovery && _context.Settings.RecoveryMatchFailDelete && results.RedumpInfo.MatchType == MatchType.MatchFail)
                        {
                            Log("Failed to Recover to Dat Entry: Deleting Output");
                            results.OutputFileName = null;
                            File.Delete(lastTmp);
                        }
                        else
                        {
                            if (renameWithMasks)
                            {
                                results.OutputFileName = _context.Dats.GetFilename(results, mask);
                                Log("Renaming Output Using Masks");
                            }
                            else
                            {
                                results.OutputFileName = SourceFiles.GetUniqueName(sourceFile.CreateOutputFilename(results.OutputFileExt));
                                Log("Renaming Output Based on Source File" + (sourceFile.AllFiles.Count() > 1 ? "s" : ""));
                            }
                            LogBlank();

                            string path = Path.GetDirectoryName(results.OutputFileName);
                            if (!Directory.Exists(path))
                                Directory.CreateDirectory(path);

                            File.Move(lastTmp, results.OutputFileName);

                            Log(string.Format("Output: {0}", Path.GetDirectoryName(results.OutputFileName)));
                            Log(string.Format("    {0}", Path.GetFileName(results.OutputFileName)));

                            //double check test mode just to be sure
                            if (_context.Settings.DeleteSource && !testMode && results.VerifyOutputResult == VerifyResult.VerifySuccess)
                            {
                                LogBlank();
                                Log("Deleting Source:");
                                foreach (string s in sourceFile.AllFiles.Length == 0 ? new string[] { sourceFile.FilePath } : sourceFile.AllFiles)
                                {
                                    Log(string.Format("    {0}", s));
                                    File.Delete(s);
                                }
                            }
                        }
                        LogBlank();
                    }

                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (lastTmp == null)
                        lastTmp = tmp;
                    if (lastTmp != null)
                    {
                        LogBlank();
                        Log("Deleting Output" + (Settings.OutputLevel != 3 ? "" : " (Skipped as OutputLevel is 3:Debug)"));
                        if (results != null)
                            results.OutputFileName = null;
                        if (Settings.OutputLevel != 3)
                            File.Delete(lastTmp);
                    }
                }
                catch { }

                if (_context.Settings.EnableSummaryLog)
                {
                    if (results == null)
                    {
                        results = new OutputResults();
                        results.Conversion = this.ConvertionName;
                        results.DiscType = (nstream?.IsGameCube ?? true) ? DiscType.GameCube : DiscType.Wii;
                        results.InputFileName = (sourceFile?.AllFiles?.Length ?? 0) == 0 ? (sourceFile?.FilePath ?? "") : (sourceFile?.AllFiles.FirstOrDefault() ?? "");
                        results.InputDiscNo = nstream?.DiscNo ?? 0;
                        results.InputDiscVersion = nstream?.Version ?? 0;
                        results.InputTitle = nstream?.Title ?? "";
                        results.InputId8 = nstream?.Id8 ?? "";
                        results.InputSize = sourceFile?.Length ?? 0;
                    }
                    results.VerifyOutputResult = VerifyResult.Error;
                    HandledException hex = ex as HandledException;
                    if (hex == null)
                        hex = new HandledException(ex, "Unhandled Exception");
                    results.ErrorMessage = hex.FriendlyErrorMessage;
                }
                throw;
            }
            finally
            {
                if (_context.Settings.EnableSummaryLog)
                {
                    summaryLog(_context.Settings, results);
                    Log("Summary Log Written" + (results.VerifyOutputResult != VerifyResult.Error ? "" : " as Errored!"));
                    LogBlank();
                }
            }

            return results;
        }

        ////////////////////////////////////////////////////////////////
        // OUTPUT STUFF
        ////////////////////////////////////////////////////////////////

        private void summaryLog(Settings settings, OutputResults results)
        {
            try
            {
                if (!File.Exists(settings.SummaryLog))
                    System.IO.File.AppendAllText(settings.SummaryLog, string.Join("\t", "TimeStamp", "Conversion", "System", "ReadResult", "OutputResult", "OutputCrc", "OutputID4", "RedumpMatch", "RedumpName", "InputSize", "OutputSize", "FullSize", "InputFilename", "OutputFilename", "MD5", "SHA1", "Passes", "SecondsElapsed", "ErrorMessage") + Environment.NewLine);

                if (settings.EnableSummaryLog)
                    System.IO.File.AppendAllText(settings.SummaryLog, string.Join("\t",
                        DateTime.Now.ToString(),
                        results.Conversion,
                        results.DiscType.ToString(),
                        results.ValidateReadResult.ToString(),
                        results.VerifyOutputResult.ToString(),
                        results.OutputCrc.ToString("X8") ?? "",
                        results.OutputId4 ?? "",
                        (results.RedumpInfo?.MatchType.ToString() ?? "") + (results.IsRecoverable ? "Recoverable" : ""),
                        results.RedumpInfo?.MatchName ?? "",
                        results.InputSize.ToString(),
                        results.OutputSize.ToString(),
                        results.FullSize.ToString(),
                        results.InputFileName ?? "",
                        results.OutputFileName ?? "",
                        results.OutputMd5 == null ? "" : BitConverter.ToString(results.OutputMd5).Replace("-", ""),
                        results.OutputSha1 == null ? "" : BitConverter.ToString(results.OutputSha1).Replace("-", ""),
                        results.Passes ?? "",
                        ((int)results.ProcessingTime.TotalSeconds).ToString(),
                        (results.ErrorMessage ?? "").Replace("\r", "").Replace('\t', ' ').Trim('\n', ' ').Replace("\n", " : ")
                        ) + Environment.NewLine);
            }
            catch { }
        }



        private void outputResults(OutputResults r)
        {
            bool output = false;
            if (r.InputId4 != r.OutputId4)
            {
                Log(string.Format("ID changed from {0} to {1}", r.InputId4, r.OutputId4));
                output = true;
            }
            if (r.InputTitle != r.OutputTitle)
            {
                Log(string.Format("Title changed from '{0}' to '{1}'", r.InputTitle, r.OutputTitle));
                output = true;
            }
            if (r.InputDiscVersion != r.OutputDiscVersion)
            {
                Log(string.Format("Version changed from v1.{0} to v1.{1}", r.InputDiscVersion.ToString("D2"), r.OutputDiscVersion.ToString("D2")));
                output = true;
            }
            if (r.InputDiscNo != r.OutputDiscNo)
            {
                Log(string.Format("Disc No. changed from {0} to {1}", r.InputDiscNo.ToString(), r.OutputDiscNo.ToString()));
                output = true;
            }
            if (output)
                LogBlank();
        }

        private string friendly(string text)
        {
            string f = text.Trim('\0') ?? "<NULL>";
            //if (Regex.IsMatch(f, "[^<>A-Z0-9-_+=]", RegexOptions.IgnoreCase))
            //    f = "Hex-" + BitConverter.ToString(Encoding.ASCII.GetBytes(f)).Replace("-", "");
            return f;
        }

        private string getPassesLine(NStream nstream, List<Processor> passes)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("{0} Pass{1}: ", passes.Count.ToString(), passes.Count == 1 ? "" : "es"));

            sb.AppendFormat("[{0}]", nstream.ExtensionString());

            for (int i = 0; i < passes.Count; i++)
            {
                sb.Append(" >> [");
                if (passes.Count > 1)
                    sb.AppendFormat("{0}:", (i + 1).ToString());
                sb.Append(passes[i].Title);
                sb.Append("]");
            }

            return sb.ToString();
        }

        ////////////////////////////////////////////////////////////////
        // ILOG LOGGING
        ////////////////////////////////////////////////////////////////


        public void ProcessingStart(long inputSize, string message)
        {
            if (LogProgress != null)
                LogProgress(this, new ProgressEventArgs() { IsStart = true, Progress = 0, TotalProgress = 0, StartMessage = message, Size = inputSize });
            _detailLinesOutput = 0;
            _inProgress = true;
        }

        public void ProcessingComplete(long outputSize, string message, bool success)
        {
            _completedPasses++;

            if (LogProgress != null)
            {
                if (success)
                    LogProgress(this, new ProgressEventArgs() { IsComplete = true, Progress = 1, TotalProgress = (_completedPasses / _totalPasses), CompleteMessage = message, Size = outputSize });
                else
                    LogBlank();
            }

            _inProgress = false;

            if (_cacheLogsWhileProcessing)
            {
                lock (_logCacheLock)
                {
                    if (_logCache != null && _logCache.Count != 0)
                    {
                        outputDetailHF(true);
                        foreach (Tuple<string, LogMessageType> m in _logCache)
                            msg(m.Item1, m.Item2, true);
                        outputDetailHF(false);
                        if (_logCache != null)
                            _logCache.Clear();
                    }
                }
            }
            else if (_detailLinesOutput != 0)
                outputDetailHF(false);
        }

        public void ProcessingProgress(float value)
        {
            float total = 0;

            if (value != 0)
                total = (float)((double)(_completedPasses + value) / (double)_totalPasses);

            if (LogProgress != null)
                LogProgress(this, new ProgressEventArgs() { Progress = value, TotalProgress = total });
        }

        public void Log(string message)
        {
            msg(message, LogMessageType.Info, false);
        }

        public void LogDetail(string message)
        {
            msg("    |" + message, LogMessageType.Detail, false);
        }
        public void LogDebug(string message)
        {
            if (Settings.OutputLevel == 3)
                msg("    >" + message, LogMessageType.Debug, false);
        }
        public void LogBlank()
        {
            msg("", LogMessageType.Info, false);
        }

        private void msg(string message, LogMessageType type, bool force)
        {
            bool detail = false;
            lock (_logCacheLock)
            {
                if (type != LogMessageType.Info && (_inProgress || _logCache != null) && !force)
                {
                    if (_cacheLogsWhileProcessing)
                    {
                        Debug.WriteLine(message);
                        lock (_logCacheLock)
                            _logCache.Add(new Tuple<string, LogMessageType>(message, type));
                        return;
                    }
                    else
                        detail = true;
                }
            }

            int level = _context?.Settings?.OutputLevel ?? 1;

            if (LogMessage != null && (int)type <= level)
            {
                if (detail && _detailLinesOutput == 0)
                {
                    _detailLinesOutput++; //increment to stop the next line entering this if again
                    outputDetailHF(true);
                }

                LogMessage(this, new MessageEventArgs() { Message = message, Type = type });
            }
        }

        private void outputDetailHF(bool header)
        {
            if (header)
            {
                msg("", LogMessageType.Detail, true);
                msg("    |DETAIL", LogMessageType.Detail, true);
                msg("    |...............................", LogMessageType.Detail, true);
            }
            else
            {
                msg("    |...............................", LogMessageType.Detail, true);
                msg("", LogMessageType.Detail, true);
            }
        }

    }
}
