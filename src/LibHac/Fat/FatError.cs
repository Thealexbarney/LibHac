using LibHac.Common.FixedArrays;

namespace LibHac.Fat;

public struct FatError
{
    public int Error;
    public int ExtraError;
    public int DriveId;
    public Array16<byte> ErrorName;
    public int Reserved;
}