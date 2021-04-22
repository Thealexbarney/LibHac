using System;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.FsSrv
{
    public class ProgramIndexMapInfoTests
    {
        [Fact]
        public void GetProgramIndexForAccessLog_IsMultiProgram_ReturnsCorrectIndex()
        {
            const int count = 7;

            Horizon hos = HorizonFactory.CreateBasicHorizon();

            var programs = new HorizonClient[count];

            programs[0] = hos.CreateHorizonClient(new ProgramLocation(new ProgramId(1), StorageId.BuiltInSystem),
                AccessControlBits.Bits.RegisterProgramIndexMapInfo);

            for (int i = 1; i < programs.Length; i++)
            {
                programs[i] =
                    hos.CreateHorizonClient(new ProgramLocation(new ProgramId((ulong)(i + 1)), StorageId.BuiltInSystem),
                        AccessControlBits.Bits.None);
            }

            var map = new ProgramIndexMapInfo[count];

            for (int i = 0; i < map.Length; i++)
            {
                map[i].MainProgramId = new ProgramId(1);
                map[i].ProgramId = new ProgramId((ulong)(i + 1));
                map[i].ProgramIndex = (byte)i;
            }

            Assert.Success(programs[0].Fs.RegisterProgramIndexMapInfo(map));

            for (int i = 0; i < programs.Length; i++)
            {
                using ReferenceCountedDisposable<LibHac.FsSrv.Sf.IFileSystemProxy> fsProxy =
                    programs[i].Fs.Impl.GetFileSystemProxyServiceObject();
                Assert.Success(fsProxy.Target.GetProgramIndexForAccessLog(out int programIndex, out int programCount));

                Assert.Equal(i, programIndex);
                Assert.Equal(count, programCount);
            }
        }

        private ProgramIndexMapInfoManager CreatePopulatedManager(int count, Func<int, long> idCreator)
        {
            var manager = new ProgramIndexMapInfoManager();

            var map = new ProgramIndexMapInfo[count];

            for (int i = 0; i < map.Length; i++)
            {
                map[i].MainProgramId = new ProgramId((ulong)idCreator(0));
                map[i].ProgramId = new ProgramId((ulong)idCreator(i));
                map[i].ProgramIndex = (byte)i;
            }

            Assert.Success(manager.Reset(map));

            return manager;
        }

        [Fact]
        public void Get_IdDoesNotExist_ReturnsNull()
        {
            const int count = 5;

            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 3);

            Assert.False(manager.Get(new ProgramId(0)).HasValue);
            Assert.False(manager.Get(new ProgramId(2)).HasValue);
            Assert.False(manager.Get(new ProgramId(8)).HasValue);
            Assert.False(manager.Get(new ProgramId(9001)).HasValue);
        }

        [Fact]
        public void Get_IdExists_ReturnsCorrectMapInfo()
        {
            const int count = 5;

            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 1);

            // ReSharper disable PossibleInvalidOperationException
            Optional<ProgramIndexMapInfo> map = manager.Get(new ProgramId(1));
            Assert.True(map.HasValue);
            Assert.Equal(new ProgramId(1), map.Value.MainProgramId);
            Assert.Equal(new ProgramId(1), map.Value.ProgramId);
            Assert.Equal(0, map.Value.ProgramIndex);

            map = manager.Get(new ProgramId(4));
            Assert.True(map.HasValue);
            Assert.Equal(new ProgramId(1), map.Value.MainProgramId);
            Assert.Equal(new ProgramId(4), map.Value.ProgramId);
            Assert.Equal(3, map.Value.ProgramIndex);
            // ReSharper restore PossibleInvalidOperationException
        }

        [Fact]
        public void GetProgramId_WithIndex_ReturnsProgramIdOfSpecifiedIndex()
        {
            const int count = 5;

            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => (x + 1) * 5);

            // Check that the program ID can be retrieved using any program in the set
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(5), 3));
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(10), 3));
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(15), 3));
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(20), 3));
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(25), 3));

            // Check that any index can be used
            Assert.Equal(new ProgramId(5), manager.GetProgramId(new ProgramId(10), 0));
            Assert.Equal(new ProgramId(10), manager.GetProgramId(new ProgramId(10), 1));
            Assert.Equal(new ProgramId(15), manager.GetProgramId(new ProgramId(10), 2));
            Assert.Equal(new ProgramId(20), manager.GetProgramId(new ProgramId(10), 3));
            Assert.Equal(new ProgramId(25), manager.GetProgramId(new ProgramId(10), 4));
        }

        [Fact]
        public void GetProgramId_InputIdDoesNotExist_ReturnsInvalidId()
        {
            const int count = 5;

            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 1);

            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(0), 3));
            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(666), 3));
            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(777), 3));
        }

        [Fact]
        public void GetProgramId_InputIndexDoesNotExist_ReturnsInvalidId()
        {
            const int count = 5;

            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 1);

            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(2), 5));
            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(2), 12));
            Assert.Equal(ProgramId.InvalidId, manager.GetProgramId(new ProgramId(2), 255));
        }

        [Fact]
        public void GetProgramCount_MapIsEmpty_ReturnsZero()
        {
            var manager = new ProgramIndexMapInfoManager();

            Assert.Equal(0, manager.GetProgramCount());
        }

        [Fact]
        public void GetProgramCount_MapHasEntries_ReturnsCorrectCount()
        {
            ProgramIndexMapInfoManager manager = CreatePopulatedManager(1, x => x + 1);
            Assert.Equal(1, manager.GetProgramCount());

            manager = CreatePopulatedManager(10, x => x + 1);
            Assert.Equal(10, manager.GetProgramCount());

            manager = CreatePopulatedManager(255, x => x + 1);
            Assert.Equal(255, manager.GetProgramCount());
        }

        [Fact]
        public void Clear_MapHasEntries_CountIsZero()
        {
            const int count = 5;
            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 1);

            manager.Clear();

            Assert.Equal(0, manager.GetProgramCount());
        }

        [Fact]
        public void Clear_MapHasEntries_CannotGetOldEntry()
        {
            const int count = 5;
            ProgramIndexMapInfoManager manager = CreatePopulatedManager(count, x => x + 1);

            manager.Clear();

            Assert.False(manager.Get(new ProgramId(2)).HasValue);
        }
    }
}