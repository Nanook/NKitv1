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
    internal class NkitReaderGc : IReader
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

            try
            {
                DatData data = ctx.Dats;
                List<string> addedFiles = new List<string>();
                DateTime dt = DateTime.Now;
                MemorySection hdr = inStream.DiscHeader;

                string idVer = hdr.ReadString(0x200, 8);
                if (idVer != "NKIT v01")
                    throw new Exception(string.Format("{0} not supported by this version", idVer));
                bool isNkit = idVer.StartsWith("NKIT");
                uint nkitCrc = hdr.ReadUInt32B(0x208);
                uint imageSize = hdr.ReadUInt32B(0x210);
                string junkId = hdr.ReadString(0x214, 4);
                if (junkId != "\0\0\0\0")
                    inStream.ChangeJunk(junkId);
                hdr.WriteUInt32B(0x200, 0);
                hdr.WriteUInt32B(0x204, 0);
                hdr.WriteUInt32B(0x208, 0);
                hdr.WriteUInt32B(0x20C, 0);
                hdr.WriteUInt32B(0x210, 0);
                hdr.WriteUInt32B(0x214, 0);
                hdr.WriteUInt32B(0x218, 0);

                MemorySection fst;
                long mainDolAddr = hdr.ReadUInt32B(0x420);

                NCrc crc = new NCrc();

                NStream target = new NStream(new CryptoStream(outStream, crc, CryptoStreamMode.Write));

                //############################################################################
                //# READ DISC START

                MemorySection hdrToFst = MemorySection.Read(inStream, hdr.ReadUInt32B(0x424) - hdr.Size);

                fst = MemorySection.Read(inStream, hdr.ReadUInt32B(0x428) + (hdr.ReadUInt32B(0x428) % 4 == 0 ? 0 : 4 - (hdr.ReadUInt32B(0x428) % 4)));
                long srcPos = hdr.ReadUInt32B(0x424) + fst.Size;


                //############################################################################
                //# WRITE DISC START

                target.Write(hdr.Data, 0, (int)hdr.Size);
                pc.ReaderCheckPoint1PreWrite(junkId, hdr.ReadUInt32B(0x208)); //size that we will output from this read
                crc.Snapshot("hdr.bin");
                target.Write(hdrToFst.Data, 0, (int)hdrToFst.Size); //padded when read
                crc.Snapshot("bi2.bin, appldr.bin, main.dol");
                target.Write(fst.Data, 0, fst.Data.Length);
                crc.Snapshot("fst.bin");

                hdrToFst = null; //let this be collected if needed

                long dstPos = hdr.ReadUInt32B(0x424) + fst.Size;
                long nullsPos = dstPos + 0x1c;

                string error;
                List<ConvertFile> conFiles = NkitFormat.GetConvertFstFiles(inStream, inStream.Length, hdr, fst, true, -1, out error); //result.ImageInfo.IsoSize

                if (conFiles == null)
                {
                    if (error != null)
                        _log?.LogDetail(error);
                    ConvertFile cf = new ConvertFile(inStream.Length - srcPos, true) //result.ImageInfo.IsoSize
                    {
                        FstFile = new FstFile(null) { DataOffset = hdr.ReadUInt32B(0x424), Offset = hdr.ReadUInt32B(0x424), Length = (int)fst.Size },
                    };
                    dstPos += writeGap(cf, ref nullsPos, ref srcPos, dstPos, inStream, target, true);
                }
                else
                {
                    //########### FILES
                    bool firstFile = true;
                    for (int i = 0; i < conFiles.Count; i++) //read the files and write them out as goodFiles (possible order difference
                    {

                        ConvertFile f = conFiles[i];
                        FstFile ff = f.FstFile;

                        if (!firstFile) //fst already written
                        {
                            //Debug.WriteLine(string.Format(@"{0}>{1} : {2}>{3} : {4} : {5}/{6}", ff.DataOffset.ToString("X8"), dstPos.ToString("X8"), (ff.DataOffset + ff.Length).ToString("X8"), (dstPos + ff.Length).ToString("X8"), ff.Length.ToString("X8"), ff.Path, ff.Name));

                            if (srcPos < ff.DataOffset)
                            {
                                inStream.Copy(ByteStream.Zeros, ff.DataOffset - srcPos); //skip any 32k align padding etc
                                srcPos += ff.DataOffset - srcPos;
                            }

                            //write file
                            if (ff.DataOffset == mainDolAddr)
                                hdr.WriteUInt32B(0x420, (uint)dstPos);
                            fst.WriteUInt32B(ff.OffsetInFstFile, (uint)dstPos);
                            dstPos += copyFile(f, ref nullsPos, ref srcPos, dstPos, imageSize, inStream, target);
                        }

                        if (dstPos < imageSize)
                        {
                            dstPos += writeGap(f, ref nullsPos, ref srcPos, dstPos, inStream, target, i == 0 || i == conFiles.Count - 1);
                            if (!firstFile)
                                fst.WriteUInt32B(ff.OffsetInFstFile + 4, (uint)ff.Length);
                        }

                        firstFile = false;
                    }
                }
                crc.Snapshot("files");

                crc.Crcs[0].PatchCrc = Crc.Compute(hdr.Data);
                crc.Crcs[0].PatchData = hdr.Data;
                crc.Crcs[2].PatchCrc = Crc.Compute(fst.Data);
                crc.Crcs[2].PatchData = fst.Data;

                if (imageSize != dstPos)
                    throw pc.SetReaderException(new HandledException("Nkit image read output {0} bytes not the expected {1}!", dstPos.ToString(), imageSize.ToString()));

                pc.ReaderCheckPoint2Complete(crc, false, nkitCrc, crc.FullCrc(true), this.VerifyIsWrite, hdr.Data, nkitCrc == crc.FullCrc(true) ? "NKit Valid" : "NKit Invalid");
                pc.ReaderCheckPoint3Complete();
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "NkitReaderGc.Read - Read and convert"); //don't let the writer lock
            }

        }

        private long copyFile(ConvertFile file, ref long nullsPos, ref long srcPos, long dstPos, long imageSize, Stream inStream, Stream target)
        {
            FstFile ff = file.FstFile;
            long size = ff.Length;

            if (size == 0)
                return 0; //could be legit or junk

            size += size % 4 == 0 ? 0 : 4 - (size % 4);
            size = Math.Min(size, imageSize - dstPos); //never overwrite file length (starfox e3 fix)
            inStream.Copy(target, size);
            srcPos += size;
            dstPos += size;
            nullsPos = dstPos + 0x1CL;

            return size;
        }

        private long writeGap(ConvertFile file, ref long nullsPos, ref long srcPos, long dstPos, NStream inStream, Stream target, bool firstOrLastFile)
        {
            if (file.GapLength == 0)
            {
                if (file.FstFile.Length == 0)
                    nullsPos = dstPos + 0x1c;
                return 0;
            }

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
                file.FstFile.Length = junkFileLen;
                junkFileLen += junkFileLen % 4 == 0 ? 0 : 4 - (junkFileLen % 4);
                ByteStream.Zeros.Copy(target, nulls);
                inStream.JunkStream.Position = dstPos + nulls;
                inStream.JunkStream.Copy(target, junkFileLen - nulls);
                dstPos += junkFileLen;

                if (file.GapLength <= 8)
                    return junkFileLen;
                else
                {
                    //read gap
                    inStream.Read(ms.Data, 0, 4);
                    srcPos += 4;
                    size = ms.ReadUInt32B(0);
                    gt = (GapType)(size & 0b11);
                    size &= 0xFFFFFFFC;
                }
            }
            else if (file.FstFile.Length == 0) //last zero byte file was legit
                nullsPos = dstPos + 0x1c;


            long maxNulls = Math.Max(0, nullsPos - dstPos); //0x1cL
            if (size < maxNulls) //need to test this commented if
                nulls = size;
            else
                nulls = size >= 0x40000 && !firstOrLastFile ? 0 : maxNulls;

            if (gt == GapType.AllJunk)
            {
                ByteStream.Zeros.Copy(target, nulls);
                inStream.JunkStream.Position = dstPos + nulls;
                inStream.JunkStream.Copy(target, size - nulls);
                dstPos += size;
            }
            else if (gt == GapType.AllScrubbed)
            {
                ByteStream.Zeros.Copy(target, size);
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
                        Stream bs;
                        switch (btByte)
                        {
                            case 0x00: bs = ByteStream.Zeros; break;
                            case 0x55: bs = ByteStream.Fives; break;
                            case 0xff: bs = ByteStream.FFs; break;
                            default: bs = new ByteStream(btByte); break;
                        }
                        bs.Copy(target, bytes);
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
                        inStream.JunkStream.Position = dstPos + nulls;
                        inStream.JunkStream.Copy(target, bytes - nulls);
                    }
                    prg -= bytes;
                    dstPos += bytes;
                }
            }

            return size + junkFileLen;
        }

    }
}
