using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Nanook.NKit
{
    internal class ByteStream : Stream
    {
        public static Stream Zeros { get; }
        public static Stream Fives { get; }
        public static Stream FFs { get; }

        public override long Position { get; set; }
        public override long Length { get { return _length; } }

        public byte[] Decrypted { get; set; }

        private long _length;
        private byte _byte;

        static ByteStream()
        {
            Zeros = new ByteStream(0x00);
            Fives = new ByteStream(0x55);
            FFs = new ByteStream(0xff);
        }

        internal ByteStream(byte b) : this (b, null)
        {

        }

        internal ByteStream(byte b, byte[] decrypted)
        {
            _byte = b;
            this.Decrypted = decrypted;
        }


        public override int Read(byte[] buffer, int offset, int size)
        {
            if (this.Decrypted != null)
            {
                int x = (int)(this.Position % this.Decrypted.Length);
                for (int i = offset; i < offset + size; i++)
                {
                    buffer[i] = this.Decrypted[x++];
                    if (x >= this.Decrypted.Length)
                        x = 0;
                }
            }
            else
            {
                for (int i = offset; i < offset + size; i++)
                    buffer[i] = _byte;
            }
            this.Position += size;
            return size;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return true; } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: Position = offset; break;
                case SeekOrigin.Current: Position += offset; break;
                case SeekOrigin.End: Position = Length + offset; break;
            }
            return Position;
        }
        public override void SetLength(long value) { _length = value; }
        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
