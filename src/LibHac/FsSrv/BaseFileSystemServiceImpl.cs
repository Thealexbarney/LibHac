using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv;

public class BaseFileSystemServiceImpl
{
    private Configuration _config;

    public delegate Result BisWiperCreator(ref UniqueRef<IWiper> outWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize);

    public BaseFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
    }

    public struct Configuration
    {
        public IBuiltInStorageFileSystemCreator BisFileSystemCreator;
        public IGameCardFileSystemCreator GameCardFileSystemCreator;
        public ISdCardProxyFileSystemCreator SdCardFileSystemCreator;
        // CurrentTimeFunction
        // FatFileSystemCacheManager
        // BaseFileSystemCreatorHolder
        public BisWiperCreator BisWiperCreator;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    public Result OpenBaseFileSystem(ref SharedRef<IFileSystem> outFileSystem, BaseFileSystemId fileSystemId)
    {
        throw new NotImplementedException();
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        throw new NotImplementedException();
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId)
    {
        return OpenBisFileSystem(ref outFileSystem, partitionId, false);
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystem> outFileSystem, BisPartitionId partitionId,
        bool caseSensitive)
    {
        Result res = _config.BisFileSystemCreator.Create(ref outFileSystem, partitionId, caseSensitive);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result CreatePaddingFile(long size)
    {
        throw new NotImplementedException();
    }

    public Result DeleteAllPaddingFiles()
    {
        throw new NotImplementedException();
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

        return res;
    }

    public Result OpenSdCardProxyFileSystem(ref SharedRef<IFileSystem> outFileSystem)
    {
        return OpenSdCardProxyFileSystem(ref outFileSystem, false);
    }

    public Result OpenSdCardProxyFileSystem(ref SharedRef<IFileSystem> outFileSystem, bool openCaseSensitive)
    {
        return _config.SdCardFileSystemCreator.Create(ref outFileSystem, openCaseSensitive);
    }

    public Result FormatSdCardProxyFileSystem()
    {
        return _config.SdCardFileSystemCreator.Format();
    }

    public Result FormatSdCardDryRun()
    {
        throw new NotImplementedException();
    }

    public bool IsExFatSupported()
    {
        // Returning false should probably be fine
        return false;
    }

    public Result OpenBisWiper(ref UniqueRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        return _config.BisWiperCreator(ref outBisWiper, transferMemoryHandle, transferMemorySize);
    }

    internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfo(out programInfo, processId);
    }
}
