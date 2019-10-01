using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class CrcItem
    {
        public byte[] PatchData { get; internal set; }
        public string PatchFile { get; internal set; }
        public uint PatchCrc { get; internal set; }
        public long Offset { get; internal set; }
        public long Length { get; internal set; }
        public uint Value { get; internal set; }
        public string Name { get; internal set; }
        public override string ToString()
        {
            return string.Format("Offset: {0}, CRC: {1}, Length: {2}, Name: {3}", Offset.ToString("X8"), Value.ToString("X8"), Length.ToString("X8"), Name);
        }
    }

    public class NCrc : Crc
    {
        private long _count;
        private List<CrcItem> _crcs;
        //private List<CrcItem> _bruteForceCrcs;
        private long _startPos;
        private bool _reset;

        internal NCrc(IEnumerable<CrcItem> crcs)
        {
            _crcs = crcs.ToList();
        }

        public NCrc() : base()
        {
            _startPos = 0;
            _count = 0;
            _crcs = new List<CrcItem>();
            _reset = true;
        }

        public void Snapshot(string name)
        {
            if (_crcs.Count != 0 && _crcs[_crcs.Count - 1].Offset == _startPos)
                return; //don't create 2 for same offset
            reset();
            _crcs.Add(new CrcItem() { Offset = _startPos, Length = _count - _startPos, Value = base.Value, Name = name });
            _reset = true;
        }

        private void reset()
        {
            if (_reset)
            {
                _startPos = _count;
                base.Initialize();
                _reset = false;
            }
        }

        protected override void HashCore(byte[] data, int offset, int count)
        {
            reset();
            _count += count;
            base.HashCore(data, offset, count);
        }

        public uint FullCrc()
        {
            return FullCrc(false);
        }

        public uint FullCrc(bool patched)
        {
            if (_crcs.Count == 0)
                return 0;

            uint crc = (patched && _crcs[0].PatchCrc != 0) ? _crcs[0].PatchCrc : _crcs[0].Value;
            for (int i = 1; i < _crcs.Count; i++)
                crc = ~Crc.Combine(~crc, (patched && _crcs[i].PatchCrc != 0) ? ~_crcs[i].PatchCrc : ~_crcs[i].Value, _crcs[i].Length);

            return crc;
        }

        public CrcItem[] Crcs {  get { return _crcs?.ToArray(); } }

        /// <summary>
        /// crc for data at position
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public uint this[long position]
        {
            get
            {
                CrcItem crc = _crcs.FirstOrDefault(a => a.Offset == position);
                if (crc == null)
                    return 0;
                return crc.Value;
            }
        }
    }
}
