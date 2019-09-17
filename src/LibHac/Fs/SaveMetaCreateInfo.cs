using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct SaveMetaCreateInfo
    {
        [FieldOffset(0)] public int Size;
        [FieldOffset(4)] public SaveMetaType Type;
    }
}
