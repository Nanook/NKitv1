using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Nanook.NKit
{
    /// <summary>
    /// Class to manage the encryption and hash state to ensure the minimal amount of hashing and encryption happens per group
    /// </summary>
    internal class WiiPartitionGroupEncryptionState
    {
        private class block
        {
            public block(int index, PartitionHashTable h1Table, PartitionHashTable h2Table, byte[] key)
            {
                this.Index = index;
                this.Offset = index * 0x8000;
                this.DataOffset = index * 0x8000 + 0x400;
                this.Aes = Aes.Create();
                this.Sha1 = SHA1.Create();
                this.Aes.Padding = PaddingMode.None;
                this.Aes.Key = key;
                this.H0Table = new PartitionHashTable(31); //31 data sectors of 0x400 in a block 
                this.H1Table = h1Table;
                this.H2Table = h2Table;

            }
            public readonly int Index;
            public readonly int Offset;
            public readonly int DataOffset;
            public readonly Aes Aes;
            public readonly SHA1 Sha1;

            public bool IsDirty;  //quick lookup to detect which blocks have been changed
            public bool IsScrubbed;  //set from outside the State object
            public byte ScrubByte;
            public bool IsUsed;
            public PartitionHashTable H0Table { get; private set; }
            public PartitionHashTable H1Table { get; private set; }
            public PartitionHashTable H2Table { get; private set; }
            public override string ToString()
            {
                return string.Format("Index:{0}, Offset:{1}, {2}, {3}, {4}", this.Index.ToString("X"), this.Offset.ToString("X"), this.IsUsed ? "Used" : "NotNused", this.IsScrubbed ? "Scrubbed" : "NotScrubbed", this.IsDirty ? "Dirty" : "Clean");
            }
        }

        private byte[] _h3Table;
        private byte[] _h3Value;
        private bool _isValid;
        private int _groupIdx;
        private byte[] _enc;
        private byte[] _dec;
        private bool _hasEnc; //true if encrypted data is set on populate and decrypted data has not been modified
        private bool _hasDec; //true if decrypted data is set or we have decrypted the encrypted data
        private bool _hasHashes; //false if not refreshed
        private int _maxSize;
        private bool _isDirty; //we have decrypted data and it has been modified
        private bool _forcedHashes;
        private bool _hashedRecalulated;
        private block[] _blocks;
        private int _size;
        private int _usedBlocks;
        public byte[] _unusedBlankHash;



        public WiiPartitionGroupEncryptionState(int maxSize, byte[] key, byte[] h3Table)
        {
            if (maxSize % 0x8000 != 0)
                throw new HandledException("Max group size is not a multiple of 0x8000");
            _h3Table = h3Table;
            _maxSize = maxSize;
            _blocks = new block[maxSize / 0x8000];

            PartitionHashTable h1 = null;
            PartitionHashTable h2 = new PartitionHashTable(8);

            for (int i = 0; i < _blocks.Length; i++)
            {
                if (i % 8 == 0)  //share the h2 hash table across 8 blocks
                    h1 = new PartitionHashTable(8); //8 lots of 8 blocks = 64 blocks
                _blocks[i] = new block(i, h1, h2, key);
            }

            _unusedBlankHash = _blocks[0].Sha1.ComputeHash(new byte[0x400]);
        }

        public void Populate(byte[] data, int size, bool isEnc, bool isEncHeader, int groupIndex)
        {
            int s = Math.Min(size, data.Length);

            if (s % 0x8000 != 0)
                throw new HandledException("Group size is not a multiple of 0x8000");

            _groupIdx = groupIndex;

            _size = s;
            if (s != _maxSize) // || true) //force clone for testing
            {
                _enc = _enc ?? new byte[_maxSize];
                _dec = _dec ?? new byte[_maxSize];
                Array.Copy(data, isEnc ? _enc : _dec, Math.Min(s, _maxSize));
                if (s < _maxSize)
                    Array.Clear(isEnc ? _enc : _dec, s, _maxSize - s);
            }
            else if (isEnc)
            {
                _enc = data;
                _dec = _dec ?? new byte[_maxSize];
            }
            else
            {
                _dec = data;
                _enc = _enc ?? new byte[_maxSize];
            }

            _hasDec = !(_hasEnc = isEnc); //dec array isDirty if isEnc
            _isDirty = false; //not dirty if encrypted or new
            _usedBlocks = s / 0x8000;
            _hasHashes = false;
            _hashedRecalulated = false;
            _forcedHashes = false;

            for (int i = 0; i < _blocks.Length; i++)
            {
                _blocks[i].IsDirty = _isDirty && (i < _usedBlocks); //isDirty is always false - _usedBlocks for clarity of condition
                _blocks[i].IsUsed = i < _usedBlocks;
                _blocks[i].IsScrubbed = false;
                _blocks[i].ScrubByte = 0;
            }

            if (!isEnc && isEncHeader)
            {
                Parallel.ForEach(_blocks, b =>
                {
                    b.Aes.IV = new byte[16];
                    using (ICryptoTransform cryptor = b.Aes.CreateDecryptor())
                        cryptor.TransformBlock(_dec, b.Offset, 0x400, _dec, b.Offset);
                });
            }
            else if (isEnc)
                Parallel.ForEach(_blocks, b => setScrubbedBlockInfo(b));

        }

        /// <summary>
        /// Prepares the group and encrypts it if required. Can be expensive if changes are made to the decrypted data
        /// </summary>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] //if visual studio executes this while debuging it can calculate headers at bad times and reset the dirty flags incorrectly
        public byte[] Encrypted { get { return ensureEncrypted(); } }
        public byte[] Decrypted { get { return ensureDecrypted(); } }
        public int UsedBlocks { get { return _usedBlocks; } }
        public int UsedSize { get { return _size; } }
        public int BlockOffset(int blockIndex) { return _blocks[blockIndex].Offset; }
        public int BlockDataOffset(int blockIndex) { return _blocks[blockIndex].DataOffset; }
        public void MarkBlockDirty(int blockIndex)
        {
            _blocks[blockIndex].IsDirty = true;
            _isDirty = true;
            _hasEnc = false; //invalidate encrypted data
        }
        public void MarkBlockScrubbed(int blockIndex, byte scrubByte)
        {
            _blocks[blockIndex].IsScrubbed = blockIndex < _usedBlocks; //can only be scrubbed if used
            _blocks[blockIndex].ScrubByte = scrubByte;
        }
        public void MarkBlockUnscrubbedAndDirty(int blockIndex)
        {
            _blocks[blockIndex].IsScrubbed = false;
            _blocks[blockIndex].ScrubByte = 0;
            MarkBlockDirty(blockIndex);
        }

        public bool AllScrubbedSameByte()
        {
            byte b = _blocks[0].ScrubByte;
            return _blocks.Where(a => a.IsUsed).All(a => a.IsScrubbed && a.ScrubByte == b);
        }

        internal bool FastHashIsValid()
        {
            ensureDecrypted();
            ensureHashCache();
            block b1 = _blocks.First();

            bool anyInvalid = _blocks.AsParallel().Any(b =>
            {
                if (b.IsUsed)
                {
                    if (b.IsScrubbed || !blockIsValid(b))
                        return true;

                    //ensure the blank areas are blank - some customs fail this - Mario Kart Black
                    if (!this.Decrypted.Equals(b.Offset + 0x26c, 20, 0) || !this.Decrypted.Equals(b.Offset + 0x320, 32, 0) || !this.Decrypted.Equals(b.Offset + 0x3e0, 32, 0))
                        return true;

                    //Test H1
                    if (!this.Decrypted.Equals(b.Offset + 0x280, b.H1Table.Bytes, 0, 0xA0))
                        return true;

                    //Test H2
                    if (b != b1 && !this.Decrypted.Equals(b.Offset + 0x340, b1.H2Table.Bytes, 0, 0xA0))
                        return true;

                }
                else
                    hashCacheH0BlockCalc(b); //set the unused hashes so that the H1+H2 match the H3
                return false;
            });

            if (anyInvalid) //|| !hashCacheH1H2GroupCalc() || !_isValid) //hashCacheH1H2GroupCalc sets _isValid to a new result
                return false;
            hashCacheH1H2GroupCalc();
            if (!_isValid)
                return false;
            return true;
        }

        public int ScrubbedBlocks { get { return _blocks.Count(a => a.IsScrubbed); } }

        private void recalculateHashes()
        {
            hashCacheH0GroupCalc();
            hashCacheH1H2GroupCalc();
            _isDirty = false;
            _hashedRecalulated = true;
            _forcedHashes = false;
        }

        public void ForceHashes(byte[] hashes)
        {
            ensureDecrypted();
            for (int i = 0; i < _usedBlocks; i++)
            {
                if (hashes != null) //null if applied already
                    Array.Copy(hashes, i * 0x8000, _dec, i * 0x8000, 0x400);
                _blocks[i].IsDirty = false;
            }
            _forcedHashes = true;
            _hasHashes = true;
            _isDirty = false;
            _hasEnc = false; //cancel the encrypted data
        }

        public bool IsValid()
        {
            return this.IsValid(false);
        }
        public bool IsValid(bool hashRecalculateIfDirty)
        {
            bool dirty = _isDirty;
            ensureDecrypted();
            ensureHashCache();
            if (hashRecalculateIfDirty && dirty)
                recalculateHashes();
            return _isValid;
        }

        /// <summary>
        /// Full hash H0 regen test - if calling on multi threads call IsValid(false) first
        /// </summary>
        public bool BlockIsValid(int blockIndex)
        {
            return blockIsValid(_blocks[blockIndex]);
        }

        private bool blockIsValid(block b)
        {
            ensureDecrypted();
            ensureHashCache();
            for (int i = 1; i < 32; i++)
            {
                if (!b.H0Table.Equals(i - 1, b.Sha1.ComputeHash(_dec, b.Offset + (i * 0x400), 0x400)))
                    return false;
            }
            return true;
        }

        public bool BlockIsScrubbed(int blockIndex)
        {
            return _blocks[blockIndex].IsScrubbed;
        }

        private void ensureHashCache()
        {
            if (!_hasHashes)
            {
                hashCacheH0H1GroupPopulate();
                hashCacheH2Populate();
                _hasHashes = true;
                _isDirty = false;
            }
        }

        private byte[] ensureEncrypted()
        {
            if (!_hasEnc) //if false we must have decrypted data
            {
                ensureHashCache(); //calc hashes if dirty
                Parallel.ForEach(_blocks, b =>
                {
                    if (_hashedRecalulated)
                        commitHashCache(b);
                    encrypt(b);
                });
                _isDirty = false;
                _hasEnc = true;
            }
            return _enc;
        }

        private byte[] ensureDecrypted()
        {
            if (_hasEnc && !_hasDec) //_hasEnc only true when the data is not Populated as so and not dirty
            {
                Parallel.ForEach(_blocks, b =>
                {
                    decrypt(b); //decrypt
                    hashCacheH0H1BlockPopulate(b);
                });
                hashCacheH2Populate();
                _hasHashes = true;
                _hasDec = true;
            }
            return _dec;
        }

        private void encrypt(block b)
        {
            byte[] iv = new byte[16];

            //if this image is scrubbed with a value that's not 00 we must restore the IV value that creates the sequence of events when decrypting scrubbed data
            if (b.IsScrubbed && b.ScrubByte != 0 && !_forcedHashes)
                iv.Clear(0, iv.Length, b.ScrubByte);

            b.Aes.IV = iv; //takes as copy

            using (ICryptoTransform cryptor = b.Aes.CreateEncryptor())
                cryptor.TransformBlock(_dec, b.Offset, 0x400, _enc, b.Offset);

            Array.Copy(_enc, b.Offset + 0x3d0, iv, 0, 16);
            b.Aes.IV = iv;

            using (ICryptoTransform cryptor = b.Aes.CreateEncryptor())
                cryptor.TransformBlock(_dec, b.DataOffset, 0x7c00, _enc, b.DataOffset);

            b.IsDirty = false;
        }

        private void decrypt(block b)
        {
            byte[] iv = new byte[16];

            b.Aes.IV = iv; //takes a copy
            Array.Copy(_enc, b.Offset + 0x3d0, iv, 0, 16); //get iv from encrypted header

            //decrypt the header to get the key
            using (ICryptoTransform cryptor = b.Aes.CreateDecryptor())
                cryptor.TransformBlock(_enc, b.Offset, 0x400, _dec, b.Offset);

            b.Aes.IV = iv;

            using (ICryptoTransform cryptor = b.Aes.CreateDecryptor())
                cryptor.TransformBlock(_enc, b.DataOffset, 0x7c00, _dec, b.DataOffset);

            b.IsDirty = false;
        }

        private bool hashCacheH0H1GroupPopulate()
        {
            bool eq = true;
            Parallel.ForEach(_blocks, b => hashCacheH0H1BlockPopulate(b));
            return eq;
        }

        private void hashCacheH0H1BlockPopulate(block b)
        {
            if (!b.IsUsed) //set the h1 table
                b.H0Table.Reset(new byte[0x26c], 0); //0 to 0x26c
            else
                b.H0Table.Reset(_dec, b.Offset); //0 to 0x26c

            if (b.Index % 8 == 0) //get the h2 table from the first block in the 8 block group
                b.H1Table.Reset(_dec, b.Offset + 0x280); //0x280 to 0x320
        }
        private void hashCacheH2Populate()
        {
            _blocks[0].H2Table.Reset(_dec, 0x340); //set to all blocks
            _h3Value = _blocks[0].Sha1.ComputeHash(_blocks[0].H2Table.Bytes);
            _isValid = _h3Value.Equals(0, _h3Table, _groupIdx * 20, 20);
        }


        private bool hashCacheH0GroupCalc()
        {
            bool eq = true;
            Parallel.ForEach(_blocks, b =>
            {
                if (!hashCacheH0BlockCalc(b))
                    eq = false;
            });
            return eq;
        }

        private bool hashCacheH0BlockCalc(block b)
        {
            bool eq = true;
            for (int i = 1; i < 32; i++)
            {
                if (!b.H0Table.Set(i - 1, b.IsUsed ? b.Sha1.ComputeHash(_dec, b.Offset + (i * 0x400), 0x400) : _unusedBlankHash, eq))
                    eq = false;
            };
            return eq; //true if same = no change
        }

        private bool hashCacheH1H2GroupCalc()
        {
            bool eq = true;
            Parallel.For(0, 8, j =>
            {
                SHA1 sha1 = _blocks[j].Sha1;
                for (int i = 0; i < 8; i++) //set hashes in to blocks of 8 - set against first in group
                {
                    if (!_blocks[8 * j].H1Table.Set(i, sha1.ComputeHash(_blocks[8 * j + i].H0Table.Bytes), eq))
                        eq = false;
                };

                if (!_blocks[0].H2Table.Set(j, sha1.ComputeHash(_blocks[8 * j].H1Table.Bytes), eq))
                    eq = false;
            });
            _h3Value = _blocks[0].Sha1.ComputeHash(_blocks[0].H2Table.Bytes);
            _isValid = _h3Value.Equals(0, _h3Table, _groupIdx * 20, 20);
            return eq;
        }

        private void commitHashCache(block b)
        {
            b.H0Table.CopyAll(_dec, b.Offset); //31 20 byte hashes
            Array.Clear(_dec, b.Offset + 0x26c, 0x280 - 0x26c); //0x26c 31 20 byte hashes
            b.H1Table.CopyAll(_dec, b.Offset + 0x280); //8 20 byte hashes
            Array.Clear(_dec, b.Offset + 0x280 + 0xA0, 0x340 - (0x280 + 0xA0)); //0xA0 8 20 byte hashes
            b.H2Table.CopyAll(_dec, b.Offset + 0x340); //8 20 byte hashes
            Array.Clear(_dec, b.Offset + 0x340 + 0xA0, 0x400 - (0x340 + 0xA0));
        }

        private void setScrubbedBlockInfo(block b)
        {
            if (_hasEnc)
            {
                int end = b.DataOffset;
                byte byt = _enc[b.Offset];
                if (byt == 0 || byt == 0xff)
                {
                    bool scrubbed = true;
                    for (int i = end - 0x400; i < end; i++)
                    {
                        if (_enc[i] != byt)
                        {
                            scrubbed = false;
                            break;
                        }
                    }
                    if (b.IsUsed)
                    {
                        b.IsScrubbed = scrubbed;
                        b.ScrubByte = byt;
                    }
                }
                else
                    b.IsScrubbed = false;
            }
        }
    }
}
