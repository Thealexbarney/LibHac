using System.IO;

namespace LibHac.Npdm
{
    public class FsAccessControl
    {
        public int Version { get; }
        public ulong PermissionsBitmask { get; }
        public int Unknown1 { get; }
        public int Unknown2 { get; }
        public int Unknown3 { get; }
        public int Unknown4 { get; }

        public FsAccessControl(Stream stream, int offset)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var reader = new BinaryReader(stream);

            Version = reader.ReadInt32();
            PermissionsBitmask = reader.ReadUInt64();
            Unknown1 = reader.ReadInt32();
            Unknown2 = reader.ReadInt32();
            Unknown3 = reader.ReadInt32();
            Unknown4 = reader.ReadInt32();
        }
    }
}
