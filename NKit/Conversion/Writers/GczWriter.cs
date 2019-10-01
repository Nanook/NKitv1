using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Deflate;

namespace Nanook.NKit
{
    internal class GczWriter : IWriter
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }
        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Write(Context ctx, Stream input, Stream output, Coordinator pc)
        {
            try
            {
                long imageSize = pc.OutputSize;
                string junkId;
                pc.WriterCheckPoint1WriteReady(out junkId); //wait until read has written the header and set the length

                MemorySection ms = new MemorySection(new byte[4 + 4 + 8 + 8 + 4 + 4]);

                int blockSize = 0x4000;
                int blocks = (int)(imageSize / blockSize) + (imageSize % blockSize == 0 ? 0 : 1);

                ms.WriteUInt32L(0x00, 0xB10BC001);
                ms.WriteUInt32L(0x04, 0); //insert NKIT later
                ms.WriteUInt64L(0x08, 0); //size place holder
                ms.WriteUInt64L(0x10, (ulong)imageSize);
                ms.WriteUInt32L(0x18, (uint)blockSize);
                ms.WriteUInt32L(0x1C, (uint)blocks);

                MemorySection pnt = new MemorySection(new byte[blocks * 8]);
                MemorySection hsh = new MemorySection(new byte[blocks * 4]);

                long offset = 0;
                NCrc crc = new NCrc();

                CryptoStream target = new CryptoStream(output, crc, CryptoStreamMode.Write);

                target.Write(ms.Data, 0, (int)ms.Size);
                crc.Snapshot("Header");
                target.Write(pnt.Data, 0, (int)pnt.Size);
                crc.Snapshot("Pointers");
                target.Write(hsh.Data, 0, (int)hsh.Size);
                crc.Snapshot("Hashes");

                long dataOffset = (pnt.Size + hsh.Size + ms.Size); //fso.position

                int rdBlk = 0;
                int wrBlk = 0;
                object rdBlkLock = new object();
                object wrBlkLock = new object();

                Task[] tasks = new Task[3];
                ManualResetEvent mr = new ManualResetEvent(false);

                for (int i = 0; i < tasks.Length; i++) //4 threads
                {
                    tasks[i] = (new TaskFactory()).StartNew((Object prm) =>
                    {
                        ManualResetEvent m = (ManualResetEvent)prm;
                        int blkIdx;
                        byte[] rawBlk = new byte[blockSize];
                        byte[] buff = new byte[blockSize + 0x100];
                        using (MemoryStream blk = new MemoryStream(buff))
                        {
                            while (true)
                            {
                                lock (rdBlkLock)
                                {
                                    blkIdx = rdBlk++;
                                    if (blkIdx >= blocks)
                                        return; //no more processing
                                    int read = input.Read(rawBlk, 0, (int)Math.Min(imageSize - (blkIdx * blockSize), blockSize));
                                    if (read != blockSize)
                                        Array.Clear(rawBlk, read, blockSize - read);
                                }

                                blk.Position = 0;
                                ZlibStream zl = new ZlibStream(blk, SharpCompress.Compressors.CompressionMode.Compress, SharpCompress.Compressors.Deflate.CompressionLevel.BestCompression, Encoding.Default);
                                zl.FlushMode = FlushType.Finish;
                                zl.Write(rawBlk, 0, blockSize);
                                zl.Flush();

                                while (blkIdx != wrBlk)
                                    m.WaitOne();

                                mr.Reset();

                                if (blk.Position < blockSize)
                                {
                                    target.Write(buff, 0, (int)blk.Position);
                                    pnt.WriteUInt64L(blkIdx * 8, (ulong)(offset));
                                    hsh.WriteUInt32L(blkIdx * 4, adler32(buff, (int)blk.Position));
                                    offset += (int)blk.Position;
                                }
                                else
                                {
                                    target.Write(rawBlk, 0, blockSize);
                                    pnt.WriteUInt64L(blkIdx * 8, (ulong)(offset) | 0x8000000000000000);
                                    hsh.WriteUInt32L(blkIdx * 4, adler32(rawBlk, blockSize));
                                    offset += (int)blockSize;
                                }

                                target.Flush();

                                wrBlk++;
                                mr.Set();
                            }
                        }
                    }, mr);
                }
                Task.WaitAll(tasks);

                crc.Snapshot("Files");

                NCrc readerCrcs;
                uint validationCrc;
                pc.WriterCheckPoint2Complete(out readerCrcs, out validationCrc, null, dataOffset + offset); //wait until reader has completed and get crc patches.

                ms.WriteUInt64L(0x08, (ulong)offset);

                crc.Crcs[0].PatchCrc = Crc.Compute(ms.Data);
                crc.Crcs[0].PatchData = ms.Data;
                crc.Crcs[1].PatchCrc = Crc.Compute(pnt.Data);
                crc.Crcs[1].PatchData = pnt.Data;
                crc.Crcs[2].PatchCrc = Crc.Compute(hsh.Data);
                crc.Crcs[2].PatchData = hsh.Data;

                ms.WriteUInt32B(0x04, CrcForce.Calculate(crc.FullCrc(true), output.Length, readerCrcs.FullCrc(true), 0x04, ms.ReadUInt32B(0x04))); //magic to force crc

                crc.Crcs[0].PatchCrc = Crc.Compute(ms.Data);

                pc.WriterCheckPoint3ApplyPatches(crc, false, crc.FullCrc(true), crc.FullCrc(true), this.VerifyIsWrite, "GCZ Written");
            }
            catch (Exception ex)
            {
                throw pc.SetWriterException(ex, "GczWriter.Write - Compress");
            }
        }

        private static uint adler32(byte[] str, int len)
        {
            const int mod = 65521;
            uint a = 1, b = 0;
            for (int i = 0; i < len; i++)
            {
                a = (a + str[i]) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
        }
    }
}
