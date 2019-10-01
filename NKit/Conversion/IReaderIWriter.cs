using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.NKit
{
    internal interface IValidation
    {
        bool VerifyIsWrite { get; set; }
        bool RequireVerifyCrc { get; set; }
        bool RequireValidationCrc { get; set; }
    }
    internal interface IReader : IValidation
    {
        void Construct(ILog log);
        void Read(Context ctx, NStream input, Stream output, Coordinator pc);
    }

    internal interface IWriter : IValidation
    {
        void Construct(ILog log);
        void Write(Context ctx, Stream input, Stream output, Coordinator pc);
    }

    internal interface IProgress
    {
        float Value { get; }
    }

}
