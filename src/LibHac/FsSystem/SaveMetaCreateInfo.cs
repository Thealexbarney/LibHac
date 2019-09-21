using System.Runtime.InteropServices;

namespace LibHac.FsSystem
{
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct SaveMetaCreateInfo
    {
        [FieldOffset(0)] public int Size;
        [FieldOffset(4)] public SaveMetaType Type;
    }
}
