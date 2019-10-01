using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiPartitionPlaceHolder : WiiPartitionInfo, IDisposable
    {
        private WiiPartitionSection _reader;
        private bool _isPlaceholder;
        private NStream _nStream;
        private NStream _ws;

        public WiiPartitionPlaceHolder(NStream nStream, string filename, PartitionType type, long offset, int table) : base(type, offset, table, 0)
        {
            _nStream = nStream;
            this.Filename = filename;
            this._isPlaceholder = true;
        }

        public WiiPartitionPlaceHolder(NStream nStream, PartitionType type, long offset, int table) : base(type, offset, table, 0)
        {
            _nStream = nStream;
            this.Filename = null;
            this._isPlaceholder = false;
        }

        public bool IsPlaceholder { get { return _isPlaceholder; } }

        public string Filename { get; set; }
        public long FileLength { get { return new FileInfo(this.Filename).Length; } }

        public NStream Stream
        {
            get
            {
                if (this.Filename != null)
                {
                    _ws = new NStream(File.OpenRead(this.Filename));
                    _ws.Initialize(false);
                }
                return _ws;
            }
        }


        internal WiiPartitionSection Reader
        {
            get
            {
                if (_reader == null && this.Filename != null)
                {
                    _ws = new NStream(File.OpenRead(this.Filename));
                    _ws.Initialize(false);
                    _reader = new WiiPartitionSection(_nStream, (WiiDiscHeaderSection)_nStream.DiscHeader, _ws, 0);
                }
                return _reader;
            }
        }
        public override string ToString()
        {
            return string.Format("{0}", this.DiscOffset.ToString("X8"));
        }
        public void Dispose()
        {
            try
            {
                if (_nStream != _ws)
                    _ws.Close();
            }
            catch { }
        }
    }
}
