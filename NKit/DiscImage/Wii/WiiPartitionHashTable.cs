using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class PartitionHashTable
    {
        public byte[] Bytes { get; private set; }
        public int HashCount { get; private set; }

        internal PartitionHashTable(int hashCount)
        {
            this.Bytes = new byte[hashCount * 20]; //20 is sha1 length
            this.HashCount = hashCount;
        }
        public void Reset(byte[] group, int offset)
        {
            Array.Copy(group, offset, this.Bytes, 0, Math.Min(this.Bytes.Length, group.Length - offset));
        }
        public int CopyAll(byte[] buffer, int offset)
        {
            Array.Copy(Bytes, 0, buffer, offset, Math.Min(this.Bytes.Length, buffer.Length - offset));
            return this.Bytes.Length;
        }
        public bool Set(int blockIndex, byte[] sha1, bool testEqual)
        {
            if (testEqual && sha1.Equals(0, Bytes, blockIndex * 20, 20))
                return true;
            Array.Copy(sha1, 0, Bytes, blockIndex * 20, 20);
            return false;
        }
        public bool Equals(int blockIndex, byte[] sha1)
        {
            return sha1.Equals(0, this.Bytes, blockIndex * 20, 20);
        }
    }

}
