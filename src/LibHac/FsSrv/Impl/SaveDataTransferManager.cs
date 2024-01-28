// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using IFile = LibHac.Fs.Fsa.IFile;

namespace LibHac.FsSrv.Impl;

public class SaveDataTransferManager : ISaveDataTransferManager
{
    private SharedRef<ISaveDataTransferCoreInterface> _transferCoreInterface;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private bool _isTokenSet;
    private Array16<byte> _challengeData;
    private Array16<byte> _transferAesKeySeed;
    private Array16<byte> _iv;
    private int _openedExporterCount;
    private int _openedImporterCount;

    public SaveDataTransferManager(SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<ISaveDataTransferCoreInterface> transferCoreInterface)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result GetChallenge(OutBuffer outChallengeBuffer)
    {
        throw new NotImplementedException();
    }

    public Result SetToken(InBuffer tokenBuffer)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporter(ref SharedRef<ISaveDataExporter> outExporter, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref SharedRef<ISaveDataImporter> outImporter, out long outRequiredSize,
        InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataConcatenationFile(ref SharedRef<IFile> outFile,
        ref UniqueRef<SaveDataMacUpdater> outMacUpdater, in SaveDataInfo saveDataInfo, OpenMode openMode,
        bool isTemporaryTransferSave)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataInternalFile : IFile
{
    private SharedRef<IFileSystem> _baseFileSystem;
    private SharedRef<IFile> _baseFile;

    public SaveDataInternalFile(ref readonly SharedRef<IFileSystem> baseFileSystem, ref readonly SharedRef<IFile> baseFile)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}