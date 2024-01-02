// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using ISaveDataDivisionExporter = LibHac.FsSrv.Sf.ISaveDataDivisionExporter;
using ISaveDataDivisionImporter = LibHac.FsSrv.Sf.ISaveDataDivisionImporter;

namespace LibHac.FsSrv.Impl;

file class NoEncryptorFactory : IChunkEncryptorFactory
{
    public NoEncryptorFactory()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Create(ref SharedRef<AesGcmSource.IEncryptor> outEncryptor)
    {
        throw new NotImplementedException();
    }

    private class Encryptor : AesGcmSource.IEncryptor
    {
        public Encryptor()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(ref AesGcmStreamHeader header, in AesIv iv)
        {
            throw new NotImplementedException();
        }

        public Result Update(Span<byte> destination, ReadOnlySpan<byte> source)
        {
            throw new NotImplementedException();
        }

        public Result GetMac(out AesMac outMac)
        {
            throw new NotImplementedException();
        }
    }
}

file class NoDecryptorFactory : IChunkDecryptorFactory
{
    public NoDecryptorFactory()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Create(ref SharedRef<AesGcmSink.IDecryptor> outDecryptor, in AesMac mac)
    {
        throw new NotImplementedException();
    }

    private class Decryptor : AesGcmSink.IDecryptor
    {
        public Decryptor()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Result Initialize(in AesGcmStreamHeader header)
        {
            throw new NotImplementedException();
        }

        public Result Update(Span<byte> destination, ReadOnlySpan<byte> source)
        {
            throw new NotImplementedException();
        }

        public Result Verify()
        {
            throw new NotImplementedException();
        }
    }
}

public class SaveDataTransferManagerForRepair : ISaveDataTransferManagerForRepair
{
    private SharedRef<ISaveDataTransferCoreInterface> _saveTransferInterface;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private SaveDataPorterManager _porterManager;

    public SaveDataTransferManagerForRepair(SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<ISaveDataTransferCoreInterface> coreInterface, SaveDataPorterManager porterManager)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private Result DecryptInitialDataWithExternalKey(out Box<InitialDataVersion2Detail.Content> outInitialDataContent,
        in InitialDataVersion2Detail initialDataEncrypted)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporter(ref SharedRef<ISaveDataDivisionExporter> outExporter, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    private Result CreateImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2Detail.Content initialDataContent, in InitialDataVersion2Detail initialDataEncrypted,
        SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData,
        SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}