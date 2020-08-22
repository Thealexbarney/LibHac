using System.Runtime.InteropServices;
using LibHac.Ncm;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    public struct ProgramIndexMapInfo
    {
        [FieldOffset(0x00)] public ProgramId ProgramId;
        [FieldOffset(0x08)] public ProgramId MainProgramId;
        [FieldOffset(0x10)] public byte ProgramIndex;
    }
}
