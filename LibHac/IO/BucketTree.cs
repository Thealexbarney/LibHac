using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LibHac.IO
{
    public class BucketTree
    {
        private const int BucketAlignment = 0x4000;
        public BucketTreeHeader Header { get; }

        public BucketTree(Storage header, Storage data)
        {
            Header = new BucketTreeHeader(header);
        }
    }

    public class BucketTreeHeader
    {
        public string Magic;
        public int Version;
        public int NumEntries;
        public int Field1C;

        public BucketTreeHeader(Storage stream)
        {
            var reader = new BinaryReader(stream.AsStream());

            Magic = reader.ReadAscii(4);
            Version = reader.ReadInt32();
            NumEntries = reader.ReadInt32();
            Field1C = reader.ReadInt32();
        }
    }
}
