using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs() : base() { }
        public string Message { get; internal set; }
        public LogMessageType Type { get; internal set; }
    }

    public class ProgressEventArgs : EventArgs
    {
        public bool IsStart { get; internal set; }
        public bool IsComplete { get; internal set; }
        public float Progress { get; internal set; }
        public float TotalProgress { get; internal set; }
        public string StartMessage { get; internal set; }
        public string CompleteMessage { get; internal set; }
        public long Size { get; internal set; }
    }
}
