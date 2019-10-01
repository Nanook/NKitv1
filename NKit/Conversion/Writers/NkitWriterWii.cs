using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class NkitWriterWii : IWriter
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }
        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Write(Context ctx, Stream inStream, Stream outStream, Coordinator pc)
        {
            try
            {
                WiiDiscHeaderSection hdr = null;
                WiiPartitionHeaderSection pHdr = null;
                string lastPartitionId = null;
                PartitionType lastPartitionType = PartitionType.Other;
                NCrc crc = new NCrc();
                Crc updateCrc = new Crc();
                bool updateRemoved = false;
                string updateTmpFileName = null;
                string updateFileName = null;
                bool extractingUpdate = false;
                CryptoStream updateCrcStream = null;
                NStream updateTarget = null;
                CryptoStream target = null;
                MemorySection removedUpdateFiller = null;
                int preservedHashCount = 0;

                NkitInfo nkitDiscInfo = new NkitInfo();
                long fstFileAlignment = -1;

                WiiPartitionSection lastPart = null;

                long dstPos = 0;

                long imageSize = pc.OutputSize; //for Wii: pHdr.PartitionDataLength
                string ignoreJunkId;
                pc.WriterCheckPoint1WriteReady(out ignoreJunkId); //wait until read has written the header and set the length

                NDisc disc = new NDisc(_log, inStream);

                foreach (IWiiDiscSection s in disc.EnumerateSections(imageSize)) //no fixing, image should be good //ctx.ImageLength
                {
                    if (s is WiiDiscHeaderSection)
                    {
                        hdr = (WiiDiscHeaderSection)s;
                        hdr.Write8(0x60, 1);
                        hdr.Write8(0x61, 1);

                        fstFileAlignment = ctx?.Settings?.PreserveFstFileAlignment?.FirstOrDefault(a => a.Item1 == hdr.Id8)?.Item2 ?? -1;

                        target = new CryptoStream(outStream, crc, CryptoStreamMode.Write);
                        target.Write(hdr.Data, 0, hdr.Data.Length); //write the header
                        nkitDiscInfo.BytesData += hdr.Size;
                        dstPos += hdr.Size;

                        crc.Snapshot("Disc Header");
                    }
                    else if (s is WiiPartitionSection)
                    {
                        WiiPartitionSection ps = (WiiPartitionSection)s;
                        WiiHashStore hashes = new WiiHashStore(ps.PartitionDataLength);
                        ScrubManager scrub = ps.Header.ScrubManager;

                        NkitInfo nkitPartInfo = new NkitInfo();

                        if (ps.Header.Type == PartitionType.Update && ctx.Settings.NkitUpdatePartitionRemoval && hdr.Partitions.Count() > 1) //only remove update if there's more partitions
                        {
                            updateRemoved = true;
                            extractingUpdate = true;

                            pHdr = ps.Header;
                            extractingUpdate = true;
                            updateCrcStream = new CryptoStream(ByteStream.Zeros, updateCrc, CryptoStreamMode.Write);
                            crc.Initialize();
                            nkitPartInfo.BytesData += disc.WriteRecoveryPartitionData(updateCrcStream, false, ps, 0, out updateTmpFileName, out updateFileName, out updateTarget);
                            _log?.LogDetail(string.Format("Extracted and Removed {0} Recovery Partition: {1}", ps.Header.Type.ToString(), ps.Header.Id.ToString()));
                            removedUpdateFiller = new MemorySection(new byte[0x8000]);
                            removedUpdateFiller.Write(0, hdr.Read(0x40000, 0x100)); //backup the original partition table in case it's non standard in some way
                        }
                        else
                        {
                            target.Write(ps.Header.Data, 0, (int)ps.Header.Size);
                            nkitDiscInfo.BytesData += ps.Header.Size;

                            crc.Snapshot(ps.Id + " Hdr");
                            crc.Crcs.Last().PatchData = ps.Header.Data;
                            dstPos += ps.Header.Size;
                            ps.NewDiscOffset = dstPos;

                            //long written = 0;
                            using (StreamCircularBuffer decrypted = new StreamCircularBuffer(ps.PartitionDataLength, null, null, output =>
                            {
                                //read decrypted partition
                                foreach (WiiPartitionGroupSection pg in ps.Sections)
                                {
                                    nkitPartInfo.BytesHashesData += (pg.Size / 0x8000 * 0x400); //size of input hashes

                                    if (pg.PreserveHashes())
                                    {
                                        nkitPartInfo.BytesHashesPreservation += hashes.Preserve(pg.Offset, pg.Decrypted, pg.Size);
                                        if (++preservedHashCount >= 1500) //too many will bomb the patch caching when converting back. Something is wrong
                                            throw pc.SetWriterException(new HandledException("Over 1500 hashes preserved, aborting as image is corrupt or poorly scrubbed."));
                                    }
#if !DECRYPT
                                    for (int i = 0; i < pg.Size / 0x8000; i++)
                                        output.Write(pg.Decrypted, (i * 0x8000) + 0x400, 0x7c00);
#else
                            output.Write(pg.Decrypted, 0, (int)pg.Size);
#endif
                                }
                            }))
                            {
                                long len = partitionWrite(decrypted, crc, target, ps, ctx, pc, nkitPartInfo, scrub, hashes, fstFileAlignment);
                                ps.NewPartitionDataLength = len;
                                dstPos += len;
                                lastPart = ps;
                            }
                        }
                        NkitFormat.LogNkitInfo(nkitPartInfo, _log, ps.Id, false);

                        lastPartitionId = ps.Id;
                        lastPartitionType = ps.Header.Type;
                    }
                    else if (s is WiiFillerSection)
                    {
                        WiiFillerSection fs = (WiiFillerSection)s;
                        ScrubManager scrub = new ScrubManager(null);
                        JunkStream junk = new JunkStream(lastPartitionType != PartitionType.Data ? hdr.ReadString(0, 4) : lastPartitionId, hdr.Read8(6), lastPartitionType == PartitionType.Update ? 0 : imageSize);

                        if (lastPartitionType == PartitionType.Update && updateRemoved)
                        {
                            //preserve the original partition table and update filename
                            target.Write(removedUpdateFiller.Data, 0, (int)removedUpdateFiller.Size); //remove update partition by adding a 32k placeholder. Avoid having a non update partition at 0x50000
                            nkitDiscInfo.BytesPreservationDiscPadding += removedUpdateFiller.Size;
                            nkitDiscInfo.BytesGaps += fs.Size;
                            dstPos += removedUpdateFiller.Size;

                            crc.Snapshot(string.Format("{0}{1}Replacement Filler", lastPartitionId ?? "", string.IsNullOrEmpty(lastPartitionId) ? "" : " "));
                            if (extractingUpdate)
                            {
                                int storeType;
                                if ((storeType = disc.WriteRecoveryPartitionFiller(updateCrcStream, junk, fs.DiscOffset, true, true, fs, updateTarget, updateTmpFileName, ref updateFileName, updateCrc, true)) != 0)
                                    _log.LogDetail(string.Format("{0}Update recovery partition stored: {1}", storeType == 2 ? "Other " : "", updateFileName));
                                extractingUpdate = false;
                            }
                        }
                        else
                        {
                            if (fs.Size != 0)
                            {
                                using (StreamCircularBuffer filler = new StreamCircularBuffer(fs.Size, null, null, output =>
                                {
                                    foreach (WiiFillerSectionItem item in ((WiiFillerSection)s).Sections)
                                        output.Write(item.Data, 0, (int)item.Size);
                                }))
                                {
                                    Gap gap = new Gap(fs.Size, false);
                                    long srcPos = fs.DiscOffset;
                                    long gapLen = gap.Encode(filler, ref srcPos, lastPartitionType == PartitionType.Update ? fs.Size : 0x1cL, fs.Size, junk, scrub, target, _log);
                                    nkitDiscInfo.BytesPreservationData += gapLen;
                                    nkitDiscInfo.BytesGaps += fs.Size;
                                    dstPos += gapLen;
                                    if (lastPart != null)
                                        lastPart.NewPartitionDataLength += gapLen;
                                }
                            }
                            //pad partition to 32k
                            int gapLen2 = (int)(dstPos % 0x8000 == 0 ? 0 : 0x8000 - (dstPos % 0x8000));
                            ByteStream.Zeros.Copy(target, gapLen2);
                            nkitDiscInfo.BytesPreservationDiscPadding += gapLen2;
                            dstPos += gapLen2;
                            if (lastPart != null)
                                lastPart.NewPartitionDataLength += gapLen2;

                            if (lastPart != null)
                            {
                                lastPart.Header.WriteUInt32B(0x2bc, (uint)(lastPart.NewPartitionDataLength / 4)); //updates the array in the crc data
                                hdr.Partitions.First(a => a.DiscOffset == lastPart.DiscOffset).DiscOffset = (lastPart.NewDiscOffset - 0x20000);
                            }
                            crc.Snapshot(string.Format("{0}{1}Files+Filler", lastPartitionId ?? "", string.IsNullOrEmpty(lastPartitionId) ? "" : " "));
                        }
                    }
                }

                NkitFormat.LogNkitInfo(nkitDiscInfo, _log, hdr.Id, true);

                foreach (CrcItem ci in crc.Crcs.Where(a => a.PatchData != null))
                    ci.PatchCrc = Crc.Compute(ci.PatchData);

                NCrc readerCrcs;
                uint validationCrc;
                pc.WriterCheckPoint2Complete(out readerCrcs, out validationCrc, hdr.Data, dstPos); //wait until reader has completed and get crc patches.

                if (updateRemoved && crc.Crcs.Length > 2) //freeloader wii only has update partition
                    hdr.RemoveUpdatePartition(crc.Crcs[2].Offset);

                hdr.UpdateOffsets();
                hdr.WriteString(0x200, 8, "NKIT v01"); //header and version
                hdr.Write8(0x60, 1);
                hdr.Write8(0x61, 1);
                hdr.WriteUInt32B(0x208, readerCrcs.FullCrc(true)); //original crc
                hdr.WriteUInt32B(0x210, (uint)(imageSize / 4L)); //ctx.ImageLength
                hdr.WriteUInt32B(0x218, updateCrc.Value); //Update crc - Only if removed
                crc.Crcs[0].PatchCrc = Crc.Compute(hdr.Data);
                crc.Crcs[0].PatchData = hdr.Data;
                hdr.WriteUInt32B(0x20C, CrcForce.Calculate(crc.FullCrc(true), dstPos, readerCrcs.FullCrc(true), 0x20C, 0)); //magic to force crc
                crc.Crcs[0].PatchCrc = Crc.Compute(hdr.Data); //update with magic applied

                pc.WriterCheckPoint3ApplyPatches(crc, false, crc.FullCrc(true), crc.FullCrc(true), this.VerifyIsWrite, "NKit Written");
            }
            catch (Exception ex)
            {
                throw pc.SetWriterException(ex, "NkitWriterWii.Write - Convert");
            }
        }

        private long partitionWrite(Stream inStream, NCrc crc, Stream target, WiiPartitionSection pHdr, Context ctx, Coordinator pc, NkitInfo imageInfo, ScrubManager scrub, WiiHashStore hashes, long fstFileAlignment)
        {
#if DEHASH
            return pHdr.NewPartitionDataLength = inStream.Copy(target, pHdr.PartitionDataLength, null);
#elif DECRYPT
            inStream.Copy(target, pHdr.PartitionLength, null);
            return pHdr.NewPartitionDataLength = pHdr.PartitionDataLength; 
#endif
            MemorySection hdr = MemorySection.Read(inStream, 0x440);

            //ProgressResult result = ctx.Result;
            long mlt = 4L; //GC 1L
            long logicalSize = pHdr.PartitionDataLength; //GC: ctx.ImageLength;
            long physicalSize = pHdr.PartitionDataLength; //GC: result.ImageInfo.IsoSize;
            string junkId = hdr.ReadString(0, 4);
            //bool write = true;

            List<string> addedFiles = new List<string>();

            long srcPos;
            long dstPos = 0;

            JunkStream js = new JunkStream(junkId, hdr.Read8(6), logicalSize);

            try
            {
                if (junkId == "\0\0\0\0")
                {
                    srcPos = hdr.Size;
                    imageInfo.BytesData = srcPos;
                    _log?.LogDetail("Null Partition ID, preserving partition as raw");
                    ConvertFile cf = new ConvertFile(logicalSize - hdr.Size, true) //Size isn't important for writing //result.ImageInfo.IsoSize
                    {
                        FstFile = new FstFile(null) { DataOffset = hdr.Size, Offset = hdr.Size, Length = 0 },
                    };
                    long nullsPos = 0;
                    target.Write(hdr.Data, 0, (int)hdr.Size); //0x400
                    target.Write(pHdr.Header.Data, 0x2bc, 4); //copy the original partition length
                    dstPos += 0x440 + 4 + NkitFormat.ProcessGap(ref nullsPos, cf, ref srcPos, inStream, js, true, scrub, target, _log);
                }
                else
                {
                    hdr.WriteString(0x200, 8, "NKIT v01");
                    hdr.WriteUInt32B(0x210, (uint)(pHdr.PartitionLength / mlt));

                    MemorySection fst;
                    List<JunkDiff> junkDiffs = new List<JunkDiff>();
                    long mainDolAddr = hdr.ReadUInt32B(0x420) * mlt;

                    //############################################################################
                    //# READ DISC START
                    target.Write(hdr.Data, 0, (int)hdr.Size);

                    inStream.Copy(target, (hdr.ReadUInt32B(0x424) * mlt) - hdr.Size);

                    //read fst with 4 byte boundary
                    fst = MemorySection.Read(inStream, (hdr.ReadUInt32B(0x428) * mlt) + (((hdr.ReadUInt32B(0x428) * mlt) % 4 == 0 ? 0 : 4 - ((hdr.ReadUInt32B(0x428) * mlt) % 4))));
                    crc.Snapshot(junkId + " PrtHdr");
                    target.Write(fst.Data, 0, (int)fst.Size);
                    crc.Snapshot(junkId + " Fst");
                    target.Write(hashes.FlagsToByteArray(), 0, (int)hashes.FlagsLength);
                    crc.Snapshot(junkId + " HashFlags");

                    srcPos = (hdr.ReadUInt32B(0x424) * mlt) + fst.Size;

                    long nullsPos = srcPos + 0x1c;
                    dstPos = srcPos + hashes.FlagsLength;

                    //create as late as possible in case id is swaped  - Dairantou Smash Brothers DX (Japan) (Taikenban), Star Wars - Rogue Squadron II (Japan) (Jitsuen-you Sample)

                    string error;
                    List<ConvertFile> conFiles = NkitFormat.GetConvertFstFiles(inStream, physicalSize, hdr, fst, false, fstFileAlignment, out error);

                    imageInfo.BytesData = srcPos;

                    if (conFiles == null)
                    {
                        if (error != null)
                            _log?.LogDetail(error);
                        ConvertFile cf = new ConvertFile(pHdr.PartitionDataLength - srcPos, true) //Size isn't important for writing //result.ImageInfo.IsoSize
                        {
                            FstFile = new FstFile(null) { DataOffset = hdr.ReadUInt32B(0x424), Offset = hdr.ReadUInt32B(0x424), Length = (int)fst.Size },
                        };
                        dstPos += NkitFormat.ProcessGap(ref nullsPos, cf, ref srcPos, inStream, js, true, scrub, target, _log);
                    }
                    else
                    {
                        //############################################################################
                        //# WRITE THE FILESYSTEM
                        List<ConvertFile> missing;
                        NkitFormat.NkitWriteFileSystem(ctx, imageInfo, mlt, inStream, ref srcPos, ref dstPos, hdr, fst, ref mainDolAddr, target, nullsPos, js, conFiles, out missing, scrub, pHdr.PartitionDataLength, _log);
                        dstPos += hashes.HashesToStream(target);

                        if (missing.Count != 0)
                        {
                            _log?.LogDetail(string.Format("{0} Junk File{1} Removed (Files listed in the FST, but not in the image)", missing.Count.ToString(), missing.Count == 1 ? "" : "s"));
                            foreach (ConvertFile cf in missing)
                                _log?.LogDebug(string.Format("File content is Junk {0}: {1} - Size: {2}", cf.FstFile.DataOffset.ToString("X8"), cf.FstFile.Name, cf.FstFile.Length));
                        }
                        crc.Crcs[crc.Crcs.Length - 1].PatchData = hashes.FlagsToByteArray();
                        crc.Crcs[crc.Crcs.Length - 1].PatchCrc = Crc.Compute(crc.Crcs[crc.Crcs.Length - 2].PatchData);
                        crc.Crcs[crc.Crcs.Length - 2].PatchData = fst.Data;
                        crc.Crcs[crc.Crcs.Length - 2].PatchCrc = Crc.Compute(fst.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "NkitWriterGc.Write - Convert");
            }
            return dstPos;
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
