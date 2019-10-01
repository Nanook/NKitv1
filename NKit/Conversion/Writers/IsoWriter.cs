using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class IsoWriter : IWriter
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }
        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Write(Context ctx, Stream inStream, Stream outStream, Coordinator pc)
        {
            try
            {
                long imageSize = pc.OutputSize;
                string ignoreJunkId;
                pc.WriterCheckPoint1WriteReady(out ignoreJunkId); //wait until read has written the header and set the length
                inStream.Copy(outStream, imageSize);

                NCrc readerCrcs;
                uint validationCrc;
                pc.WriterCheckPoint2Complete(out readerCrcs, out validationCrc, null, imageSize); //wait until reader has completed and get crc patches.

                uint crc = readerCrcs?.FullCrc(true) ?? 0;
                pc.WriterCheckPoint3ApplyPatches(null, false, crc, crc, this.VerifyIsWrite, null);

            }
            catch (Exception ex)
            {
                throw pc.SetWriterException(ex, "IsoWriter.Write - Image Write");
            }
        }
    }
}