// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;

namespace LibHac.FsSrv.Impl;

public class SaveDataMacUpdater : IDisposable
{
    private SharedRef<IFileSystem> _fileSystem;

    public SaveDataMacUpdater(ref readonly SharedRef<IFileSystem> fileSystem)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result UpdateMac()
    {
        throw new NotImplementedException();
    }
}

public class SaveDataImporter : ISaveDataImporter
{
    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private SaveDataInfo _saveDataInfo;
    private bool _isFinished;
    private ulong _pushedSize;
    private ulong _totalSize;
    private SharedRef<IFile> _saveDataFile;
    private ulong _currentFileOffset;
    private UniqueRef<Aes128GcmDecryptor> _decryptor;
    private AesMac _expectedMac;
    private UniqueRef<SaveDataMacUpdater> _macUpdater;

    public SaveDataImporter(ref readonly SharedRef<ISaveDataTransferCoreInterface> transferInterface,
        in SaveDataInfo saveDataInfo, ref readonly SharedRef<IFile> file, long fileSize,
        ref UniqueRef<SaveDataMacUpdater> macUpdater, ref UniqueRef<Aes128GcmDecryptor> decryptor)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result GetSaveDataInfo(out SaveDataInfo outInfo)
    {
        throw new NotImplementedException();
    }

    public Result GetRestSize(out ulong outRemainingSize)
    {
        throw new NotImplementedException();
    }

    public Result Push(InBuffer buffer)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeImport()
    {
        throw new NotImplementedException();
    }
}