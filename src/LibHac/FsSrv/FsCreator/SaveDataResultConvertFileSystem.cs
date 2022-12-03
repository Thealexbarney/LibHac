using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using static LibHac.FsSrv.FsCreator.SaveDataResultConverter;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Wraps an <see cref="IFile"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataResultConvertFile : IResultConvertFile
{
    private bool _isReconstructible;

    public SaveDataResultConvertFile(ref UniqueRef<IFile> baseFile, bool isReconstructible) : base(ref baseFile)
    {
        _isReconstructible = isReconstructible;
    }

    protected override Result ConvertResult(Result result)
    {
        return ConvertSaveDataFsResult(result, _isReconstructible).Ret();
    }
}

/// <summary>
/// Wraps an <see cref="IDirectory"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataResultConvertDirectory : IResultConvertDirectory
{
    private bool _isReconstructible;

    public SaveDataResultConvertDirectory(ref UniqueRef<IDirectory> baseDirectory, bool isReconstructible) : base(
        ref baseDirectory)
    {
        _isReconstructible = isReconstructible;
    }

    protected override Result ConvertResult(Result result)
    {
        return ConvertSaveDataFsResult(result, _isReconstructible).Ret();
    }
}

/// <summary>
/// Wraps an <see cref="ISaveDataFileSystem"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class SaveDataResultConvertFileSystem : IResultConvertFileSystem<ISaveDataFileSystem>
{
    private bool _isReconstructible;

    public SaveDataResultConvertFileSystem(ref SharedRef<ISaveDataFileSystem> baseFileSystem, bool isReconstructible) :
        base(ref baseFileSystem)
    {
        _isReconstructible = isReconstructible;
    }

    public override Result WriteExtraData(in SaveDataExtraData extraData)
    {
        return ConvertSaveDataFsResult(GetFileSystem().WriteExtraData(in extraData), _isReconstructible).Ret();
    }

    public override Result CommitExtraData(bool updateTimeStamp)
    {
        return ConvertSaveDataFsResult(GetFileSystem().CommitExtraData(updateTimeStamp), _isReconstructible).Ret();
    }

    public override Result ReadExtraData(out SaveDataExtraData extraData)
    {
        return ConvertSaveDataFsResult(GetFileSystem().ReadExtraData(out extraData), _isReconstructible).Ret();
    }

    public override Result RollbackOnlyModified()
    {
        return ConvertSaveDataFsResult(GetFileSystem().RollbackOnlyModified(), _isReconstructible).Ret();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var file = new UniqueRef<IFile>();
        Result res = ConvertResult(GetFileSystem().OpenFile(ref file.Ref, in path, mode));
        if (res.IsFailure()) return res.Miss();

        using UniqueRef<SaveDataResultConvertFile> resultConvertFile =
            new(new SaveDataResultConvertFile(ref file.Ref, _isReconstructible));

        outFile.Set(ref resultConvertFile.Ref);
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        using var directory = new UniqueRef<IDirectory>();
        Result res = ConvertResult(GetFileSystem().OpenDirectory(ref directory.Ref, in path, mode));
        if (res.IsFailure()) return res.Miss();

        using UniqueRef<SaveDataResultConvertDirectory> resultConvertDirectory =
            new(new SaveDataResultConvertDirectory(ref directory.Ref, _isReconstructible));

        outDirectory.Set(ref resultConvertDirectory.Ref);
        return Result.Success;
    }

    protected override Result ConvertResult(Result result)
    {
        return ConvertSaveDataFsResult(result, _isReconstructible).Ret();
    }

    public override bool IsSaveDataFileSystemCacheEnabled()
    {
        return GetFileSystem().IsSaveDataFileSystemCacheEnabled();
    }

    public override void RegisterExtraDataAccessorObserver(ISaveDataExtraDataAccessorObserver observer,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        GetFileSystem().RegisterExtraDataAccessorObserver(observer, spaceId, saveDataId);
    }
}