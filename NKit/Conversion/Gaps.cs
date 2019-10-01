using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal enum GapType
    {
        AllJunk = 0b00,
        AllScrubbed = 0b01,
        Mixed = 0b10,
        JunkFile = 0b11
    }

    internal enum GapBlockType
    {
        Junk = 0b00,
        NonJunk = 0b01,
        ByteFill = 0b10,
        Repeat = 0b11
    }

    internal class GapBlock
    {
        public GapBlockType Type { get; set; }
        public MemoryStream NonJunk { get; set; }
        public byte Byte { get; set; }
        public int Count { get; set; }
        public override string ToString()
        {
            return string.Format("{0}({1})", Type.ToString(), Count.ToString());
        }
    }

    internal class Gap
    {
        public const long BlockSize = 0x100; //256

        private List<GapBlock> _blocks;
        private GapBlock _current;
        private long _gapLength;
        private bool _headerWritten;
        private bool _isGc;
        public uint JunkFile { get; private set; }
        public int JunkFileNulls { get; private set; } //to cater for XGIII where 1 short 0x14 junk followed by 0x30 junk. First is all nulls - never marked so second file has less nulls
        public Gap(long gapLength, bool isGC)
        {
            _isGc = isGC;
            _gapLength = gapLength;
            _blocks = new List<GapBlock>();
        }

        public long Encode(Stream s, ref long srcPos, long nulls, long gapLength, JunkStream junk, ScrubManager scrub, Stream output, ILog log)
        {
            int read = 0;
            long written = 0;
            _headerWritten = false;
            junk.Position = srcPos;

            if (gapLength != 0)
            {
                int[] results = new int[0x400];
                byte[] scrubBytes = new byte[results.Length];
                byte[] buff = new byte[Math.Min(gapLength, Gap.BlockSize * results.Length)];
                byte[] jbuff = new byte[buff.Length]; //cache some junk up front so we can thread the block comparisons
                long start = srcPos;

                do
                {
                    int req = (int)Math.Min(buff.Length, gapLength - this.Length);
                    Task.WaitAll(
                        Task.Run(() => read = s.Read(buff, 0, req)),
                        Task.Run(() => junk.Read(jbuff, 0, req))
                    );

                    int blocks = (int)(read / Gap.BlockSize) + (int)Math.Min(1, read % Gap.BlockSize);
                    long pos = srcPos;
                    Parallel.For(0, blocks, bb =>
                    {
                        byte? scrubByte;
                        if ((results[bb] = blockCompare(nulls, jbuff, junk.JunkLength, scrub, buff, (int)Gap.BlockSize * bb, (int)Math.Min(Gap.BlockSize * (bb + 1), read), pos, start == pos, out scrubByte)) == 1)
                            scrubBytes[bb] = scrubByte.Value;
                    });

                    for (int bb = 0; bb < blocks; bb++)
                    {
                        if (results[bb] == 0)
                            this.Set(); //junk
                        else if (results[bb] == 1)
                        {
                            this.Set(scrubBytes[bb]); //scrubbed
                            //log?.LogDebug(string.Format(">>> Scrubbed Written: {0} : {1}", srcPos.ToString("X8"), bb.ToString()));
                        }
                        else
                        {
                            //log?.LogDebug(string.Format(">>> NonJunk Written: {0} : {1}", srcPos.ToString("X8"), bb.ToString()));
                            written += this.Set(buff, (int)Gap.BlockSize * bb, (int)Math.Min(Gap.BlockSize * (bb + 1), read), output); //preserve (also data that's part scrubbed part junk in a 256 byte block)
                        }
                    }
                    srcPos += read;
                }
                while (this.Length < gapLength && read != 0); //sum of processed gap blocks < exact gap
            }

            written += write(output, !_headerWritten);
            return written;
        }

        private int blockCompare(long nulls, byte[] junk, long junkLength, ScrubManager scrub, byte[] buff, int bStart, int bEnd, long srcPos, bool isGapStart, out byte? scrubByte)
        {
            scrubByte = null;
            int bTst = bStart;
            bool scrubDecrypted = false; //assume 00 are junk when outside of the range where junk is generated. For Wii we must be sure it's not decrypted 00

            if (bTst < bEnd)
            {
                byte scrubB;
                bTst += scrub.IsScrubbed(srcPos + bStart, buff, bTst, bEnd - bStart, out scrubB, out scrubDecrypted);
                if (bTst == bEnd)
                    scrubByte = scrubB;
            }

            if (bTst < bStart + 0x20) //arbitrary, but larger than 0x1c + 3 padding which can be nulls
            {
                int leadingNulls = (int)(isGapStart && bStart == 0 ? nulls : 0);
                for (bTst = bStart; bTst < bEnd; bTst++)
                {
                    if (leadingNulls-- > 0)
                    {
                        if (buff[bTst] != 0)
                            break;
                    }
                    else if (buff[bTst] != junk[bTst])
                        break;
                }
            }
            if (bTst == bEnd)
            {
                if (scrubByte == null || (scrubByte == 0x00 && (bTst - bStart <= nulls || (!scrubDecrypted && srcPos + bStart >= junkLength)))) //junk or leading nulls OR junk is requested after the partition length
                    return 0;
                else
                    return 1; //scrubbed
            }
            else
                return 2; //preserve (also data that's part scrubbed part junk in a 256 byte block)
        }

        private void set(GapBlockType type)
        {
            if (_current == null || _current.Type != type)
                _blocks.Add(_current = new GapBlock() { Type = type, Count = 1 });
            else
                _current.Count++;
            this.Length += BlockSize;
        }
        private long write(Stream s, bool writeHeader)
        {
            if (_gapLength % 4 != 0) //right most 2 bits will be 00
                throw new Exception("GapLength should be on a 4 byte boundary");
            long written = 0;
            MemorySection m = new MemorySection(new byte[0x10]);

            //for junk files % 4 (0 to 3) - load the 4 bytes, in a real iso the last byte would always be null (if not a legit junk file)
            //if a removed juk file the last byte will end with 2 bits (GapType.JunkFile) the rest of the 4 bytes is the file size / 4
            //multiply value by 4 and add the fst file size. 

            if (writeHeader)
            {
                if (JunkFile != 0)
                {
                    m.WriteUInt32B(0, (uint)(JunkFileNulls << 2) | (uint)GapType.JunkFile); //junk file nulls (first 3 bytes are unused)
                    m.WriteUInt32B(4, JunkFile); //length of file
                    s.Write(m.Data, 0, 8);
                    written += 8;
                }
            }
            else if (_blocks.Count == 0)
                return 0;

            if (_blocks.Count == 0) //non if padding for 32k alignment, or junk file with no padding
                Set(); //add a junk item, length will be 0
            GapType gt;
            if (_blocks.Count == 1 && _blocks[0].Type == GapBlockType.Junk)
                gt = GapType.AllJunk;
            else if (_blocks.Count == 1 && _blocks[0].Type == GapBlockType.ByteFill && _blocks[0].Byte == 0) //00 scrubbed
                gt = GapType.AllScrubbed;
            else
                gt = GapType.Mixed;

            if (writeHeader)
            {
                long hdr = _gapLength >= 0xFFFFFFFCL ? 0xFFFFFFFCL : (uint)_gapLength; //always use 4 bytes if the gap is <= 0xFFFFFFFF - it should never be larger than that anyway. if so then use another 4 bytes
                hdr |= (uint)gt;
                m.WriteUInt32B(0, (uint)hdr);
                s.Write(m.Data, 0, 4);
                written += 4;

                if (hdr >= 0xFFFFFFFCL && _gapLength >= 0xFFFFFFFC)
                {
                    m.WriteUInt32B(0, (uint)(_gapLength - 0xFFFFFFFCL)); //will cater for dual layer where most or all of it is empty
                    s.Write(m.Data, 0, 4);
                    written += 4;
                }
            }

            if (gt == GapType.Mixed || !writeHeader) //mixed
            {
                foreach (GapBlock b in _blocks)
                {
                    if (b.NonJunk != null)
                        b.NonJunk.Position = 0;

                    uint cnt = (uint)b.Count; //total amount of blocks
                    uint max;
                    uint v = 0;
                    uint w = 0;
                    GapBlockType t = b.Type;
                    while (cnt != 0)
                    {
                        if (t == GapBlockType.Junk || t == GapBlockType.NonJunk || t == GapBlockType.Repeat)
                        {
                            max = 0x3FFFFFFF;
                            if (cnt > max)
                                v = max;
                            else
                                v = cnt;
                            cnt -= v;
                            w = (uint)t << 30 | v; //type | count of 256 byte blocks
                        }
                        else if (t == GapBlockType.ByteFill)
                        {
                            max = 0x3FFFFF;
                            if (cnt > max)
                                v = max;
                            else
                                v = cnt;
                            cnt -= v;
                            w = (uint)t << 30 | v << 8 | b.Byte; //type | count of 256 byte blocks | fill byte
                        }

                        m.WriteUInt32B(0, w);
                        s.Write(m.Data, 0, 4); //write encoded bytes
                        written += 4;

                        if (b.Type == GapBlockType.NonJunk)
                        {
                            written += b.NonJunk.Copy(s, Math.Min(b.NonJunk.Length - b.NonJunk.Position, v * Gap.BlockSize));
                            b.NonJunk.Close();
                            b.NonJunk = null; //free it
                        }
                        t = GapBlockType.Repeat;
                    }
                }
            }
            _blocks.Clear();
            _current = null;
            return written;
        }


        public void Set()
        {
            set(GapBlockType.Junk);
        }

        public void Set(byte b)
        {
            if (_current == null || _current.Type != GapBlockType.ByteFill || _current.Byte != b)
                _blocks.Add(_current = new GapBlock() { Type = GapBlockType.ByteFill, Count = 1, Byte = b });
            else
                _current.Count++;
            this.Length += BlockSize;
        }

        public long Set(byte[] nonJunk, int offset, int end, Stream output)
        {
            long w = 0;
            if (_current?.NonJunk != null && _current.NonJunk.Length + nonJunk.Length > (1024 * 1024 * 50)) //write in sections when > 50mb
            {
                w = write(output, !_headerWritten);
                _headerWritten = true;
            }

            set(GapBlockType.NonJunk);
            if (_current.NonJunk == null)
                _current.NonJunk = new MemoryStream();
            _current.NonJunk.Write(nonJunk, offset, end - offset);
            return w;
        }

        public void SetJunkFile(uint length, int nulls)
        {
            this.JunkFile = length;
            this.JunkFileNulls = nulls;
        }

        public long Length { get; private set; }

        public override string ToString()
        {
            return string.Join(" - ", _blocks.Select(a => a.ToString()));
        }
    }

}
