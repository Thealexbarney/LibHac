using System;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSystem;

public struct RuntimeKeySourceInfo
{
    public uint Offset;
    public uint Size;
    public Hash Hash;
}

public struct RuntimeNcaHeader
{
    public struct FsInfo
    {
        public uint StartSector;
        public uint EndSector;
        public uint HashSectors;
        public Hash Hash;
    }

    public struct SignInfo
    {
        public uint Offset;
        public uint Size;
        public Hash Hash;
    }

    public NcaHeader.DistributionType DistributionType;
    public NcaHeader.ContentType ContentType;
    public byte KeyGeneration;
    public ulong ProgramId;
    public Array16<byte> RightsId;
    public uint FsHeadersOffset;
    public Array4<FsInfo> FsInfos;
    public SignInfo Header2SignInfo;
    public RuntimeKeySourceInfo KeySourceInfo;

    public static Result CheckUnsupportedVersion(uint magicValue)
    {
        throw new NotImplementedException();
    }

    public Result InitializeCommonForV3(in NcaHeader header, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        throw new NotImplementedException();
    }
}