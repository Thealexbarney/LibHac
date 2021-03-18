using System.Runtime.InteropServices;

namespace LibHac.Bcat.Impl.Service.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 0x80)]
    internal struct DeliveryCacheFileMetaEntry
    {
        [FieldOffset(0x00)] public FileName Name;
        [FieldOffset(0x20)] public long Id;
        [FieldOffset(0x28)] public long Size;
        [FieldOffset(0x30)] public Digest Digest;
    }
}
