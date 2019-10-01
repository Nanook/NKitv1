using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nanook.NKit
{
    //typedef struct PACKED wbfs_head {
    //        be32_t magic;
    //    be32_t n_hd_sec;    // total number of hd_sec in this partition
    //    uint8_t hd_sec_sz_s;    // sector size in this partition
    //    uint8_t wbfs_sec_sz_s;  // size of a wbfs sec
    //    uint8_t padding3[2];
    //    uint8_t disc_table[0];	// size depends on hd sector size
    //}
    //wbfs_head_t;

    /// <summary>
    /// Currently supports ISO & None WBFS (Read/write) and WBFS (Read only)
    /// </summary>
    public class NStream : Stream
    {
        private const int _HeaderSizeWii = 0x50000;
        private const int _HeaderSizeGc = 0x440;
        private Stream _stream;
        private long _streamDataStart;
        private ZlibStream _zstream;
        private int _currentBlockIndex;
        private int _clusterSize;
        private List<uint> _clusterTable;
        private List<uint> _clusterTableCompressed;
        private long _position;
        private long _imageSize;
        private long _readLength;
        private bool _isWbfs;
        private bool _isIsoDec;
        private bool _isWii;
        private bool _isGamecube;
        private bool _isNkit;
        private bool _isNkitUpdateRemoved;
        private bool _isGcz;
        private bool _isIso;
        private bool _headerRead;
        private byte[] _id;
        private bool _complete;
        private long _IsoDecMultiply;
        private long _junkBaseOffset;

        private byte[] _currentBlock;

        public bool HeaderRead { get { return _headerRead; } }
        public string Id { get { return this.DiscHeader?.ReadString(0, 4); } }
        public string Id6 { get { return this.DiscHeader?.ReadString(0, 6); } }
        public string Id8 { get { return this.DiscHeader == null ? null : string.Concat(this.Id6, this.DiscNo.ToString("X2"), this.Version.ToString("X2")); } }

        public int DiscNo { get { return (int)(this.DiscHeader?.Read8(6) ?? 0); } }
        public int Version { get { return (int)(this.DiscHeader?.Read8(7) ?? 0); } }

        public string Title { get { return this.DiscHeader?.ReadStringToNull(0x20, 0x60); } }
        internal MemorySection DiscHeader { get; set; }
        internal JunkStream JunkStream { get; set; }

        internal bool JunkGeneration { get; set; }

        internal virtual Stream BaseStream { get { return _stream; } }

        public bool IsNkit { get { return _isNkit; } }
        public bool IsNkitUpdateRemoved { get { return _isNkitUpdateRemoved; } }
        public bool IsGcz { get { return _isGcz; } }
        public bool IsIso { get { return _isIso; } }
        public bool IsWii { get { return _isWii; } }
        public bool IsGameCube { get { return _isGamecube; } }
        public bool IsWbfs { get { return _isWbfs; } }
        public bool IsIsoDec { get { return _isIsoDec; } }

        public int ClusterSize { get { return _clusterSize; } }

        //These lengths may show the same values as each other in most cases. They are different when working with truncated isos, in nkit, recovery, nkit.gcz combinations
        public override long Length { get { return _readLength; } } //length that will be read (size read from wbfs, gcz, iso.dec containers) 

        public long ImageSize { get { return _imageSize; } } //Same as above, if nkit it refers to the converted size

        public long RecoverySize { get { return lenCalc(_imageSize); } } //same as above, but in the case of truncated isos, it's the full recovered size

        public long SourceSize { get { return _stream.Length; } } //size of the source file on the disc in which ever format

        public override long Position
        {
            get { return _position; }
            set
            {
                if (_isWbfs || _isIsoDec)
                    _position = value;
                else
                    _stream.Position = _position = value;
            }
        }

        internal NStream(Stream stream)
        {
            this.JunkGeneration = true;
            _currentBlock = new byte[0];
            _stream = stream;
            _isWbfs = false;
            _isGamecube = false;
            _isNkit = false;
            _isIsoDec = false;
            _clusterTable = new List<uint>();
            _position = 0;
            _complete = false;
            _IsoDecMultiply = 0x100; //wii default
        }

        internal bool Initialize(bool readHeader)
        {
            if (_id != null)
                return _headerRead;

            List<long> IsoDecParts = new List<long>();
            _headerRead = readHeader;
            try
            {
                if (readHeader)
                {
                    _currentBlockIndex = -1;

                    _id = read(4);
                    string id = Encoding.ASCII.GetString(_id);

                    if (id == "WBFS")
                    {
                        uint hdSecSize = this.readUInt32B(); //n_hd_sec
                        long pos = (uint)(1 << this.read8()) + 0x100;
                        _clusterSize = 1 << this.read8();
                        int clusters = (int)(FullSizeWii5 / _clusterSize) * 2;

                        //if ((hdSecSize * _clusterSize) / 0x1000L != _stream.Length) //WBFS header check
                        //    Debug.WriteLine("MatchFail");

                        _stream.Copy(ByteStream.Zeros, pos - 10);

                        MemorySection ms = MemorySection.Read(_stream, 2 * clusters);
                        uint pointer;
                        long maxPointer = 0;
                        long blank = 0;
                        long blankTmp = 0;
                        long fileSize = _stream.Length;
                        for (int i = 0; i < clusters; i++)
                        {
                            if ((pointer = ms.ReadUInt16B(i * 2)) != 0)
                                _imageSize += _clusterSize;
                            if (pointer == 0)
                                blankTmp++;
                            else if (pointer * (long)_clusterSize < fileSize)
                            {
                                blank += blankTmp;
                                blankTmp = 0;
                                maxPointer = Math.Max(maxPointer, pointer * (long)_clusterSize);
                            }
                            _clusterTable.Add(pointer);
                        }
                        _streamDataStart = _stream.Position;
                        _imageSize = _readLength = lenCalc(Math.Max(maxPointer + (blank * _clusterSize), _imageSize));
                        _isWbfs = _isWii = true; //must set after above code has ran
                        setDiscHeader(false, null);
                    }
                    else if ((id == "WII5" || id == "WII9") && !_isGamecube) //IsoDec :(
                    {
                        int sectorSize = id == "WII5" ? 0x1182800 : 0x1FB5000;
                        string discId = this.readString(4);
                        byte[] md5 = this.read(16);
                        int partitions = (int)this.readUInt32L();
                        for (int i = 0; i < partitions; i++)
                        {
                            var part = new //throw away
                            {
                                DataOffset = (long)((ulong)this.readUInt32L() << 2),
                                DataSize = (long)((ulong)this.readUInt32L() << 2),
                                PartitionOffset = (long)((ulong)this.readUInt32L() << 2),
                                PartitionEndOffset = (long)((ulong)this.readUInt32L() << 2),
                                PartitionKey = this.read(16)
                            };
                            IsoDecParts.Add(part.PartitionOffset);
                        };

                        _clusterSize = 0x400; //1k
                        _clusterTable = new List<uint>();
                        int c = (sectorSize - (28 + (partitions * 32))) / 4;
                        MemorySection ms = MemorySection.Read(_stream, 4 * c);
                        for (int i = 0; i < c; i++)
                            _clusterTable.Add(ms.ReadUInt32L(i * 4));
                        _imageSize = _readLength = (id == "WII5" ? FullSizeWii5 : FullSizeWii9);
                        _isIsoDec = _isWii = true;
                        _streamDataStart = _stream.Position;
                        setDiscHeader(false, IsoDecParts.ToArray());
                    }
                    else if (id == "GCML") //GC IsoDec
                    {
                        string discId = this.readString(4);
                        byte[] md5 = this.read(16);
                        _clusterTable = new List<uint>();
                        _clusterSize = 0x800; //2k
                        int c = (0x2B8800 - 24) / 4;
                        MemorySection ms = MemorySection.Read(_stream, 4 * c);
                        for (int i = 0; i < c; i++)
                            _clusterTable.Add(ms.ReadUInt32L(i * 4));
                        _streamDataStart = _stream.Position;
                        _IsoDecMultiply = 0x1L; //none
                        _imageSize = _readLength = FullSizeGameCube;
                        _isIsoDec = _isGamecube = true;
                        setDiscHeader(false, null);
                    }
                    else if (BitConverter.ToString(_id) == "01-C0-0B-B1") //GCZ - C001B10B as big endian
                    {
                        MemorySection ms = MemorySection.Read(_stream, 0x20 - 4);

                        long compSize = (long)ms.ReadUInt64L(0x8 - 4);
                        _imageSize = _readLength = (long)ms.ReadUInt64L(0x10 - 4);
                        _clusterSize = (int)ms.ReadUInt32L(0x18 - 4);
                        uint blocks = ms.ReadUInt32L(0x1C - 4);
                        _currentBlock = new byte[_clusterSize];

                        MemorySection pnt = MemorySection.Read(_stream, blocks * 8);
                        MemorySection hsh = MemorySection.Read(_stream, blocks * 4);
                        ulong dataOffset = (ulong)(pnt.Size + hsh.Size + 0x20);
                        _clusterTable = new List<uint>();
                        _clusterTableCompressed = new List<uint>();

                        for (int i = 0; i < blocks; i++)
                        {
                            ulong offset = pnt.ReadUInt64L(i * 8) + dataOffset; //offset in raw file
                            bool compressed = (offset & 0x8000000000000000) == 0;
                            offset &= ~0x8000000000000000;
                            _clusterTable.Add((uint)offset); //divide by 4 for wii
                            _clusterTableCompressed.Add((uint)(compressed ? 1 : 0));
                        }

                        _isGcz = true;
                        MemorySection xx = MemorySection.Read(this, 4);
                        _id = xx.Read(0, 4);

                        setDiscHeader(true, null);

                    }
                    else
                    {
                        _streamDataStart = 0;
                        _isIso = true;
                        _imageSize = _readLength = _stream.Length;
                        setDiscHeader(true, null); //true sets length too
                        _clusterSize = _isWii ? 0x400 : 0x800;
                    }
                    this.ResetJunk();
                    return true;
                }
                else
                {
                    _imageSize = _readLength = _stream.Length; //partial streams not disc
                    this.Position = 0;
                    _streamDataStart = 0;

                }
                return false;
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "NStream.Initialize - Read Header");
            }
        }


        public void ResetJunk()
        {
            this.JunkStream = new JunkStream(this.Id, this.DiscNo, this.RecoverySize);
        }
        public void ChangeJunk(string id)
        {
            this.JunkStream = new JunkStream(id, this.DiscHeader.Read8(6), _imageSize);
        }
        public void ChangeJunk(long baseAddress, string id, int discNo, long length)
        {
            _junkBaseOffset = baseAddress;

            if (length == FullSizeWiiRvtr) //rvtr is restricted to regular iso length - bit of a hack for now.
                length =  FullSizeWii5;

            this.JunkStream = new JunkStream(id, discNo, length);
        }

        private void setDiscHeader(bool autodetect, long[] isoDecParts)
        {
            if (autodetect)
            {
                //4 bytes have already been read
                //read the next part of the header
                byte[] x = new byte[0x20];
                _id.CopyTo(x, 0);
                this.Read(x, 4, 0x20 - 4);

                _isWii = x[0x18] == 0x5d && x[0x19] == 0x1c && x[0x1a] == 0x9e && x[0x1b] == 0xa3;
                _isGamecube = x[0x1c] == 0xc2 && x[0x1d] == 0x33 && x[0x1e] == 0x9f && x[0x1f] == 0x3d;

                if (!_isWii && !_isGamecube)
                    _isGamecube = true; //v1.1 BugFix for Dodger Demo_shrunk.gcm

                byte[] h = new byte[_isWii ? _HeaderSizeWii : _HeaderSizeGc];
                x.CopyTo(h, 0);
                this.Read(h, x.Length, h.Length - x.Length);
                this.DiscHeader = new MemorySection(h);

            }
            else
                this.DiscHeader = MemorySection.Read(this, _isWii ? _HeaderSizeWii : _HeaderSizeGc);

            if (this.IsWii)
            {
                this.DiscHeader = new WiiDiscHeaderSection(this.DiscHeader);
                ((WiiDiscHeaderSection)this.DiscHeader).IsoDecPartitions = isoDecParts;
            }
            _isNkit = this.DiscHeader.ReadString(0x200, 4) == "NKIT";
            _isNkitUpdateRemoved = this.DiscHeader.ReadUInt32B(0x218) != 0; //update not 0means the update partition has been removed
            if (_isNkit)
                _imageSize = this.DiscHeader.ReadUInt32B(0x210) * (_isWii ? 4L : 1L);
            _position = this.DiscHeader.Size;
        }

        private long lenCalc(long l)
        {
            if (_isGamecube)
                return FullSizeGameCube; //GC ISO
            else if (l == FullSizeWiiRvtr) //rvtr
                return l;
            else if (l <= FullSizeWiiOversized) //oversized dvd5
                return FullSizeWii5; //real size dvd5
            else
                return FullSizeWii9; //dvd9
        }

        public static long FullSizeGameCube { get { return 0x57058000L; } }
        public static long FullSizeWiiRvtr { get { return 0x118940000L; } }
        public static long FullSizeWiiOversized { get { return 0x118248000L; } }
        public static long FullSizeWii5 { get { return 0x118240000L; } }
        public static long FullSizeWii9 { get { return 0x1FB4E0000L; } }

        public long RealPosition(long position)
        {
            if (_isWbfs)
            {
                int clusterIdx = (int)(position / _clusterSize);
                int inClusterOffset = (int)(_position % _clusterSize);
                uint clusterWbfsIdx = _clusterTable[clusterIdx];

                while (_clusterTable[clusterIdx] == 0)
                    clusterIdx++;
                return (long)(_clusterTable[clusterIdx] * (long)_clusterSize) + (long)inClusterOffset;
            }
            else if (_isIsoDec)
            {
                int clusteroffset = (int)(position / _clusterSize); //start cluster

                while (_clusterTable[clusteroffset] == 0xffffffff)
                    clusteroffset++;

                return (long)(_clusterTable[clusteroffset] * _IsoDecMultiply) + (long)(position % (long)_clusterSize);
            }
            else
            {
                return position;
            }
        }

        public override int Read(byte[] buffer, int offset, int size)
        {
            //int read = Math.Min(size, _stream.Length != 0 ? (int)(_stream.Length - _stream.Position) : size);

            int r = Read(buffer, offset, size, null);
            if (size != 0 && r == 0)
                throw new HandledException(string.Format("NStream.Read - No data read at Position {0} ({1}) - Requested {2}", this.Position.ToString("X"), this.RealPosition(this.Position).ToString("X"), size.ToString()));
            return r;
        }

        public int Read(byte[] buffer, int offset, int length, Action<int, int> deferedJunk)
        {
            int size = length;
            if (_complete)
                return size; //pretend

            if (!this.CanRead)
                throw new HandledException("NStream.Read: Stream is not readable");
            try
            {
                if (_isGcz)
                {
                    int clusteroffset = (int)(_position / _clusterSize); //start cluster
                    int clusters = _clusterTable.Count;

                    int rawRead = (int)Math.Min(_clusterSize, _readLength - _position);

                    for (int i = clusteroffset; i < clusters; i++)
                    {
                        if (i != _currentBlockIndex)
                        {
                            int clusterSize = (int)(((i + 1) != clusters ? (long)_clusterTable[i + 1] : _stream.Length) - _clusterTable[i]);
                            int rd;
                            if (_clusterTableCompressed[i] == 1)
                            {
                                _zstream = new ZlibStream(_stream, SharpCompress.Compressors.CompressionMode.Decompress);
                                if (_stream is StreamForward)
                                    ((StreamForward)_stream).ForceGczReadBugFix = clusterSize;
                                rd = _zstream.Read(_currentBlock, 0, (int)Math.Min(_clusterSize, _readLength - _position)); //BUG - ALREADS READS 0x4000 to baseStream // Math.Min(_clusterSize, (int)(_realImageLength - _stream.Position)));
                            }
                            else
                                rd = _stream.Read(_currentBlock, 0, (int)Math.Min(_clusterSize, _readLength - _position));

                            ((StreamForward)_stream).ForceGczReadBugFix = 0;

                            _currentBlockIndex = i;
                        }

                        int len = (int)Math.Min(_clusterSize - (_position % _clusterSize), size);
                        Array.Copy(_currentBlock, _position % _clusterSize, buffer, offset, len);

                        size -= len;
                        _position += len;
                        offset += len;
                        if (size == 0)
                            break;
                    }
                    return length - size;
                }
                else if (_isWbfs)
                {

                    int clusterIdx = (int)(_position / _clusterSize);
                    while (size > 0)
                    {
                        int inClusterOffset = (int)(_position % _clusterSize);
                        uint clusterWbfsIdx = _clusterTable[clusterIdx];
                        int clusterCopySize = Math.Min(_clusterSize - inClusterOffset, size);
                        if (clusterWbfsIdx == 0)
                            Array.Clear(buffer, offset, clusterCopySize); //required if autodetecting junk by sector validation failure
                        else
                        {
                            long pos = (long)(clusterWbfsIdx * (long)_clusterSize) + (long)inClusterOffset;
                            if (_stream.Position < pos)
                                _stream.Seek(pos, SeekOrigin.Begin);
                            _stream.Read(buffer, offset, clusterCopySize);
                        }
                        clusterIdx++;
                        offset += clusterCopySize;
                        size -= clusterCopySize;
                        _position += clusterCopySize;
                    }
                    return length - size;
                }
                else if (_isIsoDec)
                {
                    int clusteroffset = (int)(_position / _clusterSize); //start cluster
                    bool firstSeek = true;
                    int clusters = _clusterTable.Count;
                    for (int i = clusteroffset; i < clusters; i++)
                    {
                        int len = Math.Min(size, (int)(_clusterSize - (_position % _clusterSize))); // Math.Min(_clusterSize, size);
                        if (_clusterTable[i] == 0xffffffff)
                        {
                            if (!this.JunkGeneration)
                                Array.Clear(buffer, offset, len); //required if autodetecting junk by sector validation failure
                            else
                            {
                                if (deferedJunk == null)
                                {
                                    this.JunkStream.Position = _junkBaseOffset == 0 ? _position : this.OffsetToData(_position - _junkBaseOffset);
                                    this.JunkStream.Read(buffer, offset, len);
                                }
                                else
                                    deferedJunk(offset, len);
                            }
                        }
                        else if (i != 0 && _clusterTable[i] == 0)
                            Array.Clear(buffer, offset, len); //rare corrpt image had this
                        else
                        {
                            if (firstSeek)
                            {
                                long pos = (long)(_clusterTable[i] * _IsoDecMultiply) + (long)(_position % (long)_clusterSize);
                                while (_stream.Position < pos)
                                    _stream.Copy(ByteStream.Zeros, (int)Math.Min(0x2000000L, pos - _stream.Position));
                                firstSeek = false;
                            }
                            _stream.Read(buffer, offset, len);
                        }

                        size -= len;
                        _position += len;
                        offset += len;
                        if (size == 0)
                            break;
                    }
                    return length - size;

                }
                else
                {
                    int len = _stream.Read(buffer, offset, size);
                    _position += len;
                    return len;
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "NStream.Read - Image Read");
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_complete)
                return _position;
            try
            {
                switch (origin)
                {
                    case SeekOrigin.Begin: _position = offset; break;
                    case SeekOrigin.Current: _position += offset; break;
                    case SeekOrigin.End: _position = Length + offset; break;
                }
                _stream.Position = this.RealPosition(_position);
                return _position;
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "NStream.Seek failure");
            }
        }

        public string ExtensionString()
        {
            return SourceFiles.ExtensionString(_isIsoDec, _isWbfs, _isNkit, _isGcz);
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _isWbfs || _isIsoDec ? false : _stream.CanWrite; }
        }

        public override void Flush()
        {
            if (!_isWbfs && !_isIsoDec)
                _stream.Flush();
        }

        public override void SetLength(long value)
        {
            _imageSize = lenCalc(value); //used for wbfs when detected as single layer but is dual layer
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.CanWrite)
                throw new HandledException("NStream.Write: Stream is not writable");

            if (_isWbfs || _isIsoDec)
                throw new HandledException("NStream.Write: Wbfs and IsoDec does not supporting writing.");
            else
            {
                _stream.Write(buffer, offset, count);
                _stream.Flush();
                _position += count;
            }
        }

        public void Complete()
        {
            if (!_complete)
            {
                _complete = true;
            }
        }

        public override void Close()
        {
            Complete();
            try
            {
                if (_stream != null)
                    _stream.Close();
                _stream = null;
            }
            catch { }
            base.Close();
        }

        //a few private methods to help parse format headers
        private byte read8() { return this.read(1)[0]; }
        private ushort readUInt16B() { return bigEndian(BitConverter.ToUInt16(this.read(2), 0)); }
        private uint readUInt32L() { return littleEndian(BitConverter.ToUInt32(this.read(4), 0)); }
        private uint readUInt32B() { return bigEndian(BitConverter.ToUInt32(this.read(4), 0)); }
        public string readString(int length) { return Encoding.ASCII.GetString(this.read(length)); }

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

        private byte[] read(int amount)
        {
            byte[] buffer = new byte[amount];
            _stream.Read(buffer, 0, amount);
            return buffer;
        }

        internal static long DataToHashedLen(long dataLen)
        {
            return (dataLen / 0x7c00L * 0x8000L) + (dataLen % 0x7c00L);
        }
        internal static long HashedLenToData(long dataLen)
        {
            return (dataLen / 0x8000L * 0x7c00L) + (dataLen % 0x8000L);
        }

        public static long OffsetToData(long o, bool isWii)
        {
            if (!isWii)
                return o;
            return (o / 0x8000L * 0x7c00L) + ((o % 0x8000L) > 0x400L ? (o % 0x8000L) - 0x400L : 0L);
        }

        public static long DataToOffset(long o, bool isWii)
        {
            if (!isWii)
                return o;
            return (o / 0x7c00L * 0x8000L) + (o % 0x7c00L) + 0x400L;
        }

        public long OffsetToData(long o)
        {
            return OffsetToData(o, _isWii);
        }

        public long DataToOffset(long o)
        {
            return DataToOffset(o, _isWii);
        }

    }
}