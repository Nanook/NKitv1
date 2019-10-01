using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class IsoReader : IReader
    {
        public bool EncryptWiiPartitions { get; set; }

        private ILog _log;
        public void Construct(ILog log)
        {
            _log = log;
        }

        public bool VerifyIsWrite { get; set; }
        public bool RequireVerifyCrc { get; set; }
        public bool RequireValidationCrc { get; set; }

        public void Read(Context ctx, NStream inStream, Stream outStream, Coordinator pc)
        {

            NCrc crc = new NCrc();
            try
            {
                CryptoStream target = new CryptoStream(outStream, crc, CryptoStreamMode.Write);
                uint outputCrc = 0;

                if (inStream.IsNkit || inStream.IsGameCube || !EncryptWiiPartitions)
                {
                    pc.ReaderCheckPoint1PreWrite(null, 0); //size that we will output from this read
                    if (inStream.HeaderRead)
                    {
                        target.Write(inStream.DiscHeader.Data, 0, (int)inStream.DiscHeader.Size); //write the header
                        inStream.Copy(target, inStream.Length - inStream.DiscHeader.Size);
                    }
                    else
                        inStream.Copy(target, inStream.Length);
                }
                else
                {
                    WiiDiscHeaderSection hdr = null;
                    using (NDisc disc = new NDisc(_log, inStream))
                    {
                        foreach (IWiiDiscSection s in disc.EnumerateSections(inStream.Length)) //ctx.ImageLength
                        {
                            if (s is WiiDiscHeaderSection)
                            {
                                hdr = (WiiDiscHeaderSection)s;
                                target.Write(hdr.Data, 0, hdr.Data.Length); //write the header
                                pc.ReaderCheckPoint1PreWrite(null, 0); //size that we will output from this read //ctx.ImageLength
                            }
                            else if (s is WiiPartitionSection)
                            {
                                WiiPartitionSection ps = (WiiPartitionSection)s;
                                //bool lengthChanged = inStream.CheckLength(ps.DiscOffset, ps.Header.PartitionSize);

                                target.Write(ps.Header.Data, 0, (int)ps.Header.Size);
                                foreach (WiiPartitionGroupSection pg in ps.Sections)
                                    target.Write(pg.Encrypted, 0, (int)pg.Size);
                            }
                            else if (s is WiiFillerSection)
                            {
                                WiiFillerSection fs = (WiiFillerSection)s;
                                if (fs.Size != 0)
                                {
                                    foreach (WiiFillerSectionItem item in ((WiiFillerSection)s).Sections)
                                        target.Write(item.Data, 0, (int)item.Size);
                                }
                            }
                        }
                    }
                }
                crc.Snapshot("iso");

                if (inStream.IsNkit)
                    outputCrc = inStream.DiscHeader.ReadUInt32B(0x208); //assume source nkit crc is correct
                else
                    outputCrc = crc.FullCrc(true);

                pc.ReaderCheckPoint2Complete(crc, false, outputCrc, outputCrc, this.VerifyIsWrite, null, null);
                pc.ReaderCheckPoint3Complete();

            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "IsoReader.Read - Read Image"); //don't let the writer lock
            }
        }

        private string friendly(string text)
        {
            string f = text.Trim('\0') ?? "<NULL>";
            //if (Regex.IsMatch(f, "[^<>A-Z0-9-_+=]", RegexOptions.IgnoreCase))
            //    f = "Hex-" + BitConverter.ToString(Encoding.ASCII.GetBytes(f)).Replace("-", "");
            return f;
        }

    }
}
