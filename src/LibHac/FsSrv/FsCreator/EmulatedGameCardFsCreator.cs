using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;

namespace LibHac.FsSrv.FsCreator;

public class EmulatedGameCardFsCreator : IGameCardFileSystemCreator
{
    // ReSharper disable once NotAccessedField.Local
    private EmulatedGameCardStorageCreator _storageCreator;
    private EmulatedGameCard _gameCard;

    public EmulatedGameCardFsCreator(EmulatedGameCardStorageCreator storageCreator, EmulatedGameCard gameCard)
    {
        _storageCreator = storageCreator;
        _gameCard = gameCard;
    }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionType)
    {
        // Use the old xci code temporarily

        Result rc = _gameCard.GetXci(out Xci xci, handle);
        if (rc.IsFailure()) return rc;

        if (!xci.HasPartition((XciPartitionType)partitionType))
        {
            return ResultFs.PartitionNotFound.Log();
        }

        XciPartition fs = xci.OpenPartition((XciPartitionType)partitionType);
        outFileSystem.Reset(fs);
        return Result.Success;
    }
}