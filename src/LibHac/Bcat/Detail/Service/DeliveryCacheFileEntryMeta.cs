using System.Runtime.InteropServices;

namespace LibHac.Bcat.Detail.Service
{
    [StructLayout(LayoutKind.Explicit, Size = 0x80)]
    internal struct DeliveryCacheFileEntryMeta
    {
        [FieldOffset(0x00)] public FileName Name;
        [FieldOffset(0x20)] public long Size;
        [FieldOffset(0x30)] public Digest Digest;
    }
}
