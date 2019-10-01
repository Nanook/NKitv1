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
    internal class HashWriter : IWriter
    {
        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
            this.VerifyIsWrite = true;
        }

        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Write(Context ctx, Stream inStream, Stream output, Coordinator pc)
        {
            try
            {
                long imageSize = pc.OutputSize;
                string junkId;
                pc.WriterCheckPoint1WriteReady(out junkId); //wait until read has written the header and set the length

                ChecksumsResult chk = new ChecksumsResult();

                Crc crc = new Crc();
                SHA1 sha1 = SHA1.Create();
                MD5 md5 = MD5.Create();

                List<CryptoStream> targets = new List<CryptoStream>();
                targets.Add(new CryptoStream(Stream.Null, crc, CryptoStreamMode.Write));
                targets.Add(new CryptoStream(Stream.Null, sha1, CryptoStreamMode.Write));
                targets.Add(new CryptoStream(Stream.Null, md5, CryptoStreamMode.Write));

                int len = 0x200000; //arbitrary
                byte[] buffer = new byte[len];
                byte[] buffer2 = new byte[len]; //double buffered
                byte[] tmp;
                int read = 0;
                int read2 = 0;
                long total = imageSize;
                long prg = 0;
                Task t = null;
                while (prg != total)
                {
                    read2 = 0;
                    prg += read;
                    if (prg < total)
                    {
                        t = Task.Run(() => read2 = inStream.Read(buffer2, 0, (int)Math.Min((long)len, total - prg)));
                        t.ConfigureAwait(false);
                    }
                    else
                        t = null;
                    Parallel.ForEach(targets, (target) => target.Write(buffer, 0, read));
                    if (t != null && !t.IsCompleted)
                        t.Wait();

                    tmp = buffer2;
                    buffer2 = buffer;
                    buffer = tmp;
                    if (read == 0 && read2 == 0)
                        throw new Exception("Could not read from stream");
                    read = read2;
                }

                foreach (CryptoStream target in targets)
                    target.Close();
                //using (CryptoStream target = new CryptoStream(new CryptoStream(new CryptoStream(System.IO.Stream.Null, crc, CryptoStreamMode.Write), sha1, CryptoStreamMode.Write), md5, CryptoStreamMode.Write))
                //    _stream.Copy(0, target, _stream.Length, this.Progress);

                chk.Crc = crc.Value;
                chk.Sha1 = sha1.Hash;
                chk.Md5 = md5.Hash;

                _log?.LogDetail(string.Format("CRC: {0}", chk.Crc.ToString("X8")));
                _log?.LogDetail(string.Format("MD5: {0}", BitConverter.ToString(chk.Md5).Replace("-", "")));
                _log?.LogDetail(string.Format("SHA: {0}", BitConverter.ToString(chk.Sha1).Replace("-", "")));

                //inStream.Copy(Stream.Null, imageSize);

                NCrc readerCrcs;
                uint validationCrc;
                pc.WriterCheckPoint2Complete(out readerCrcs, out validationCrc, null, imageSize); //wait until reader has completed and get crc patches.

                uint fullCrc = readerCrcs.FullCrc(true);
                string msg;
                if (validationCrc == 0)
                    msg = string.Format("Crc:{0} - No Test Crc Found", fullCrc.ToString("X8"));
                else if (validationCrc == fullCrc)
                    msg = string.Format("Crc:{0} - Success", fullCrc.ToString("X8"));
                else
                    msg = string.Format("Crc:{0} - Failed Test Crc:{1}", fullCrc.ToString("X8"), validationCrc.ToString("X8"));

                pc.WriterCheckPoint3ApplyPatches(null, false, chk.Crc, chk, this.VerifyIsWrite, msg);
            }
            catch (Exception ex)
            {
                throw pc.SetWriterException(ex, "VerifyWriter.Write - Image Write");
            }
        }
    }
}