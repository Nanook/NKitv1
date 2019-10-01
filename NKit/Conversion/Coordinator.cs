using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.NKit
{

    /// <summary>
    /// A class to coordinate the reader and writer classes that are running in tandem
    /// </summary>
    internal class Coordinator
    {
        public event EventHandler<StartedEventArgs> Started;
        public event EventHandler<CompletedEventArgs> Completed;

        private enum stateEnum { Error, Unset, ReaderCheckPoint1PreWrite, WriterCheckPoint1WriteReady, WriterCheckPoint2Complete, ReaderCheckPoint2Complete, WriterCheckPoint3ApplyPatches, Complete };

        private string _aliasJunkId;
        private stateEnum _state;
        private object _stateLock;
        private NCrc _crcs;
        private long _processSize;
        private byte[] _header;
        private string _resultMessage;
        private bool _isRecoverable;

        private uint _validationCrc;
        private uint _verifiedCrc;
        private bool _verifyIsWrite;
        private byte[] _md5;
        private byte[] _sha1;

        private bool _readerVerifyCrc;
        private bool _writerVerifyCrc;
        private bool _readerValidationCrc;
        private bool _writerValidationCrc;
        private HandledException _readerException;
        private HandledException _writerException;
        private bool _writerFirst;


        public NCrc Patches { get; private set; }
        public NCrc ReaderCrcs { get; private set; }
        public NCrc WriterCrcs { get; private set; }

        public HandledException Exception { get { return _readerException == null || _writerFirst ? _writerException : _readerException; } }
        public long OutputSize { get { return _processSize; } }

        internal Coordinator(uint validationCrc, IValidation reader, IValidation writer, long processSize)
        {
            _processSize = processSize;
            _crcs = null;
            _validationCrc = validationCrc;
            _state = stateEnum.Unset;
            _stateLock = new object();
            _readerVerifyCrc = reader.RequireVerifyCrc;
            _readerValidationCrc = reader.RequireValidationCrc;
            _writerVerifyCrc = writer.RequireVerifyCrc;
            _writerValidationCrc = writer.RequireValidationCrc;
            _readerException = null;
            _writerException = null;
            _writerFirst = false;
        }

        public HandledException SetReaderException(Exception ex, string message, params string[] args)
        {
            if (ex is HandledException)
                return this.SetReaderException((HandledException)ex);
            return this.SetReaderException(new HandledException(ex, message, args));
        }

        public HandledException SetReaderException(HandledException ex)
        {
            lock (_stateLock)
            {
                if (_readerException == null)
                    _readerException = ex;
                _state = stateEnum.Error;
            }
            return _readerException;
        }

        public HandledException SetWriterException(Exception ex, string message, params string[] args)
        {
            if (ex is HandledException)
                return this.SetWriterException((HandledException)ex);
            return this.SetWriterException(new HandledException(ex, message, args));
        }
        public HandledException SetWriterException(HandledException ex)
        {
            lock (_stateLock)
            {
                if (_writerException == null)
                {
                    _writerFirst = _readerException == null;
                    _writerException = ex;
                }
                _state = stateEnum.Error;
            }
            return _writerException;
        }


        public void ReaderCheckPoint1PreWrite(string aliasJunkId, uint nkitSourceCrc)
        {
            _aliasJunkId = aliasJunkId;
            if (_readerValidationCrc)
                _validationCrc = nkitSourceCrc;
            progress(stateEnum.Unset, stateEnum.ReaderCheckPoint1PreWrite);
            progress(stateEnum.WriterCheckPoint1WriteReady, stateEnum.WriterCheckPoint1WriteReady);
            if (this.Started != null)
                this.Started(this, new StartedEventArgs(_processSize, aliasJunkId));
        }

        public void WriterCheckPoint1WriteReady(out string aliasJunkId)
        {
            progress(stateEnum.ReaderCheckPoint1PreWrite, stateEnum.WriterCheckPoint1WriteReady);
            aliasJunkId = _aliasJunkId;
        }

        public void WriterCheckPoint2Complete(out NCrc crcsPatches, out uint validationCrc, byte[] header, long outputSize)
        {
            progress(stateEnum.WriterCheckPoint1WriteReady, stateEnum.WriterCheckPoint2Complete);
            progress(stateEnum.ReaderCheckPoint2Complete, stateEnum.ReaderCheckPoint2Complete);
            validationCrc = _validationCrc; //let the 
            crcsPatches = _crcs;

            if (outputSize != 0)
                _processSize = outputSize;

            if (header != null) //overwrite reader
                _header = header;
        }

        public void ReaderCheckPoint2Complete(NCrc crcsPatches, bool isRecoverable, uint validationCrc, uint verifiedCrc, bool verifyIsWrite, byte[] header, string resultMessage)
        {
            _resultMessage = resultMessage;
            this.ReaderCrcs = crcsPatches;
            _crcs = crcsPatches;
            if (_validationCrc == 0)
                _validationCrc = validationCrc;
            _header = header;
            if (_readerVerifyCrc)
            {
                _verifiedCrc = verifiedCrc;
                _verifyIsWrite = verifyIsWrite;
            }
            if (_readerValidationCrc)
                _validationCrc = validationCrc;

            if (isRecoverable)
                _isRecoverable = true;

            progress(stateEnum.WriterCheckPoint2Complete, stateEnum.ReaderCheckPoint2Complete);
        }
        public void WriterCheckPoint3ApplyPatches(NCrc crcsPatches, bool isRecoverable, uint validationCrc, ChecksumsResult checksums, bool verifyIsWrite, string resultMessage)
        {
            _md5 = checksums.Md5;
            _sha1 = checksums.Sha1;
            this.WriterCheckPoint3ApplyPatches(crcsPatches, isRecoverable, validationCrc, checksums.Crc, verifyIsWrite, resultMessage);
        }

        public void WriterCheckPoint3ApplyPatches(NCrc crcsPatches, bool isRecoverable, uint validationCrc, uint verifiedCrc, bool verifyIsWrite, string resultMessage)
        {
            //apply the patches  get the final crcs
            this.WriterCrcs = crcsPatches;

            progress(stateEnum.ReaderCheckPoint2Complete, stateEnum.Complete);
            if (_resultMessage != null && resultMessage != null)
                _resultMessage = string.Format("{0} / {1}", _resultMessage, resultMessage);
            else if (resultMessage != null)
                _resultMessage = resultMessage;
            if (_writerVerifyCrc)
            {
                _verifiedCrc = verifiedCrc;
                _verifyIsWrite = verifyIsWrite;
            }
            if (_writerValidationCrc)
                _validationCrc = validationCrc;

            if (isRecoverable)
                _isRecoverable = true;
            this.Patches = crcsPatches ?? _crcs; //the reader might be the patch issuer and the writer might be a plain iso writer
        }

        public void ReaderCheckPoint3Complete()
        {
            progress(stateEnum.Complete, stateEnum.Complete);
            if (this.Completed != null)
            {
                this.Completed(this, new CompletedEventArgs(this.Patches?.FullCrc(true) ?? 0, this.Patches?.FullCrc(false) ?? 0, _processSize, _header, _resultMessage, _validationCrc, _verifiedCrc, _verifyIsWrite, _isRecoverable, _md5, _sha1));
            }
        }

        private void progress(stateEnum testState, stateEnum setState)
        {
            stateEnum s;
            lock (_stateLock)
            {
                s = _state;
                if (s == testState)
                {
#if DEBUG
//                    Console.WriteLine(string.Format("\r\nState a: {1}(Test={2})({0})", Thread.CurrentThread.ManagedThreadId.ToString(), setState.ToString(), testState.ToString()));
#endif

                    _state = setState;
                    return;
                }
                else if (s > testState)
                    throw new Exception(string.Format("State is {0} (beyond {1})", s.ToString(), testState.ToString()));
            }

#if DEBUG
//            lock (_stateLock)
//                Console.WriteLine(string.Format("\r\nState b: {1}(Test={2})({0})", Thread.CurrentThread.ManagedThreadId.ToString(), setState.ToString(), testState.ToString()));
#endif

            while (_state != testState)
            {
                Thread.Sleep(250); //lazy wait, it's not time critical
                if (_state == stateEnum.Error)
                    throw new Exception("Exception reported to ProcessCoordinator - Exceptioning out");
            }

            lock (_stateLock)
            {
#if DEBUG
//                Console.WriteLine(string.Format("\r\nState c: {1}(Test={2})({0})", Thread.CurrentThread.ManagedThreadId.ToString(), setState.ToString(), testState.ToString()));
#endif
                _state = setState;
            }
        }
    }

    public class StartedEventArgs : EventArgs
    {
        public StartedEventArgs(long readerLength, string aliasJunkId)
        {
            ReaderLength = readerLength;
            AliasJunkId = aliasJunkId;
        }
        public long ReaderLength { get; }
        public string AliasJunkId { get; }
    }

    public class CompletedEventArgs : EventArgs
    {
        public CompletedEventArgs(uint patchedCrc, uint unpatchedCrc, long outputSize, byte[] header, string resultMessage, uint validationCrc, uint verifyCrc, bool verifyIsWrite, bool isRecoverable, byte[] md5, byte[] sha1)
        {
            PatchedCrc = patchedCrc;
            UnpatchedCrc = unpatchedCrc;
            OutputSize = outputSize;
            Header = header;
            ResultMessage = resultMessage;
            ValidationCrc = validationCrc;
            VerifyCrc = verifyCrc;
            VerifyIsWrite = verifyIsWrite;
            IsRecoverable = isRecoverable;
            Md5 = md5;
            Sha1 = sha1;
        }

        public uint PatchedCrc { get; }
        public uint UnpatchedCrc { get; }
        public long OutputSize { get; }
        public uint NkitSourceCrc { get; }
        public byte[] Header { get; }
        public string ResultMessage { get; }
        public uint ValidationCrc { get; }
        public uint VerifyCrc { get; }
        public bool VerifyIsWrite { get; }
        public bool IsRecoverable { get; }
        public byte[] Md5 { get; }
        public byte[] Sha1 { get; }
    }

}
