using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.Swiit.SuperWiiDiscLibrary
{
    public class MemorySection : WiiDiscSection
    {

        public MemorySection(byte[] data) : base(null, 0, data, data.Length)
        {
        }
    }
}
