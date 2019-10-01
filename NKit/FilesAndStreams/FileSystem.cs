using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    public class FstFolder
    {
        public FstFolder(FstFolder parent)
        {
            this.Folders = new List<FstFolder>();
            this.Files = new List<FstFile>();
            this.Parent = parent;
        }
        public FstFolder Parent { get; private set; }
        public List<FstFolder> Folders { get; private set; }
        public List<FstFile> Files { get; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name ?? "";
        }
    }

    internal class ConvertFile
    {
        private bool _isGc;

        /// <summary>
        /// forconverting from nkit
        /// </summary>
        public ConvertFile(bool isGc)
        {
            _isGc = isGc;
            Alignment = -1; //default
        }

        /// <summary>
        /// For converting to nkit
        /// </summary>
        public ConvertFile(long gapLength, bool isGc) : this(isGc)
        {
            GapLength = gapLength;
            Gap = new Gap(gapLength, _isGc);
        }
        public FstFile FstFile { get; set; }
        public long NewSize { get { return IsJunk ? FstFile.Length % 4 : FstFile.Length; } }
        public Gap Gap { get; internal set; }
        public long GapLength { get; internal set; }
        public bool HasGap { get { return GapLength != 0; } }
        public long Alignment { get; set; }
        public bool IsJunk { get; set; }

        public override string ToString()
        {
            return string.Format("{0} : {1} : {2}", FstFile.ToString(), GapLength.ToString("X8"), Alignment.ToString());
        }
    }

    public class FstFile
    {
        internal FstFile(FstFolder parent)
        {
            this.Parent = parent;
        }
        public FstFile Clone()
        {
            return new FstFile(this.Parent) { PartitionId = this.PartitionId, DataOffset = this.DataOffset, Length = this.Length, Name = this.Name, Offset = this.Offset, IsNonFstFile = this.IsNonFstFile, OffsetInFstFile = this.OffsetInFstFile };
        }
        public string PartitionId { get; internal set; }
        internal FstFolder Parent { get; private set; }
        public string Name { get; internal set; }
        public long DataOffset { get; internal set; }
        internal long Offset { get; set; }
        public long Length { get; internal set; }
        public bool IsNonFstFile { get; internal set; }
        internal int OffsetInFstFile { get; set; }
        public string Path
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                FstFolder f = this.Parent;
                while (f != null)
                {
                    if (sb.Length != 0)
                        sb.Insert(0, "/");
                    sb.Insert(0, f.Name);
                        f = f.Parent;
                }
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return string.Format("{0} : {1} : {2} : {3}", OffsetInFstFile.ToString("X8"), DataOffset.ToString("X8"), Length.ToString("X8"), Name);
        }
    }

    public class FileSystem
    {
        private FileSystem(FstFolder root)
        {
            this.Root = root;
        }

        public FstFile[] Files { get { return recurseFolders(this.Root, new List<FstFile>()).OrderBy(a => a.Offset).ToArray(); } }

        private List<FstFile> recurseFolders(FstFolder folder, List<FstFile> files)
        {
            files.AddRange(folder.Files);

            foreach (FstFolder fl in folder.Folders)
                recurseFolders(fl, files);
            return files;
        }


        public FstFolder Root { get; private set; }

        public static FileSystem Parse(byte[] fstData, long fstOffset, string id, bool isGc)
        {
            MemorySection ms = new MemorySection(fstData);
            FstFile ff = new FstFile(null) { Name = "fst.bin", DataOffset = fstOffset, Offset = NStream.DataToOffset(fstOffset, !isGc), IsNonFstFile = true, Length = (int)fstData.Length };
            return Parse(ms, ff, id, isGc);
        }

        public static FileSystem Parse(Stream fstData, long fstOffset, long length, string id, bool isGc)
        {
            MemorySection ms = MemorySection.Read(fstData, length);
            FstFile ff = new FstFile(null) { Name = "fst.bin", DataOffset = fstOffset, Offset = NStream.DataToOffset(fstOffset, !isGc), IsNonFstFile = true, Length = (int)fstData.Length };
            return Parse(ms, ff, id, isGc);
        }

        internal static FileSystem Parse(MemorySection ms, FstFile fst, string id)
        {
            return Parse(ms, fst, id, false);
        }

        internal static FileSystem Parse(MemorySection ms, FstFile fst, string id, bool isGc)
        {
            FstFolder fld = new FstFolder(null);

            long nFiles = ms.ReadUInt32B(0x8);
            if (12 * nFiles > ms.Size)
                return null;

            if (fst != null)
                fld.Files.Add(fst);
            recurseFst(ms, fld, 12 * nFiles, 0, id, isGc);
            return new FileSystem(fld);
        }

        private static uint recurseFst(MemorySection ms, FstFolder folder, long names, uint i, string id, bool isGc)
        {
            uint j;
            uint hdr = ms.ReadUInt32B((int)(12 * i));
            long name = names + hdr & 0x00ffffffL;
            int type = (int)(hdr >> 24);
            string nm = ms.ReadStringToNull((int)name);
            uint size = ms.ReadUInt32B((int)(12 * i + 8));

            if (type == 1)
            {
                FstFolder f = i == 0 ? folder : new FstFolder(folder) { Name = nm };
                if (i != 0)
                    folder.Folders.Add(f);
                for (j = i + 1; j < size;)
                    j = recurseFst(ms, f, names, j, id, isGc);
                return size;
            }
            else
            {
                int pos = (int)(12 * i + 4);
                long doff = ms.ReadUInt32B(pos) * (isGc ? 1L : 4L); //offset in data
                size = ms.ReadUInt32B((int)(12 * i + 8));
                long off = NStream.DataToOffset(doff, !isGc); //offset in raw partition
                folder.Files.Add(new FstFile(folder) { DataOffset = doff, Offset = off, Length = size, Name = nm, PartitionId = id, OffsetInFstFile = pos });
                return i + 1;
            }
        }

    }
}
