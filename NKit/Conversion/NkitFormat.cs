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
    internal class NkitFormat
    {

        internal static long CopyFile(ref long nullsPos, ConvertFile conFile, FstFile prevFile, Stream dest, ref long srcPos, long dstPos, Stream srcStream, JunkStream junkNStream, long imageSize, out bool missing)
        {
            //long pos = dest.Position;
            missing = false;
            FstFile file = conFile.FstFile;

            long size = file.Length + (file.Length % 4 == 0 ? 0 : (4 - (file.Length % 4)));
            if (srcPos + size > imageSize)
                size = file.Length; //some rare GC demos end at the end of a non aligned file. This fixes it - v1.2 bugfix
            long written = size;

            byte[] f = new byte[Math.Min(0x30, size)];
            srcStream.Read(f, 0, f.Length); //then read while junk is created

            if (prevFile != null && prevFile.DataOffset == file.DataOffset && prevFile.Length == 0) //null file overlapped this file so set nullsPos to have a gap (XGIII) needs fst sorting by offset then size
                nullsPos = srcPos + 0x1CL; //will already be aligned

            int nulls = (int)(nullsPos - srcPos);
            int countNulls = 0;

            if (f.Length > nulls)
            {
                junkNStream.Position = file.DataOffset; //async junk gen
                for (int i = 0; i < f.Length && f[i] == 0; i++)
                    countNulls++;

                if (countNulls < f.Length) //don't test all nulls
                    missing = junkNStream.Compare(f, 0, f.Length, Math.Max(0, countNulls)) == f.Length;
            }

            if (missing) //start of file is junk
            {
                //check the remainder of the file
                MemorySection junkFile = MemorySection.Read(srcStream, size - f.Length);
                missing = junkNStream.Compare(junkFile.Data, 0, (int)junkFile.Size, 0) == junkFile.Size;

                if (missing)
                {
                    written = 0;
                    conFile.Gap.SetJunkFile((uint)conFile.FstFile.Length, countNulls);
                }
                else //not 100% junk, write the file out
                {
                    dest.Write(f, 0, f.Length);
                    dest.Write(junkFile.Data, 0, (int)junkFile.Size);
                }
                junkFile = null;
            }
            else
            {
                dest.Write(f, 0, f.Length);
                srcStream.Copy(dest, size - f.Length); //copy file
            }

            if (!missing) //reset the gap when no junk
            {
                nullsPos = srcPos + size + 0x1c;
                if (nullsPos % 4 != 0)
                    nullsPos += 4 - (nullsPos % 4);
            }

            srcPos += size;
            return written;
        }

        internal static List<ConvertFile> GetConvertFstFiles(Stream inStream, long size, MemorySection hdr, MemorySection fst, bool isGc, long fstFileAlignment, out string error)
        {
            string[] align = new string[] { ".tgc" }; //, ".dol", ".thp", ".adp", ".poo", ".pcm", ".pcm16", ".son", ".act", ".bin", ".gpl", ".tpl", ".bmp", ".sam", ".sdi", ".tcs", ".txt", ".skn", ".stp", ".anm" };
            error = null;
            List<ConvertFile> conFiles = new List<ConvertFile>();
            try
            {
                List<FstFile> srcFiles = FileSystem.Parse(fst, null, hdr.ReadString(0, 4), isGc)?.Files?.OrderBy(a => a.Offset)?.ThenBy(a => a.Length)?.ToList();

                //get list of files and gaps
                long end;
                long gap;
                long fstLen = (long)hdr.ReadUInt32B(0x424) * (isGc ? 1L : 4L);
                FstFile ff = new FstFile(null) { Name = "fst.bin", DataOffset = fstLen, Offset = fstLen, Length = (int)fst.Size, IsNonFstFile = true };
                for (int i = 0; i < srcFiles.Count; i++)
                {
                    ff = i == 0 ? ff : srcFiles[i - 1];
                    end = ff.DataOffset + ff.Length;
                    end += end % 4 == 0 ? 0 : 4 - (end % 4);

                    gap = srcFiles[i].DataOffset - end;

                    if (gap < 0)
                    {
                        error = string.Format("The gap between '{0}' and '{1}' is {2} - Converting as bad image", ff.Name, srcFiles[i].Name, gap.ToString());
                        return null;
                    }
                    conFiles.Add(new ConvertFile(gap, isGc) { FstFile = ff });
                }
                ff = srcFiles.Last();
                end = ff.DataOffset + ff.Length;
                end += end % 4 == 0 ? 0 : 4 - (end % 4);
                gap = size - end;
                if (gap >= -3 && gap < 0)
                    gap = 0; //some hacked gc images converted from tgc end on the file end (star fox e3)
                if (gap < 0)
                {
                    error = string.Format("The gap between '{0}' and the end of the image is {1} - Converting as bad image/partition", ff.Name, gap.ToString());
                    return null;
                }

                conFiles.Add(new ConvertFile(gap, isGc) { FstFile = ff });

                //set alignment
                foreach (ConvertFile cf in conFiles)
                {
                    ff = cf.FstFile;
                    if (fstFileAlignment == 0)
                        cf.Alignment = 0; //preserve alignment
                    else if (fstFileAlignment == -1 && ff.DataOffset % 0x8000 == 0 && (ff.Length % 0x8000 == 0 || align.Contains(Path.GetExtension(ff.Name).ToLower())))
                        cf.Alignment = 0x8000; //default behaviour
                    else if (fstFileAlignment != 0 && ff.DataOffset % fstFileAlignment == 0) //src matches alignment
                        cf.Alignment = fstFileAlignment; //align to largest multiple
                    else
                        cf.Alignment = -1; //-1 = do not align this file
                }
            }
            catch
            {
                error = "Fst parsing error - Converting as bad image";
                return null;
            }
            return conFiles;
        }

        internal static void NkitWriteFileSystem(Context ctx, NkitInfo imageInfo, long mlt, Stream inStream, ref long srcPos, ref long dstPos, MemorySection hdr, MemorySection fst, ref long mainDolAddr, Stream target, long nullsPos, JunkStream js, List<ConvertFile> conFiles, out List<ConvertFile> missingFiles, ScrubManager scrub, long imageSize, ILog log)
        {
            FstFile ff;
            //########### FILES
            bool firstFile = true;
            ConvertFile lastf = conFiles.Last();
            ConvertFile prevf = null;
            missingFiles = new List<ConvertFile>();

            foreach (ConvertFile f in conFiles) //read the files and write them out as goodFiles (possible order difference
            {
                ff = f.FstFile;

                if (!firstFile)
                {
                    if (srcPos == mainDolAddr && mainDolAddr == (hdr.ReadUInt32B(0x420) * mlt))
                        mainDolAddr = dstPos; //main/default.dol is moving

                    //Debug.WriteLine(string.Format(@"{5} : {0} : {1} : {2} : {3}/{4}", ff.DataOffset.ToString("X8"), (ff.DataOffset + ff.Length).ToString("X8"), /*(nextFile.DataOffset - lastEnd).ToString("X8"),*/ ff.Length.ToString("X8"), ff.Path, ff.Name, ff.OffsetInFstFile.ToString("X8")));

                    //srcPos aligned at 32k and the length is 32k (audio file) ensure new dest is also aligned
                    if (f.Alignment == 0 || (f.Alignment != -1 && dstPos % f.Alignment != 0)) //XGIII audio in race, Zelda collection games for TGC
                    {
                        long pad;
                        if (f.Alignment == 0) //no shrinking
                            pad = Math.Max(0, ff.DataOffset - dstPos);
                        else //align
                            pad = (int)(dstPos % f.Alignment == 0 ? 0 : f.Alignment - (dstPos % f.Alignment));
                        imageInfo.FilesAligned += 1;
                        ByteStream.Zeros.Copy(target, pad);
                        imageInfo.BytesPreservationDiscPadding += pad;
                        dstPos += pad;
                    }

                    fst.WriteUInt32B(ff.OffsetInFstFile, (uint)(dstPos / mlt));

                    //replace copy with junk test - adjust fst length to %4
                    long l = srcPos;
                    bool missing;

                    imageInfo.FilesTotal += 1;
                    long written = NkitFormat.CopyFile(ref nullsPos, f, prevf?.FstFile, target, ref srcPos, dstPos, inStream, js, imageSize, out missing);
                    if (missing)
                        missingFiles.Add(f);
                    dstPos += written;
                    imageInfo.BytesData += srcPos - l; //read

                    if (f.Gap.JunkFile != 0) //encode the gap and write - pad to %4
                    {
                        imageInfo.BytesJunkFiles += f.Gap.JunkFile + (f.Gap.JunkFile % 4 == 0 ? 0 : (4 - (f.Gap.JunkFile % 4)));
                        fst.WriteUInt32B(ff.OffsetInFstFile + 4, 0); //modify size to be remainder of 4
                    }
                }

                if (f.GapLength != 0 || f.Gap.JunkFile != 0)
                {
                    long l = NkitFormat.ProcessGap(ref nullsPos, f, ref srcPos, inStream, js, firstFile || f == lastf, scrub, target, log);

                    imageInfo.BytesGaps += f.GapLength;
                    imageInfo.BytesPreservationData += l;
                    dstPos += l;
                }

                firstFile = false;
                prevf = f;
            }
        }

        internal static long ProcessGap(ref long nullsPos, ConvertFile file, ref long srcPos, Stream s, JunkStream junk, bool firstOrLastFile, ScrubManager scrub, Stream output, ILog log)
        {
            long nulls = 0;

            if (file.GapLength != 0)
            {
                if (srcPos % 4 != 0)
                    throw new Exception("Src Position should be on a 4 byte boundary");

                long size = file.GapLength;
                long start = srcPos;

                long maxNulls = Math.Max(0, nullsPos - srcPos); //0x1cL
                                                                //maxNulls = 0x1c;
                if (size < maxNulls) //need to test this commented if
                    nulls = size;
                else
                    nulls = size >= 0x40000 && !firstOrLastFile ? 0 : maxNulls;
            }
            //might need to still call if we have a junk file
            return file.Gap.Encode(s, ref srcPos, nulls, file.GapLength, junk, scrub, output, log);
        }

        internal static void LogNkitInfo(NkitInfo imageInfo, ILog log, string id, bool isDisc)
        {
            string pfx = string.Format("NKit {0} [{1}]", isDisc ? "Disc" : "Prtn", id);

            log?.LogDetail(string.Format("{0}: In [{1,2:#.0} MiB] ({2} bytes), Out [{3,2:#.0} MiB] (bytes {4})", pfx, imageInfo.BytesReadSize / (double)(1024 * 1024), imageInfo.BytesReadSize.ToString(), imageInfo.BytesWriteSize / (double)(1024 * 1024), imageInfo.BytesWriteSize.ToString()));
            if (imageInfo.BytesGcz != 0)
                log?.LogDetail(string.Format("{0}: GCZ Out [{1,2:#.0} MiB] ({2} bytes)", pfx, imageInfo.BytesGcz / (double)(1024 * 1024), imageInfo.BytesGcz.ToString()));
            if (imageInfo.BytesJunkFiles != 0)
                log?.LogDetail(string.Format("{0}: Junk Files Removed [{1,2:#.0} MiB] ({2} bytes)", pfx, imageInfo.BytesJunkFiles / (double)(1024 * 1024), imageInfo.BytesJunkFiles.ToString()));
            if (imageInfo.BytesHashesData != 0 || imageInfo.BytesHashesPreservation != 0)
                log?.LogDetail(string.Format("{0}: Hashes In [{1,2:#.0} MiB] ({2} bytes), Preserved [{3,2:#.0} MiB] (bytes {4})", pfx, imageInfo.BytesHashesData / (double)(1024 * 1024), imageInfo.BytesHashesData.ToString(), imageInfo.BytesHashesPreservation / (double)(1024 * 1024), imageInfo.BytesHashesPreservation.ToString()));
            if (imageInfo.BytesPreservationData != 0)
                log?.LogDetail(string.Format("{0}: Preservation Data [{1,2:#.0} MiB] {2} bytes", pfx, imageInfo.BytesPreservationData / (double)(1024 * 1024), imageInfo.BytesPreservationData.ToString()));
            if (imageInfo.BytesPreservationDiscPadding != 0)
                log?.LogDetail(string.Format("{0}: Preservation Padding [{1,2:#.0} MiB] {2} bytes", pfx, imageInfo.BytesPreservationDiscPadding / (double)(1024 * 1024), imageInfo.BytesPreservationDiscPadding.ToString()));
            if (imageInfo.FilesTotal != 0 || imageInfo.FilesAligned != 0)
                log?.LogDetail(string.Format("{0}: {1} Total Files, {2} aligning boundary preserved", pfx, imageInfo.FilesTotal.ToString(), imageInfo.FilesAligned.ToString()));

        }

    }
}
