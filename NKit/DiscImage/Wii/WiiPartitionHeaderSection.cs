using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiPartitionHeaderSection : BaseSection
    {
        private const string _lame = @"oWPrYLjkSisqarQicfReI2GFtU6TKS7krhNIi/LZ7P7FMvtFyLpzFkyB/Juqqn73";
        public bool IsEncrypted { get; private set; } //reset as encrypted
        //XYZ internal JunkStream JunkStream { get; private set; }
        public byte[] H3Table { get; private set; }
        public byte[] Key { get; private set; }
        public Aes Aes { get; private set; }
        private SHA1 _sha1;
        public long PartitionSize { get; private set; }
        public long PartitionDataSize { get; private set; }
        public PartitionType Type { get { return _hdr.Partitions.FirstOrDefault(a => a.DiscOffset == this.DiscOffset).Type; } }
        private long _dataOffset;
        public readonly bool IsKorean;
        public readonly bool IsRvt;
        public readonly bool IsRvtR;
        public readonly bool IsRvtH;
        public readonly byte[] ContentSha1;
        private long _dolOffset;
        public long FstOffset { get; private set; }
        public long FstSize { get; private set; }
        private FileSystem _fileSystem;
        private FstFile[] _flatFiles;
        private byte[] _fst;
        private WiiDiscHeaderSection _hdr;
        private WiiPartitionGroupSection _firstSection;
        private ScrubManager _scrubManager;

        public WiiDiscHeaderSection DiscHeader { get { return _hdr; } }
        public ScrubManager ScrubManager { get { return _scrubManager; } set { _scrubManager = value; } }
        public byte[] DecryptedScrubbed00 { get; set; }
        public byte[] DecryptedScrubbedFF { get; set; }

        internal WiiPartitionHeaderSection(WiiDiscHeaderSection header, NStream stream, long discOffset, byte[] data, long size) : base(stream, discOffset, data, size)
        {
            _hdr = header;
            _fileSystem = null;
            Aes = Aes.Create();
            _sha1 = SHA1.Create();

            Aes.Padding = PaddingMode.None;
            _dataOffset = this.ReadUInt32B(0x2b8) * 4L;
            PartitionSize = this.ReadUInt32B(0x2bc) * 4L;
            PartitionDataSize = NStream.HashedLenToData(PartitionSize);

            int h3Offset = (int)this.ReadUInt32B(0x2b4) * 4;
            int tmdOffset = (int)(this.ReadUInt32B(0x2a8) * 4);
            if (h3Offset != 0)
                H3Table = this.Read(h3Offset, 0x18000);
            if (tmdOffset != 0)
                ContentSha1 = this.Read(tmdOffset + 0x1e4 + 0x10, 20);

            // Determine the common key to use.
            string issuer = Encoding.ASCII.GetString(this.Read(0x140, 64)).TrimEnd('\0');
            IsRvt = issuer == "Root-CA00000002-XS00000006"; //Use the RVT-R key.
            IsKorean = !IsRvt && this.Read8(0x1f1) == 1; //Use the Korean Key
            IsRvtR = !(IsRvtH = IsRvt && PartitionSize == 0);
            if (IsRvtH)
                return; //notsupported

            int i = IsRvt ? 0 : IsKorean ? 1 : 2;
            byte[] lame = Convert.FromBase64String(_lame);
            byte[] l = new byte[lame.Length / 3];
            for (int j = 0; j < l.Length; i += 3)
                l[j++] = lame[i];
            Aes.Key = l;

            byte[] titleKey = this.Read(0x1bf, 16);
            byte[] iv = this.Read(0x1dc, 16);
            Array.Clear(iv, 8, 8);
            Aes.IV = iv;

            using (ICryptoTransform cryptor = Aes.CreateDecryptor())
                cryptor.TransformBlock(titleKey, 0, 16, titleKey, 0);

            this.Key = titleKey;
            Aes.Key = this.Key;

            //decrypt scrubbed values. This is to allow the comparison of scrubbed partition data in the decrypted layer
            DecryptedScrubbed00 = new byte[16];
            DecryptedScrubbedFF = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

            Aes.IV = (byte[])DecryptedScrubbedFF.Clone(); //if properly scrubbed then the KEY is FFs too
            using (ICryptoTransform cryptor = Aes.CreateDecryptor())
                cryptor.TransformBlock(DecryptedScrubbedFF, 0, 16, DecryptedScrubbedFF, 0);

            Aes.IV = new byte[16];
            using (ICryptoTransform cryptor = Aes.CreateDecryptor())
                cryptor.TransformBlock(DecryptedScrubbed00, 0, 16, DecryptedScrubbed00, 0);

            _scrubManager = new ScrubManager(this);

        }

        internal void Initialise(WiiPartitionGroupSection firstSection, long newSize)
        {
            this.Initialise(firstSection);
            PartitionSize = newSize;
        }

        internal void Initialise(bool encrypted, string id)
        {
            this.IsEncrypted = encrypted;
            this.Id = id;
        }

        internal void Initialise(WiiPartitionGroupSection firstSection)
        {
            _firstSection = firstSection;
            IsEncrypted = firstSection.IsEncrypted;

            MemorySection ms = new MemorySection(firstSection.Decrypted);
            this.Id = ms.ReadString(0x400, 4);
            this.DiscNo = (int)ms.Read8(0x406);

            if (this.Stream != null)
                this.Stream.ChangeJunk(this.DiscOffset + _dataOffset, this.Id, this.DiscNo, PartitionDataSize);

            if (this.Id != "\0\0\0\0")
            {
                _dolOffset = ms.ReadUInt32B(0x820) * 4L; //+400 to skip hashes
                FstOffset = ms.ReadUInt32B(0x824) * 4L;
                FstSize = ms.ReadUInt32B(0x828) * 4L;
            }
        }

        internal void ParseFst(byte[] fst)
        {
            if (_fileSystem == null && _fst == null)
                _fst = fst;
            if (this.FileSystem == null)
                return;
            _fileSystem = this.FileSystem; //force read of filesystem here

            List<Tuple<long, int, FstFile>> h3 = new List<Tuple<long, int, FstFile>>();
            List<FstFile> nonNull = _fileSystem.Files.Where(a => a.Length != 0).ToList();

            for (int i = 0; i < nonNull.Count; i++) //already ordered by offset
            {
                FstFile fl = nonNull[i];
                FstFile fn = null;
                if (i + 1 < nonNull.Count)
                    fn = nonNull[i + 1];

                long end = fl.DataOffset + fl.Length;

                if (end % 0x7c00L == 0 || (int)(end % 0x7c00L) > 0x7c00 - 28)
                {
                    if (fn == null)
                        h3.Add(new Tuple<long, int, FstFile>(fl.DataOffset, 28, fl));
                    else
                    {
                        int nullCount = 28;
                        if (end % 0x7c00L != 0)
                        {
                            nullCount = (0x7c00 - (int)(end % 0x7c00L));
                            end += nullCount;
                            nullCount = 28 - nullCount;
                        }

                        long diff = (int)(fn.DataOffset - end);
                        if (diff >= 0x7c00)
                            h3.Add(new Tuple<long, int, FstFile>(end, nullCount + (nullCount % 2), fl)); //data offset
                    }
                }
            }
            _scrubManager.H3Nulls.AddRange(h3.Union(_fileSystem.Files.Where(a => a.Length == 0).Select(a => new Tuple<long, int, FstFile>(a.DataOffset, 28, a))));
        }

        public string Id { get; private set; }
        public int DiscNo { get; private set; }

        public FileSystem FileSystem
        {
            get
            {
                if (_fileSystem == null && _fst != null && _fst.Length != 0)
                {
                    MemorySection ms = new MemorySection(_fst);
                    _fileSystem = FileSystem.Parse(ms, new FstFile(null) { DataOffset = FstOffset, Length = FstSize, Name = ".fst", Offset = NStream.DataToOffset(FstOffset, true), IsNonFstFile = true }, this.Id, false);
                }
                return _fileSystem;
            }
        }

        public FstFile[] FlatFileSystem
        {
            get
            {
                if (_flatFiles == null && this.FileSystem != null)
                    _flatFiles = this.FileSystem.Files;
                return _flatFiles;
            }
        }
    }
}
