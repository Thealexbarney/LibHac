using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Fat
{
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    public struct FatError
    {
        private const int FunctionNameLength = 0x10;

        [FieldOffset(0x00)] public int Error;
        [FieldOffset(0x04)] public int ExtraError;
        [FieldOffset(0x08)] public int DriveId;
        [FieldOffset(0x0C)] private byte _functionName;

        public U8SpanMutable ErrorName =>
            new U8SpanMutable(SpanHelpers.CreateSpan(ref _functionName, FunctionNameLength));
    }
}
