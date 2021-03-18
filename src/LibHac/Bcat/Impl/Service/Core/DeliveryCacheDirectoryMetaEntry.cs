using System.Runtime.InteropServices;

namespace LibHac.Bcat.Impl.Service.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    internal struct DeliveryCacheDirectoryMetaEntry
    {
        [FieldOffset(0x00)] public DirectoryName Name;
        [FieldOffset(0x20)] public Digest Digest;
    }
}
