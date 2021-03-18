using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public class EmulatedGameCardFsCreator : IGameCardFileSystemCreator
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private EmulatedGameCardStorageCreator StorageCreator { get; }
        private EmulatedGameCard GameCard { get; }

        public EmulatedGameCardFsCreator(EmulatedGameCardStorageCreator storageCreator, EmulatedGameCard gameCard)
        {
            StorageCreator = storageCreator;
            GameCard = gameCard;
        }

        public Result Create(out IFileSystem fileSystem, GameCardHandle handle, GameCardPartition partitionType)
        {
            // Use the old xci code temporarily

            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GameCard.GetXci(out Xci xci, handle);
            if (rc.IsFailure()) return rc;

            if (!xci.HasPartition((XciPartitionType)partitionType))
            {
                return ResultFs.PartitionNotFound.Log();
            }

            fileSystem = xci.OpenPartition((XciPartitionType)partitionType);
            return Result.Success;
        }

        public Result Create(out ReferenceCountedDisposable<IFileSystem> fileSystem, GameCardHandle handle, GameCardPartition partitionType)
        {
            // Use the old xci code temporarily

            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GameCard.GetXci(out Xci xci, handle);
            if (rc.IsFailure()) return rc;

            if (!xci.HasPartition((XciPartitionType)partitionType))
            {
                return ResultFs.PartitionNotFound.Log();
            }

            XciPartition fs = xci.OpenPartition((XciPartitionType)partitionType);
            fileSystem = new ReferenceCountedDisposable<IFileSystem>(fs);
            return Result.Success;
        }
    }
}
