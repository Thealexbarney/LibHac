using System;
using System.Collections.Generic;
using System.Text;

namespace LibHac.IO
{
    public class IndirectStorage : Storage
    {


        public IndirectStorage()
        {

        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            throw new NotImplementedException();
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length { get; }
    }
}
