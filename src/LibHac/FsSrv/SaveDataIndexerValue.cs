using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSrv;

public struct SaveDataIndexerValue
{
    public ulong SaveDataId;
    public long Size;
    public ulong Field10;
    public SaveDataSpaceId SpaceId;
    public SaveDataState State;
    public Array38<byte> Reserved;
}