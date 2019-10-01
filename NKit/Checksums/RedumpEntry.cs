using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class RedumpEntry
    {
        internal RedumpEntry(string name, uint crc, byte[] md5, byte[] sha1)
        {
            Name = name;
            Crc = crc;
            Md5 = md5;
            Sha1 = sha1;
        }

        public string Name { get; }
        public uint Crc { get; }
        public byte[] Md5 { get; }
        public byte[] Sha1 { get; }

        public override string ToString()
        {
            return string.Format("{0} [{1}]", Name, Crc.ToString("X8"));
        }
    }
}
