using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Configuration;
using System.Reflection;
using System.Text;

namespace Nanook.NKit.RecoveryExtract
{
    class Program
    {
        private static bool logEnabled;


        static int Main(string[] args)
        {
            List<ExtractRecoveryResult> results = new List<ExtractRecoveryResult>();
            logEnabled = true;
            try
            {
                logEnabled = args.Length == 0 || Settings.Read("OutputLevel", "1") != "0";
            }
            catch { }

            Version v = Assembly.GetEntryAssembly().GetName().Version;
            Version vl = Assembly.GetEntryAssembly().GetName().Version;
            logLine(string.Format("{0} v{1}.{2}, NKit.dll v{3}.{4} :: Nanook", Settings.ExeName, v.Major.ToString(), v.Minor.ToString(), vl.Major.ToString(), vl.Minor.ToString()));
            logLine("");

            try
            {
                if (args.Length == 0)
                {
                    logLine(string.Format(@"Extracts recovery items that can be used by NKit to help restore modified images

Usage: {0} <files|paths|masks> ...

Parameters can be 1 or more filename and paths with or without masks.
Masks can search subfolders. Edit NKit.dll.config

Supported files:  iso, wbfs, gcm, iso.dec, zip, rar, 7z

If paths or filenames have spaces ensure they are enclosed in ""s

Examples
  {0} c:\temp\image.wbfs
  {0} c:\temp\scrubbed.iso
  {0} c:\temp
  {0} *.*
  {0} c:\temp\image.wbfs \temp\*.zip x*.iso.dec
  {0} ""c:\path 1\*.zip"" ""c:\path 2\*.gcm"" ""..\path 3\*.iso""

Edit 'NKit.dll.config' to specify all the required values
", Settings.ExeName));
                    return 2;
                }

                bool recurse = Settings.Read("SearchSubfolders", "true") == "true";

                logLine("Processing command line and scanning files...");
                SourceFile[] files = SourceFiles.Scan(args, recurse);
                logLine(string.Format("Found {0} file{1}", files.Length.ToString(), files.Length == 1 ? "" : "s"));
                logLine("");


                foreach (SourceFile src in files)
                {
                    Converter nkitConvert = new Converter(src, true);
                    try
                    {
                        nkitConvert.LogMessage += dx_LogMessage;
                        nkitConvert.LogProgress += dx_LogProgress;

                        int fileTotalLen = src.TotalFiles.ToString().Length;
                        logLine(string.Format("#####[ {0} / {1} ]{2}", (src.Index + 1).ToString().PadLeft(fileTotalLen), src.TotalFiles.ToString(), new string('#', 79 - ((fileTotalLen * 2) + 12))));
                        using (NDisc dx = new NDisc(nkitConvert, src.Name))
                        {
                            try
                            {
                                if (dx != null)
                                {
                                    if (Settings.Read("OutputLevel", "1") == "3")
                                    {
                                        if (!dx.IsGameCube)
                                            File.AppendAllText(@"WiiHeader.txt", string.Format("{0}\t{1}_{2}\t{3}\r\n", src.Name, BitConverter.ToString(dx.Header.Read(0x4e010, 0x10)).Replace("-", ""), dx.Header.ReadUInt32B(0x4e000).ToString(), BitConverter.ToString(dx.Header.Read(0, 256)).Replace("-", " ")));
                                        else
                                            File.AppendAllText(@"GcHeader.txt", string.Format("{0}\t{1}\r\n", src.Name, BitConverter.ToString(dx.Header.Read(0, 256)).Replace("-", " ")));
                                    }
                                    results.AddRange(dx.ExtractRecoveryFiles().Recovery);
                                }
                            }
                            catch (Exception ex)
                            {
                                outputExceptionDetails(ex);
                            }
                            finally
                            {
                            }
                        }
                        logLine("");
                    }
                    finally
                    {
                        nkitConvert.LogMessage -= dx_LogMessage;
                        nkitConvert.LogProgress -= dx_LogProgress;
                    }
                }
                if (files.Length == 0)
                    logLine("No files found");
                else
                {
                    int isRedump = results.Count(a => !a.IsNew);
                    int isOther = results.Count(a => a.IsNew);
                    logLine(string.Format("Saved {0} Redump recovery file{1}, {2} Other recovery file{3}", isRedump.ToString(), isRedump == 1 ? "" : "s", isOther.ToString(), isOther == 1 ? "" : "s"));
                }
            }
            catch (Exception ex)
            {
                outputExceptionDetails(ex);
            }

            if (logEnabled && Settings.Read("WaitForKeyAfterProcessing", "true") == "true")
            {
                logLine("");
                logLine("Press enter / return to exit . . .");
                Console.ReadLine();
            }

            Environment.ExitCode = 2;
            return 2;

        }

        private static void outputExceptionDetails(Exception ex)
        {
            try
            {
                HandledException hex = ex as HandledException;
                if (hex == null && ex is AggregateException)
                    hex = (HandledException)((AggregateException)ex).InnerExceptions.FirstOrDefault(a => a is HandledException);

                logLine("");
                logLine("Failed");
                logLine("-------");
                if (hex != null)
                    logLine(hex.FriendlyErrorMessage);
                else
                    logLine(ex.Message);
                if (Settings.Read("OutputLevel", "1") == "3")
                    logLine(ex.StackTrace);
            }
            catch { }
        }

        private static int _lastProgress;
        private static DateTime _startDate = DateTime.MinValue;

        private static void dx_LogProgress(object sender, ProgressEventArgs e)
        {
            if (e.IsStart)
                log(string.Format("{0}:{1}", e.StartMessage ?? "Processing", new string(' ', 14 - (e.StartMessage ?? "Processing").Length + 1)));

            int prg = (int)(e.Progress * 20F);
            if (prg == 0 && _startDate == DateTime.MinValue)
                _startDate = DateTime.Now;

            for (int i = _lastProgress + 1; i <= prg; i++)
                log(i % 2 == 1 ? "." : (i / 2).ToString());

            _lastProgress = prg;

            if (e.IsComplete)
            {
                TimeSpan ts = DateTime.Now - _startDate;
                log(string.Format(" ~{0,2}m {1,2:D2}s", ((int)ts.TotalMinutes).ToString(), ts.Seconds.ToString()));
                _startDate = DateTime.MinValue; //reset

                if (e.Size != 0)
                    log(string.Format("  [MiB:{0,7:#####.0}]", (e.Size / (double)(1024 * 1024))));
                else if (e.CompleteMessage != null)
                    log("             ");

                if (e.CompleteMessage != null)
                    log(string.Format("  {0}", e.CompleteMessage));

                logLine("");
            }
        }

        private static void dx_LogMessage(object sender, MessageEventArgs e)
        {
            string m = e.Message;
            logLine(m);
        }

        private static void logLine(string message)
        {
            Debug.WriteLine(message);
            if (logEnabled)
                Console.WriteLine(message);
        }

        private static void log(string message)
        {
            Debug.Write(message);
            if (logEnabled)
                Console.Write(message);
        }

    }
}
