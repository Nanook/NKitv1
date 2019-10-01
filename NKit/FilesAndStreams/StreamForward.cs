using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    //do not wrap a BufferedStream around this class as it can cause a seek even though it might have the data in its internal buffer

    public class StreamForward : Stream
    {
        private long _size;
        private Stream _stream;
        private IDisposable _disposable;
        private long _read = 0;

        public StreamForward(Stream stream, IDisposable dispose) : this(-1, stream, dispose)
        {
        }

        public StreamForward(long size, Stream stream, IDisposable dispose)
        {
            _disposable = dispose;
            _stream = stream;
            _size = size == -1 ? _stream.Length : size;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public int ForceGczReadBugFix { get; set; } //zlib read always reads the decompressed size - this is a hack work around

        public override int Read(byte[] buffer, int offset, int count)
        {
            int r = _stream.Read(buffer, offset, ForceGczReadBugFix != 0 ? ForceGczReadBugFix : count);
            _read += r;
            return r;
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return true; } } //sometimes

        public override bool CanWrite { get { return false; } }

        public override long Length { get { return _size; } }

        public override long Position
        {
            get { return _read; }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush()
        {
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            long p = _read;
            switch (origin)
            {
                case SeekOrigin.Current: p += offset; break;
                case SeekOrigin.End: p = Length + offset; break;
                default: /*case SeekOrigin.Begin:*/ p = offset; break;
            }

            if (p < _read)
                throw new Exception("Cannot seek backwards");

            while (p > _read)
            {
                int r = (int)Math.Min(0x2000000L, p - _read);
                _stream.Copy(ByteStream.Zeros, r);
                _read += (long)r;
            }

            return _read;
        }

        public override void SetLength(long value)
        {
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                _stream?.Close();
                _stream?.Dispose();
                _stream = null;
            }
            catch { }

            try
            {
                _disposable?.Dispose();
                _disposable = null;
            }
            catch { }

            try
            {
                base.Dispose(disposing);
            }
            catch { }
        }

    }
}
