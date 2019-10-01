using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class SettingDisc
    {
        internal SettingDisc(string id8)
        {
            Id8 = id8;
        }

        public string Id8 { get; }
    }

    public class JunkIdSubstitution : SettingDisc
    {
        internal JunkIdSubstitution(string id8, string junkId) : base(id8)
        {
            JunkId = junkId;
        }

        public string JunkId { get; }
    }

    public class JunkStartOffset : SettingDisc
    {
        internal JunkStartOffset(string id8, long offset) : base(id8)
        {
            Offset = offset;
        }

        public long Offset { get; }
    }

    public class JunkRedumpPatch : JunkStartOffset
    {
        internal JunkRedumpPatch(string id8, long offset, byte[] data) : base(id8, offset)
        {
            Data = data;
        }

        public byte[] Data { get; }
    }
}
