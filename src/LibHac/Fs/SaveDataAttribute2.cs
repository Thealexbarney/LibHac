using System.Runtime.InteropServices;
using LibHac.Fs.Save;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    public struct SaveDataAttribute2
    {
        [FieldOffset(0x00)] public ulong TitleId;
        [FieldOffset(0x08)] public UserId UserId;
        [FieldOffset(0x18)] public ulong SaveDataId;
        [FieldOffset(0x20)] public SaveDataType Type;
        [FieldOffset(0x21)] public byte Rank;
        [FieldOffset(0x22)] public short Index;
    }
}
