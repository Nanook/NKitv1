using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiHashStore
    {
        private MemorySection _flags;
        private MemoryStream _hashes;
        private long _partitionSize;

        public WiiHashStore()
        {
        }

        public WiiHashStore(long partitionDataSize)
        {
            _flags = new MemorySection(new byte[intsCount(partitionDataSize) * 4]);
            _hashes = new MemoryStream();
        }

        private int intsCount(long partitionDataSize)
        {
            long size = partitionDataSize / 0x7c00 * 0x8000;
            _partitionSize = size;
            long groups = (size / WiiPartitionSection.GroupSize) + (size % WiiPartitionSection.GroupSize == 0L ? 0L : 1L);
            return (int)((groups / 32L) + (groups % 32 == 0L ? 0L : 1L));
        }

        public long Preserve(long offset, byte[] decrypted, long size)
        {
            int x = (int)(offset / WiiPartitionSection.GroupSize);
            int byt = x / 8;
            int bit = 1 << (7 - (x % 8));
            _flags.Write8(byt, (byte)(_flags.Read8(byt) | bit));
            long written = 0;

            for (int i = 0; i < size; i += 0x8000)
            {
                _hashes.Write(decrypted, i, 0x400);
                written += 0x400;
            }

            return written;
        }

        public bool IsPreserved(long offset)
        {
            int x = (int)(offset / WiiPartitionSection.GroupSize);
            int byt = x / 8;
            if (_flags == null || _flags.Size <= byt)
                return false;
            int bit = 1 << (7 - (x % 8));
            return (_flags.Read8(byt) & bit) != 0;
        }

        public long ReadPatchData(long partitionDiscOffset, Dictionary<long, MemorySection> hashes, Stream stream)
        {
            long read = 0;
            //must read every flagged block from the stream
            for (long o = 0; o < _partitionSize; o += WiiPartitionSection.GroupSize)
            {
                if (IsPreserved(o))
                {
                    long[] keys = hashes.Keys.ToArray();
                    int blocks = (int)Math.Min(WiiPartitionSection.GroupSize, _partitionSize - o) / 0x8000; //read partial groups (< 64 blocks)
                    long offset;
                    if ((offset = keys.FirstOrDefault(a => a == partitionDiscOffset + o)) != 0)
                        stream.Read(hashes[partitionDiscOffset + o].Data, 0, blocks * 0x400); //needs to be encrypted and CRCd
                    else
                        stream.Copy(Stream.Null, blocks * 0x400);
                    read += blocks * 0x400;
                }
            }
            return read;
        }

        public void WriteFlagsData(long partitionDataSize, Stream readStream)
        {
            _flags = MemorySection.Read(readStream, intsCount(partitionDataSize) * 4);
        }

        public byte[] FlagsToByteArray()
        {
            return _flags.Data;
        }

        public long FlagsLength { get { return _flags.Size; } }

        public long HashesToStream(Stream output)
        {
            _hashes.Position = 0;
            _hashes.Copy(output, _hashes.Length);
            return _hashes.Length;
        }
    }
}
