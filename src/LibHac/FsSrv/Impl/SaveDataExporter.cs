// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;

namespace LibHac.FsSrv.Impl;

public class SaveDataExporter : ISaveDataExporter
{
    private Box<SaveDataExtraData> _extraData;
    private SaveDataInfo _saveDataInfo;
    private ulong _pulledSize;
    private ulong _totalSize;
    private SharedRef<IFile> _saveDataFile;
    private ulong _currentFileOffset;
    private UniqueRef<Aes128GcmEncryptor> _initialDataEncryptor;
    private UniqueRef<Aes128GcmEncryptor> _encryptor;
    private uint _initialDataVersion;

    public SaveDataExporter(Box<SaveDataExtraData> extraData, in SaveDataInfo saveDataInfo,
        ref readonly SharedRef<IFile> saveDataFile, long fileSize,
        ref UniqueRef<Aes128GcmEncryptor> initialDataEncryptor, ref UniqueRef<Aes128GcmEncryptor> encryptor,
        uint initialDataVersion)
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

    public Result Pull(out ulong outBytesRead, OutBuffer buffer)
    {
        throw new NotImplementedException();
    }

    public Result PullInitialData(OutBuffer initialDataBuffer)
    {
        throw new NotImplementedException();
    }
}