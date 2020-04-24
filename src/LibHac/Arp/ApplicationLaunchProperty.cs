using System.Runtime.InteropServices;

namespace LibHac.Arp
{
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct ApplicationLaunchProperty
    {
        [FieldOffset(0x0)] public ApplicationId ApplicationId;
        [FieldOffset(0x8)] public uint Version;
        [FieldOffset(0xC)] public Ncm.StorageId BaseStorageId;
        [FieldOffset(0xD)] public Ncm.StorageId UpdateStorageId;
    }
}
