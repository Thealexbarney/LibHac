using LibHac.Fs;

namespace LibHac.FsService.Creators
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

            fileSystem = default;

            Result rc = GameCard.GetXci(out Xci xci, handle);
            if (rc.IsFailure()) return rc;

            if (!xci.HasPartition((XciPartitionType)partitionType))
            {
                return ResultFs.PartitionNotFound.Log();
            }

            fileSystem = xci.OpenPartition((XciPartitionType)partitionType);
            return Result.Success;
        }
    }
}
