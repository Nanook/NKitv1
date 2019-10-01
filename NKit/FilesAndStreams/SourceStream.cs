using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    /// <summary>
    /// Source stream to wrap split file and make them look like one
    /// </summary>
    public class SourceStream : Stream
    {
        private SourceFile _src;
        private long[] _lens;
        private int _idx; //current file index for split files
        private long _prevFileLens;
        private FileStream _fs;

        public SourceStream(SourceFile src)
        {

            _lens = src.AllFiles.Select(a => (new FileInfo(a)).Length).ToArray();

            _src = src;
            _idx = 0;
            _prevFileLens = 0;
            _fs = File.OpenRead(src.FilePath);
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return true; } }

        public override bool CanWrite { get { return false; } }

        public override long Length { get { return _src.Length; } }

        public override long Position
        {
            get { return _prevFileLens + _fs.Position; }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        public override void Flush()
        {
            _fs.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total = count;
            int r = -1;

            while (count != 0 && r != 0)
            {
                r = _fs.Read(buffer, offset, count);
                count -= r;
                offset += r;

                if (_src.IsSplit && _fs.Position == _fs.Length) //load next part
                    this.Seek(0, SeekOrigin.Current); //will load next file
            }

            return total - count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long pos = _fs.Position + _prevFileLens;
            switch (origin)
            {
                case SeekOrigin.Begin: pos = offset; break;
                case SeekOrigin.Current: pos += offset; break;
                case SeekOrigin.End: pos = this.Length + offset; break;
            }

            if (_src.IsSplit)
            {
                _prevFileLens = 0;
                for (int i = 0; i < _lens.Length; i++)
                {
                    if (_prevFileLens + _lens[i] > pos)
                    {
                        if (_idx != i)
                        {
                            if (_fs != null)
                                _fs.Close();
                            _fs = File.OpenRead(_src.AllFiles[i]);
                            _idx = i;
                        }
                        _idx = i;
                        break;
                    }
                    _prevFileLens += _lens[i];
                }
            }

            _fs.Seek(pos - _prevFileLens, SeekOrigin.Begin);
            return pos;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            try
            {
                if (_fs != null)
                    _fs.Close();
                _fs = null;
            }
            catch { }
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_fs != null)
                    _fs.Dispose();
                _fs = null;
            }
            catch { }
            base.Dispose(disposing);
        }
    }
}
