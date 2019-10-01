using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class WiiDiscHeaderSection : MemorySection, IWiiDiscSection
    {
        private const int _PartitionTableOffset = 0x40000;
        private const int _PartitionTableLength = 0x100;

        private List<WiiPartitionInfo> _partitions;

        public string Id { get { return Encoding.ASCII.GetString(Data, 0, 4); } }
        public string Id6 { get { return Encoding.ASCII.GetString(Data, 0, 6); } }
        public string Id8 { get { return string.Concat(this.Id6, Data[6].ToString("X2"), Data[7].ToString("X2")); } }

        public int Version { get { return (int)Data[7]; } }

        public int DiscNo { get { return (int)Data[6]; } }

        public string Title { get; private set; }
        public bool HasUpdatePartition { get; private set; }
        public WiiPartitionInfo[] Partitions { get { return _partitions.ToArray(); } }

        public long[] IsoDecPartitions { get; internal set; }

        public bool IsIsoDecPartition(long discOffset)
        {
            if (IsoDecPartitions == null)
                return false;
            return IsoDecPartitions.Contains(discOffset);
        }

        public static long PartitionTableOffset { get { return _PartitionTableOffset; } }
        public static long PartitionTableLength { get { return _PartitionTableLength; } }

        internal WiiDiscHeaderSection(MemorySection header) : base(header.Data)
        {
            List<WiiPartitionInfo> partitions = new List<WiiPartitionInfo>();

            this.Title = this.ReadStringToNull(0x20, 0x60);

            _partitions = CreatePartitionInfos(header, _PartitionTableOffset).ToList();
            this.HasUpdatePartition = _partitions[0].Type == PartitionType.Update;
        }

        internal static IEnumerable<WiiPartitionInfo> CreatePartitionInfos(MemorySection section, int offset)
        {
            for (int tableIdx = 0; tableIdx < 4; tableIdx++) //up to 4 partitions on the disk
            {
                uint c = section.ReadUInt32B(offset + (tableIdx * 8)); //count of partitions for tableIdx

                //_log.Message(string.Format("Table {0} - Partitions {1}", tableIdx.ToString(), c.ToString("X8")), 2);
                if (c == 0)
                    continue;
                int tableOffset = (int)section.ReadUInt32B(offset + (tableIdx * 8) + 4) * 4; //first partition entry for tableIdx
                int adjustReadOffset = offset + (tableOffset - _PartitionTableOffset);
                for (int i = 0; i < c; i++)
                {
                    long partitionOffset = section.ReadUInt32B(adjustReadOffset + (i * 8)) * 4L;
                    PartitionType partitionType = (PartitionType)section.ReadUInt32B(adjustReadOffset + (i * 8) + 4);
                    //_log.Message(string.Format("  PartitionOffset Offset {0} - Type {1}", partitionOffset.ToString("X8"), partitionType.ToString()), 2);
                    yield return new WiiPartitionInfo(partitionType, partitionOffset, tableIdx, tableOffset + (i * 8));
                    //_log.Message(string.Format("    ID {0}", partitions.Last().ReadStream.Id), 2);
                }
            }
        }

        internal void RemoveUpdatePartition(long baseAddress)
        {
            if (this.Partitions.Length == 0 || this.Partitions[0].Type != PartitionType.Update)
                return;

            _partitions.RemoveAt(0);

            Array.Clear(this.Data, 0x60, 2);
            Array.Clear(this.Data, _PartitionTableOffset, _PartitionTableLength);

            //write the partition info
            MemorySection hdrStream = new MemorySection(this.Data);

            WiiPartitionInfo firstNonUpdate = _partitions.FirstOrDefault(a => a.Type != PartitionType.Update);

            foreach (IGrouping<int, WiiPartitionInfo> grp in _partitions.GroupBy(a => a.Table))
            {
                int offset = (int)(_PartitionTableOffset + 0x20 + (grp.Key * 0x20L));

                hdrStream.WriteUInt32B((int)(_PartitionTableOffset + (grp.Key * 0x8L)), (uint)grp.Count());
                hdrStream.WriteUInt32B((int)(_PartitionTableOffset + (grp.Key * 0x8L) + 4), (uint)(offset / 4));

                offset -= 4; //adjust for the first calc
                foreach (WiiPartitionInfo part in grp)
                {
                    hdrStream.WriteUInt32B(offset += 4, (uint)(part.DiscOffset / 4L));
                    part.TableOffset = offset;
                    hdrStream.WriteUInt32B(offset += 4, (uint)(part.Type));
                }
            }
        }

        public void AddPartitionPlaceHolder(WiiPartitionPlaceHolder partition)
        {
            _partitions.Add(partition);
            UpdateRepair();
        }

        public void RemovePartitionChannels()
        {
            _partitions.RemoveAll(a => a.Type != PartitionType.Update && a.Type != PartitionType.Data && a.Type != PartitionType.GameData);
            UpdateRepair();
        }


        private long offsetSortFix(WiiPartitionInfo p)
        {
            if (p.DiscOffset == 0 && p is WiiPartitionPlaceHolder)
                return 0xF800000 + (long)((WiiPartitionPlaceHolder)p).Type;
            return p.DiscOffset;
        }

        public void UpdateOffsets()
        {
            MemorySection hdrStream = new MemorySection(this.Data);

            foreach (IGrouping<int, WiiPartitionInfo> grp in _partitions.GroupBy(a => a.Table))
            {
                foreach (WiiPartitionInfo part in grp)
                    hdrStream.WriteUInt32B((int)part.TableOffset, (uint)(part.DiscOffset / 4L));
            }
        }

        public void UpdateRepair()
        {
            try
            {
                _partitions.Sort((a, b) => offsetSortFix(a) < offsetSortFix(b) ? -1 : (offsetSortFix(a) > offsetSortFix(b) ? 1 : 0));

                Array.Clear(this.Data, 0x60, 2);
                Array.Clear(this.Data, _PartitionTableOffset, _PartitionTableLength);

                //write the partition info
                MemorySection hdrStream = new MemorySection(this.Data);

                long partOffsetFix = 0;
                WiiPartitionInfo firstNonUpdate = _partitions.FirstOrDefault(a => a.Type != PartitionType.Update);
                if (firstNonUpdate != null && firstNonUpdate.DiscOffset < 0xF800000)
                {
                    partOffsetFix = 0xF800000 - firstNonUpdate.DiscOffset;
                    foreach (WiiPartitionInfo pi in _partitions.Where(a => a.Type != PartitionType.Update))
                        pi.DiscOffset += partOffsetFix;
                }

                foreach (IGrouping<int, WiiPartitionInfo> grp in _partitions.GroupBy(a => a.Table))
                {
                    int offset = (int)(0x40020 + (grp.Key * 0x20L));

                    hdrStream.WriteUInt32B((int)(_PartitionTableOffset + (grp.Key * 0x8L)), (uint)grp.Count());
                    hdrStream.WriteUInt32B((int)(_PartitionTableOffset + (grp.Key * 0x8L) + 4), (uint)(offset / 4));

                    offset -= 4; //adjust for the first calc
                    foreach (WiiPartitionInfo part in grp)
                    {
                        hdrStream.WriteUInt32B(offset += 4, (uint)(part.DiscOffset / 4L));
                        part.TableOffset = offset;
                        hdrStream.WriteUInt32B(offset += 4, (uint)(part.Type));
                    }
                }
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "WiiDiscHeaderSection.Update");
            }
        }

    }
}
