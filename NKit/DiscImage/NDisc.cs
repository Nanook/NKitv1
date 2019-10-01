using SharpCompress.Archives;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class NDisc : IDisposable
    {
        public Settings _settings;
        public ILog Log { get; set; }

        public Settings Settings { get { return _settings; } }
        public string SourceFileName { get; private set; }

        internal NStream NStream { get; private set; }
        public BaseSection Header { get { return NStream?.DiscHeader; } }

        public bool IsGameCube { get { return NStream?.IsGameCube ?? true; } }

        public string BruteForceJunkId(long offset, byte[] junk)
        {
            if (offset != 0 && junk.Length != 0)
            {
                Console.WriteLine(string.Format("Found junk at {0}", offset.ToString("X8")));
                Console.WriteLine(string.Format("Attempting to brute force ID for junk: {0}", BitConverter.ToString(junk)));
                string id = NStream.JunkStream.BruteForceId((byte)NStream.DiscNo, junk, offset);
                return id;
            }
            return null;
        }
        public NDisc(Converter cvt, string sourceFileName)
        {
            SourceFileName = sourceFileName;
            this.NStream = cvt.NStream;
            this.NStream.Initialize(true);

            _settings = new Settings(NStream.IsGameCube ? DiscType.GameCube : DiscType.Wii);

            this.Log = (ILog)cvt;
        }

        public NDisc(ILog log, Stream stream)
        {
            this.NStream = (stream is NStream) ? (NStream)stream : new NStream(stream);
            if (!(stream is NStream))
                this.NStream.Initialize(true);

            _settings = new Settings(NStream.IsGameCube ? DiscType.GameCube : DiscType.Wii);

            this.Log = log;
        }

        internal NDisc(ILog log, NStream nStream, string sourceFileName)
        {
            NStream = nStream;
            SourceFileName = sourceFileName;

            _settings = new Settings(NStream.IsGameCube ? DiscType.GameCube : DiscType.Wii);

            this.Log = log;
        }


        private int storeRecoveryFile(bool isRedump, string fullPath, string name, string recoveryPath, string newRecoveryPath, bool logAsDetail)
        {
            string nf = Path.Combine(newRecoveryPath, name);
            string f = Path.Combine(recoveryPath, name);
            string msg = null;
            int ret = 0;
            if (isRedump)
            {
                if (File.Exists(f))
                {
                    msg = string.Format("    Redump - Already exists: {0}", f);
                    File.Delete(fullPath);
                }
                else
                {
                    File.Move(fullPath, f);
                    msg = string.Format("    Redump - Saved to: {0}", f);
                    ret = 1;
                }
                if (File.Exists(nf))
                    File.Delete(nf); //delete from new as it's redump
            }
            else
            {
                if (File.Exists(nf))
                {
                    msg = string.Format("    Other - Already exists: {0}", nf);
                    File.Delete(fullPath);
                }
                else if (File.Exists(f))
                {
                    msg = string.Format("    Other - Already exists: {0}", f);
                    File.Delete(fullPath);
                }
                else
                {
                    File.Move(fullPath, nf);
                    msg = string.Format("    Other - Saved to: {0}", nf);
                    ret = 2;
                }
            }

            if (msg != null && Log != null)
            {
                if (logAsDetail)
                    Log.LogDetail(msg);
                else
                    Log.Log(msg);
            }
            return ret;
        }



        private void ensurePosition(NStream stream, long discOffset)
        {
            if (stream.Position != discOffset)
                stream.Seek(discOffset, SeekOrigin.Begin); //stream will seek forward
        }


        /// <summary>
        /// Forward read of the full iso
        /// </summary>
        /// <param name="generateUpdateFiller">True: blank the update filler, False: copy it to catch the unknown extra data sin some images</param>
        /// <param name="generateOtherFiller">True: skip reading other filler (junk) and generate it, False: read filler sections from the source</param>
        /// <param name="forceFillerJunk">True: Generate the junk even if either of the above is false for comparison purposes</param>
        /// <returns></returns>
        public IEnumerable<IWiiDiscSection> EnumerateSectionsFix(bool generateUpdateFiller, bool generateOtherFiller, bool forceFillerJunk)
        {
            long discOffset = 0;
            WiiDiscHeaderSection hdr = (WiiDiscHeaderSection)NStream.DiscHeader;
            //this.Header = (BaseSection)hdr;
            yield return hdr;
            discOffset += hdr.Size;
            string lastId = null;
            long lastDiscOffset = 0;
            long updateGapPadding = 0;

            foreach (WiiPartitionInfo part in hdr.Partitions) //already sorted
            {
                WiiPartitionPlaceHolder placeholder = part as WiiPartitionPlaceHolder;

                //do we have a gap
                if (lastId != null || part.DiscOffset - discOffset != 0) //null if last was header
                {
                    if (lastDiscOffset <= 0x50000L && part.DiscOffset > part.SrcDiscOffset && updateGapPadding == 0) //only once
                    {
                        updateGapPadding = part.DiscOffset - part.SrcDiscOffset;
                        Log?.LogDetail(string.Format("Moving Data Partition from {0} to {1}", part.SrcDiscOffset.ToString("X8"), part.DiscOffset.ToString("X8")));
                    }

                    if (part.DiscOffset < discOffset)
                        throw new HandledException("Partition alignment error, this could be a bug when adding missing partitions");

                    WiiFillerSection gap = new WiiFillerSection(NStream, discOffset < 0xF800000L, discOffset, part.DiscOffset - discOffset, updateGapPadding, null, generateUpdateFiller, generateOtherFiller, forceFillerJunk);
                    yield return gap;
                    discOffset += gap.Size;
                    ensurePosition(NStream, discOffset - updateGapPadding);
                }

                WiiPartitionSection partSec;
                if (placeholder != null)
                {
                    if (placeholder.Filename != null)
                    {
                        if (generateOtherFiller && NStream.Position + updateGapPadding > hdr.Partitions.Max(a => a is WiiPartitionPlaceHolder ? 0 : a.DiscOffset))
                            NStream.Complete(); //Placeholders from now, no stream reading required

                        partSec = new WiiPartitionSection(NStream, (WiiDiscHeaderSection)NStream.DiscHeader, placeholder.Stream, discOffset);
                        ensurePosition(NStream, discOffset + partSec.Size - updateGapPadding); //used to be a seek - _stream.Seek(partSec.Size, SeekOrigin.Current); //stream will seek forward
                    }
                    else
                        continue; //force filler
                }
                else //should not get called when _stream is null
                {
                    partSec = new WiiPartitionSection(NStream, (WiiDiscHeaderSection)NStream.DiscHeader, this.NStream, discOffset);
                }

                //correct the stream length - required for 1 dual layer than when shrank is seen as single layer
                if (partSec.DiscOffset + partSec.PartitionLength > NStream.Length)
                    NStream.SetLength(partSec.DiscOffset + partSec.PartitionLength);

                yield return partSec;
                ensurePosition(NStream, discOffset + partSec.Size - updateGapPadding);
                if (generateOtherFiller && NStream.Position + updateGapPadding > hdr.Partitions.Max(a => a is WiiPartitionPlaceHolder ? 0 : a.DiscOffset))
                    NStream.Complete(); //Placeholders from now, no stream reading required

                if (placeholder != null && placeholder.Filename != null)
                    placeholder.Dispose();

                lastId = partSec.Id;
                lastDiscOffset = partSec.DiscOffset + updateGapPadding;
                discOffset += partSec.Size;
            }

            if (lastId != null)
            {
                yield return new WiiFillerSection(NStream, false, discOffset, NStream.RecoverySize - discOffset, 0, lastDiscOffset == 0xF800000 && lastId == "RELS" ? lastId : null, generateUpdateFiller, generateOtherFiller, forceFillerJunk);
            }
        }

        public IEnumerable<IWiiDiscSection> EnumerateSections(long imageSize)
        {
            bool generateUpdateFiller = false;
            bool generateOtherFiller = false;
            bool forceFillerJunk = false;
            long discOffset = 0;
            WiiDiscHeaderSection hdr = (WiiDiscHeaderSection)NStream.DiscHeader;
            //this.Header = (BaseSection)hdr;
            yield return hdr;
            discOffset += hdr.Size;
            string lastId = null;
            long lastDiscOffset = 0;
            long updateGapPadding = 0;

            foreach (WiiPartitionInfo part in hdr.Partitions) //already sorted
            {
                WiiPartitionPlaceHolder placeholder = part as WiiPartitionPlaceHolder;

                //do we have a gap
                if (lastId != null || part.DiscOffset - discOffset != 0) //null if last was header
                {
                    if (part.DiscOffset < discOffset)
                        throw new HandledException("Partition alignment error, this could be a bug when adding missing partitions");

                    WiiFillerSection gap = new WiiFillerSection(NStream, part.Type == PartitionType.Update, discOffset, part.DiscOffset - discOffset, updateGapPadding, null, generateUpdateFiller, generateOtherFiller, forceFillerJunk);
                    yield return gap;
                    discOffset += gap.Size;
                    ensurePosition(NStream, discOffset - updateGapPadding);
                }

                WiiPartitionSection partSec;
                if (placeholder != null)
                {
                    if (placeholder.Filename != null)
                    {
                        if (generateOtherFiller && NStream.Position + updateGapPadding > hdr.Partitions.Max(a => a is WiiPartitionPlaceHolder ? 0 : a.DiscOffset))
                            NStream.Complete(); //Placeholders from now, no stream reading required

                        partSec = new WiiPartitionSection(NStream, (WiiDiscHeaderSection)NStream.DiscHeader, placeholder.Stream, discOffset);
                        ensurePosition(NStream, discOffset + partSec.Size - updateGapPadding); //used to be a seek - _stream.Seek(partSec.Size, SeekOrigin.Current); //stream will seek forward
                    }
                    else
                        continue; //force filler
                }
                else //should not get called when _stream is null
                {
                    partSec = new WiiPartitionSection(NStream, (WiiDiscHeaderSection)NStream.DiscHeader, this.NStream, discOffset);
                }

                yield return partSec;
                ensurePosition(NStream, discOffset + partSec.Size - updateGapPadding);
                if (generateOtherFiller && NStream.Position + updateGapPadding > hdr.Partitions.Max(a => a is WiiPartitionPlaceHolder ? 0 : a.DiscOffset))
                    NStream.Complete(); //Placeholders from now, no stream reading required

                if (placeholder != null && placeholder.Filename != null)
                    placeholder.Dispose();

                lastId = partSec.Id;
                lastDiscOffset = partSec.DiscOffset + updateGapPadding;
                discOffset += partSec.Size;
            }

            if (lastId != null)
            {
                yield return new WiiFillerSection(NStream, false, discOffset, (imageSize == 0 ? NStream.Length : imageSize) - discOffset, 0, null, generateUpdateFiller, generateOtherFiller, forceFillerJunk);
            }
        }

        public void SaveMainDol(string outPath)
        {
            MemorySection hdr = this.NStream.DiscHeader;
            this.NStream.Copy(ByteStream.Zeros, hdr.ReadUInt32B(0x420) - 0x440);

            //dol info https://github.com/jchv/gcmtools/blob/master/gcm/dol.hpp
            byte[] dolLen = new byte[4];
            this.NStream.Read(dolLen, 0, 4);
            MemorySection mx = new MemorySection(dolLen);
            byte[] dolHdr = new byte[mx.ReadUInt32B(0)];
            dolLen.CopyTo(dolHdr, 0);
            this.NStream.Read(dolHdr, 4, dolHdr.Length - 4);
            MemorySection maindol = new MemorySection(dolHdr);
            uint maindolSize = 0;
            for (int i = 0; i < 18; i++)
            {
                if (maindol.ReadUInt32B(0x0 + (i * 4)) != 0) //7 text offsets, 11 data offsets
                    maindolSize = Math.Max(maindolSize, maindol.ReadUInt32B(0x0 + (i * 4)) + maindol.ReadUInt32B(0x90 + (i * 4)));
            }
            byte[] dol = new byte[maindol.Size + maindolSize];
            dolHdr.CopyTo(dol, 0);
            this.NStream.Read(dol, dolHdr.Length, dol.Length - dolHdr.Length);
            string dolName = Path.Combine(outPath, string.Format("main[{0}][{1}][{2}].dol", this.NStream.Id8, hdr.ReadUInt32B(0x420).ToString("X8"), Crc.Compute(dol).ToString("X8")));
            if (!File.Exists(dolName))
                File.WriteAllBytes(dolName, dol);
        }

        public ExtractResult ExtractBasicInfo()
        {
            Region region;
            if (this.IsGameCube)
            {
                MemorySection bi2bin = MemorySection.Read(this.NStream, 0x2000);
                region = (Region)bi2bin.ReadUInt32B(0x18);
            }
            else
                region = (Region)this.Header.ReadUInt32B(0x4e000);

            return createExtractResult(region, null);
        }

        public ExtractResult ExtractFiles(Func<ExtractedFile, bool> filter, Action<Stream, ExtractedFile> extract)
        {
            if (this.IsGameCube)
                return this.ExtractFilesGc(filter, extract);
            else if (this.NStream.IsNkit)
                return this.ExtractFilesWiiNkit(filter, extract);
            else
                return this.ExtractFilesWii(filter, extract);
        }

        private ExtractResult extractFiles(string id, MemorySection hdr, Stream inStream, Func<ExtractedFile, bool> filter, Action<Stream, ExtractedFile> extract)
        {
            long mlt = this.IsGameCube ? 1L : 4L;

            long srcPos = 0;
            List<FstFile> files = new List<FstFile>();

            MemorySection bootbin = (this.IsGameCube || this.NStream.IsNkit) ? hdr: MemorySection.Read(inStream, 0x440);
            files.Add(new FstFile(null) { Name = "boot.bin", PartitionId = id, DataOffset = srcPos, Offset = srcPos, Length = (int)bootbin.Size, IsNonFstFile = true, OffsetInFstFile = 0 });
            srcPos += bootbin.Size;

            MemorySection bi2bin = MemorySection.Read(inStream, 0x2000);
            files.Add(new FstFile(null) { Name = "bi2.bin", PartitionId = id, DataOffset = srcPos, Offset = srcPos, Length = (int)bi2bin.Size, IsNonFstFile = true, OffsetInFstFile = 0 });
            srcPos += bi2bin.Size;

            //########### APPLOADER (appldr.bin)  0x2440 - Action Reply can have 0 as the main.dol
            MemorySection appldr = MemorySection.Read(inStream, Math.Min(bootbin.ReadUInt32B(0x420) == 0 ? uint.MaxValue : (bootbin.ReadUInt32B(0x420) * mlt), (bootbin.ReadUInt32B(0x424) * mlt)) - srcPos);
            files.Add(new FstFile(null) { Name = "appldr.bin", PartitionId = id, DataOffset = srcPos, Offset = srcPos, Length = 0x20 + appldr.ReadUInt32B(0x14) + appldr.ReadUInt32B(0x18), IsNonFstFile = true, OffsetInFstFile = 0 });
            srcPos += appldr.Size;

            //########### APP (main.dol)
            MemorySection maindol = null;
            if (bootbin.ReadUInt32B(0x420) < bootbin.ReadUInt32B(0x424))
            {
                maindol = MemorySection.Read(inStream, (bootbin.ReadUInt32B(0x424) * mlt) - srcPos);
                uint maindolSize = 0;
                for (int i = 0; i < 18; i++)
                {
                    if (maindol.ReadUInt32B(0x0 + (i * 4)) != 0) //7 text offsets, 11 data offsets
                        maindolSize = Math.Max(maindolSize, maindol.ReadUInt32B(0x0 + (i * 4)) + maindol.ReadUInt32B(0x90 + (i * 4)));
                }
                files.Add(new FstFile(null) { Name = "main.dol", PartitionId = id, DataOffset = srcPos, Offset = srcPos, Length = maindolSize, IsNonFstFile = true, OffsetInFstFile = 0 });
                srcPos += maindol.Size;
            }

            MemorySection fstbin = MemorySection.Read(inStream, bootbin.ReadUInt32B(0x428) * mlt);
            files.Add(new FstFile(null) { Name = "fst.bin", PartitionId = id, DataOffset = srcPos, Offset = srcPos, Length = (int)fstbin.Size, IsNonFstFile = true, OffsetInFstFile = 0 });
            srcPos += fstbin.Size;

            List<FstFile> fstFiles = FileSystem.Parse(fstbin, null, id, this.IsGameCube)?.Files?.OrderBy(a => a.Offset)?.ThenBy(a => a.Length)?.ToList();
            if (fstFiles == null)
                throw new HandledException(string.Format("FST Corrupt or misaligned, could not be parsed at position 0x{0}", (bootbin.ReadUInt32B(0x424) * mlt).ToString("X8")));

            files.AddRange(fstFiles);
            Dictionary<string, MemorySection> mem = new Dictionary<string, MemorySection>();
            mem.Add("boot.bin", bootbin);
            mem.Add("bi2.bin", bi2bin);
            mem.Add("appldr.bin", appldr);
            mem.Add("main.dol", maindol);
            mem.Add("fst.bin", fstbin);

            List<ExtractedFile> exfiles = files.OrderBy(a => a.Offset).ThenBy(a => a.Length).Select(a => new ExtractedFile(this.IsGameCube ? DiscType.GameCube : DiscType.Wii,
                                 this.NStream.Id8,
                                 this.IsGameCube ? null : id,
                                 a.DataOffset,
                                 a.Length,
                                 a.Path,
                                 a.Name,
                                 mem.ContainsKey(a.Name) ? ExtractedFileType.System : ExtractedFileType.File)).Where(a => filter(a)).ToList();

            if (files == null || files.Count == 0)
                return createExtractResult((Region)bi2bin.ReadUInt32B(0x18), null);

            foreach (ExtractedFile f in exfiles) //read the files and write them out as goodFiles (possible order difference)
            {
                if (srcPos < f.Offset)
                {
                    inStream.Copy(Stream.Null, f.Offset - srcPos);
                    srcPos += f.Offset - srcPos;
                }

                if (f.Type == ExtractedFileType.System)
                {
                    using (MemoryStream ms = new MemoryStream(mem[f.Name].Data))
                        extract(ms, f);
                }
                else
                {
                    extract(inStream, f);
                    srcPos += f.Length;
                }
            }
            return createExtractResult((Region)bi2bin.Read8(0x18), null);
        }

        public ExtractResult ExtractFilesGc(Func<ExtractedFile, bool> filter, Action<Stream, ExtractedFile> extract)
        {
            return extractFiles(this.NStream.Id, (MemorySection)this.Header, this.NStream, filter, extract);
        }

        private ExtractResult ExtractFilesWiiNkit(Func<ExtractedFile, bool> filter, Action<Stream, ExtractedFile> extract)
        {
            WiiDiscHeaderSection hdr = (WiiDiscHeaderSection)this.Header;
            ExtractedFile exFile = new ExtractedFile(this.IsGameCube ? DiscType.GameCube : DiscType.Wii,
              this.NStream.Id8, null, 0, hdr.Size, "", "hdr.bin", ExtractedFileType.WiiDiscItem);
            long srcPos = hdr.Size;

            if (filter(exFile))
            {
                using (MemoryStream ms = new MemoryStream(hdr.Data))
                    extract(ms, exFile);
            }

            foreach (WiiPartitionInfo part in hdr.Partitions) //already sorted
            {
                if (this.NStream.Position < part.DiscOffset)
                    this.NStream.Copy(Stream.Null, part.DiscOffset - this.NStream.Position);

                long prtPos = this.NStream.Position;
                MemorySection prtHdr = MemorySection.Read(this.NStream, 0x20000);
                MemorySection prtDataHdr = MemorySection.Read(this.NStream, 0x440);
                string prtId = prtDataHdr.ReadString(0, 4);
                if (prtId != "\0\0\0\0")
                {
                    srcPos += prtHdr.Size + prtDataHdr.Size;
                    exFile = new ExtractedFile(this.IsGameCube ? DiscType.GameCube : DiscType.Wii,
                        this.NStream.Id8, null, prtPos, prtHdr.Size, "", prtId + "hdr.bin", ExtractedFileType.WiiDiscItem);
                    if (filter(exFile))
                    {
                        using (MemoryStream ms = new MemoryStream(prtHdr.Data))
                            extract(ms, exFile);
                    }

                    extractFiles(prtId, prtDataHdr, this.NStream, filter, extract);
                }
            }
            return createExtractResult((Region)this.Header.ReadUInt32B(0x4e000), null);
        }


        public ExtractResult ExtractFilesWii(Func<ExtractedFile, bool> filter, Action<Stream, ExtractedFile> extract)
        {
            List<ExtractResult> result = new List<ExtractResult>();
            List<WiiPartitionInfo> toExtract = new List<WiiPartitionInfo>();
            WiiDiscHeaderSection hdr = null;
            bool seekMode = !NStream.IsIsoDec; //experimental as iso.dec may not be able to seek to group start, archives will read ahead not seek

            ExtractedFile exFile;
            foreach (IWiiDiscSection s in this.EnumerateSectionsFix(false, true, false))
            {
                if (s is WiiDiscHeaderSection)
                {
                    hdr = (WiiDiscHeaderSection)s;
                    exFile = new ExtractedFile(this.IsGameCube ? DiscType.GameCube : DiscType.Wii,
                               this.NStream.Id8, null, 0, hdr.Size, "", "hdr.bin", ExtractedFileType.WiiDiscItem);
                    if (filter(exFile))
                    {
                        using (MemoryStream ms = new MemoryStream(hdr.Data))
                            extract(ms, exFile);
                    }
                }
                else if (s is WiiPartitionSection)
                {
                    WiiPartitionSection ps = (WiiPartitionSection)s;

                    if (ps.Header.Id != "\0\0\0\0")
                    {
                        exFile = new ExtractedFile(this.IsGameCube ? DiscType.GameCube : DiscType.Wii,
                            this.NStream.Id8, null, ps.Header.DiscOffset, ps.Header.Size, "", ps.Header.Id + "hdr.bin", ExtractedFileType.WiiDiscItem);
                        if (filter(exFile))
                        {
                            using (MemoryStream ms = new MemoryStream(ps.Header.Data))
                                extract(ms, exFile);
                        }
                    }

                    using (StreamCircularBuffer decrypted = new StreamCircularBuffer(ps.PartitionDataLength, null, null, output =>
                    {
                        foreach (WiiPartitionGroupSection pg in ps.Sections)
                        {
                            for (int i = 0; i < pg.Size / 0x8000; i++)
                                output.Write(pg.Decrypted, (i * 0x8000) + 0x400, 0x7c00);
                        }
                    }))
                    {
                        if (ps.Header.Id == "\0\0\0\0")
                            decrypted.Copy(Stream.Null, ps.PartitionDataLength);
                        else
                            extractFiles(ps.Id, new MemorySection(ps.Header.Data), decrypted, filter, extract);
                    }
                }
                else if (s is WiiFillerSection)
                {

                }
            }
            return createExtractResult((Region)hdr.ReadUInt32B(0x4e000), null);
        }
        public ExtractResult ExtractRecoveryFiles()
        {
            if (NStream.IsNkit)
            {
                Log?.Log(string.Format("Skipping NKit image: {0}", SourceFileName));
                return createExtractResult(Region.Japan, new ExtractRecoveryResult[] { new ExtractRecoveryResult() { Extracted = false } });
            }
            else if (NStream.IsGameCube)
                return ExtractRecoveryFilesGc();
            else
                return ExtractRecoveryFilesWii();
        }

        public ExtractResult ExtractRecoveryFilesGc()
        {
            MemorySection hdr = NStream.DiscHeader;
            MemorySection bi2bin;
            List<ExtractRecoveryResult> result = new List<ExtractRecoveryResult>();
            NCrc crc = new NCrc();
            uint appLdrCrc = 0;
            MemorySection fst = null;
            uint fstCrc = 0;
            string fn;
            string tmpFullName;

            Log?.Log(string.Format("Processing: {0}", SourceFileName));
            int storeType;

            using (CryptoStream bw = new CryptoStream(ByteStream.Zeros, crc, CryptoStreamMode.Write))
            {
                bw.Write(hdr.Data, 0, (int)hdr.Size);

                bi2bin = MemorySection.Read(NStream, 0x2000);
                bw.Write(bi2bin.Data, 0, (int)bi2bin.Size);

                MemorySection aplHdr = MemorySection.Read(NStream, 0x20);
                byte[] appLdr = new byte[0x20 + aplHdr.ReadUInt32B(0x14) + aplHdr.ReadUInt32B(0x18)];
                Array.Copy(aplHdr.Data, appLdr, (int)aplHdr.Size);
                NStream.Read(appLdr, (int)aplHdr.Size, appLdr.Length - (int)aplHdr.Size);
                appLdrCrc = Crc.Compute(appLdr);

                fn = string.Format("appldr[{0}][{1}].bin", aplHdr.ReadString(0, 10).Replace("/", ""), appLdrCrc.ToString("X8"));
                Log?.Log(string.Format("    1 of 2 - Extracting appldr.bin Recovery File: {0}", fn));
                tmpFullName = Path.Combine(Settings.OtherRecoveryFilesPath, fn + "_TEMP") + appLdrCrc.ToString("X8");
                if (File.Exists(tmpFullName))
                    File.Delete(tmpFullName);
                File.WriteAllBytes(tmpFullName, appLdr);
                if ((storeType = storeRecoveryFile(this.Settings.RedumpAppldrCrcs.Contains(appLdrCrc), tmpFullName, fn, Settings.RecoveryFilesPath, Settings.OtherRecoveryFilesPath, false)) != 0)
                    result.Add(new ExtractRecoveryResult() { FileName = fn, Extracted = true, Type = PartitionType.Other, IsNew = storeType == 2, IsGameCube = true });

                bw.Write(appLdr, 0, appLdr.Length); //add to fullcrc

                NStream.Copy(bw, hdr.ReadUInt32B(0x424) - (0x2440 + appLdr.Length));
                byte[] fstData = new byte[(int)((4 * 4) + (0x60 - 0x20) + hdr.ReadUInt32B(0x428))];
                fst = new MemorySection(fstData); //info:  dolAddr, fstAddr, maxfst, region, title, fst
                NStream.Read(fstData, (4 * 4) + (0x60 - 0x20), (int)hdr.ReadUInt32B(0x428));
                fst.WriteUInt32B(0x00, hdr.ReadUInt32B(0x420)); //dol
                fst.WriteUInt32B(0x04, hdr.ReadUInt32B(0x424)); //fstAddr
                fst.WriteUInt32B(0x08, hdr.ReadUInt32B(0x42C)); //maxfst
                fst.WriteUInt32B(0x0c, bi2bin.ReadUInt32B(0x18)); //region
                fst.Write(0x10, hdr.Read(0x20, 0x60 - 0x20)); //title
                fstCrc = Crc.Compute(fstData, (4 * 4) + (0x60 - 0x20), (int)hdr.ReadUInt32B(0x428)); //fst

                bw.Write(fstData, (4 * 4) + (0x60 - 0x20), (int)hdr.ReadUInt32B(0x428));
                crc.Snapshot("crc");
            }

            //fst checksums are postfst
            fn = string.Format("fst[{0}][{1}][{2}][{3}].bin", NStream.Id8, appLdrCrc.ToString("X8"), fstCrc.ToString("X8"), crc.FullCrc().ToString("X8"));
            Log?.Log(string.Format("    2 of 2 - Extracting fst.bin Recovery File: {0}", fn));
            tmpFullName = Path.Combine(Settings.OtherRecoveryFilesPath, fn + "_TEMP") + crc.FullCrc().ToString("X8");
            if (File.Exists(tmpFullName))
                File.Delete(tmpFullName);
            File.WriteAllBytes(tmpFullName, fst.Data);
            if ((storeType = storeRecoveryFile(this.Settings.RedumpFstCrcs.Contains(crc.FullCrc()), tmpFullName, fn, Settings.RecoveryFilesPath, Settings.OtherRecoveryFilesPath, false)) != 0)
                result.Add(new ExtractRecoveryResult() { FileName = fn, Extracted = true, Type = PartitionType.Other, IsNew = storeType == 2, IsGameCube = true });

            return createExtractResult((Region)bi2bin.Read8(0x18), result.ToArray());
        }

        /// <summary>
        /// Extracts files from partitions, this is not random access. The iso is read in it's entirety
        /// </summary>
        public ExtractResult ExtractRecoveryFilesWii()
        {
            List<ExtractRecoveryResult> result = new List<ExtractRecoveryResult>();
            List<WiiPartitionInfo> toExtract = new List<WiiPartitionInfo>();
            WiiDiscHeaderSection hdr = null;
            WiiPartitionHeaderSection pHdr = null;
            NStream target = null;
            bool extracting = false;
            int channel = 1;
            string lastPartitionId = null;
            PartitionType lastPartitionType = PartitionType.Other;

            string fileName = null;
            string tmpFileName = null;
            int extracted = 0;
            Crc crc = new Crc();
            Stream crcStream = null;
            long imageSize = this.NStream.RecoverySize; //for Wii: pHdr.PartitionDataLength


            bool isIso = false; //force to always scrub //Path.GetExtension(_name).ToLower() == ".iso";

            foreach (IWiiDiscSection s in this.EnumerateSectionsFix(false, true, false))
            {
                if (s is WiiDiscHeaderSection)
                {
                    hdr = (WiiDiscHeaderSection)s;

                    Log?.Log(string.Format("Processing: {0}", SourceFileName));

                    toExtract = hdr.Partitions.Where(a => a.Type != PartitionType.Data && a.Type != PartitionType.GameData).ToList();
                    if (toExtract.Count() == 0)
                    {
                        Log?.Log(string.Format("    Skipped: No Recovery Partitions to extract - {0}", SourceFileName));
                        break;
                    }
                }
                else if (s is WiiPartitionSection)
                {
                    WiiPartitionSection ps = (WiiPartitionSection)s;

                    if (!toExtract.Any(a => a.DiscOffset == ps.DiscOffset))
                    {
                        Log?.Log(string.Format("    Skipping {0} Partition: {1}...", ps.Header.Type.ToString(), ps.Header.Id.ToString()));
                        continue;
                    }
                    extracting = true;

                    pHdr = ps.Header;
                    extracting = true;
                    Log?.Log(string.Format("    {0} of {1} - Extracting {2} Recovery Partition: {3}", (extracted + 1).ToString(), toExtract.Count().ToString(), ps.Header.Type.ToString(), ps.Header.Id.ToString()));

                    crcStream = new CryptoStream(ByteStream.Zeros, crc, CryptoStreamMode.Write);
                    crc.Initialize();

                    WriteRecoveryPartitionData(crcStream, isIso, ps, channel, out tmpFileName, out fileName, out target);
                    lastPartitionId = ps.Id;
                    lastPartitionType = ps.Header.Type;
                }
                else if (s is WiiFillerSection)
                {
                    JunkStream junk = new JunkStream(lastPartitionType != PartitionType.Data ? hdr.ReadString(0, 4) : lastPartitionId, hdr.Read8(6), lastPartitionType == PartitionType.Update ? 0 : imageSize);
                    WiiFillerSection fs = (WiiFillerSection)s;
                    if (extracting)
                    {
                        int storeType;
                        if ((storeType = WriteRecoveryPartitionFiller(crcStream, junk, fs.DiscOffset, pHdr.Type == PartitionType.Update, false, fs, target, tmpFileName, ref fileName, crc, false)) != 0)
                            result.Add(new ExtractRecoveryResult() { FileName = fileName, Extracted = true, Type = PartitionType.Other, IsNew = storeType == 2, IsGameCube = false });

                        if (pHdr.Type != PartitionType.Update)
                            channel++;
                        extracted++;
                        extracting = false;
                        bool complete = (extracted == toExtract.Count());
                        if (complete)
                            break;
                    }
                }
            }
            return createExtractResult((Region)hdr.ReadUInt32B(0x4e000), result.ToArray());
        }

        private ExtractResult createExtractResult(Region region, ExtractRecoveryResult[] recovery)
        {
            ExtractResult result = new ExtractResult() { Recovery = recovery };
            result.Id = this.NStream.Id8;
            result.DiscType = this.IsGameCube ? DiscType.GameCube : DiscType.Wii;
            result.Title = this.NStream.Title;
            result.Region = region;
            return result;
        }

        internal long WriteRecoveryPartitionData(Stream crcStream, bool unscrub, WiiPartitionSection ps, int channelNo, out string tempFileName, out string fileName, out NStream output)
        {
            if (ps.Header.Type == PartitionType.Update)
                fileName = string.Format("{0}_{1}_", BitConverter.ToString(ps.Header.ContentSha1).Replace("-", ""), ps.Header.IsKorean ? "K" : "N");
            else
                fileName = string.Format("{0}_{1}_{2}_{3}_", this.NStream.Id8, channelNo.ToString().PadLeft(2, '0'), ps.Header.Id, ps.Header.IsKorean ? "K" : "N");
            tempFileName = Path.Combine(Settings.OtherRecoveryFilesPath, fileName + "TEMP");

            Directory.CreateDirectory(Settings.OtherRecoveryFilesPath);
            output = new NStream(File.Create(tempFileName));
            output.Write(ps.Header.Data, 0, (int)ps.Header.Size);
            crcStream.Write(ps.Header.Data, 0, (int)ps.Header.Size);
            long read = ps.Header.Size;
            foreach (WiiPartitionGroupSection pg in ps.Sections)
            {
                if (unscrub)
                    pg.Unscrub(null);
                output.Write(pg.Encrypted, 0, (int)pg.Size);
                crcStream.Write(pg.Encrypted, 0, (int)pg.Size);
                read = ps.Size;
            }
            return read;
        }
        internal int WriteRecoveryPartitionFiller(Stream crcStream, JunkStream junk, long pos, bool isUpdate, bool isNkit, WiiFillerSection fs, NStream target, string tmpFileName, ref string fileName, Crc crc, bool logAsDetail)
        {
            long nullBlocks = 0; //check for random junk - only on a handful of launch releases (Rampage, Ant Bully, Grim Adventures, Happy Feet etc)

            junk.Position = pos;
            long leadingNullsPos = pos;

            foreach (WiiFillerSectionItem fi in fs.Sections)
            {
                crcStream.Write(fi.Data, 0, (int)fi.Size);

                for (int i = 0; i < fi.Size; i += 0x8000)
                {
                    int len = (int)Math.Min(0x8000L, fi.Size - (long)i);
                    bool match = junk.Compare(fi.Data, i, len, junk.Position == leadingNullsPos ? 0x1c : 0) == len;

                    if (match)
                        nullBlocks++;
                    else
                    {
                        if (nullBlocks != 0)
                        {
                            if (pos == leadingNullsPos)
                            {
                                ByteStream.Zeros.Copy(target, 0x1cL);
                                junk.Position = pos + 0x1cL;
                                junk.Copy(target, (nullBlocks * 0x8000L) - 0x1cL);
                            }
                            else
                            {
                                junk.Position = pos;
                                junk.Copy(target, nullBlocks * 0x8000L);
                            }
                            pos += nullBlocks * 0x8000L;
                        }
                        nullBlocks = 0;
                        target.Write(fi.Data, i, len);
                    }
                }
            }
            target.Close();
            crcStream.Close();

            string fName = (fileName += crc.Value.ToString("X8"));
            bool redump;
            if (isUpdate)
                redump = this.Settings.RedumpUpdateCrcs.Contains(crc.Value);
            else
                redump = this.Settings.RedumpChannels.FirstOrDefault(a => fName.StartsWith(a.Item1)) != null;
            if (isNkit)
                Directory.CreateDirectory(Settings.NkitRecoveryFilesPath);

            return storeRecoveryFile(redump, tmpFileName, fileName, Settings.RecoveryFilesPath, isNkit ? Settings.NkitRecoveryFilesPath : Settings.OtherRecoveryFilesPath, logAsDetail);
            //rename the file, delete if dupe
        }



        internal void CloseStream()
        {
            try
            {
                if (NStream != null)
                    NStream.Close();
            }
            catch { }
        }

        public void Dispose()
        {
            this.CloseStream();
        }

    }
}
