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
    internal class NkitWriterGc : IWriter
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

                long mlt = 1L; //for Wii: 4L
                long imageSize = pc.OutputSize; //for Wii: pHdr.PartitionDataLength
                string junkId;
                pc.WriterCheckPoint1WriteReady(out junkId); //wait until read has written the header and set the length

                List<string> addedFiles = new List<string>();

                NCrc crc = new NCrc();
                long srcPos;
                long dstPos = 0;

                MemorySection hdr = MemorySection.Read(inStream, 0x440);
                string id8 = string.Concat(hdr.ReadString(0, 6), hdr.Data[6].ToString("X2"), hdr.Data[7].ToString("X2"));

                if (junkId == null)
                {
                    junkId = ctx.Settings.JunkIdSubstitutions.FirstOrDefault(a => a.Id8 == id8)?.JunkId;
                    if (junkId != null)
                        _log?.LogDetail(string.Format("Using ID {0} for junk not image ID {1}", junkId, id8.Substring(0, 4)));
                }

                if (junkId == null)
                    junkId = hdr.ReadString(0, 4);

                MemorySection fst;
                List<JunkDiff> junkDiffs = new List<JunkDiff>();
                long mainDolAddr = hdr.ReadUInt32B(0x420) * mlt;

                long fstFileAlignment = ctx?.Settings?.PreserveFstFileAlignment?.FirstOrDefault(a => a.Item1 == id8)?.Item2 ?? -1;

                CryptoStream target = new CryptoStream(outStream, crc, CryptoStreamMode.Write);

                //############################################################################
                //# READ DISC START
                target.Write(hdr.Data, 0, (int)hdr.Size);
                crc.Snapshot("hdr.bin");

                inStream.Copy(target, (hdr.ReadUInt32B(0x424) * mlt) - hdr.Size);
                crc.Snapshot("bi2.bin, appldr.bin, main.dol");

                //read fst with 4 byte boundary
                fst = MemorySection.Read(inStream, (hdr.ReadUInt32B(0x428) * mlt) + (((hdr.ReadUInt32B(0x428) * mlt) % 4 == 0 ? 0 : 4 - ((hdr.ReadUInt32B(0x428) * mlt) % 4))));
                target.Write(fst.Data, 0, (int)fst.Size);
                crc.Snapshot("fst.bin");

                srcPos = (hdr.ReadUInt32B(0x424) * mlt) + fst.Size;

                long nullsPos = srcPos + 0x1c;
                dstPos = srcPos;

                //create as late as possible in case id is swaped  - Dairantou Smash Brothers DX (Japan) (Taikenban), Star Wars - Rogue Squadron II (Japan) (Jitsuen-you Sample)
                JunkStream js = new JunkStream(junkId, hdr.Read8(6), NStream.FullSizeGameCube);
                string error;
                List<ConvertFile> conFiles = NkitFormat.GetConvertFstFiles(inStream, imageSize, hdr, fst, true, fstFileAlignment, out error); //Size isn't important for writing //result.ImageInfo.IsoSize

                NkitInfo nkitInfo = new NkitInfo();
                nkitInfo.BytesData = srcPos;
                nkitInfo.BytesGaps = 0;
                nkitInfo.BytesJunkFiles = 0;
                nkitInfo.BytesPreservationData = 0;
                nkitInfo.BytesPreservationDiscPadding = 0;

                ScrubManager scrub = new ScrubManager();
                if (conFiles == null)
                {
                    if (error != null)
                        _log?.LogDetail(error);
                    ConvertFile cf = new ConvertFile(imageSize - srcPos, true) //Size isn't important for writing //result.ImageInfo.IsoSize
                    {
                        FstFile = new FstFile(null) { DataOffset = hdr.ReadUInt32B(0x424), Offset = hdr.ReadUInt32B(0x424), Length = (int)fst.Size },
                    };
                    NkitFormat.ProcessGap(ref nullsPos, cf, ref srcPos, inStream, js, true, scrub, target, _log);
                }
                else
                {

                    //############################################################################
                    //# WRITE THE FILESYSTEM
                    List<ConvertFile> missing;
                    NkitFormat.NkitWriteFileSystem(ctx, nkitInfo, mlt, inStream, ref srcPos, ref dstPos, hdr, fst, ref mainDolAddr, target, nullsPos, js, conFiles, out missing, scrub, imageSize, _log);
                    if (missing.Count != 0)
                    {
                        _log?.LogDetail(string.Format("{0} Junk File{1} Removed (Files listed in the FST, but not in the image)", missing.Count.ToString(), missing.Count == 1 ? "" : "s"));
                        foreach (ConvertFile cf in missing)
                            _log?.LogDebug(string.Format("File content is Junk {0}: {1} - Size: {2}", cf.FstFile.DataOffset.ToString("X8"), cf.FstFile.Name, cf.FstFile.Length));
                    }
                }

                if (dstPos % 0x800 != 0)
                {
                    long l = 0x800 - (dstPos % 0x800);
                    ByteStream.Zeros.Copy(target, l);
                    dstPos += l;
                    nkitInfo.BytesPreservationDiscPadding += l;
                }
                crc.Snapshot("files");

                NkitFormat.LogNkitInfo(nkitInfo, _log, hdr.ReadString(0, 4), true);

                NCrc readerCrcs;
                uint validationCrc;
                pc.WriterCheckPoint2Complete(out readerCrcs, out validationCrc, hdr.Data, dstPos); //wait until reader has completed and get crc patches.

                hdr.WriteUInt32B(0x420, (uint)mainDolAddr);

                hdr.WriteString(0x200, 8, "NKIT v01"); //header and version
                hdr.WriteUInt32B(0x208, readerCrcs.FullCrc(true)); //original crc
                hdr.WriteUInt32B(0x210, (uint)imageSize); //result.ImageInfo.IsoSize);
                hdr.WriteString(0x214, 4, hdr.ReadString(0, 4) != junkId ? junkId : "\0\0\0\0");

                crc.Crcs[0].PatchCrc = Crc.Compute(hdr.Data);
                crc.Crcs[0].PatchData = hdr.Data;
                crc.Crcs[2].PatchCrc = Crc.Compute(fst.Data);
                crc.Crcs[2].PatchData = fst.Data;

                hdr.WriteUInt32B(0x20C, CrcForce.Calculate(crc.FullCrc(true), dstPos, readerCrcs.FullCrc(true), 0x20C, 0)); //magic to force crc
                crc.Crcs[0].PatchCrc = Crc.Compute(hdr.Data);

                pc.WriterCheckPoint3ApplyPatches(crc, false, crc.FullCrc(true), crc.FullCrc(true), this.VerifyIsWrite, "NKit Written");
            }
            catch (Exception ex)
            {
                throw pc.SetWriterException(ex, "NkitWriterGc.Write - Convert");
            }
        }



    }
}
