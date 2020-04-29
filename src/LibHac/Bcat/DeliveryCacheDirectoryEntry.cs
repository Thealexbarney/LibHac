using System.Runtime.InteropServices;

namespace LibHac.Bcat
{
    [StructLayout(LayoutKind.Explicit, Size = 0x38)]
    public struct DeliveryCacheDirectoryEntry
    {
        [FieldOffset(0x00)] public FileName Name;
        [FieldOffset(0x20)] public long Size;
        [FieldOffset(0x28)] public Digest Digest;

        public DeliveryCacheDirectoryEntry(ref FileName name, long size, ref Digest digest)
        {
            Name = name;
            Size = size;
            Digest = digest;
        }
    }
}
