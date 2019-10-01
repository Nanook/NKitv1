using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    //do not wrap a BufferedStream around this class as it can cause a seek even though it might have the data in its internal buffer

    public class StreamCircularBuffer : Stream, IProgress
    {
        private long _size;
        private byte[] _b;
        private int _r;
        private int _w;
        private bool _writingComplete;
        private bool _readingComplete;
        private object _lock;
        private object _lock2;
        private bool _readPaused;
        private bool _writePaused;
        private long _rPosition;
        private long _wPosition;
        private long _seekPosition;
        private Task _thread;
        private CancellationTokenSource _cancelWrite;
        private Stream _stream;
        private IDisposable _disposable;
        private int _writerThreadId;

        //private Stopwatch _rsw;
        //private Stopwatch _wsw;

        public Exception WriterException { get; private set; }

        float IProgress.Value
        {
            get { return (float)((double)_rPosition / (double)_size); }
        }

        public StreamCircularBuffer(long size, Stream stream, IDisposable dispose, Action<Stream> write)
        {
            //_rsw = new Stopwatch();
            //_wsw = new Stopwatch();
            _disposable = dispose;
            _stream = stream;
            _size = size == -1 ? _stream.Length : size;
            _b = new byte[0x500000]; //more than double the max read size
            _r = 0;
            _w = 0;
            _rPosition = 0;
            _wPosition = 0;
            _seekPosition = -1;
            _writingComplete = false;
            _readingComplete = false;
            _lock = new object();
            _lock2 = new object();
            _readPaused = false;
            _writePaused = false;
            _cancelWrite = new CancellationTokenSource();
            _thread = Task.Run(() =>
            {
                _writerThreadId = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    write(this);
                }
                catch (Exception ex)
                {
                    if (this.WriterException == null)
                        this.WriterException = ex;
                }
            }, _cancelWrite.Token);
            _thread.ContinueWith(t =>
            {
                _cancelWrite = null;
                _writingComplete = true;
                if (this.WriterException != null)
                {
                    lock (_lock2)
                    {
                        if (_readPaused)
                            Monitor.Pulse(_lock2);
                    }
                    throw this.WriterException;
                }
            });
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // 1 2 3 4
            //   w   r  = read=2, write=2
            //     r w  = read=1, write=3
            //rw        = read=0, write=4

            if (_writingComplete || (_size != 0 && _wPosition >= _size))
            {
                return; //this prevents the writer locking if read has completed
                //throw new Exception("Thread killed to stop extraction");  //should be swallowed
            }

            while (count != 0 && !_writingComplete)
            {
                lock (_lock)
                {
                    if (_seekPosition != -1 && _wPosition < _seekPosition)
                    {
                        if (_wPosition + count >= _seekPosition)
                            _seekPosition += 0;

                        int c = (int)Math.Min((long)count, _seekPosition - _wPosition);
                        _wPosition += c;
                        offset += c;
                        count -= c;

                        if (_wPosition == _seekPosition)
                        {
                            //Debug.WriteLine("SEEK: " + _wPosition.ToString("X"));
                            _rPosition = _seekPosition;
                            _w = _r = (int)(_rPosition % (long)_b.Length);
                            _seekPosition = -1; //stop seeking
                        }
                        else
                            return;
                    }


                    if (_seekPosition == -1)
                    {
                        int l = _r - _w;
                        if (l < 0 || (l == 0 && _wPosition == _rPosition))
                            l = (_b.Length + _r) - _w; //0 becomes max amount

                        l = Math.Min(l, count);
                        int l1 = Math.Min(l, _b.Length - _w);

                        Array.Copy(buffer, offset, _b, _w, l1);
                        _w = (_w + l1) == _b.Length ? 0 : _w + l1;

                        if (l != l1)
                        {
                            Array.Copy(buffer, offset + l1, _b, _w, l - l1);
                            _w += l - l1;
                        }
                        offset += l; //for write
                        count -= l;
                        _wPosition += l;
                    }
                }


                lock (_lock2)
                {
                    if (_readPaused)
                        Monitor.Pulse(_lock2); //allow reader to continue
                    else if (_readingComplete || (_size != 0 && _rPosition >= _size))
                        break; //read has completed
                    else if (this.WriterException != null || (count != 0 && !_writingComplete && !_writePaused && !_readPaused))
                    {
                        _writePaused = true;
                        //_wsw.Start();
                        Monitor.Wait(_lock2);
                        //_wsw.Stop();
                        _writePaused = false;
                    }
                }

            }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            // 1 2 3 4
            //   w   r  = read=2, write=2
            //     r w  = read=1, write=3
            //rw        = read=0, write=4

            int size = count;
            while (count != 0 && (!_writingComplete || _rPosition < _wPosition))
            {
                if (_seekPosition == -1) // && _wPosition > _rPosition)
                {
                    lock (_lock)
                    {
                        int l = _w - _r;
                        if (l < 0 || (l == 0 && _rPosition < _wPosition))
                            l = (_b.Length + _w) - _r;

                        l = Math.Min(l, count);
                        int l1 = Math.Min(l, _b.Length - _r);

                        Array.Copy(_b, _r, buffer, offset, l1);
                        _r = (_r + l1) == _b.Length ? 0 : _r + l1;

                        if (l != l1)
                        {
                            Array.Copy(_b, _r, buffer, offset + l1, l - l1);
                            _r += l - l1;
                        }
                        offset += l; //for write
                        count -= l;
                        _rPosition += l;
                    }
                }
                lock (_lock2)
                {
                    if (_writePaused)
                        Monitor.Pulse(_lock2); //allow writer to continue
                    else if (this.WriterException != null || (_size != 0 && _rPosition >= _size))
                        break;
                    else if (count != 0 && !_writingComplete && !_readPaused && !_writePaused)
                    {
                        _readPaused = true;
                        //_rsw.Start();
                        Monitor.Wait(_lock2);
                        //_wsw.Stop();
                        _readPaused = false;
                    }
                }
            }

            return size - count;
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return true; } }

        public override bool CanWrite { get { return true; } }

        public override long Length { get { return _size; } }

        public override long Position
        {
            get { return _rPosition; }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        public long WriterPosition {  get { return _wPosition; } }

        public override void Flush()
        {
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            //if (_complete)
            //    return _rPosition;

            long p = _rPosition;
            switch (origin)
            {
                case SeekOrigin.Current: p += offset; break;
                case SeekOrigin.End: p = Length + offset; break;
                default: /*case SeekOrigin.Begin:*/ p = offset; break;
            }

            if (p > _rPosition)
            {
                lock (_lock)
                {
                    if (_wPosition > p) //we have it
                    {
                        _rPosition = p;
                        _r = (int)(_rPosition % (long)_b.Length);
                    }
                    else
                    {
                        lock (_lock2)
                        {
                            _seekPosition = p;
                            if (_writePaused)
                                Monitor.Pulse(_lock2);
                        }
                    }
                }
            }
            else if (p == Position)
            {
            }
            else
                throw new NotImplementedException("Only forward seek is supported");

            return _rPosition;
        }

        public override void SetLength(long value)
        {
        }

        protected override void Dispose(bool disposing) //called when reader completes
        {
            if (_writerThreadId == Thread.CurrentThread.ManagedThreadId) //writer thread
                return;

            _readingComplete = true;

            try
            {
                if (!_writingComplete)
                {
                    try
                    {
                        if (_cancelWrite != null)
                            _cancelWrite.Cancel();
                    }
                    catch { }

                    while (!_writingComplete)
                    {
                        if (_writePaused)
                        {
                            lock(_lock2)
                                Monitor.Pulse(_lock2);
                        }
                        Thread.Sleep(100); //ensure the write thread has quit
                    }
                }

                if (_stream != null)
                {
                    try
                    {
                        _stream.Close();
                        _stream.Dispose();
                    }
                    catch { }
                    _stream = null;
                }

                if (_disposable != null)
                    _disposable.Dispose();
                _disposable = null;

            }
            catch { }

            try
            {
                base.Dispose(disposing);
            }
            catch { }

            //Debug.WriteLine(string.Format("Writer TimeLocked: {0}, Read TimeLocked: {1}", _wsw.ElapsedMilliseconds.ToString(), _rsw.ElapsedMilliseconds.ToString()));

            _b = null;
        }

    }
}
