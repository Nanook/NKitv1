using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiFillerSectionItem : BaseSection
    {
        private JunkStream _junk;
        private byte[] _junkData;
        private bool _useBuff;

        internal WiiFillerSectionItem(NStream stream, long discOffset, byte[] data, long size, bool useBuff, JunkStream junk) : base(stream, discOffset, data, size)
        {
            _useBuff = useBuff;
            _junk = junk;
            if (_junk != null)
            {
                _junk.Position = discOffset;
                _junkData = new byte[this.Data.Length];
                _junk.Read(_junkData, 0, (int)base.Size);
                Array.Clear(_junkData, 0, 28);
                base.Data = _useBuff ? data : _junkData;
            }
        }

        public void Populate(byte[] data, long discOffset, long size)
        {
            base.DiscOffset = discOffset;
            base.Size = size;
            if (_junk != null)
                _junk.Read(_junkData, 0, (int)base.Size);
            base.Data = _useBuff ? data : _junkData;
        }

        public byte[] Junk { get { return _junkData; } }
    }
}
