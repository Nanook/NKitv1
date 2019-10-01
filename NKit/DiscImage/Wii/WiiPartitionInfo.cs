using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public enum PartitionType { Data, Update, Channel, GameData, Other };
    internal class WiiPartitionInfo
    {

        internal WiiPartitionInfo(PartitionType type, long offset, int table, long tablePos)
        {
            DiscOffset = SrcDiscOffset = offset;
            Table = table;
            Type = type;
            TableOffset = tablePos;
        }

        public PartitionType Type { get; private set; }
        public long DiscOffset { get; internal set; }
        internal long SrcDiscOffset { get; set; }
        internal int Table { get; set; }
        internal long TableOffset { get; set; }

    }

}
