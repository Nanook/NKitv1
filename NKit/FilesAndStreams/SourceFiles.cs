using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nanook.NKit
{

    public static class SourceFiles
    {
        public static SourceFile OpenFile(string filePath)
        {
            return new SourceFile()
            {
                Name = Path.GetFileName(filePath),
                Path = Path.GetDirectoryName(filePath),
                FilePath = filePath,
                AllFiles = new[] { filePath },
                IsSplit = false,
                Length = new FileInfo(filePath).Length
            };
        }

        public static SourceFile[] Scan(string[] masks, bool scanSubfolders)
        {
            List<string> files = new List<string>();

            foreach (string fn in masks)
            {
                string f = fn;
                try
                {
                    if (f.EndsWith("\"") && !f.StartsWith("\"")) //weird scenario if param ends with \ e.g. "c:\test\"  the last " is preserved
                        f = f.Substring(0, f.Length - 1);

                    string mask;
                    string path;
                    if (!f.Contains("*") && !f.Contains("?") && File.GetAttributes(f).HasFlag(FileAttributes.Directory))
                    {
                        path = f;
                        mask = "*.*";
                    }
                    else
                    {
                        path = Path.GetDirectoryName(f);
                        mask = Path.GetFileName(f);
                    }

                    if (string.IsNullOrEmpty(path))
                        path = Environment.CurrentDirectory;

                    addFiles(files, new DirectoryInfo(path), mask, scanSubfolders);
                }
                catch (Exception ex)
                {
                    throw new HandledException(ex, "SourceFiles.Scan failed scanning '{0}'", f ?? "");
                }
            }
            return buildCollection(files).ToArray();

        }

        public static string PathFix(string path)
        {
            try
            {
                return string.Join(Path.DirectorySeparatorChar.ToString(), Regex.Split(path, @"[\\/]")); // @"(?<!:|^|\\|/)(?!\\|/)"));
            }
            catch { }
            return path;
        }

        public static string ExtensionString(bool isIsoDec, bool isWbfs, bool isNkit, bool isGcz)
        {
            string f;

            if (isIsoDec)
                f = "ISO.Dec";
            else if (isWbfs)
                f = "WBFS";
            else
            {
                if (isNkit)
                {
                    if (isGcz)
                        f = "NKit.GCZ";
                    else
                        f = "NKit.ISO";
                }
                else
                {
                    if (isGcz)
                        f = "GCZ";
                    else
                        f = "ISO";
                }
            }
            return f;
        }


        public static string RemoveExtension(string filename, bool filenameOnly, out string extension)
        {
            if (filenameOnly)
                filename = Path.GetFileName(filename);

            Match m = Regex.Match(filename, @"\.(nkit\.gcz|nkit\.iso|iso\.dec|part[0-9]\.rar|zip\.[0-9]+)(:?_[0-9]*)?$", RegexOptions.IgnoreCase);
            if (m.Success)
                extension = m.Value;
            else
                extension = Path.GetExtension(filename);

            return filename.Substring(0, filename.Length - extension.Length);
        }

        public static string RemoveExtension(string filename, bool filenameOnly)
        {
            string ext;
            return RemoveExtension(filename, filenameOnly, out ext);
        }

        public static string ChangeExtension(string filename, bool filenameOnly, string newExtension)
        {
            string ext;
            return string.Format("{0}.{1}", RemoveExtension(filename, filenameOnly, out ext), newExtension.TrimStart('.'));
        }

        public static string GetUniqueName(string fullName)
        {
            string pth = Path.GetDirectoryName(fullName);

            string tmp = fullName;
            int i = 1;
            while (File.Exists(fullName))
                fullName = tmp + "_" + (i++).ToString();
            return fullName;
        }

        private static void addFiles(List<string> files, DirectoryInfo d, string mask, bool recurse)
        {
            foreach (FileInfo file in d.GetFiles(mask))
            {
                if (files.Any(a => a.ToLower() == file.FullName.ToLower()))
                    return;
                files.Add(file.FullName);
            }

            if (recurse && (mask.Contains("*") || mask.Contains("?")))
            {
                foreach (DirectoryInfo di in d.GetDirectories())
                    addFiles(files, di, mask, recurse);
            }
        }

        private static IEnumerable<SourceFile> buildCollection(List<string> files)
        {
            List<SourceFile> srcFiles = new List<SourceFile>();
            long length;

            List<string> firstParts = new List<string>();
            firstParts = files.Where(a => Regex.IsMatch(a, @"\.(part0*1\.rar|z01|001|r00|wbf1)$")).ToList();

            //get multi file sets
            foreach (string s in firstParts)
            {
                length = 0;
                try
                {
                    Match m = Regex.Match(s.ToLower(), @"^(.*\.)(part0*1\.rar|z01|001|r00|wbf1)$");
                    if (m.Success)
                    {
                        string firstName = Regex.Replace(s, @".r00$", ".rar", RegexOptions.IgnoreCase);
                        firstName = Regex.Replace(firstName, @".wbf1$", ".wbfs", RegexOptions.IgnoreCase);
                        List<string> parts = files.Where(a => string.Compare(firstName, a, true) != 0 && a.Length == s.Length && a.ToLower().StartsWith(m.Groups[1].Value)).OrderBy(a => a).ToList();
                        if (parts.Count != 0)
                            parts.Insert(0, firstName);
                        SourceFile sf = new SourceFile()
                        {
                            Name = Path.GetFileName(firstName),
                            Path = Path.GetDirectoryName(firstName),
                            FilePath = firstName,
                            AllFiles = parts.ToArray(),
                            IsSplit = firstName.EndsWith(".001") || firstName.ToLower().EndsWith("wbfs")
                        };
                        srcFiles.Add(sf);
                        files.RemoveAll(a => sf.AllFiles.Contains(a)); //relies on ToLower used above
                        foreach (FileInfo fi in sf.AllFiles.Select(a => new FileInfo(a)))
                            length += fi.Length;
                        sf.Length = length;
                    }
                }
                catch (Exception ex)
                {
                    throw new HandledException(ex, "SourceFiles.buildCollection failed on '{0}' getting multi file sets", s ?? "");
                }
            }

            //get single file sets
            foreach (string fn in files.Where(a => Regex.IsMatch(Path.GetExtension(a), @"\.(gcz|gcm|iso|dec|wbfs|zip|rar|7z|gz|z)(:?_[0-9]*)?$", RegexOptions.IgnoreCase)))
            {
                try
                {
                    SourceFile sf = new SourceFile()
                    {
                        Name = Path.GetFileName(fn),
                        Path = Path.GetDirectoryName(fn),
                        FilePath = fn,
                        AllFiles = new string[0],
                        IsSplit = false,
                        Length = (new FileInfo(fn)).Length
                    };
                    srcFiles.Add(sf);
                }
                catch (Exception ex)
                {
                    throw new HandledException(ex, "SourceFiles.buildCollection failed on '{0}' getting single file info", fn ?? "");
                }
            }

            //get files from archives
            for (int i = srcFiles.Count - 1; i >= 0; i--)
            {
                try
                {
                    string[] arcs = GetArchiveFiles(srcFiles[i]);
                    if (arcs == null) 
                        continue; //not archive, ignore
                    else if (arcs.Length == 0) //archive with no valid files
                        srcFiles.RemoveAt(i);
                    else
                    {
                        for (int j = 0; j < arcs.Length; j++)
                        {
                            if (j == 0)
                            {
                                srcFiles[i].IsArchive = true;
                                srcFiles[i].Name = Path.GetFileName(arcs[j]);
                                srcFiles[i].Path = Path.GetDirectoryName(arcs[j]);
                            }
                            else
                            {
                                SourceFile sf = new SourceFile()
                                {
                                    IsArchive = srcFiles[i].IsArchive,
                                    Name = Path.GetFileName(arcs[j]),
                                    Path = Path.GetDirectoryName(arcs[j]),
                                    FilePath = srcFiles[i].FilePath,
                                    AllFiles = srcFiles[i].AllFiles,
                                    IsSplit = srcFiles[i].IsSplit,
                                    Length = srcFiles[i].Length
                                };
                                srcFiles.Add(sf);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new HandledException(ex, "SourceFiles.buildCollection failed on '{0}' listing files from archive", (srcFiles[i]?.FilePath) ?? "");
                }
            }

            int idx = 0;
            foreach (SourceFile f in srcFiles.OrderBy(a => a.Name))
            {
                f.Index = idx++;
                f.TotalFiles = srcFiles.Count;
                yield return f;
            }
        }

        public static string[] GetArchiveFiles(SourceFile file)
        {
            List<string> results = new List<string>();
            string path = file.FilePath;
            using (Stream stream = file.OpenStream())
            {
                try
                {
                    using (IArchive archive = ArchiveFactory.Open(stream))
                    {
                        if (archive.Type == SharpCompress.Common.ArchiveType.Tar)
                            return null; //default type when not recognised

                        foreach (IArchiveEntry entry in archive.Entries)
                        {
                            if (!entry.IsDirectory)
                            {
                                try
                                {
                                    string ext = Path.GetExtension(entry.Key).ToLower();
                                    if (ext == ".nkit" || ext == ".gcz" || ext == ".gcm" || ext == ".iso" || ext == ".dec" || ext == ".wbfs")
                                        results.Add(entry.Key);
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch
                {
                    return null;
                }
            }
            return results.ToArray();
        }
    }
}
