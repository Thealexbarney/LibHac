using LibHac.Common.FixedArrays;
using LibHac.Ncm;

namespace LibHac.Fs;

public struct ProgramIndexMapInfo
{
    public ProgramId ProgramId;
    public ProgramId MainProgramId;
    public byte ProgramIndex;
    public Array15<byte> Reserved;
}