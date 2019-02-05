// ReSharper disable UnusedVariable
using System;
using System.IO;

namespace LibHac.Npdm
{
    public class FsAccessHeader
    {
        public int Version { get; }
        public ulong PermissionsBitmask { get; }

        public FsAccessHeader(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            Version = reader.ReadInt32();
            PermissionsBitmask = reader.ReadUInt64();

            int dataSize = reader.ReadInt32();

            if (dataSize != 0x1c)
            {
                throw new Exception("FsAccessHeader is corrupted!");
            }

            int contentOwnerIdSize = reader.ReadInt32();
            int dataAndContentOwnerIdSize = reader.ReadInt32();

            if (dataAndContentOwnerIdSize != 0x1c)
            {
                throw new NotImplementedException("ContentOwnerId section is not implemented!");
            }
        }
    }
}
