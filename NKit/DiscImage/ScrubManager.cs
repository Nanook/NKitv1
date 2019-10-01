using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class ScrubRegion
    {
        public long Offset;
        public long Length;
        public byte Byte;
    }

    internal class ScrubManager
    {
        private Queue<ScrubRegion> _scrub;
        private List<ScrubRegion> _cache; //for debugging
        private ScrubRegion _next;
        private ScrubRegion _last;
        private object _lock;

        private bool _wiiPartition;
        private ByteStream _00;
        private ByteStream _FF;

        public List<Tuple<long, int, FstFile>> H3Nulls { get; internal set; }

        public ScrubManager() : this(null)
        {

        }

        public ScrubManager(WiiPartitionHeaderSection header)
        {
            H3Nulls = new List<Tuple<long, int, FstFile>>();
            _wiiPartition = header != null;
            if (header != null)
            {
                _00 = new ByteStream(0, header.DecryptedScrubbed00);
                _FF = new ByteStream(0xFF, header.DecryptedScrubbedFF);
            }
            else
            {
                _00 = new ByteStream(0);
                _FF = new ByteStream(0xFF);
            }
            _lock = new object();
            _scrub = new Queue<ScrubRegion>();
            _cache = new List<ScrubRegion>();
        }

        public int IsScrubbed(long srcPos, byte[] buff, int offset, int size, out byte scrubByte, out bool decrypted)
        {
            decrypted = false;

            int bTst = offset;
            int bEnd = offset + size;

            if (!_wiiPartition)
            {
                byte b = buff[bTst];
                while (bTst < bEnd)
                {
                    if (buff[bTst] != b)
                        break;
                    bTst++;
                }
                scrubByte = b;
                return bTst - offset;
            }
            else
            {
                byte[] enc = _00.Decrypted;
                int x = (int)((srcPos + bTst) % 16);
                while (bTst < bEnd)
                {
                    if (buff[bTst] != enc[x++])
                        break;
                    bTst++;
                    if (x == 16)
                        x = 0;
                }

                if (bTst != bEnd && bTst - offset < enc.Length) //if not at end and < 16 (at least one set of decrypted values)
                {
                    bTst = offset;
                    bEnd = offset + size;

                    enc = _FF.Decrypted;
                    x = (int)((srcPos + bTst) % 16);
                    while (bTst < bEnd)
                    {
                        if (buff[bTst] != enc[x++])
                            break;
                        bTst++;
                        if (x == 16)
                            x = 0;
                    }
                    scrubByte = 0xff;
                    decrypted = true;
                    return bTst - offset;
                }
                else
                {
                    scrubByte = 0;
                    decrypted = true;
                    return bTst - offset;
                }
           }
        }

        public void Scrub(Stream stream, long partitionDataOffset, long size, byte scrubByte)
        {

            Stream bs;
            if (!_wiiPartition)
            {
                switch (scrubByte)
                {
                    case 0x00: bs = ByteStream.Zeros; break;
                    case 0x55: bs = ByteStream.Fives; break;
                    case 0xff: bs = ByteStream.FFs; break;
                    default: bs = new ByteStream(scrubByte); break;
                }
            }
            else
            {
                switch (scrubByte)
                {
                    case 0x00: bs = _00; break;
                    case 0xff: bs = _FF; break;
                    default: throw new Exception(string.Format("Wii Partition Scrubbing does not support byte 0x{0}", scrubByte.ToString()));
                }

                bs.Position = partitionDataOffset;
                add(partitionDataOffset, size, scrubByte);
            }
            bs.Copy(stream, size);
        }

        public void AddGap(long fileLength, long gapOffset, long gapLength)
        {
            long s = (gapOffset + 28L) % 0x7c00L;
            if (fileLength == 0)
                H3Nulls.Add(new Tuple<long, int, FstFile>(gapOffset, (int)Math.Min(28, gapLength), null));
            else if (s <= 28L && gapLength - (28L - s) >= 0x7c00L) //nulls spill to next block and length > block
                H3Nulls.Add(new Tuple<long, int, FstFile>(gapOffset + (28 - s), (int)s, null));
        }

        private void add(long offset, long length, byte b) //called on different thread to IsScrubbed
        {
            //round to the nearest block start
            if (offset % 0x7c00L != 0)
            {
                length += offset % 0x7c00L;
                offset -= offset % 0x7c00L;
            }
            if (length % 0x7c00L != 0)
                length += 0x7c00L - (length % 0x7c00L); //pad to block end


            offset = (long)(offset / 0x7c00L) * 0x8000L;
            length = (long)(length / 0x7c00L) * 0x8000L;
            if (_last != null && _last.Byte == b && offset >= _last.Offset && offset <= _last.Offset + _last.Length) //extend
            {
                if (offset + length > _last.Offset + _last.Length)
                    _last.Length = (offset + length) - _last.Offset;
            }
            else
            {
                _last = new ScrubRegion() { Offset = offset, Length = length, Byte = b };
                lock (_scrub)
                    _scrub.Enqueue(_last);
                _cache.Add(_last);
            }
        }

        public bool IsBlockScrubbedScanMode(long offset, out byte scrubByte)
        {
            if (_next == null || _next.Offset + _next.Length < offset)
            {
                lock (_scrub)
                {
                    if (_scrub.Count != 0)
                        _next = _scrub.Dequeue();
                    else
                        _next = null;
                }
            }
            return isBlockScrubbed(_next, offset, out scrubByte);
        }

        public bool IsBlockScrubbed(long offset, out byte scrubByte)
        {
            scrubByte = 0;
            foreach (ScrubRegion region in _cache)
            {
                if (isBlockScrubbed(region, offset, out scrubByte))
                    return true;
            }
            return false;
        }

        private bool isBlockScrubbed(ScrubRegion region, long offset, out byte scrubByte)
        {
            scrubByte = 0;

            if (region != null)
            {
                if ((offset >= region.Offset && offset < region.Offset + region.Length))
                {
                    scrubByte = region.Byte;
                    return true;
                }
            }
            return false;
        }

    }
}
