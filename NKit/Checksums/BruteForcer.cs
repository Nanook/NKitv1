using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal class BruteForceCrcResult
    {
        public bool HeaderChanged { get; set; }
        public byte[] Header { get; set; }
        public uint HeaderCrc { get; set; }
        public uint MatchedCrc { get; set; }
        public bool MatchedCrcIsRedump { get; set; }
        public bool UpdateChanged { get; set; }
        public uint UpdateCrc { get; set; }
        public bool RegionChanged { get; set; }
        public int Region { get; set; }
        public int OriginalRegion { get; set; }

        public byte[] RegionData { get; set; }
    }

    internal class HeaderBruteForcer
    {
        //private List<Tuple<uint[], byte[]>> _header; //headers (3 parts - pre region, region, post region)
        //private List<byte[]> _originalRegion = new List<byte[]>();

        private class headerCrc
        {
            public uint Crc;
            public uint CrcPreRegion;
            public uint CrcRegion;
            public uint CrcPostRegion;
            public byte[] Data;
        }

        private class regionCrc
        {
            public uint Crc;
            public headerCrc Header;
            public byte[] RegionData;
            public int Region;
        }


        private uint[] _updateCrcs;
        private SortedList<uint, bool> _checkCrcs;

        private uint[] _origHeaderCrcs;

        private uint _origHeaderCrc;
        private uint _origUpdateCrc;
        private int _origRegion;
        private byte[] _origRegionData;
        private List<headerCrc> _hdrs;
        private List<regionCrc> _regionData;

        public HeaderBruteForcer(uint[] updateCrcs, SortedList<uint, bool> checkCrcs, Tuple<byte[], int[]>[] regionData, params byte[][] headers)
        {
            _updateCrcs = updateCrcs;
            _checkCrcs = checkCrcs;

            //DateTime dt = DateTime.Now;

            _hdrs = headers.Select(a => new headerCrc() { Crc = Crc.Compute(a), CrcPreRegion = Crc.Compute(a, 0, 0x4e000), CrcRegion = Crc.Compute(a, 0x4e000, 0x20), CrcPostRegion = Crc.Compute(a, 0x4e020, 0x1fe0), Data = a }).ToList();

            MemorySection ms = new MemorySection(headers[0]);
            _origRegion = (int)ms.ReadUInt32B(0x4e000);
            _origRegionData = ms.Read(0x4e010, 0x10);

            _origHeaderCrcs = _hdrs.Select(a => a.Crc).ToArray(); //original header Crcs

            for (int h = _hdrs.Count - 1; h >= 1; h--)
            {
                if (_hdrs[h].Crc == _hdrs[h - 1].Crc)
                    _hdrs.RemoveAt(h); //remove duplicate headers
            }

            _regionData = new List<regionCrc>();
            for (int h = 0; h < _hdrs.Count; h++)
            {
                for (int i = 0; i < regionData.Length; i++)
                {
                    for (int r = 0; r < regionData[i].Item2.Length; r++)
                        _regionData.Add(new regionCrc() { Crc = 0, Header = _hdrs[h], RegionData = regionData[i].Item1, Region = (int)regionData[i].Item2[r] });
                }
            }


            //parallel precalculate the crcs for the matching
            Parallel.ForEach(_regionData, rgn =>
            {
                MemorySection rg = new MemorySection(new byte[0x20]);
                rg.WriteUInt32B(0, (uint)rgn.Region);
                rg.Write(0x10, rgn.RegionData);
                rgn.Crc = ~Crc.Combine(Crc.Combine(~rgn.Header.CrcPreRegion, ~Crc.Compute(rg.Data), 0x20), ~rgn.Header.CrcPostRegion, 0x1fe0);
            });

            //Trace.WriteLine(string.Format("Crc Headers: {0} - Took: {1}", _regionData.Count.ToString(), (DateTime.Now - dt).TotalMilliseconds.ToString()));
        }

        public BruteForceCrcResult Match(CrcItem[] crcs)
        {
            //reduce crcs to 3 - header, update, postUpdate

            _origHeaderCrc = crcs[0].Value;
            _origUpdateCrc = crcs[1].Value;
            long updateSize = (crcs.Length > 2 ? crcs[2].Offset - crcs[1].Offset : 0);
            uint postUpdateCrc = (crcs.Length > 2 ? crcs[2].Value : 0);
            long postUpdateSize = (crcs.Length > 2 ? crcs[2].Length : 0);

            for (int x = 3; x < crcs.Length; x++)
            {
                postUpdateCrc = ~Crc.Combine(~postUpdateCrc, ~crcs[x].Value, crcs[x].Length);
                postUpdateSize += crcs[x].Length;
            }

            //DateTime dt = DateTime.Now;

            //quick test without region brute force (which can take a while)
            headerCrc headerMatch = null;
            regionCrc regionMatch = null;
            uint matchCrc = 0;

            uint matchUpdateCrc = 0;
            uint u = _updateCrcs.AsParallel().FirstOrDefault(uCrc =>
            {
                headerCrc m = _hdrs.AsParallel().FirstOrDefault(hdr =>
                {
                    uint crc = ~Crc.Combine(Crc.Combine(~hdr.Crc, ~uCrc, updateSize), ~postUpdateCrc, postUpdateSize);
                    if (_checkCrcs.ContainsKey(crc))
                    {
                        matchCrc = crc;
                        headerMatch = hdr;
                        matchUpdateCrc = uCrc;
                    }
                    return matchCrc != 0;
                });
                return matchCrc != 0;
            });
            //Trace.WriteLine(string.Format("Crc Update Combos: {0} - Took: {1}", _updateCrcs.Length.ToString(), (DateTime.Now - dt).TotalMilliseconds.ToString()));

            if (headerMatch == null)
            {
                //dt = DateTime.Now;

                //region brute force tests - FirstOrDefault is used and the results are discarded as nesting them causes threading issues
                uint u2 = _updateCrcs.AsParallel().FirstOrDefault(uCrc => 
                {
                    //combine the update crc with the post update crc
                    uint updDataCrc = ~Crc.Combine(~uCrc, ~postUpdateCrc, postUpdateSize);
                    long updDataSize = updateSize + postUpdateSize;

                    //compare the header/region with the update and post update combines crc
                    regionCrc m = _regionData.AsParallel().FirstOrDefault(rgn =>
                    {
                        uint crc = ~Crc.Combine(~rgn.Crc, ~updDataCrc, updDataSize);
                        if (_checkCrcs.ContainsKey(crc))
                        {
                            matchCrc = crc;
                            regionMatch = rgn;
                            matchUpdateCrc = uCrc;
                        }
                        return matchCrc != 0;
                    });

                    return matchCrc != 0;
                });
                //Trace.WriteLine(string.Format("Crc Header / Update Combos: {0} - Took: {1}", (_updateCrcs.Length * _regionData.Count).ToString(), (DateTime.Now - dt).TotalMilliseconds.ToString()));
            }

            return results(headerMatch, regionMatch, matchUpdateCrc, matchCrc, matchCrc == 0 ? false : _checkCrcs[matchCrc]);

        }

        private BruteForceCrcResult results(headerCrc matchHdr, regionCrc matchRgn, uint updateCrc, uint matchCrc, bool isRedump)
        {
            if (matchHdr == null && matchRgn == null)
                return new BruteForceCrcResult() { Region = _origRegion };

            //create the header
            byte[] header = (matchHdr ?? matchRgn.Header).Data;
            bool regionDataChanged = false;
            if (matchRgn != null)
            {
                MemorySection ms = new MemorySection(header);
                regionDataChanged = !matchRgn.Region.Equals(_origRegionData);
                ms.WriteUInt32B(0x4e000, (uint)matchRgn.Region);
                ms.Write(0x4e010, matchRgn.RegionData);
            }
            uint headerCrc = Crc.Compute(header);
            if (headerCrc == _origHeaderCrc)
                header = null;

            return new BruteForceCrcResult()
            {
                MatchedCrc = matchCrc,
                MatchedCrcIsRedump = isRedump,

                HeaderChanged = header != null && _origHeaderCrc != headerCrc,
                Header = header,
                HeaderCrc = Crc.Compute(header),

                UpdateChanged = updateCrc != 0 && _origUpdateCrc != updateCrc,
                UpdateCrc = updateCrc,

                Region = matchRgn?.Region ?? _origRegion,
                OriginalRegion = _origRegion,
                RegionData = matchRgn?.RegionData,
                RegionChanged = matchRgn != null && (regionDataChanged || matchRgn.Region != _origRegion)
            };
        }

    }
}
