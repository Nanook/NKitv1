using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class RecoverReaderGc : IReader
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }
        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Read(Context ctx, NStream inStream, Stream outStream, Coordinator pc)
        {
            string resultMsg = "";
            if (!Settings.ConfigFileFound)
                _log?.Log("!! No config file found - This is required to restore and validate images");

            NCrc crc = new NCrc();
            Settings settings = ctx.Settings;
            List<string> addedFiles = new List<string>();

            DatData data = ctx.Dats;
            RecoveryData rec = ctx.Recovery;

            List<JunkRedumpPatch> patches = rec.JunkPatches;
            MemorySection hdr = null;

            try
            {

                long junkStart = settings.JunkStartOffsets.FirstOrDefault(a => a.Id8 == inStream.Id8)?.Offset ?? 0;

                string forceJunkId = settings.JunkIdSubstitutions.FirstOrDefault(a => a.Id8 == inStream.Id8)?.JunkId;
                if (forceJunkId != null)
                {
                    _log?.LogDetail(string.Format("Using ID {0} for junk not image ID {1}", forceJunkId, inStream.Id));
                    //result.ImageInfo.JunkId = forceJunkId;
                    inStream.ChangeJunk(forceJunkId);
                }
                if (junkStart != 0)
                    _log?.LogDetail(string.Format("Junk forced to start at 0x{0}", junkStart.ToString("X8")));

                hdr = inStream.DiscHeader;

                FstFileItem goodFst = (FstFileItem)rec.GcBinFiles.Where(a => a is FstFileItem).Cast<FstFileItem>().FirstOrDefault(a => a.Id8 == inStream.Id8 && !(inStream.Id8 == "GNHE5d0000" && a.Length == 116));

                if (goodFst != null)
                    _log?.LogDetail(string.Format("Recovery:   {0}", goodFst.Filename));

                if (goodFst == null && rec.GcNewBinFiles != null)
                    goodFst = (FstFileItem)rec.GcNewBinFiles.Where(a => a is FstFileItem).Cast<FstFileItem>().FirstOrDefault(a => a.Id8 == inStream.Id8);
                if (goodFst != null)
                    goodFst.Populate();
                ApploaderFileItem goodAldr = goodFst == null ? null : (ApploaderFileItem)rec.GcBinFiles.FirstOrDefault(a => a.Crc == goodFst.AppLoadCrc);
                if (goodAldr == null && rec.GcNewBinFiles != null)
                    goodAldr = goodFst == null ? null : (ApploaderFileItem)rec.GcNewBinFiles.FirstOrDefault(a => a.Crc == goodFst.AppLoadCrc);

                //is it an action replay (custom hacks)
                if ((hdr.ReadUInt32B(0x420) == 0 && inStream.Id8 == "GNHE5d0000") || inStream.Id8 == "DTLX010000" || inStream.Id8 == "102E010007")
                {
                    if (inStream.Id8 == "102E010007")
                        resultMsg = "Aging Disc detected - Skipping recover";
                    else
                        resultMsg = "Datel Action Replay detected - Skipping recover";
                    try
                    {
                        using (CryptoStream target = new CryptoStream(outStream, crc, CryptoStreamMode.Write))
                        {
                            target.Write(hdr.Data, 0, (int)hdr.Size);
                            pc.ReaderCheckPoint1PreWrite(forceJunkId, 0); //size that we will output from this read
                            inStream.Copy(target, pc.OutputSize - hdr.Size);
                        }
                        _log?.LogDetail(resultMsg);
                    }
                    catch (Exception ex)
                    {
                        throw new HandledException(ex, resultMsg);
                    }
                    crc.Snapshot("files");
                }
                else
                {
                    //############################################################################
                    //# READ DISC START

                    //########### Header (boot.bin)  0 (read by base stream already)
                    long srcPos = hdr.Size;

                    _log?.LogDetail(string.Format("Header Read: {0} - {1}", friendly(inStream.Id), friendly(inStream.Title)));

                    //########### Header Info (bi2.bin)  0x440
                    MemorySection bi2bin = MemorySection.Read(inStream, 0x2000);
                    srcPos += bi2bin.Size;

                    //########### APPLOADER (appldr.bin)  0x2440 - Action Reply can have 0 as the main.dol
                    MemorySection appldr = MemorySection.Read(inStream, Math.Min(hdr.ReadUInt32B(0x420) == 0 ? uint.MaxValue : hdr.ReadUInt32B(0x420), hdr.ReadUInt32B(0x424)) - srcPos);
                    srcPos += appldr.Size;

                    //########### APP (main.dol)
                    MemorySection maindol;
                    if (hdr.ReadUInt32B(0x420) < hdr.ReadUInt32B(0x424))
                        maindol = MemorySection.Read(inStream, hdr.ReadUInt32B(0x424) - srcPos);
                    else
                        maindol = new MemorySection(new byte[0]);
                    srcPos += maindol.Size;

                    //########### FST (fst.bin)
                    MemorySection srcFst = MemorySection.Read(inStream, hdr.ReadUInt32B(0x428));
                    srcPos += hdr.ReadUInt32B(0x428);

                    //############################################################################
                    //# CORRECT ISSUES

                    List<FstFile> srcFiles = FileSystem.Parse(srcFst, null, inStream.Id, true)?.Files?.OrderBy(a => a.Offset)?.ThenBy(a => a.Length)?.ToList();
                    if (srcFiles == null)
                    {
                        throw new HandledException(string.Format("FST Corrupt or misaligned, could not be parsed at position 0x{0}", hdr.ReadUInt32B(0x424).ToString("X8")));
                    }

                    uint crcTmp = Crc.Compute(appldr.Data, 0, Math.Min((int)appldr.Size, 0x20 + (int)(appldr.ReadUInt32B(0x14) + appldr.ReadUInt32B(0x18))));
                    //adjust appldr
                    if (goodAldr != null && crcTmp != goodAldr.Crc)
                    {
                        _log?.LogDetail(string.Format("Replacing appldr.bin crc {0} with Recovery appldr.bin {1}", crcTmp.ToString("X8"), goodAldr.Crc.ToString("X8")));
                        addedFiles.Add(goodAldr.Filename);
                        appldr = new MemorySection(File.ReadAllBytes(goodAldr.Filename));
                    }

                    //adjust main.dol
                    if (goodFst != null)
                    {
                        if (goodFst.MainDolOffset != hdr.ReadUInt32B(0x420))
                        {
                            //main.dol is before fst in image and after in goodfst
                            if (hdr.ReadUInt32B(0x420) < hdr.ReadUInt32B(0x424) && goodFst.MainDolOffset > goodFst.FstOffset)
                            {
                                _log?.LogDetail( string.Format("Skipping main.dol at 0x{0} using 0x{1}", hdr.ReadUInt32B(0x420).ToString("X8"), goodFst.MainDolOffset.ToString("X8")));
                                maindol = new MemorySection(new byte[0]);
                            }
                            else
                                _log?.LogDetail( string.Format("Moving main.dol address 0x{0} to 0x{1}", hdr.ReadUInt32B(0x420).ToString("X8"), goodFst.MainDolOffset.ToString("X8")));
                            hdr.WriteUInt32B(0x420, (uint)goodFst.MainDolOffset);
                        }

                        if (goodFst.Region != (Region)bi2bin.ReadUInt32B(0x18))
                        {
                            _log?.LogDetail( string.Format("Region Changed to {0} from {1}", goodFst.Region.ToString(), ((Region)bi2bin.ReadUInt32B(0x18)).ToString()));
                            bi2bin.WriteUInt32B(0x18, (uint)goodFst.Region);
                        }
                        if (goodFst.MaxFst != hdr.ReadUInt32B(0x42C))
                        {
                            _log?.LogDetail( string.Format("Max Fst Size changed to {0} from {1}", goodFst.MaxFst.ToString("X8"), hdr.ReadUInt32B(0x42C).ToString("X8")));
                            hdr.WriteUInt32B(0x42C, (uint)goodFst.MaxFst);
                        }
                        string newTitle = Encoding.ASCII.GetString(goodFst.Title);
                        if (newTitle != hdr.ReadString(0x20, 0x60 - 0x20))
                        {
                            _log?.LogDetail(string.Format("Title changed to '{0}' from '{1}'", newTitle.TrimEnd('\0'), hdr.ReadString(0x20, 0x60 - 0x20).TrimEnd('\0')));
                            hdr.Write(0x20, goodFst.Title);
                        }
                    }

                    MemorySection fst = srcFst;
                    List<FstFile> fstFiles = srcFiles;
                    crcTmp = Crc.Compute(srcFst.Data);
                    if (goodFst != null && crcTmp != goodFst.Crc)
                    {
                        _log?.LogDetail(string.Format("Replacing fst.bin crc {0} with Recovery fst {1}", crcTmp.ToString("X8"), goodFst.Crc.ToString("X8")));
                        addedFiles.Add(goodFst.Filename);
                        fst = new MemorySection(goodFst.FstData);
                        fstFiles = FileSystem.Parse(fst, null, inStream.Id, true).Files.OrderBy(a => a.Offset).ThenBy(a => a.Length).ToList();
                    }
                    //adjust fst.bin
                    if (goodFst != null && (goodFst.FstOffset != hdr.ReadUInt32B(0x424) || goodFst.FstData.Length != hdr.ReadUInt32B(0x428)))
                    {
                        _log?.LogDetail(string.Format("Moving / Resizing fst.bin address 0x{0} (Length {1}) to 0x{2} (Length {3})", hdr.ReadUInt32B(0x424).ToString("X8"), hdr.ReadUInt32B(0x428).ToString(), goodFst.FstOffset.ToString("X8"), goodFst.FstData.Length.ToString()));
                        hdr.WriteUInt32B(0x424, (uint)goodFst.FstOffset);
                        hdr.WriteUInt32B(0x428, (uint)goodFst.FstData.Length);
                    }


                    int c;
                    int failCount = 0;
                    int filesMoved = 0;
                    int nkitJunkFiles = 0;
                    foreach (FstFile f in srcFiles)
                    {
                        FstFile[] fnd = fstFiles.Where(a => a.Name == f.Name && a.Path == f.Path).ToArray();
                        if (fnd.Length == 0)
                        {
                            failCount++;
                            _log?.LogDetail(string.Format("FST Error: No File Found - {0}/{1} (Length {2})", f.Path, f.Name, f.Length.ToString()));
                        }
                        else if (inStream.IsNkit && (c = fnd.Count(a => a.Length != 0 && f.Length == 0)) == 1)
                            nkitJunkFiles++;
                        else if ((c = fnd.Count(a => a.Length == f.Length)) != 1)
                        {
                            failCount++;
                            _log?.LogDetail(string.Format("FST Error: File Size bad - {0}/{1} (Length {2}){3}", f.Path, f.Name, f.Length.ToString(), c <= 1 ? "" : string.Format(" {0} files found", c.ToString())));
                        }
                        else if (f.DataOffset != fnd[0].DataOffset)
                            filesMoved++;
                        if (failCount >= 10)
                            break;
                    }
                    if (failCount != 0)
                    {
                        if (failCount >= 10)
                            throw new HandledException(string.Format("{0} or more FST errors found", failCount.ToString()));
                    }
                    if (filesMoved != 0)
                        _log?.LogDetail(string.Format("{0} file{1} of {2} will be repositioned when rebuilding this image", filesMoved.ToString(), filesMoved == 1 ? "" : "s", fstFiles.Count.ToString()));
                    if (nkitJunkFiles != 0)
                        _log?.LogDetail(string.Format("{0} file{1} of {2} will be generated from junk when rebuilding this image", nkitJunkFiles.ToString(), nkitJunkFiles == 1 ? "" : "s", fstFiles.Count.ToString()));

                    if (goodFst != null)
                    {
                        //is the data output so far correct
                        if (!bruteForceValidHeader(ctx, goodFst, hdr, bi2bin, appldr, maindol, fst, pc))
                            throw new HandledException(string.Format("Post FST Crc Failed 0x{0}", goodFst.PostFstCrc.ToString("X8")));
                    }

                    //############################################################################
                    //# WRITE DISC START
                    CryptoStream target = new CryptoStream(outStream, crc, CryptoStreamMode.Write);

                    target.Write(hdr.Data, 0, (int)hdr.Size);
                    pc.ReaderCheckPoint1PreWrite(forceJunkId, 0); //size that we will output from this read

                    long dstPos = hdr.Data.Length;
                    crc.Snapshot("boot.bin");

                    target.Write(bi2bin.Data, 0, (int)bi2bin.Size);
                    dstPos += bi2bin.Size;
                    crc.Snapshot("bi2.bin");

                    target.Write(appldr.Data, 0, (int)appldr.Size);
                    dstPos += appldr.Size;
                    ByteStream.Zeros.Copy(target, Math.Min(hdr.ReadUInt32B(0x420), hdr.ReadUInt32B(0x424)) - dstPos);
                    dstPos += Math.Min(hdr.ReadUInt32B(0x420), hdr.ReadUInt32B(0x424)) - dstPos;
                    crc.Snapshot("appldr.bin");

                    target.Write(maindol.Data, 0, (int)maindol.Size);
                    dstPos += maindol.Size;

                    if (goodFst != null)
                    {
                        long padding = goodFst.FstOffset - dstPos;
                        ByteStream.Zeros.Copy(target, padding);
                        dstPos += padding;
                    }
                    crc.Snapshot("main.dol");


                    target.Write(fst.Data, 0, fst.Data.Length);
                    dstPos += fst.Size;

                    bool firstFile = true;

                    Dictionary<FstFile, byte[]> cache = new Dictionary<FstFile, byte[]>(); 
                    crc.Snapshot("fst.bin");

                    //############################################################################
                    //# WRITE THE FILESYSTEM

                    int fidx = -1;
                    FstFile lastFile = new FstFile(null) { DataOffset = hdr.ReadUInt32B(0x424), Offset = hdr.ReadUInt32B(0x424), Length = fst.Size };
                    FstFile nextFile = fstFiles[++fidx];
                    FstFile cacheFile = null;

                    long nullsPos = dstPos + 0x1c;
                    if (nullsPos % 4 != 0)
                        nullsPos += 4 - (nullsPos % 4);

                    //########### FILES
                    foreach (FstFile f in srcFiles) //read the files and write them out as goodFiles (possible order difference)
                    {
                        while ((cacheFile = cache.Keys.FirstOrDefault(a => (a.Length == nextFile.Length || (inStream.IsNkit && a.Length == 0)) && a.Name == nextFile.Name && a.Path == nextFile.Path)) != null) //write cache
                        {
                            bool isJunk = inStream.IsNkit && cacheFile.Length == 0 && cacheFile.Name == nextFile.Name && cacheFile.Path == nextFile.Path;
                            using (MemoryStream cacheStream = new MemoryStream(cache[cacheFile]))
                                dstPos += writeFile(ref nullsPos, target, dstPos, ref firstFile, cacheStream, lastFile, nextFile, patches, inStream, junkStart, isJunk);
                            cache.Remove(cacheFile);
                            lastFile = nextFile;
                            nextFile = fidx + 1 < fstFiles.Count ? fstFiles[++fidx] : null;
                        }

                        if (f.DataOffset - srcPos > 0) //skip src junk
                        {
                            inStream.Copy(ByteStream.Zeros, f.DataOffset - srcPos);
                            srcPos += f.DataOffset - srcPos;
                        }
                        if (nextFile != null && (f.Length != nextFile.Length || f.Name != nextFile.Name || f.Path != nextFile.Path)) //cache file (nkit junk files are cached)
                        {
                            bool isNkitJunk = inStream.IsNkit && f.Length == 0 && nextFile.Length != 0 && f.Name == nextFile.Name && f.Path == nextFile.Path;

                            byte[] cacheItem = new byte[isNkitJunk ? 8 : f.Length];
                            inStream.Read(cacheItem, 0, cacheItem.Length); //read the nkit junk data - details (real length and null counts)
                            cache.Add(f, cacheItem);
                            srcPos += cacheItem.Length;
                        }
                        else //copy file
                        {
                            dstPos += writeFile(ref nullsPos, target, dstPos, ref firstFile, inStream, lastFile, nextFile, patches, inStream, junkStart, false);
                            lastFile = nextFile;
                            nextFile = fidx + 1 < fstFiles.Count ? fstFiles[++fidx] : null;
                            srcPos += f.Length;
                        }
                    }

                    while (nextFile != null && fidx < fstFiles.Count && (cacheFile = cache.Keys.FirstOrDefault(a => a.Length == nextFile.Length && a.Name == nextFile.Name && a.Path == nextFile.Path)) != null)
                    {
                        using (MemoryStream cacheStream = new MemoryStream(cache[cacheFile]))
                            dstPos += writeFile(ref nullsPos, target, dstPos, ref firstFile, cacheStream, lastFile, nextFile, patches, inStream, junkStart, false);
                        cache.Remove(cacheFile);
                        lastFile = nextFile;
                        nextFile = fidx + 1 < fstFiles.Count ? fstFiles[++fidx] : null;
                    }

                    writeDestGap(nullsPos, target, dstPos, pc.OutputSize - dstPos, true, patches, junkStart, inStream);

                    crc.Snapshot("files");
                }

                resultMsg = "MatchFail";
                uint finalCrc = crc.FullCrc(true);
                if (ctx.Dats.RedumpData.FirstOrDefault(a => a.Crc == finalCrc) != null)
                    resultMsg = "Match Redump";
                if (ctx.Dats.CustomData.FirstOrDefault(a => a.Crc == finalCrc) != null)
                    resultMsg = "Match Custom";

                pc.ReaderCheckPoint2Complete(crc, false, finalCrc, finalCrc, this.VerifyIsWrite, hdr.Data, resultMsg);
                pc.ReaderCheckPoint3Complete();
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "RestoreReaderGc.Read - Read and repair"); //don't let the writer lock
            }
        }

        private int scrubData(byte[] data, int offset, int length)
        {
            int headerRemoved = 0;
            for (int i = 0; i < length; i++)
            {
                if (data[i + offset] != 0)
                {
                    headerRemoved++;
                    data[i + offset] = 0;
                }
            }
            return headerRemoved;
        }

        private bool bruteForceValidHeader(Context ctx, FstFileItem goodFst, MemorySection hdr, MemorySection bi2bin, MemorySection appldr, MemorySection maindol, MemorySection fst, Coordinator pc)
        {
            int hdrScrub = 0;
            int bi2Scrub = 0;
            int maindolScrub = 0;

            foreach (int scrub in new int[] { 0, 1, 2 })
            {
                if (scrub == 1)
                {
                    hdrScrub = scrubData(hdr.Data, 0x300, 4);
                    bi2Scrub = scrubData(bi2bin.Data, 0x500 - 0x440, 4);
                    if (hdrScrub == 0 && bi2Scrub == 0)
                        continue; //no change
                }
                if (scrub == 2)
                {
                    hdrScrub = scrubData(hdr.Data, 0x80, (int)(0x3F0 - 0x80));
                    bi2Scrub = scrubData(bi2bin.Data, 0x30, bi2bin.Data.Length - (0x30 + 0x60)); //0x30 at start and 0x50 from end (conflict I, II + Splinter cell have 0x40 at end)
                    if (maindol.Size != 0)
                    {
                        uint maindolSize = 0;
                        for (int i = 0; i < 18; i++)
                        {
                            if (maindol.ReadUInt32B(0x0 + (i * 4)) != 0) //7 text offsets, 11 data offsets
                                maindolSize = Math.Max(maindolSize, maindol.ReadUInt32B(0x0 + (i * 4)) + maindol.ReadUInt32B(0x90 + (i * 4)));
                        }
                        maindolScrub = scrubData(maindol.Data, (int)maindolSize, (int)(maindol.Data.Length - maindolSize));
                    }
                    if (hdrScrub == 0 && bi2Scrub == 0 && maindolScrub == 0)
                        continue; //no change
                }


                NCrc crc = new NCrc();
                MemoryStream ms = new MemoryStream();
                using (CryptoStream target = new CryptoStream(ms, crc, CryptoStreamMode.Write))
                {
                    target.Write(hdr.Data, 0, (int)hdr.Size);
                    long dstPos = hdr.Data.Length;

                    target.Write(bi2bin.Data, 0, (int)bi2bin.Size);
                    dstPos += bi2bin.Size;

                    target.Write(appldr.Data, 0, (int)appldr.Size);
                    dstPos += appldr.Size;
                    ByteStream.Zeros.Copy(target, Math.Min(hdr.ReadUInt32B(0x420), goodFst.FstOffset) - dstPos);
                    dstPos += Math.Min(hdr.ReadUInt32B(0x420), goodFst.FstOffset) - dstPos;

                    target.Write(maindol.Data, 0, Math.Min((int)(goodFst.FstOffset - dstPos), (int)maindol.Size));
                    dstPos += Math.Min((int)(goodFst.FstOffset - dstPos), (int)maindol.Size);

                    long padding = goodFst.FstOffset - dstPos;
                    ByteStream.Zeros.Copy(target, padding);
                    dstPos += padding;

                    target.Write(fst.Data, 0, fst.Data.Length);
                    dstPos += fst.Size;

                    Dictionary<FstFile, byte[]> cache = new Dictionary<FstFile, byte[]>();
                    crc.Snapshot("header");
                }
                if (crc.FullCrc() == goodFst.PostFstCrc)
                {
                    if (hdrScrub != 0 || bi2Scrub != 0 || maindolScrub != 0)
                    {
                        //ctx.Result.GameCube.UpdatedHeader = true;
                        _log?.LogDetail("Header brute forced to match");
                        if (scrub == 1)
                            _log?.LogDetail(string.Format("  Bytes scrubbed at 0x300 and 0x500", hdrScrub.ToString(), bi2Scrub.ToString()));
                        else if (scrub == 2)
                            _log?.LogDetail(string.Format("  Bytes scrubbed in hdr.bin ({0}), bi2.bin ({1}), main.dol ({2})", hdrScrub.ToString(), bi2Scrub.ToString(), maindolScrub.ToString()));
                    }
                    return true;
                }
            }
            return false;
        }

        private long writeFile(ref long nullsPos, Stream dest, long dstPos, ref bool firstFile, Stream srcStream, FstFile lastFile, FstFile file, List<JunkRedumpPatch> patches, NStream nstream, long junkStart, bool isJunkFile)
        {
            //Debug.WriteLine(string.Format(@"{0} : {1} : {2} : {3}/{4}", file.DataOffset.ToString("X8"), (file.DataOffset + file.Length).ToString("X8"), /*(nextFile.DataOffset - lastEnd).ToString("X8"),*/ file.Length.ToString("X8"), file.Path, file.Name));

            //file found
            long gap = writeDestGap(nullsPos, dest, dstPos, file.DataOffset - dstPos, firstFile, patches, junkStart, nstream);

            firstFile = false;

            bool missing = false;

            if (file.Length == 0)
            {
                //    missing = true;
            }
            else if (isJunkFile)
            {
                missing = true;
                MemorySection ms = MemorySection.Read(srcStream, 8);

                long size = ms.ReadUInt32B(0);
                GapType gt = (GapType)(size & 0b11);
                size &= 0xFFFFFFFC;

                //set nullsPos value if zerobyte file without junk
                if (gt == GapType.JunkFile)
                {
                    nullsPos = Math.Min(nullsPos - (dstPos + gap), 0); //reset the nulls
                    long nulls = (size & 0xFC) >> 2;
                    long junkFileLen = ms.ReadUInt32B(4);
                    if (junkFileLen != file.Length)
                        throw new HandledException(string.Format("NKit Junk file restoration length mismatch {0}: {1}", file.DataOffset.ToString("X8"), file.Name));
                    ByteStream.Zeros.Copy(dest, nulls);
                    nstream.JunkStream.Position = (dstPos + gap) + nulls;
                    nstream.JunkStream.Copy(dest, junkFileLen - nulls);
                    _log?.LogDetail(string.Format("Generated file content with Junk {0}: {1}", file.DataOffset.ToString("X8"), file.Name));
                }
                else
                    throw new HandledException(string.Format("NKit Junk file restoration bytes invalid {0}: {1}", file.DataOffset.ToString("X8"), file.Name));
            }
            else
            {
                byte[] f = new byte[Math.Min(0x30, file.Length)];
                srcStream.Read(f, 0, f.Length); //then read while junk is created

                if (lastFile.DataOffset == file.DataOffset && lastFile.Length == 0) //null file overlapped this file so set nullsPos to have a gap (XGIII) needs fst sorting by offset then size
                    nullsPos = dstPos + 0x1CL; //will already be aligned

                int nulls = (int)(nullsPos - (dstPos + gap));
                nstream.JunkStream.Position = file.DataOffset; //async junk gen
                int countNulls = 0;
                for (int i = 0; i < f.Length && f[i] == 0; i++)
                    countNulls++;
                if (f.Length > nulls && countNulls < f.Length) //don't test all nulls
                    missing = nstream.JunkStream.Compare(f, 0, f.Length, Math.Max(0, nulls)) == f.Length;
                if (missing)
                    _log?.LogDetail(string.Format("File content is Junk {0}: {1}", file.DataOffset.ToString("X8"), file.Name));
                dest.Write(f, 0, f.Length);
                srcStream.Copy(dest, file.Length - f.Length); //copy file
            }

            if (!missing) //reset the gap when no junk
            {
                nullsPos = dstPos + gap + file.Length + 0x1c;
                if (nullsPos % 4 != 0)
                    nullsPos += 4 - (nullsPos % 4);
            }


            return gap + file.Length;
        }

        private long writeDestGap(long nullsPos, Stream dest, long pos, long size, bool firstOrLastFile, List<JunkRedumpPatch> patches, long junkStart, NStream nstream) //gap 0 == 28 null, 1 == junk, -1 == auto detect
        {
            long nulls = 0;

            long maxNulls = Math.Max(0, nullsPos - pos); //0x1cL
            //maxNulls = 0x1c;
            if (size < maxNulls) //need to test this commented if
                nulls = size;
            else
            {
                nulls = maxNulls;
                if (size >= 0x40000 && !firstOrLastFile)
                {
                    nulls = 0;
                    if (pos % 0x4L != 0)
                        nulls += 0x4L - pos % 0x4L; //pad to 4 byte boundary
                }
            }

            if (junkStart != 0 && junkStart > pos)
            {
                nulls = Math.Min(junkStart - pos, size);
                if (junkStart < pos + size)
                    junkStart = 0; //revert to normal
            }

            if (patches != null)
                patches.RemoveAll(a => a.Offset < pos);

            long copied = 0;
            if (nulls != 0 && (patches.Count == 0 || patches[0].Offset != pos)) //if there are strange bytes in the padding then the full padding must be replaced (no cases as yet, never used)
            {
                ByteStream.Zeros.Copy(dest, nulls);
                pos += nulls;
                copied = nulls;
            }

            int c = 0;

            while (patches.Count != 0 && patches[0].Offset >= pos && patches[0].Offset < (pos - copied) + size)
            {
                if (patches.Count != 0 && patches[0].Offset > pos)
                {
                    nstream.JunkStream.Position = pos;
                    c = (int)(patches[0].Offset - pos);
                    nstream.JunkStream.Copy(dest, c);
                    copied += c;
                    pos += c;
                }
                c = (int)Math.Min(size - copied, patches[0].Data.Length);
                dest.Write(patches[0].Data, 0, c);
                _log?.LogDetail(string.Format("Junk patched at 0x{0} - {1} byte{2}", patches[0].Offset.ToString("X8"), patches[0].Data.Length.ToString(), patches[0].Data.Length == 1 ? "" : "s"));
                patches.RemoveAt(0);
                copied += c;
                pos += c;
                nulls = copied;
            }

            if (copied < size)
            {
                nstream.JunkStream.Position = pos;
                nstream.JunkStream.Copy(dest, size - nulls);
            }

            return size;

        }

        private string friendly(string text)
        {
            string f = text.Trim('\0') ?? "<NULL>";
            //if (Regex.IsMatch(f, "[^<>A-Z0-9-_+=]", RegexOptions.IgnoreCase))
            //    f = "Hex-" + BitConverter.ToString(Encoding.ASCII.GetBytes(f)).Replace("-", "");
            return f;
        }

    }
}
