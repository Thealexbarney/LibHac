using System;
using System.IO;

namespace LibHac.Npdm
{
    public class FsAccessHeader
    {
        public int   Version            { get; private set; }
        public ulong PermissionsBitmask { get; private set; }

        public FsAccessHeader(Stream stream, int offset, int size)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            BinaryReader reader = new BinaryReader(stream);

            Version            = reader.ReadInt32();
            PermissionsBitmask = reader.ReadUInt64();

            int dataSize = reader.ReadInt32();

            if (dataSize != 0x1c)
            {
                throw new Exception("FsAccessHeader is corrupted!");
            }

            int ContentOwnerIdSize        = reader.ReadInt32();
            int DataAndContentOwnerIdSize = reader.ReadInt32();

            if (DataAndContentOwnerIdSize != 0x1c)
            {
                throw new NotImplementedException("ContentOwnerId section is not implemented!");
            }
        }
    }
}
