using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Nanook.NKit
{
    public enum ProcessorSizeMode { Source, Stream, Image, Recover }

    internal class Processor
    {
        public IReader Reader;
        public IWriter Writer;

        private Timer _timer;
        private bool _timerRunning; //prevent 2 running at once - when user selects text in console fix

        private ILog _log;
        private ProcessorSizeMode _sizeMode;

        public Processor(IReader reader, IWriter writer, string title, ILog log, bool readerCanLog, bool writerCanLog, ProcessorSizeMode sizeMode)
        {
            _log = log;
            _sizeMode = sizeMode;
            this.Reader = reader;
            this.Writer = writer;
            this.Reader.Construct(readerCanLog ? _log : null);
            this.Writer.Construct(writerCanLog ? _log : null);
            this.Title = title;
        }

        public bool IsVerify { get { return this.Writer is VerifyWriter || this.Writer is HashWriter; } }
        public bool IsCompressor { get { return this.Writer is GczWriter; } }


        public bool HasWriteStream {  get { return !(Writer is VerifyWriter || Writer is HashWriter || Writer == null); } }

        public string Title { get; set; }

        public virtual OutputResults Process(Context ctx, NStream input, Stream output)
        {
            OutputResults results = new OutputResults() { Conversion = ctx.ConversionName, VerifyOutputResult = VerifyResult.Unverified, ValidateReadResult = VerifyResult.Unverified };
            try
            {
                StreamCircularBuffer fs = null;

                _timer = new Timer();
                _timer.Interval = 250;
                _timer.Elapsed += (s, e) =>
                {
                    if (_timerRunning)
                        return; //keep processing
                    try
                    {
                        _timerRunning = true;
                        _log.ProcessingProgress(((IProgress)fs)?.Value ?? 0);
                    }
                    catch { }
                    finally
                    {
                        _timerRunning = false;
                    }
                };
                _timer.Enabled = true;

                long size;
                switch (_sizeMode)
                {
                    case ProcessorSizeMode.Source:
                        size = input.SourceSize;
                        break;
                    case ProcessorSizeMode.Stream:
                        size = input.Length;
                        break;
                    case ProcessorSizeMode.Image:
                        size = input.ImageSize;
                        break;
                    case ProcessorSizeMode.Recover:
                    default:
                        size = input.RecoverySize;
                        break;
                }

                Coordinator pc = new Coordinator(ctx.ValidationCrc, (IValidation)this.Reader, (IValidation)this.Writer, size);

                pc.Started += (s, e) =>
                {
                    _timer.Enabled = true;
                    results.AliasJunkId = e.AliasJunkId;
                };
                pc.Completed += (s, e) =>
                {
                    _timer.Enabled = false;
                    if (this.Writer is HashWriter)
                    {
                        results.OutputMd5 = e.Md5;
                        results.OutputSha1 = e.Sha1;
                    }
                    else
                    {
                        MemorySection hdr = new MemorySection(e.Header ?? input.DiscHeader.Data);
                        results.OutputTitle = hdr.ReadStringToNull(0x20, 0x60);
                        results.OutputDiscNo = hdr.Read8(6);
                        results.OutputDiscVersion = hdr.Read8(7);
                        results.OutputId8 = string.Concat(hdr.ReadString(0, 6), results.OutputDiscNo.ToString("X2"), results.OutputDiscVersion.ToString("X2"));
                        results.ProcessorMessage = e.ResultMessage;
                        results.OutputCrc = e.PatchedCrc;
                        results.IsRecoverable = e.IsRecoverable;
                        if (e.ValidationCrc != 0)
                        {
                            results.ValidationCrc = e.ValidationCrc;
                            results.ValidateReadResult = e.ValidationCrc == e.PatchedCrc ? VerifyResult.VerifySuccess : VerifyResult.VerifyFailed;
                        }

                        if (!(this.Writer is VerifyWriter))
                            results.OutputSize = e.OutputSize; //never store the verify size
                        else
                        {
                            results.VerifyCrc = e.VerifyCrc;
                            if (e.VerifyIsWrite) //e.ValidationCrc can be set from a previous process run
                                results.VerifyOutputResult = results.ValidationCrc == results.VerifyCrc ? VerifyResult.VerifySuccess : VerifyResult.VerifyFailed;
                        }

                    }

                    bool l9 = pc.Patches.Crcs.Any(a => a.Offset > 0xFFFFFFFFL || a.Length > 0xFFFFFFFFL);
                    if (pc.ReaderCrcs != null)
                    {
                        foreach (CrcItem c in pc.ReaderCrcs.Crcs)
                            _log.LogDebug(string.Format("R-CRC {0}  Before:{1}  After:{2}  L:{3} {4}", c.Offset.ToString(l9 ? "X9" : "X8"), c.Value.ToString("X8"), c.PatchCrc == 0 ? "        " : c.PatchCrc.ToString("X8"), c.Length.ToString(l9 ? "X9" : "X8"), c.Name));
                        _log.LogDebug(string.Format("ReadCRC {0}Before:{1} After:{2}", l9 ? " " : "", pc.ReaderCrcs.FullCrc(false).ToString("X8"), pc.ReaderCrcs.FullCrc(true).ToString("X8")));
                    }
                    if (pc.WriterCrcs != null)
                    {
                        foreach (CrcItem c in pc.WriterCrcs.Crcs)
                            _log.LogDebug(string.Format("W-CRC {0}  Before:{1}  After:{2}  L:{3} {4}", c.Offset.ToString(l9 ? "X9" : "X8"), c.Value.ToString("X8"), c.PatchCrc == 0 ? "        " : c.PatchCrc.ToString("X8"), c.Length.ToString(l9 ? "X9" : "X8"), c.Name));
                        _log.LogDebug(string.Format("WriteCRC {0}Before:{1} After:{2}", l9 ? " " : "", pc.WriterCrcs.FullCrc(false).ToString("X8"), pc.WriterCrcs.FullCrc(true).ToString("X8")));
                    }
                    _log.ProcessingComplete(results.OutputSize, results.ProcessorMessage, true);
                };

                try
                {
                    _log.ProcessingStart(input.SourceSize, this.Title);

                    using (fs = new StreamCircularBuffer(size, input, null, s => this.Reader.Read(ctx, input, s, pc))) //read in stream and write to circular buffer
                        this.Writer.Write(ctx, fs, output, pc);
                }
                catch
                {
                    if (pc.Exception != null)
                        throw pc.Exception;
                    if (fs.WriterException != null)
                        throw fs.WriterException;
                    throw; //writer exception
                }

                foreach (CrcItem crc in pc.Patches.Crcs.Where(a => a.PatchData != null || a.PatchFile != null))
                {
                    output.Seek(crc.Offset, SeekOrigin.Begin);
                    if (crc.PatchFile == null)
                        output.Write(crc.PatchData, 0, (int)Math.Min(crc.Length, crc.PatchData.Length)); //PatchData might be larger
                    else
                    {
                        using (FileStream pf = File.OpenRead(crc.PatchFile))
                        {
                            pf.Copy(output, pf.Length);
                            ByteStream.Zeros.Copy(output, crc.Length - pf.Length);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_timer != null)
                    _timer.Enabled = false;
                try
                {
                    _log.ProcessingComplete(results.OutputSize, results.ProcessorMessage, false); // force any log lines to be output - handy for diagnosis
                }
                catch { }
                throw new HandledException(ex, "Failed processing {0} -> {1}", this.Reader?.GetType()?.Name ?? "<null>", this.Writer?.GetType()?.Name ?? "<null>");
            }
            finally
            {
                if (_timer != null)
                {
                    _timer.Enabled = false;
                    _timer = null;
                }
            }

            return results;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1} [{2}/{3}] => {4} [{5}/{6}])", this.GetType().Name, this.Reader.GetType().Name, this.Reader.RequireValidationCrc ? "Vld":"-", this.Reader.RequireVerifyCrc ? ("Vfy" + (this.Reader.VerifyIsWrite ? "-W" : "-R")) : "-", this.Writer.GetType().Name, this.Writer.RequireValidationCrc ? "Vld" : "-", this.Writer.RequireVerifyCrc ? ("Vfy" + (this.Writer.VerifyIsWrite ? "-W" : "-R")) : "-");
        }

    }
}
