using System;
using LibHac.Common;
using LibHac.Fat;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage;
using LibHac.Os;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using Path = LibHac.Fs.Path;
using Utility = LibHac.FsSystem.Utility;

namespace LibHac.FsSrv;

public class BaseFileSystemServiceImpl
{
    private Configuration _config;

    public FileSystemServer FsServer => _config.FsServer;

    private static ReadOnlySpan<byte> PaddingDirectoryName => "/Padding"u8;
    private const int PaddingFileCountMax = 0x10000000;

    public delegate Result BisWiperCreator(ref UniqueRef<IWiper> outWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize);

    public BaseFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        // nn::fat::SetCurrentTimeStampCallback(configuration.CurrentTimeFunction);
    }

    public struct Configuration
    {
        public IBuiltInStorageFileSystemCreator BisFileSystemCreator;
        public IGameCardFileSystemCreator GameCardFileSystemCreator;
        public ISdCardProxyFileSystemCreator SdCardFileSystemCreator;
        // CurrentTimeFunction
        public FatFileSystemCacheManager FatFileSystemCacheManager;
        public BaseFileSystemCreatorHolder BaseFileSystemCreatorHolder;
        public BisWiperCreator BisWiperCreator;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    public Result OpenBaseFileSystem(ref SharedRef<IFileSystem> outFileSystem, BaseFileSystemId fileSystemId)
    {
        Result res = _config.BaseFileSystemCreatorHolder.Get(out IBaseFileSystemCreator baseFsCreator, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        return baseFsCreator.Create(ref outFileSystem, fileSystemId).Ret();
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        Result res = _config.BaseFileSystemCreatorHolder.Get(out IBaseFileSystemCreator baseFsCreator, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        return baseFsCreator.Format(fileSystemId).Ret();
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId)
    {
        return OpenBisFileSystem(ref outFileSystem, partitionId, caseSensitive: false).Ret();
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId,
        bool caseSensitive)
    {
        return _config.BisFileSystemCreator.Create(ref outFileSystem, partitionId, caseSensitive).Ret();
    }

    public Result CreatePaddingFile(long size)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenBisFileSystem(ref fileSystem.Ref, BisPartitionId.User);
        if (res.IsFailure()) return res.Miss();

        using var pathPaddingDirectory = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathPaddingDirectory.Ref(), PaddingDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = Utility.EnsureDirectory(fileSystem.Get, in pathPaddingDirectory);
        if (res.IsFailure()) return res.Miss();

        Span<byte> pathPaddingFileBuffer = stackalloc byte[0x80];

        for (int i = 0; i < PaddingFileCountMax; i++)
        {
            using scoped var pathPaddingFile = new Path();
            res = PathFunctions.SetUpFixedPathEntryWithInt(ref pathPaddingFile.Ref(), pathPaddingFileBuffer, PaddingDirectoryName, i);
            if (res.IsFailure()) return res.Miss();

            res = fileSystem.Get.CreateFile(in pathPaddingFile, size, CreateFileOptions.CreateConcatenationFile);
            if (!res.IsSuccess())
            {
                if (ResultFs.PathAlreadyExists.Includes(res))
                {
                    res.Catch().Handle();
                }
                else
                {
                    return res.Miss();
                }
            }
            else
            {
                return Result.Success;
            }
        }

        return ResultFs.PathAlreadyExists.Log();
    }

    public Result DeleteAllPaddingFiles()
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = OpenBisFileSystem(ref fileSystem.Ref, BisPartitionId.User);
        if (res.IsFailure()) return res.Miss();

        using var pathPaddingDirectory = new Path();
        res = PathFunctions.SetUpFixedPath(ref pathPaddingDirectory.Ref(), PaddingDirectoryName);
        if (res.IsFailure()) return res.Miss();

        res = fileSystem.Get.DeleteDirectoryRecursively(in pathPaddingDirectory);
        if (!res.IsSuccess())
        {
            if (ResultFs.PathNotFound.Includes(res))
            {
                res.Catch().Handle();
            }
            else
            {
                return res.Miss();
            }
        }

        return Result.Success;
    }

    public Result OpenGameCardFileSystem(ref SharedRef<IFileSystem> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        const int maxTries = 2;
        Result res = Result.Success;

        for (int i = 0; i < maxTries; i++)
        {
            res = _config.GameCardFileSystemCreator.Create(ref outFileSystem, handle, partitionId);

            if (!ResultFs.DataCorrupted.Includes(res))
                break;
        }

        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result OpenSdCardProxyFileSystem(ref SharedRef<IFileSystem> outFileSystem)
    {
        return OpenSdCardProxyFileSystem(ref outFileSystem, openCaseSensitive: false).Ret();
    }

    public Result OpenSdCardProxyFileSystem(ref SharedRef<IFileSystem> outFileSystem, bool openCaseSensitive)
    {
        return _config.SdCardFileSystemCreator.Create(ref outFileSystem, openCaseSensitive).Ret();
    }

    public Result FormatSdCardProxyFileSystem()
    {
        return _config.SdCardFileSystemCreator.Format().Ret();
    }

    public Result FormatSdCardDryRun()
    {
        Result res = FsServer.Storage.GetSdCardUserAreaNumSectors(out uint userAreaSectors);
        if (res.IsFailure()) return res.Miss();

        res = FsServer.Storage.GetSdCardProtectedAreaNumSectors(out uint protectedAreaSectors);
        if (res.IsFailure()) return res.Miss();

        return Fat.Impl.FatFileSystemStorageAdapter.FormatDryRun(userAreaSectors, protectedAreaSectors).Ret();
    }

    public bool IsExFatSupported()
    {
        return FatFileSystem.IsExFatSupported();
    }

    public void FlushFatCache()
    {
        using UniqueLock<SdkRecursiveMutex> scopedLock = _config.FatFileSystemCacheManager.GetScopedLock();

        FatFileSystemCacheManager.Iterator iter = _config.FatFileSystemCacheManager.GetIterator();

        while (!iter.IsEnd())
        {
            using SharedRef<IFileSystem> fileSystem = iter.Get();
            if (fileSystem.HasValue)
            {
                fileSystem.Get.Flush().IgnoreResult();
            }

            iter.Next();
        }
    }

    public Result OpenBisWiper(ref UniqueRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        return _config.BisWiperCreator(ref outBisWiper, transferMemoryHandle, transferMemorySize).Ret();
    }

    internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfo(out programInfo, processId).Ret();
    }
}