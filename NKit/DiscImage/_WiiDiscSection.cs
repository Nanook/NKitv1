using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.Swiit.SuperWiiDiscLibrary
{
    public enum SectionType { DiscHeader, PartitionHeader, Partition, Gap, End }

    public abstract class WiiDiscSection
    {

        public WiiDiscSection(WiiDisc disc, long discOffset, byte[] data, long size)
        {
            this.Disc = disc;
            this.DiscOffset = discOffset;
            this.Data = data;
            this.Size = size;
        }

        protected WiiDisc Disc { get; private set; }
        public long DiscOffset { get; protected set; }
        public long Size { get; protected set; }
        public byte[] Data { get; protected set; }

        public byte Read8(int offset) { return Data[offset]; }
        public ushort ReadUInt16B(int offset) { return bigEndian(BitConverter.ToUInt16(Data, offset)); }
        public uint ReadUInt32B(int offset) { return bigEndian(BitConverter.ToUInt32(Data, offset)); }
        public ushort ReadUInt16L(int offset) { return littleEndian(BitConverter.ToUInt16(Data, offset)); }
        public uint ReadUInt32L(int offset) { return littleEndian(BitConverter.ToUInt32(Data, offset)); }
        public string ReadString(int offset, int length) { return Encoding.ASCII.GetString(Data, offset, length); }
        public string ReadStringToNull(int offset) { return readStringToNull(offset, -1); }
        public string ReadStringToNull(int offset, int maxLength) { return readStringToNull(offset, maxLength); }
        public byte[] Read(int offset, int length)
        {
            byte[] buffer = new byte[length];
            Array.Copy(Data, offset, buffer, 0, length);
            return buffer;
        }
        public void Write8(int offset, byte value) { Data[offset] = value; }
        public void WriteUInt16B(int offset, ushort value) { BitConverter.GetBytes(bigEndian(value)).CopyTo(Data, offset); }
        public void WriteUInt32B(int offset, uint value) { BitConverter.GetBytes(bigEndian(value)).CopyTo(Data, offset); }
        public void WriteUInt16L(int offset, ushort value) { BitConverter.GetBytes(littleEndian(value)).CopyTo(Data, offset); }
        public void WriteUInt32L(int offset, uint value) { BitConverter.GetBytes(littleEndian(value)).CopyTo(Data, offset); }
        public void WriteString(int offset, int length, string value) { Array.Copy(Encoding.ASCII.GetBytes(value), 0, Data, offset, length); }


        public void Write(int offset, byte[] buffer)
        {
            Array.Copy(buffer, 0, Data, offset, buffer.Length);
        }
        private uint bigEndian(uint x)
        {
            if (!BitConverter.IsLittleEndian) //don't swap on big endian CPUs
                return x;
            x = (x >> 16) | (x << 16);
            return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
        }

        private uint littleEndian(uint x)
        {
            if (BitConverter.IsLittleEndian) //don't swap on big endian CPUs
                return x;
            x = (x >> 16) | (x << 16);
            return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
        }

        private ushort bigEndian(ushort x) { return !BitConverter.IsLittleEndian ? x : (ushort)((x >> 8) | (x << 8)); }
        private ushort littleEndian(ushort x) { return BitConverter.IsLittleEndian ? x : (ushort)((x >> 8) | (x << 8)); }

        private string readStringToNull(int offset, int maxLength)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                byte b;
                int i = offset;
                while ((maxLength == -1 || sb.Length <= maxLength) && (b = Data[i++]) != '\0')
                    sb.Append((char)b);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "WiiStream.readStringToNull failure");
            }
        }
    }
}
