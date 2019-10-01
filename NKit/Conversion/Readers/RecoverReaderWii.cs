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
    internal class RecoverReaderWii : IReader
    {

        private WiiDiscHeaderSection _hdr;
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
            if (!Settings.ConfigFileFound)
                _log?.Log("!! No config file found - This is required to restore and validate images");
            bool truncatedRvtr = false;

            bool write = !(outStream is ByteStream || outStream == Stream.Null);
            //ProgressResult result = ctx.Result;

            byte[] origHeader = null;
            byte[] dataHeader = new byte[256];
            uint headerCrc = 0;
            uint dataHeaderCrc = 0;
            //bool fstMissingWithH3Error = false;

            NCrc crc = new NCrc();
            CryptoStream target = null;
            int h3Errors = 0;
            List<string> requiredUpdateFiles = new List<string>();

            try
            {
                //long progress = 0;
                string lastPartitionId = "";
                bool generateUpdateFiller = false;
                bool generateOtherFiller = true;
                bool forceFillerJunk = false;

                NDisc disc = new NDisc(_log, inStream);

                foreach (IWiiDiscSection s in disc.EnumerateSectionsFix(generateUpdateFiller, generateOtherFiller, forceFillerJunk))
                {
                    if (s is WiiDiscHeaderSection)
                    {
                        _hdr = (WiiDiscHeaderSection)s;
                        origHeader = (byte[])_hdr.Data.Clone();

                        target = new CryptoStream(outStream, crc, CryptoStreamMode.Write);
                        target.Write(_hdr.Data, 0, _hdr.Data.Length); //write the header

                        applyPartitionTableFixes(inStream, requiredUpdateFiles, ctx.Settings, ctx.Recovery);
                        headerCrc = Crc.Compute(_hdr.Data);

                        crc.Snapshot("Header");

                        pc.ReaderCheckPoint1PreWrite(null, 0); //size that we will output from this read
                    }
                    else if (s is WiiPartitionSection)
                    {
                        WiiPartitionSection ps = (WiiPartitionSection)s;

                        if (!truncatedRvtr && ps.Header.IsRvtR && inStream.RecoverySize == NStream.FullSizeWii5)
                        {
                            _log.LogDetail(string.Format("Truncated RVT-R image detected. Pad it with 00 to {0} bytes for NKit to handle it properly", NStream.FullSizeWiiRvtr.ToString()));
                            truncatedRvtr = true;
                        }

                        if (applyFixes(ps.Header, inStream))
                        {
                            _hdr.UpdateRepair();
                            headerCrc = Crc.Compute(_hdr.Data); //recalculate
                        }

                        if (ps.Header.Type == PartitionType.Data)
                        {
                            if (applyDataPartitionFixes(ps.Header))
                                headerCrc = Crc.Compute(_hdr.Data); //recalculate
                            Array.Copy(ps.Header.Data, dataHeader, dataHeader.Length);
                        }

                        target.Write(ps.Header.Data, 0, (int)ps.Header.Size);
                        foreach (WiiPartitionGroupSection pg in ps.Sections)
                        {
                            if (ps.Header.Type == PartitionType.Data && pg.DiscOffset == ps.Header.DiscOffset + ps.Header.Size)
                                Array.Copy(pg.Decrypted, 0x400, dataHeader, 0, dataHeader.Length); //copy out datapartition header (256 bytes)
                            pg.Unscrub(ctx.Recovery.JunkPatches);
                            h3Errors += pg.H3Errors;
                            target.Write(pg.Encrypted, 0, (int)pg.Size);
                        }
                        if (h3Errors != 0)
                            _log?.LogDetail(string.Format("{0} unrecoverable group errors, this image will now be corrupted due to failed unscrubbing attempts!", h3Errors.ToString()));
                        lastPartitionId = ps.Id;
                    }
                    else if (s is WiiFillerSection)
                    {
                        WiiFillerSection fs = (WiiFillerSection)s;
                        if (fs.Size != 0)
                        {
                            foreach (WiiFillerSectionItem item in ((WiiFillerSection)s).Sections)
                                target.Write(item.Data, 0, (int)item.Size);
                        }
                        crc.Snapshot(((WiiFillerSection)s).DiscOffset == 0x50000 ? "[Update Filler]" : lastPartitionId);
                    }
                }
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "RestoreReaderWii.Restore - Read and repair");
            }

            try
            {
                Tuple<string, string, string, uint>[] allParts = ctx.Recovery.WiiUPartsData.Union(ctx.Recovery.WiiUOtherPartsData).ToArray();
                uint[] uniqueCrcs = allParts.Select(a => a.Item4).Union(ctx.Settings.RedumpUpdateCrcs.Where(a => !allParts.Any(b => a == b.Item4))).ToArray();


                //create a data header based on the modified header
                byte[] dataHdr = dataHeader;
                dataHeader = (byte[])_hdr.Data.Clone();
                Array.Copy(dataHdr, dataHeader, dataHdr.Length);
                Array.Clear(dataHeader, 0x60, 2);
                dataHeaderCrc = Crc.Compute(dataHeader);

                bool isCustom = false;
                bool updatePartitionMissing = false;

                SortedList<uint, bool> checkCrcs = new SortedList<uint, bool>();
                foreach (RedumpEntry r in ctx.Dats.RedumpData)
                    checkCrcs.Add(r.Crc, true);
                foreach (RedumpEntry r in ctx.Dats.CustomData.Where(a => !checkCrcs.ContainsKey(a.Crc)))
                    checkCrcs.Add(r.Crc, false);


                HeaderBruteForcer crcMtch = new HeaderBruteForcer(uniqueCrcs, checkCrcs, ctx.Settings.RedumpRegionData, _hdr.Data, dataHeader);
                BruteForceCrcResult bfMatch = crcMtch.Match(crc.Crcs);

                string updateFilename = allParts?.FirstOrDefault(a => a.Item4 == (bfMatch.UpdateChanged ? bfMatch.UpdateCrc : crc.Crcs[1].Value))?.Item1;
                updatePartitionMissing = bfMatch.UpdateChanged && !allParts.Any(a => a.Item4 == bfMatch.UpdateCrc); //matched, but update crc not an update partition

                if (bfMatch.HeaderChanged)
                {
                    crc.Crcs[0].PatchCrc = bfMatch.HeaderCrc;
                    crc.Crcs[0].PatchData = bfMatch.Header;

                    if (bfMatch.RegionChanged)
                        _log.LogDetail(bfMatch.Region != bfMatch.OriginalRegion ? string.Format("Region changed from {0} to {1}", ((Region)bfMatch.OriginalRegion).ToString(), ((Region)bfMatch.Region).ToString()) : string.Format("Region age ratings changed for {0} region", ((Region)bfMatch.Region).ToString()));
                }
                bool isRecoverable = false;

                if (bfMatch.UpdateChanged)
                {
                    if (!updatePartitionMissing)
                    {
                        _log?.LogDetail(string.Format("Matched recovery update partition: {0}", updateFilename ?? ""));
                        crc.Crcs[1].Name = updateFilename ?? string.Format("[UNKNOWN {0}]", bfMatch.UpdateCrc.ToString("X8"));
                        crc.Crcs[1].PatchCrc = bfMatch.UpdateCrc;
                        crc.Crcs[1].PatchFile = Path.Combine(ctx.Recovery.WiiUPartsData.Any(a => a.Item4 == bfMatch.UpdateCrc) ? ctx.Settings.RecoveryFilesPath : ctx.Settings.OtherRecoveryFilesPath, updateFilename);
                    }
                    else
                    {
                        _log?.LogDetail(string.Format("Missing update recovery partition file *_{0}", bfMatch.UpdateCrc.ToString("X8")));
                        crc.Crcs[1].Name = "Missing Recovery Partition File";
                        crc.Crcs[1].PatchCrc = bfMatch.UpdateCrc;
                        crc.Crcs[1].PatchFile = null;
                        isRecoverable = true;
                    }
                }
                else if (!string.IsNullOrEmpty(updateFilename))
                    crc.Crcs[1].Name += string.Format(" [Matches {0}{1}]", updateFilename, isRecoverable ? " (Recoverable)" : "");

                string resultMsg = "MatchFail";
                if (bfMatch.MatchedCrc != 0)
                {
                    if (updatePartitionMissing)
                        resultMsg = string.Format("Match {0} (Recoverable: missing update partition {1})", isCustom ? "Custom" : "Redump", bfMatch.UpdateCrc.ToString("X8"));
                    else
                        resultMsg = string.Format("Match {0}", isCustom ? "Custom" : "Redump");
                }

                pc.ReaderCheckPoint2Complete(crc, isRecoverable, crc.FullCrc(true), crc.FullCrc(true), this.VerifyIsWrite, bfMatch?.Header ?? _hdr.Data, resultMsg);
                pc.ReaderCheckPoint3Complete();
            }
            catch (Exception ex)
            {
                throw pc.SetReaderException(ex, "RestoreReaderWii.Read - Read and repair"); //don't let the writer lock
            }
        }

        private string friendly(string text)
        {
            string f = text.Trim('\0') ?? "<NULL>";
            //if (Regex.IsMatch(f, "[^<>A-Z0-9-_+=]", RegexOptions.IgnoreCase))
            //    f = "Hex-" + BitConverter.ToString(Encoding.ASCII.GetBytes(f)).Replace("-", "");
            return f;
        }



        private bool applyFixes(WiiPartitionHeaderSection partHdr, NStream inStream)
        {
            bool changed = false;
            try
            {
                if (inStream.Id == "010E" && partHdr.Id == "RELS")
                {
                    _hdr.Data[0] = (byte)'4';
                    _log?.LogDetail("Disc ID swapped from 010E to 410E");
                    changed = true;
                }

                //if (inStream.Id.StartsWith("RSB") && partHdr.Id.StartsWith("HA8")) //Super Smash Brothers Brawl
                if (inStream.Id.StartsWith("RSB") && partHdr.Type != PartitionType.Update && partHdr.Type != PartitionType.Data) //Super Smash Brothers Brawl
                {
                    WiiPartitionInfo part = _hdr.Partitions.FirstOrDefault(a => a.DiscOffset == partHdr.DiscOffset);
                    if (part != null && part.Table == 0)
                    {
                        part.Table = 1; //WBM swaps this for some reason
                        //_log?.LogDetail("Fixed SSBB partition table - channel location moved");
                        _log?.LogDetail(string.Format("Fixed SSBB partition table - channel {0} moved to table 1 from 0", partHdr.Id));
                        changed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "RestoreReaderWii.applyFixes");
            }
            return changed;
        }

        private bool applyDataPartitionFixes(WiiPartitionHeaderSection dataHdr)
        {
            bool changed = false;

            try
            {
                //align any added channels etc
                long offset = 0; // dataHdr.DiscOffset + dataHdr.Size + dataHdr.PartitionSize;
                foreach (WiiPartitionInfo part in _hdr.Partitions)
                {
                    if (part.DiscOffset != 0)
                        offset = part.DiscOffset;
                    if (offset % 0x10000 != 0) //align
                        offset += (0x10000 - (offset % 0x10000));

                    if (part is WiiPartitionPlaceHolder && part.DiscOffset == 0) //added when we didn't have any data partition length info
                    {
                        part.DiscOffset = offset;
                        offset += ((WiiPartitionPlaceHolder)part).FileLength;
                        changed = true;
                    }
                    else if (offset != part.DiscOffset)
                    {
                        offset = part.DiscOffset; //might fix a case where one vc is missing. Usually all VC is missing or none.
                        changed = true;
                    }
                    if (part.DiscOffset == dataHdr.DiscOffset)
                        offset += dataHdr.Size + dataHdr.PartitionSize;
                }
                if (changed)
                    _hdr.UpdateRepair();
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "RestoreReaderWii.applyDataPartitionFixes");
            }
            return changed;
        }

        private bool applyPartitionTableFixes(NStream inStream, List<string> requiredFiles, Settings settings, RecoveryData rec)
        {
            bool updateAdded = false;

            try
            {
                if (!_hdr.Partitions.Any(a => a.Type == PartitionType.Update)) //missing update partitions
                {
                    _hdr.AddPartitionPlaceHolder(new WiiPartitionPlaceHolder(inStream, null, PartitionType.Update, 0x50000, 0)); //ensure update is first
                    _log?.LogDetail("Added missing update partition entry placeholder");
                    updateAdded = true;
                }

                //assumes that no partitions have the same type (vc is always different types). Channels are before data, vc is after data in table 1
                int prtCount = _hdr.Partitions.Count(a => a.Type != PartitionType.Update && a.Type != PartitionType.Data);
                bool truncated = _hdr.Partitions.Any(a => inStream.RealPosition(a.DiscOffset) >= inStream.SourceSize); //partitions after file ends

                if (prtCount == 0 || truncated)
                {
                    if (truncated)
                    {
                        _hdr.RemovePartitionChannels();
                        _log?.LogDetail("Removed all channels/VC as some partitions were after the file ends");
                    }
                    bool after = _hdr.Partitions.FirstOrDefault(a => a.Type == PartitionType.Data)?.DiscOffset == 0xF800000;
                    Tuple<string, string, string, uint>[] channels = rec.WiiChanData.Union(rec.WiiOtherChanData).Where(a => a.Item2 == inStream.Id8).OrderBy(a => a.Item1).ToArray();
                    Tuple<string, int> known = settings.RedumpChannels.FirstOrDefault(a => a.Item1 == inStream.Id8);

                    if (channels.Length == 1)
                    {
                        _log?.LogDetail("Added missing channel partition entry");
                        _hdr.AddPartitionPlaceHolder(new WiiPartitionPlaceHolder(inStream, Path.Combine(settings.RecoveryFilesPath, channels[0].Item1), PartitionType.Channel, after ? 0 : 0xf800000, 0)); //ensure update is first
                        requiredFiles.Add(Path.Combine(settings.RecoveryFilesPath, channels[0].Item1));
                    }
                    else
                    {
                        foreach (Tuple<string, string, string, uint> part in channels)
                        {
                            PartitionType type = (PartitionType)((uint)part.Item3[0] << 24 | (uint)part.Item3[1] << 16 | (uint)part.Item3[2] << 8 | (uint)part.Item3[3] << 0);

                            _log?.LogDetail(string.Format("Added missing VC partition entry - {0}: {1}", part.Item3, part.Item1));
                            if (!_hdr.Partitions.Any(a => a.Type == type))
                            {
                                _hdr.AddPartitionPlaceHolder(new WiiPartitionPlaceHolder(inStream, Path.Combine(settings.RecoveryFilesPath, part.Item1), type, 0, 1)); //0 offset until we get the data partition
                                requiredFiles.Add(Path.Combine(settings.RecoveryFilesPath, part.Item1));
                            }
                        }
                    }

                    if (known != null && known.Item2 != channels.Length)
                        _log?.LogDetail(string.Format("!! Known partitions mismatch: {0} known, {1} found - Add all {2}_* to Recovery Partitions folder and retry", known.Item2.ToString(), channels.Length.ToString(), inStream.Id8));
                }
                return updateAdded;
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "RestoreReaderWii.applyPartitionTableFixes");
            }
        }
        
    }
}
