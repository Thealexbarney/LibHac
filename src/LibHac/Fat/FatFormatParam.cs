using System;

namespace LibHac.Fat
{
    public struct FatFormatParam
    {
        public bool IsSdCard;
        public uint ProtectedAreaSectors;
        public Result WriteVerifyErrorResult;
        public Memory<byte> WorkBuffer;
    }
}
