using LibHac.Common.FixedArrays;
using LibHac.Ncm;

namespace LibHac.Fs;

public struct ProgramIndexMapInfo
{
    public ProgramId ProgramId;
    public Ncm.ApplicationId MainProgramId;
    public byte ProgramIndex;
    public Array15<byte> Reserved;
}