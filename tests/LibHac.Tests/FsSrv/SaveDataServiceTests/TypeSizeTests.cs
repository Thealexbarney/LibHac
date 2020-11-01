// ReSharper disable InconsistentNaming
using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.FsSrv;
using LibHac.Ncm;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.FsSrv.SaveDataServiceTests
{
    public class TypeSizeTests
    {
        [Fact]
        public static void SaveDataInfoFilterSizeIs0x60()
        {
            Assert.Equal(0x60, Unsafe.SizeOf<SaveDataInfoFilter>());
        }

        [Fact]
        public static void SaveDataInfoFilterLayoutIsCorrect()
        {
            var filter = new SaveDataInfoFilter();

            ref byte baseRef = ref Unsafe.As<SaveDataInfoFilter, byte>(ref filter);

            ref byte spaceIdRef = ref Unsafe.As<Optional<SaveDataSpaceId>, byte>(ref filter.SpaceId);
            ref byte programIdRef = ref Unsafe.As<Optional<ProgramId>, byte>(ref filter.ProgramId);
            ref byte saveTypeRef = ref Unsafe.As<Optional<SaveDataType>, byte>(ref filter.SaveDataType);
            ref byte userIdRef = ref Unsafe.As<Optional<UserId>, byte>(ref filter.UserId);
            ref byte saveIdRef = ref Unsafe.As<Optional<ulong>, byte>(ref filter.SaveDataId);
            ref byte indexRef = ref Unsafe.As<Optional<ushort>, byte>(ref filter.Index);
            ref byte rankRef = ref Unsafe.As<int, byte>(ref filter.Rank);

            Assert.Equal(0x00, (int)Unsafe.ByteOffset(ref baseRef, ref spaceIdRef));
            Assert.Equal(0x08, (int)Unsafe.ByteOffset(ref baseRef, ref programIdRef));
            Assert.Equal(0x18, (int)Unsafe.ByteOffset(ref baseRef, ref saveTypeRef));
            Assert.Equal(0x20, (int)Unsafe.ByteOffset(ref baseRef, ref userIdRef));
            Assert.Equal(0x38, (int)Unsafe.ByteOffset(ref baseRef, ref saveIdRef));
            Assert.Equal(0x48, (int)Unsafe.ByteOffset(ref baseRef, ref indexRef));
            Assert.Equal(0x4C, (int)Unsafe.ByteOffset(ref baseRef, ref rankRef));
        }
    }
}
