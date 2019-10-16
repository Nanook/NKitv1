using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Nanook.NKit
{
    public class DatData
    {
        private Regex _gameTdbSplit;

        public List<RedumpEntry> RedumpData { get; private set; }
        public List<Tuple<string, string>> GameTdbData { get; private set; }
        public List<RedumpEntry> CustomData { get; private set; }

        public DatData(Settings settings, ILog log)
        {
            log?.Log("DAT ENTRIES");
            log?.Log("-------------------------------------------------------------------------------");
            _gameTdbSplit = new Regex("^(.{4,6}) = (.*)$", RegexOptions.Multiline | RegexOptions.Compiled);
            RedumpData = populateRedump(settings.DatPathRedump);
            log?.Log(string.Format("[{0,4} redump ] {1}", RedumpData.Count.ToString(), string.IsNullOrEmpty(settings.DatPathRedump) ? "" : Path.GetFileName(settings.DatPathRedump)));
            CustomData = populateRedump(settings.DatPathCustom);
            log?.Log(string.Format("[{0,4} custom ] {1}", CustomData.Count.ToString(), string.IsNullOrEmpty(settings.DatPathCustom) ? "" : Path.GetFileName(settings.DatPathCustom)));
            GameTdbData = populateGameTdb(settings.DatPathNameGameTdb);

            log?.Log(string.Format("[{0,4} gametdb] {1}", GameTdbData.Count.ToString(), string.IsNullOrEmpty(settings.DatPathNameGameTdb) ? "" : Path.GetFileName(settings.DatPathNameGameTdb)));

            if (settings.DatPathRedump == null || RedumpData.Count == 0)
                log?.Log(string.Format("!! Add a populated redump dat to match {0}", string.IsNullOrEmpty(settings.DatPathRedumpMask) ? "" : settings.DatPathRedumpMask));
            log?.LogBlank();
        }

        public RedumpInfo GetRedumpEntry(Settings settings, DatData dats, uint crc)
        {
            RedumpEntry redump = dats.RedumpData.FirstOrDefault(a => a.Crc == crc);
            RedumpInfo output = new RedumpInfo();
            output.MatchType = MatchType.MatchFail;

            if (redump == null)
            {
                if ((redump = dats.CustomData.FirstOrDefault(a => a.Crc == crc)) != null)
                    output.MatchType = MatchType.Custom;
            }
            else
                output.MatchType = MatchType.Redump;

            if (redump != null)
            {
                output.MatchName = SourceFiles.RemoveExtension(redump.Name, true);
                output.Checksums = new ChecksumsResult() { Crc = crc, Md5 = redump.Md5, Sha1 = redump.Md5 };
            }

            return output;
        }

        private static List<RedumpEntry> populateRedump(string datPath)
        {
            try
            {
                List<RedumpEntry> data = new List<RedumpEntry>();
                if (!string.IsNullOrEmpty(datPath) && File.Exists(datPath))
                {
                    XDocument matchDoc = XDocument.Load(datPath);
                    XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
                    namespaceManager.AddNamespace("empty", "http://demo.com/2011/demo-schema");

                    foreach (XElement e in matchDoc.XPathSelectElements("//rom", namespaceManager))
                        data.Add(new RedumpEntry(e.Attribute("name").Value, uint.Parse(e.Attribute("crc").Value, NumberStyles.HexNumber), e.Attribute("md5").Value.HexToBytes(), e.Attribute("sha1").Value.HexToBytes()));
                }
                return data;
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "DatData.populateRedump");
            }
        }

        private List<Tuple<string, string>> populateGameTdb(string datPath)
        {
            try
            {
                List<Tuple<string, string>> data = new List<Tuple<string, string>>();
                if (!string.IsNullOrEmpty(datPath) && File.Exists(datPath))
                {
                    bool firstLine = true;
                    foreach (string s in File.ReadLines(datPath))
                    {
                        if (firstLine)
                        {
                            firstLine = false;
                            continue;
                        }
                        Match m = _gameTdbSplit.Match(s);
                        if (m.Success)
                            data.Add(new Tuple<string, string>(m.Groups[1].Value, m.Groups[2].Value));
                    }
                }
                return data;
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "DatData.populateGameTdb");
            }
        }

        public string GetFilename(OutputResults results, string mask)
        {
            try
            {
                string fileName = SourceFiles.CleanseFileName(SourceFiles.RemoveExtension(results.InputFileName, true));
                string titleName = SourceFiles.CleanseFileName(results.OutputTitle) ?? fileName;
                string tgdbName = SourceFiles.CleanseFileName(GameTdbData.FirstOrDefault(a => a.Item1 == results.OutputId6)?.Item2) ?? titleName;
                string matchName = results.RedumpInfo?.MatchName != null ? SourceFiles.CleanseFileName(results.RedumpInfo.MatchName) : (tgdbName ?? fileName);

                Dictionary<string, string> values = new Dictionary<string, string>();
                values.Add("%src", results.InputFileName ?? Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
                values.Add("%nmo", fileName);
                values.Add("%nmg", tgdbName);
                values.Add("%nmd", titleName);
                values.Add("%nmm", matchName);
                values.Add("%dno", results.OutputDiscNo == 0 ? "" : string.Format("(Disc {0})", results.OutputDiscNo.ToString()));
                values.Add("%ver", results.OutputDiscVersion == 0 ? "" : string.Format("(v1.{0})", results.OutputDiscVersion.ToString("D2")));
                values.Add("%rev", results.OutputDiscVersion == 0 ? "" : string.Format("(Rev {0})", results.OutputDiscVersion.ToString("D2")));
                values.Add("%crc", results.OutputCrc.ToString("X8"));
                values.Add("%md5", results.OutputMd5 == null ? "" : BitConverter.ToString(results.OutputMd5).Replace("-", ""));
                values.Add("%sha", results.OutputSha1 == null ? "" : BitConverter.ToString(results.OutputSha1).Replace("-", ""));
                values.Add("%id4", SourceFiles.CleanseFileName(results.OutputId4).PadRight(4));
                values.Add("%id6", SourceFiles.CleanseFileName(results.OutputId6).PadRight(6));
                values.Add("%id8", SourceFiles.CleanseFileName(results.OutputId8).PadRight(8));

                string[] braces = new[] { "%crc", "%md5", "%sha", "%id4", "%id6", "%id8" };

                string fn = mask;
                foreach (var v in values)
                {
                    if (fn.Contains(v.Key))
                    {
                        if (string.IsNullOrEmpty(v.Value) || Regex.IsMatch(fn, Regex.Escape(v.Value), RegexOptions.IgnoreCase))
                            fn = Regex.Replace(fn, string.Format(@"[ ._-]?{0}", v.Key), "", RegexOptions.IgnoreCase);
                        else
                            fn = fn.Replace(v.Key, string.Concat(braces.Contains(v.Key) ? "[" : "", v.Value, braces.Contains(v.Key) ? "]" : ""));
                    }
                }
                return SourceFiles.GetUniqueName(fn.Replace("%ext", results.OutputFileExt.Trim('.')));
            }
            catch (Exception ex)
            {
                throw new HandledException(ex, "DatData.GetFilename - Replace masks");
            }
        }

        public void AddRedumpEntry(string datFullFilename, string filename, long size, uint crc, byte[] sha1, byte[] md5)
        {
            string ne = Path.GetFileName(filename);
            string n = SourceFiles.RemoveExtension(filename, true);

            if (!File.Exists(datFullFilename))
            {
                string xml = string.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE datafile PUBLIC ""-//Logiqx//DTD ROM Management Datafile//EN"" ""http://www.logiqx.com/Dats/datafile.dtd""[]>
<datafile>
  <header>
    <name>Nintendo - Non-Redump Audit Dat</name>
    <description>Created by NKit</description>
    <category>Games</category>
    <version>1</version>
    <date>{0}</date>
    <author>You</author>
    <email>-not specified-</email>
    <homepage>-not specified-</homepage>
    <url>-not specified-</url>
    <comment>-not specified-</comment>
    <clrmamepro />
  </header>
</datafile>
", DateTime.Now.ToString("yyyyMMdd"));
                Directory.CreateDirectory(Path.GetDirectoryName(datFullFilename));
                File.WriteAllText(datFullFilename, xml);
            }

            if (File.Exists(datFullFilename))
            {
                XDocument matchDoc = XDocument.Load(datFullFilename);
                XmlNamespaceManager namespaceManager = new XmlNamespaceManager(new NameTable());
                namespaceManager.AddNamespace("empty", "http://demo.com/2011/demo-schema");

                XElement machine = new XElement("machine", new XAttribute("name", n));
                matchDoc.Root.Add(machine);
                XElement desc = new XElement("description", n);
                machine.Add(desc);
                XElement rom = new XElement("rom",
                    new XAttribute("name", ne),
                    new XAttribute("size", size.ToString()),
                    new XAttribute("crc", crc.ToString("x8")),
                    new XAttribute("md5", md5 == null ? "00000000000000000000000000000000" : BitConverter.ToString(md5).Replace("-", "")),
                    new XAttribute("sha1", md5 == null ? "0000000000000000000000000000000000000000" : BitConverter.ToString(md5).Replace("-", "")));
                machine.Add(rom);
                matchDoc.Save(datFullFilename);
            }
        }


    }
}
