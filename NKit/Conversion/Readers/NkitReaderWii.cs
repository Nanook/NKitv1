using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class NkitReaderWii : IReader
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }

        private class LongRef
        {
            public long Value;
        }
        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }
        public void Read(Context ctx, NStream inStream, Stream outStream, Coordinator pc)
        {
            try
            {
                WiiDiscHeaderSection hdr = (WiiDiscHeaderSection)inStream.DiscHeader;
                string idVer = hdr.ReadString(0x200, 8);
                if (idVer != "NKIT v01")
                    throw new Exception(string.Format("{0} not supported by this version", idVer));
                bool isNkit = idVer.StartsWith("NKIT");
                uint nkitCrc = hdr.ReadUInt32B(0x208);
                long imageSize = hdr.ReadUInt32B(0x210) * 4L;
                string junkId = hdr.ReadString(0x214, 4);
                uint updatePartitionCrc = hdr.ReadUInt32B(0x218);
                MemorySection updateRemovedFiller = null;

                long discOffset = 0;
                List<NkitPartitionPatchInfo> patchInfos = new List<NkitPartitionPatchInfo>();
                discOffset += hdr.Size;
                string lastPartitionId = null;
                PartitionType lastPartitionType = PartitionType.Other;
                NCrc crc = new NCrc();
                long dstPos = 0;
                long srcPos = hdr.Size;
                ScrubManager scrubFiller = new ScrubManager(null);
                bool isRecoverable = false;

                using (NDisc disc = new NDisc(_log, inStream))
                {
                    hdr.WriteUInt32B(0x200, 0);
                    hdr.WriteUInt32B(0x204, 0);
                    hdr.WriteUInt32B(0x208, 0);
                    hdr.WriteUInt32B(0x20C, 0);
                    hdr.WriteUInt32B(0x210, 0);
                    hdr.WriteUInt32B(0x214, 0);
                    hdr.WriteUInt32B(0x218, 0);

                    hdr.Write8(0x60, 0);
                    hdr.Write8(0x61, 0);

                    CryptoStream crcStream = new CryptoStream(outStream, crc, CryptoStreamMode.Write); //wrap to calculate crc
                    crcStream.Write(hdr.Data, 0, hdr.Data.Length); //write the header
                    pc.ReaderCheckPoint1PreWrite(null, nkitCrc); //size that will be output from this read
                    dstPos += hdr.Size;

                    crc.Snapshot("Disc Header");

                    foreach (WiiPartitionInfo part in hdr.Partitions) //already sorted
                    {
                        if (updatePartitionCrc != 0 && srcPos == hdr.Size) //write update partition out
                        {
                            updateRemovedFiller = MemorySection.Read(inStream, hdr.Partitions.First().DiscOffset - srcPos);
                            srcPos += updateRemovedFiller.Size;
                            WiiPartitionInfo firstPart = WiiDiscHeaderSection.CreatePartitionInfos(updateRemovedFiller, 0)?.FirstOrDefault(a => a.Type != PartitionType.Update);
                            string updateFileName = RecoveryData.GetUpdatePartition(ctx.Settings, updatePartitionCrc);

                            if (updateFileName != null)
                            {
                                using (FileStream pf = File.OpenRead(updateFileName))
                                {
                                    pf.Copy(crcStream, pf.Length);
                                    dstPos += pf.Length;
                                }
                            }
                            else
                            {
                                string msg = string.Format("!! Update partition *_{0} missing - Adding filler. It may be Recoverable", updatePartitionCrc.ToString("X8"));
                                isRecoverable = true;
                                _log?.LogDetail(msg);
                                //throw pc.SetReaderException(new HandledException("Failed to convert: " + msg));
                            }
                            ByteStream.Zeros.Copy(crcStream, firstPart.DiscOffset - dstPos); //fill full gap
                            dstPos += firstPart.DiscOffset - dstPos;
                        }

                        NkitPartitionPatchInfo patchInfo = new NkitPartitionPatchInfo() { HashGroups = new Dictionary<long, MemorySection>() };
                        patchInfos.Add(patchInfo);

                        if (part.DiscOffset > srcPos)
                        {
                            dstPos += writeFiller(ref srcPos, dstPos, dstPos + 0x1cL, inStream, crcStream, new JunkStream(lastPartitionType != PartitionType.Data ? hdr.ReadString(0, 4) : lastPartitionId, hdr.Read8(6), lastPartitionType == PartitionType.Update ? 0 : imageSize), scrubFiller);
                            inStream.Copy(ByteStream.Zeros, part.DiscOffset - srcPos); //padded to 0x8000
                            srcPos += part.DiscOffset - srcPos;
                        }

                        part.DiscOffset = dstPos; //restore the original position
                        patchInfo.DiscOffset = dstPos;
                        patchInfo.PartitionHeader = MemorySection.Read(inStream, 0x20000);
                        srcPos += patchInfo.PartitionHeader.Size;
                        long size = patchInfo.PartitionHeader.ReadUInt32B(0x2bc) * 4L;
                        LongRef origSize = new LongRef() { Value = 0 };
                        WiiPartitionGroupSection wp = null;
                        WiiPartitionHeaderSection wh = new WiiPartitionHeaderSection(hdr, null, part.DiscOffset, patchInfo.PartitionHeader.Data, patchInfo.PartitionHeader.Data.Length);
                        MemorySection ph = new MemorySection(new byte[0x8000 * 64]);
                        long remaining = long.MaxValue; //set after first block read
                        int groupIndex = 0;
                        WiiHashStore hashes = new WiiHashStore();
                        patchInfo.ScrubManager = wh.ScrubManager;
                        bool patchBlock = false;
                        StreamCircularBuffer prtStream = null;

                        try
                        {
                            using (prtStream = new StreamCircularBuffer(0, null, null, output => srcPos += partitionStreamWrite(origSize, inStream, output, size, ctx.Dats, patchInfo, hashes, pc)))
                            {
                                int gs = 0;
                                int ge = 0;
                                int i = 0;
                                MemoryStream patchBlocks = null;

                                while (remaining > 0)
                                {
                                    int blocks = (int)Math.Min(64L, remaining / 0x7c00);
                                    for (int b = 0; b < blocks; b++)
                                    {
                                        prtStream.Read(ph.Data, (b * 0x8000) + 0x400, 0x7c00); //load aligned with no hashes

                                        if (remaining == long.MaxValue) //first loop
                                        {
                                            remaining = origSize.Value;

                                            if (ph.ReadString(0x400 + 0, 4) == "\0\0\0\0")
                                            {
                                                gs = -1;
                                                ge = -1;
                                                blocks = (int)Math.Min(64L, remaining / 0x7c00);
                                                lastPartitionId = ph.ReadString(0x400 + 0, 4);
                                                patchInfo.PartitionHeader.WriteUInt32B(0x2bc, (uint)(NStream.DataToHashedLen(origSize.Value) / 4)); //restore real size
                                                crcStream.Write(patchInfo.PartitionHeader.Data, 0, patchInfo.PartitionHeader.Data.Length);
                                                dstPos += patchInfo.PartitionHeader.Size;
                                            }
                                            else
                                            {
                                                gs = (int)((ph.ReadUInt32B(0x400 + 0x424) * 4L) / (0x7c00L * 64));
                                                ge = (int)(((ph.ReadUInt32B(0x400 + 0x424) * 4L) + (ph.ReadUInt32B(0x400 + 0x428) * 4L)) / (0x7c00L * 64));
                                                if ((int)((part.DiscOffset + (ph.ReadUInt32B(0x400 + 0x428) * 4L)) % (0x7c00L * 64)) == 0)
                                                    ge--; //don't load the next group if the data will end on the last byte

                                                blocks = (int)Math.Min(64L, remaining / 0x7c00);
                                                lastPartitionId = ph.ReadString(0x400 + 0, 4);

                                                patchInfo.PartitionHeader.WriteUInt32B(0x2bc, ph.ReadUInt32B(0x400 + 0x210)); //restore real size
                                                crcStream.Write(patchInfo.PartitionHeader.Data, 0, patchInfo.PartitionHeader.Data.Length);
                                                dstPos += patchInfo.PartitionHeader.Size;

                                                ph.WriteUInt32B(0x400 + 0x200, 0);
                                                ph.WriteUInt32B(0x400 + 0x204, 0);
                                                ph.WriteUInt32B(0x400 + 0x208, 0);
                                                ph.WriteUInt32B(0x400 + 0x20C, 0);
                                                ph.WriteUInt32B(0x400 + 0x210, 0);
                                                ph.WriteUInt32B(0x400 + 0x214, 0);
                                                ph.WriteUInt32B(0x400 + 0x218, 0);
                                            }
                                            wp = new WiiPartitionGroupSection(hdr, wh, ph.Data, part.DiscOffset, blocks * 0x8000, false);
                                            wh.Initialise(wp, origSize.Value);
                                        }
                                    }

                                    if (blocks < 64)
                                        Array.Clear(ph.Data, blocks * 0x8000, ph.Data.Length - (blocks * 0x8000)); //clear remaining blocks


                                    wp.Populate(groupIndex, ph.Data, dstPos, blocks * 0x8000);

                                    int scrubbed = 0;
                                    for (int bi = 0; bi < blocks; bi++)
                                    {
                                        wp.MarkBlockDirty(bi);
                                        byte byt;
                                        if (patchInfo.ScrubManager.IsBlockScrubbedScanMode(wp.Offset + (bi * 0x8000), out byt))
                                        {
                                            wp.SetScrubbed(bi, byt);
                                            scrubbed++;
                                        }
                                    }
                                    bool isFstBlocks = i >= gs && i <= ge;
                                    bool reqHashes = hashes.IsPreserved(wp.Offset); //test with 0 partition based offset

                                    repairBlocks(wp, scrubbed, blocks, false, isFstBlocks); //only test if the hashes aren't preserved (only preserved for scrubbed/customs)

                                    if (reqHashes) //store with disc based offset
                                        patchInfo.HashGroups.Add(wp.Offset + part.DiscOffset + patchInfo.PartitionHeader.Size, new MemorySection((byte[])wp.Decrypted.Clone())); //fetch the stored hashed that couldn't be recreated

                                    groupIndex++;
                                    bool inFstArea = i >= gs && i <= ge;

                                    if (!patchBlock && (gs == i || reqHashes))
                                    {
                                        patchBlocks = new MemoryStream();
                                        crc.Snapshot(lastPartitionId + " Data");
                                        patchBlock = true;
                                    }
                                    else if (patchBlock && !inFstArea && !reqHashes)
                                    {
                                        crc.Snapshot(lastPartitionId + " Patch");
                                        crc.Crcs.Last().PatchData = patchBlocks.ToArray();
                                        patchBlocks.Dispose();
                                        patchBlock = false;
                                    }
#if DECRYPT
                            outStream.Write(wp.Decrypted, 0, blocks * 0x8000);
                            if (i >= gs && i <= ge)
                                fstBlocks.Write(wp.Decrypted, 0, blocks * 0x8000);
#else
                                    crcStream.Write(wp.Encrypted, 0, blocks * 0x8000);
                                    if (patchBlock)
                                        patchBlocks.Write(wp.Encrypted, 0, blocks * 0x8000);
#endif

                                    remaining -= (blocks * 0x7c00);
                                    dstPos += (blocks * 0x8000);
                                    i++;
                                }
                                if (patchBlock)
                                {
                                    crc.Snapshot(lastPartitionId + " Patch");
                                    crc.Crcs.Last().PatchData = patchBlocks.ToArray();
                                    patchBlocks.Dispose();
                                }
                            }
                            if (origSize.Value != prtStream.WriterPosition)
                                throw pc.SetReaderException(new HandledException("Partition read did not write the full amount to the circular buffer before completing"));

                        }
                        catch (Exception ex)
                        {
                            if (prtStream?.WriterException != null)
                                throw pc.SetReaderException(prtStream.WriterException, "Failed reading filesystem");
                            throw pc.SetReaderException(ex, "Failed converting the filesystem"); ; //writer exception
                        }

                        srcPos += hashes.ReadPatchData(part.DiscOffset + patchInfo.PartitionHeader.Size, patchInfo.HashGroups, inStream);

                        //read hash patches
                        lastPartitionType = part.Type;

                    }

                    if (srcPos < inStream.Length)
                    {
                        JunkStream partJunk = new JunkStream(lastPartitionType != PartitionType.Data ? hdr.ReadString(0, 4) : lastPartitionId, hdr.Read8(6), lastPartitionType == PartitionType.Update ? 0 : imageSize);
                        dstPos += writeFiller(ref srcPos, dstPos, lastPartitionType == PartitionType.Update ? imageSize : dstPos + 0x1cL, inStream, crcStream, partJunk, scrubFiller);
                    }
                }

                crc.Snapshot("End");

                if (updatePartitionCrc != 0)
                    hdr.Write((int)WiiDiscHeaderSection.PartitionTableOffset, updateRemovedFiller.Data, 0, (int)WiiDiscHeaderSection.PartitionTableLength); //restore the exact partition table if update was removed
                else
                    hdr.UpdateOffsets(); //just update the table with the new offsets

                crc.Crcs[0].PatchData = hdr.Data;

                foreach (CrcItem ci in crc.Crcs.Where(a => a.PatchData != null))
                {
                    NkitPartitionPatchInfo patchInfo = patchInfos.FirstOrDefault(a => ci.Offset >= a.DiscOffset + a.PartitionHeader.Size && ci.Offset < a.DiscOffset + a.PartitionHeader.Size + a.Size);
                    if (patchInfo != null)
                        patchGroups(patchInfo, hdr, ci.Offset, ci.PatchData);
                    ci.PatchCrc = Crc.Compute(ci.PatchData);
                }

                if (imageSize != dstPos)
                    throw pc.SetReaderException(new HandledException("Nkit image read output {0} bytes not the expected {1}!", dstPos.ToString(), imageSize.ToString()));

                pc.ReaderCheckPoint2Complete(crc, isRecoverable, nkitCrc, crc.FullCrc(true), this.VerifyIsWrite, hdr.Data, nkitCrc == crc.FullCrc(true) ? "NKit Valid" : "NKit Invalid");
                pc.ReaderCheckPoint3Complete();
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "NkitReaderWii.Read - Read and convert");
            }
        }

        private bool repairBlocks(WiiPartitionGroupSection wp, int scrubbed, int blocks, bool forceBlockChanged, bool fstBlocks)
        {
            bool isValid = true;

            if (scrubbed == 0)
                isValid = wp.IsValid(true);
            else if (scrubbed == blocks) //copy the first data sector hash sector for each block (to pretend we still had the scrubbed hashes)
                Parallel.For(0, blocks, i => Array.Copy(wp.Decrypted, (i * 0x8000) + 0x400, wp.Decrypted, i * 0x8000, 0x400));
            return isValid;
        }

        private void patchGroups(NkitPartitionPatchInfo patchInfo, WiiDiscHeaderSection discHeader, long crcPatchOffset, byte[] crcPatchData)
        {
            long partDataOffset = patchInfo.DiscOffset + patchInfo.PartitionHeader.Size;
            int groupIdx = (int)((crcPatchOffset - partDataOffset) / WiiPartitionSection.GroupSize);
            byte[] data = new byte[WiiPartitionSection.GroupSize];

            WiiPartitionHeaderSection wh = new WiiPartitionHeaderSection(discHeader, null, partDataOffset, patchInfo.PartitionHeader.Data, patchInfo.PartitionHeader.Size);
            wh.ScrubManager = patchInfo.ScrubManager;

            wh.Initialise(true, patchInfo.PartitionDataHeader.ReadString(0, 4));
#if DECRYPT
            WiiPartitionGroupSection wp = new WiiPartitionGroupSection(discHeader, wh, ph.Data, , ph.Data.Length, false);
#else
            WiiPartitionGroupSection wp = new WiiPartitionGroupSection(discHeader, wh, data, partDataOffset, Math.Min(WiiPartitionSection.GroupSize, crcPatchData.Length), true);
#endif

            if (patchInfo.Fst != null)
            {
                using (MemoryStream fstStream = new MemoryStream(patchInfo.Fst.Data))
                    fstPatch(patchInfo, wp, crcPatchOffset, crcPatchData, fstStream);
            }

            MemorySection d = new MemorySection(crcPatchData);

            for (long i = 0; i < crcPatchData.Length; i += WiiPartitionSection.GroupSize)
            {
                Array.Copy(d.Data, (int)i, data, 0, Math.Min(WiiPartitionSection.GroupSize, d.Size - i));
                wp.Populate(groupIdx++, data, crcPatchOffset + i, Math.Min(WiiPartitionSection.GroupSize, d.Size - i));
                wp.ForceHashes(null);
                if (patchInfo.HashGroups.ContainsKey(wp.DiscOffset))
                {
                    hashPatchGroup(patchInfo, wp);
                    d.Write((int)i, wp.Encrypted, (int)wp.Size);
                }
            }
        }

        private void hashPatchGroup(NkitPartitionPatchInfo pi, WiiPartitionGroupSection wp)
        {
            int blockCount = (int)(wp.Size / 0x8000);

            for (int i = 0; i < blockCount; i++)
                Array.Copy(pi.HashGroups[wp.DiscOffset].Data, i * 0x400, wp.Decrypted, i * 0x8000, 0x400);

            byte[] e = wp.Encrypted;

            for (int bi = 0; bi < blockCount; bi++)
            {
                byte byt;
                if (pi.ScrubManager.IsBlockScrubbed(wp.Offset + (bi * 0x8000), out byt))
                {
                    wp.MarkBlockDirty(bi); //will be reset by ApplyHashes
                    wp.SetScrubbed(bi, byt);
                }
            }
        }

        private void fstPatch(NkitPartitionPatchInfo pi, WiiPartitionGroupSection wp, long patchOffset, byte[] crcPatchData, Stream fst)
        {
            long fstOffset = pi.PartitionDataHeader.ReadUInt32B(0x424) * 4L;
            long length = pi.Fst.Data.Length;
            //seek to fst group
            int baseGroupIdx = (int)((patchOffset - (pi.DiscOffset + wp.Header.Size)) / WiiPartitionSection.GroupSize);

            int gs = (int)(fstOffset / (0x7c00L * 64));
            int ge = (int)((fstOffset + length) / (0x7c00L * 64));
            if ((int)((fstOffset + length) % (0x7c00L * 64)) == 0)
                ge--; //don't load the next group if the data will end on the last byte

            if (gs > baseGroupIdx || ge < baseGroupIdx)
                return;

            using (MemoryStream dest = new MemoryStream(crcPatchData))
            {

                if (gs != baseGroupIdx)
                    dest.Seek((gs - baseGroupIdx) * WiiPartitionSection.GroupSize, SeekOrigin.Current);

                long dstOffset = NStream.DataToHashedLen(fstOffset) % WiiPartitionSection.GroupSize; //offset in hashed group
                long total = 0;

                for (int i = gs; i <= ge; i++)
                {
                    long dataPos = 0x7c00 * 64 * i;
                    int dataLen = (int)Math.Min(WiiPartitionSection.GroupSize, (wp.Header.ReadUInt32B(0x2bc) * 4L) - (WiiPartitionSection.GroupSize * i)); //can be less than 2mb if partition is small (or were are at the end)
                    int read = dest.Read(wp.Data, 0, wp.Data.Length);
                    if (read == 0)
                        break;
                    dest.Seek(-read, SeekOrigin.Current);

                    wp.Populate(i, wp.Data, patchOffset, dataLen); //auto decrypted

                    while (length != total && dstOffset != WiiPartitionSection.GroupSize)
                    {
                        dstOffset += 0x400; //skip hashes
                        long l = fst.Read(wp.Decrypted, (int)dstOffset, (int)Math.Min(length - total, 0x8000 - (dstOffset % 0x8000))); //no padding etc as fst will be the same size
                        dstOffset += l;
                        total += l;
                    }

                    Array.Clear(wp.Decrypted, dataLen, (int)wp.Size - dataLen);

                    int bnkCount = (int)(dataLen / 0x8000);
                    int scrubbedCount = 0;
                    for (int bi = 0; bi < bnkCount; bi++)
                    {
                        wp.MarkBlockDirty(bi); //set to force hash generation
                        byte byt;
                        if (pi.ScrubManager.IsBlockScrubbed(wp.Offset + (bi * 0x8000), out byt))
                        {
                            wp.SetScrubbed(bi, byt);
                            scrubbedCount++;
                        }
                    }

                    repairBlocks(wp, scrubbedCount, bnkCount, true, false);

#if DECRYPT
                dest.Write(wp.Decrypted, 0, dataLen);
#else
                    dest.Write(wp.Encrypted, 0, dataLen);
#endif
                    dstOffset = 0;
                }
            }
        }

        private long partitionStreamWrite(LongRef outSize, Stream inStream, Stream target, long size, DatData settingsData, NkitPartitionPatchInfo patchInfo, WiiHashStore hashes, Coordinator pc)
        {
            DatData data = settingsData;

            List<string> addedFiles = new List<string>();

            DateTime dt = DateTime.Now;

            MemorySection hdr = MemorySection.Read(inStream, 0x440);
            long srcPos = hdr.Size;
            long outPos = 0;
            long imageSize = 0;

            try
            {
                if (hdr.ReadString(0, 4) == "\0\0\0\0")
                {
                    long nullsPos = 0;
                    long fileLength = -1;
                    LongRef gapLength = new LongRef() { Value = -1 };
                    target.Write(hdr.Data, 0, (int)hdr.Size);
                    MemorySection sz = MemorySection.Read(inStream, 4);
                    srcPos += 4;
                    outPos += hdr.Size;
                    imageSize = sz.ReadUInt32B(0) * 4L;
                    outSize.Value = NStream.HashedLenToData(imageSize);
                    JunkStream junk = new JunkStream(hdr.Read(0, 4), hdr.Read8(6), outSize.Value); //SET LENGTH FROM HEADER
                    outPos += writeGap(ref fileLength, gapLength, ref nullsPos, ref srcPos, outPos, inStream, target, junk, true, patchInfo.ScrubManager);
                }
                else
                {
                    string idVer = hdr.ReadString(0x200, 8);
                    if (idVer != "NKIT v01")
                        throw new Exception(string.Format("{0} not supported by this version", idVer));
                    bool isNkit = idVer.StartsWith("NKIT");
                    imageSize = NStream.HashedLenToData((hdr.ReadUInt32B(0x210) * 4L));
                    outSize.Value = imageSize;
                    string junkId = hdr.ReadString(0x214, 4);

                    JunkStream junk = new JunkStream(hdr.Read(0, 4), hdr.Read8(6), imageSize); //SET LENGTH FROM HEADER

                    MemorySection fst;
                    long mainDolAddr = hdr.ReadUInt32B(0x420);

                    //############################################################################
                    //# READ DISC START

                    MemorySection hdrToFst = MemorySection.Read(inStream, (hdr.ReadUInt32B(0x424) * 4L) - hdr.Size);
                    srcPos += hdrToFst.Size;

                    fst = MemorySection.Read(inStream, hdr.ReadUInt32B(0x428) * 4L);
                    long postFstPos = (hdr.ReadUInt32B(0x424) * 4L) + fst.Size;
                    srcPos += fst.Size;

                    hashes.WriteFlagsData(imageSize, inStream);
                    srcPos += hashes.FlagsLength;

                    patchInfo.PartitionDataHeader = hdr;
                    patchInfo.Fst = fst;

                    //############################################################################
                    //# WRITE DISC START

                    target.Write(hdr.Data, 0, (int)hdr.Size);
                    target.Write(hdrToFst.Data, 0, (int)hdrToFst.Size); //padded when read
                    target.Write(fst.Data, 0, fst.Data.Length);

                    hdrToFst = null; //let this be collected if needed

                    outPos = (hdr.ReadUInt32B(0x424) * 4L) + fst.Size;
                    long nullsPos = outPos + 0x1c;
                    string error;
                    List<ConvertFile> conFiles = NkitFormat.GetConvertFstFiles(inStream, size, hdr, fst, false, -1, out error);

                    if (conFiles == null)
                    {
                        if (error != null)
                            _log?.LogDetail(error);
                        ConvertFile cf = new ConvertFile(imageSize - srcPos, true) //result.ImageInfo.IsoSize
                        {
                            FstFile = new FstFile(null) { DataOffset = hdr.ReadUInt32B(0x424), Offset = hdr.ReadUInt32B(0x424), Length = (int)fst.Size },
                        };
                        outPos += writeGap(cf, ref nullsPos, ref srcPos, outPos, inStream, target, junk, true, patchInfo.ScrubManager);
                    }
                    else
                    {
                        conFiles[0].GapLength -= hashes.FlagsLength; //fix for a few customs (no gap between the fst and the first file on the source image, but the hash mask makes it look like there is)
                        //########### FILES
                        bool firstFile = true;
                        for (int i = 0; i < conFiles.Count; i++) //read the files and write them out as goodFiles (possible order difference
                        {

                            ConvertFile f = conFiles[i];
                            FstFile ff = f.FstFile;

                            if (!firstFile) //fst already written
                            {
                                //Debug.WriteLine(string.Format(@"{0}>{1} : {2}>{3} : {4} : {5}/{6}", ff.DataOffset.ToString("X8"), outPos.ToString("X8"), (ff.DataOffset + ff.Length).ToString("X8"), (outPos + ff.Length).ToString("X8"), ff.Length.ToString("X8"), ff.Path, ff.Name));

                                if (srcPos < ff.DataOffset) //skip any padding (not written for wii currently)
                                {
                                    inStream.Copy(ByteStream.Zeros, ff.DataOffset - srcPos); //skip any 32k align padding etc
                                    srcPos += ff.DataOffset - srcPos;
                                }

                                //write file
                                if (ff.DataOffset == mainDolAddr)
                                    hdr.WriteUInt32B(0x420, (uint)(outPos / 4L));
                                fst.WriteUInt32B(ff.OffsetInFstFile, (uint)(outPos / 4L));
                                outPos += copyFile(f, ref nullsPos, ref srcPos, outPos, inStream, target);
                            }

                            if (outPos < imageSize)
                            {
                                long gapLen = writeGap(f, ref nullsPos, ref srcPos, outPos, inStream, target, junk, i == 0 || i == conFiles.Count - 1, patchInfo.ScrubManager);
                                outPos += gapLen;
                                if (!firstFile)
                                    fst.WriteUInt32B(ff.OffsetInFstFile + 4, (uint)(ff.Length));
                            }

                            firstFile = false;

                        }
                    }
                }
                return srcPos;
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "NkitReaderWii.Read - partitionRead");
            }
        }

        private long copyFile(ConvertFile file, ref long nullsPos, ref long srcPos, long dstPos, Stream inStream, Stream target)
        {
            FstFile ff = file.FstFile;
            long size = ff.Length;

            if (size == 0)
                return 0; //could be legit or junk

            size += size % 4 == 0 ? 0 : 4 - (size % 4);
            try
            {
                inStream.Copy(target, size);
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "Copy file '{0}' failed at Data Position {1} ({2} bytes)", ff.Name, ff.DataOffset.ToString("X"), ff.Length.ToString());
            }
            srcPos += size;
            dstPos += size;
            nullsPos = dstPos + 0x1CL;

            return size;
        }

        private long writeGap(ConvertFile file, ref long nullsPos, ref long srcPos, long dstPos, Stream inStream, Stream target, JunkStream junk, bool firstOrLastFile, ScrubManager scrub)
        {
            long fileLength = file.FstFile.Length;
            LongRef gapLength = new LongRef() { Value = file.GapLength };
            dstPos = writeGap(ref fileLength, gapLength, ref nullsPos, ref srcPos, dstPos, inStream, target, junk, firstOrLastFile, scrub);
            file.FstFile.Length = fileLength;
            file.GapLength = gapLength.Value;
            return dstPos;
        }

        private long writeFiller(ref long srcPos, long dstPos, long nullsPos, Stream inStream, Stream target, JunkStream junk, ScrubManager scrub)
        {
            long fileLength = -1; //will be ignored
            LongRef gapLength = new LongRef() { Value = -1 };
            dstPos = writeGap(ref fileLength, gapLength, ref nullsPos, ref srcPos, dstPos, inStream, target, junk, true, scrub);
            return dstPos;
        }

        private long writeGap(ref long fileLength, LongRef gapLength, ref long nullsPos, ref long srcPos, long dstPos, Stream inStream, Stream target, JunkStream junk, bool firstOrLastFile, ScrubManager scrub)
        {
            if (gapLength.Value == 0)
            {
                if (fileLength == 0)
                    nullsPos = dstPos + 0x1c;
                return 0;
            }
            long srcLen = gapLength.Value; //fix added for (padding between junk files) - Zumba Fitness (Europe) (En,Fr,De,Es,It)
            MemorySection ms = MemorySection.Read(inStream, 4);
            srcPos += 4;
            long size = ms.ReadUInt32B(0);
            GapType gt = (GapType)(size & 0b11);
            size &= 0xFFFFFFFC;
            if (size == 0xFFFFFFFC) //for wii only. not a thing for GC
            {
                srcPos += 4;
                inStream.Read(ms.Data, 0, 4);
                size = 0xFFFFFFFCL + (long)ms.ReadUInt32B(0); //cater for files > 0xFFFFFFFF
            }
            gapLength.Value = size;

            scrub.AddGap(fileLength, dstPos, size); //keep track of trailing nulls when restoring scrubbed images

            long nulls;
            long junkFileLen = 0;

            //set nullsPos value if zerobyte file without junk
            if (gt == GapType.JunkFile)
            {
                nullsPos = Math.Min(nullsPos - dstPos, 0);
                nulls = (size & 0xFC) >> 2;
                inStream.Read(ms.Data, 0, 4);
                srcPos += 4;
                junkFileLen = ms.ReadUInt32B(0);
                fileLength = junkFileLen;
                junkFileLen += junkFileLen % 4 == 0 ? 0 : 4 - (junkFileLen % 4);
                ByteStream.Zeros.Copy(target, nulls);
                junk.Position = dstPos + nulls;
                junk.Copy(target, junkFileLen - nulls);
                dstPos += junkFileLen;

                if (srcLen <= 8)
                    return junkFileLen;
                else
                {
                    //read gap
                    inStream.Read(ms.Data, 0, 4);
                    srcPos += 4;
                    size = ms.ReadUInt32B(0);
                    gt = (GapType)(size & 0b11);
                    size &= 0xFFFFFFFC;
                    gapLength.Value = size;
                }
            }
            else if (fileLength == 0) //last zero byte file was legit
                nullsPos = dstPos + 0x1c;


            long maxNulls = Math.Max(0, nullsPos - dstPos); //0x1cL
            if (size < maxNulls) //need to test this commented if
                nulls = size;
            else
                nulls = size >= 0x40000 && !firstOrLastFile ? 0 : maxNulls;
            nullsPos = dstPos + nulls; //belt and braces

            if (gt == GapType.AllJunk)
            {
                ByteStream.Zeros.Copy(target, nulls);
                junk.Position = dstPos + nulls;
                junk.Copy(target, size - nulls);
                dstPos += size;
            }
            else if (gt == GapType.AllScrubbed)
            {
                scrub.Scrub(target, dstPos, size, 0);
                dstPos += size;
            }
            else
            {
                long prg = size;
                byte btByte = 0x00;
                GapBlockType bt = GapBlockType.Junk; //should never be used

                while (prg > 0)
                {
                    inStream.Read(ms.Data, 0, 4);
                    srcPos += 4;
                    long bytes;
                    long blk = ms.ReadUInt32B(0);
                    GapBlockType btType = (GapBlockType)(blk >> 30);
                    bool btRepeat = btType == GapBlockType.Repeat;
                    if (!btRepeat)
                        bt = btType;

                    long cnt = 0x3FFFFFFF & blk;

                    if (bt == GapBlockType.NonJunk)
                    {
                        bytes = Math.Min(cnt * Gap.BlockSize, prg);

                        inStream.Copy(target, bytes);
                        srcPos += bytes;
                    }
                    else if (bt == GapBlockType.ByteFill)
                    {
                        if (!btRepeat)
                        {
                            btByte = (byte)(0xFF & cnt); //last 8 bits when not repeating are the byte
                            cnt >>= 8;
                        }

                        bytes = Math.Min(cnt * Gap.BlockSize, prg);
                        scrub.Scrub(target, dstPos, bytes, btByte);
                    }
                    else //if (bt == GapBlockType.Junk)
                    {
                        bytes = Math.Min(cnt * Gap.BlockSize, prg);

                        maxNulls = Math.Max(0, nullsPos - dstPos); //0x1cL
                        if (prg < maxNulls)
                            nulls = bytes;
                        else
                            nulls = bytes >= 0x40000 && !firstOrLastFile ? 0 : maxNulls;

                        ByteStream.Zeros.Copy(target, nulls);
                        junk.Position = dstPos + nulls;
                        junk.Copy(target, bytes - nulls);
                    }
                    prg -= bytes;
                    dstPos += bytes;
                }
            }

            return gapLength.Value + junkFileLen;
        }

    }
}
