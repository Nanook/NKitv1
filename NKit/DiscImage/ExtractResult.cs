using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{

    public enum ExtractedFileType { WiiDiscItem, System, File }

    public class ExtractedFile
    {
        internal ExtractedFile(DiscType discType, string discId8, string partitionId, long offset, long length, string path, string name, ExtractedFileType type)
        {
            DiscType = discType;
            DiscId8 = discId8;
            PartitionId = partitionId;
            Offset = offset;
            Length = length;
            Path = path;
            Name = name;
            Type = type;
        }

        public DiscType DiscType { get; }
        public string DiscId8 { get; }
        public string PartitionId { get; }
        public long Offset { get; }
        public long Length { get; }
        public string Path { get; }
        public string Name { get; }
        public ExtractedFileType Type { get; }
    }

    public class ExtractResult
    {
        public DiscType DiscType { get; internal set; }
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public Region Region { get; internal set; }
        public ExtractRecoveryResult[] Recovery { get; internal set; }
    }

    public class ExtractRecoveryResult
    {
        public PartitionType Type { get; internal set; }
        public string FileName { get; internal set; }
        public bool Extracted { get; internal set; }
        public bool IsNew { get; internal set; }
        public bool IsGameCube { get; internal set; }
    }
}
