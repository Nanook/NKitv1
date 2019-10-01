using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiPartitionSection : IWiiDiscSection
    {
        public const long GroupSize = 0x8000 * 64;
        public WiiPartitionHeaderSection Header { get; private set; }
        public string Id { get { return this.Header.Id; } }
        public int DiscNo { get { return this.Header.DiscNo; } }
        public long PartitionLength { get { return this.Header.PartitionSize; } }
        public long PartitionDataLength { get { return this.Header.PartitionDataSize; } }
        public long NewPartitionDataLength { get; set; }
        public long NewDiscOffset { get; set; }
        public byte[] NewFst { get; set; }
        public long DiscOffset { get { return this.Header.DiscOffset; } }
        public long Size { get { return this.Header.Size + this.Header.PartitionSize; } }
        private WiiPartitionGroupSection _firstSection;
        private WiiDiscHeaderSection _discHdr;
        private NStream _stream;
        private byte[] _fst;
        private int _partialFst;
        private long _seek;

        public FstFolder FileSystem { get { return this.Header?.FileSystem?.Root; } }
        public FstFile[] FlatFileSystem { get { return this.Header?.FileSystem?.Files; } }

        internal WiiPartitionSection(NStream stream, WiiDiscHeaderSection header, NStream readPartitionStream, long discOffset)
        {
            _stream = readPartitionStream;
            _discHdr = header;
            _partialFst = 0;
            _seek = -1;

            //calc the header
            byte[] partHdrTmp = new byte[0x400]; //read enough to get all the details we need
            _stream.Read(partHdrTmp, 0, partHdrTmp.Length); //need to read this to get header length
            byte[] partHdrLen = new byte[4];
            Array.Copy(partHdrTmp, 0x2b8, partHdrLen, 0, 4); //location of partion header length
            long hdrLen = bigEndian(BitConverter.ToUInt32(partHdrLen, 0)) * 4;
            byte[] partHdr = new byte[hdrLen];
            Array.Copy(partHdrTmp, partHdr, partHdrTmp.Length);
            _stream.Read(partHdr, partHdrTmp.Length, partHdr.Length - partHdrTmp.Length);

            this.Header = new WiiPartitionHeaderSection(_discHdr, readPartitionStream, discOffset, partHdr, partHdr.Length);
            byte[] data = new byte[GroupSize];

            //this is an awful work around that blocks are scrubbed but the wiistream can't unscrub them because the partition ID is unknown 
            Dictionary<int, int> IsoDecUnscub = new Dictionary<int, int>(); //must cache this because of a dependency

            int dataLen = (int)Math.Min(data.Length, Header.PartitionSize);
            _stream.Read(data, 0, dataLen, (a, b) => IsoDecUnscub.Add(a, b)); //defer the decryption because we don't have the partition id etc

            WiiPartitionGroupSection ps = new WiiPartitionGroupSection(stream, _discHdr, Header, data, Header.DiscOffset + Header.Data.Length, dataLen, true);

            Header.Initialise(ps);

            //defered unscrubbing
            foreach (var x in IsoDecUnscub)
            {
                _stream.JunkStream.Position = _stream.OffsetToData(x.Key);
                _stream.JunkStream.Read(ps.Decrypted, x.Key, x.Value);
            }
            _firstSection = ps;
        }

        public IEnumerable<WiiPartitionGroupSection> Sections
        {
            get
            {
                int size;
                byte[] data = new byte[GroupSize];
                WiiPartitionGroupSection ps = _firstSection;
                _firstSection = null; //don't hold on to it

                parseFst(ps);
                yield return ps;

                int sec = 0;
                WiiPartitionGroupSection last = ps;
                while (last.DiscOffset + last.Size < Header.DiscOffset + Header.Size + Header.PartitionSize)
                {
                    if (_seek != -1 && _seek != last.Offset + last.Size)
                    {
                        long seekDiscOffset = Header.DiscOffset + Header.Size + _seek;
                        _stream.Seek(seekDiscOffset, SeekOrigin.Begin);
                        size = (int)Math.Min((Header.DiscOffset + Header.Size + Header.PartitionSize) - seekDiscOffset, (long)data.Length);
                        //unknown what this will do if seek can't seek to the group boundary if it's missing (iso.dec only, wbfs will have the group intact)
                        _stream.Read(data, 0, size);
                        sec = (int)(_seek / GroupSize);
                        ps.Populate(sec, data, Header.DiscOffset + Header.Size + _seek, size);
                    }
                    else
                    {
                        size = (int)Math.Min((Header.DiscOffset + Header.Size + Header.PartitionSize) - (last.DiscOffset + last.Size), (long)data.Length);
                        _stream.Read(data, 0, size);
                        ps.Populate(++sec, data, last.DiscOffset + last.Size, size);
                        parseFst(ps);
                    }
                    _seek = -1; //reset
                    yield return ps;
                    last = ps;
                }

            }
        }

        public void SeekToFile(FstFile file)
        {
            _seek = file.Offset - (file.Offset % GroupSize); //offset within partition of group, set to group boundary
        }

        private void parseFst(WiiPartitionGroupSection grp)
        {
            if (_partialFst != 0 || (grp.DataOffset <= this.Header.FstOffset && this.Header.FstOffset <= grp.DataOffset + (0x7c00 * 64)))
            {
                //hack to test with. doesn't support reading over groups
                if (_fst == null)
                    _fst = new byte[this.Header.FstSize];
                int read = grp.DataCopy(_partialFst == 0 ? (int)(this.Header.FstOffset - grp.DataOffset) : 0, (int)this.Header.FstSize - _partialFst, false, _fst, _partialFst);
                if (read + _partialFst == _fst.Length)
                {
                    this.Header.ParseFst(_fst);
                    _fst = null;
                    _partialFst = 0;
                }
                else
                    _partialFst += read;
            }
        }

        private uint bigEndian(uint x)
        {
            if (!BitConverter.IsLittleEndian) //don't swap on big endian CPUs
                return x;
            x = (x >> 16) | (x << 16);
            return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
        }

    }
}
