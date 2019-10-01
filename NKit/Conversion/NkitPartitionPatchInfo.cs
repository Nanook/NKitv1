using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class NkitPartitionPatchInfo
    {
        public long DiscOffset;
        public long Size { get { return PartitionHeader == null ? 0 : (PartitionHeader.ReadUInt32B(0x2bc) * 4L); } }
        public Dictionary<long, MemorySection> HashGroups;
        public ScrubManager ScrubManager;
        public MemorySection PartitionHeader;
        public MemorySection PartitionDataHeader;
        public MemorySection Fst;
    }
}
