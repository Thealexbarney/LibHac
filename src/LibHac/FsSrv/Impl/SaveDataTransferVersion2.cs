// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using ISaveDataDivisionExporter = LibHac.FsSrv.Sf.ISaveDataDivisionExporter;
using ISaveDataDivisionImporter = LibHac.FsSrv.Sf.ISaveDataDivisionImporter;

namespace LibHac.FsSrv.Impl;

public struct KeySeedPackageV0
{
    public uint Version;
    public Array4<byte> Reserved4;
    public byte KeyGeneration;
    public Array7<byte> Reserved9;
    public AesIv Iv;
    public AesMac Mac;
    public Array80<byte> Reserved30;
    public Array256<byte> Signature;
    public Content PackageContent;

    public struct Content
    {
        public Array16<byte> Unknown;
        public KeySeed TransferKeySeed;
        public InitialDataMac TransferInitialDataMac;
        public Fs.SaveDataTransferManagerVersion2.Challenge Challenge;
        public Array64<byte> Reserved;
    }
}

file static class Anonymous
{
    public static Result DecryptoAndVerifyPortContext(Span<byte> destination, ReadOnlySpan<byte> source,
        SaveDataTransferCryptoConfiguration cryptoConfig)
    {
        throw new NotImplementedException();
    }
}

file class KeySeedChunkEncryptorFactory : IChunkEncryptorFactory
{
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private KeySeedPackageV0.Content _keySeedPackage;
    private SaveDataTransferCryptoConfiguration.Attributes _attribute;

    public KeySeedChunkEncryptorFactory(SaveDataTransferCryptoConfiguration cryptoConfig,
        in KeySeedPackageV0.Content keySeedPackage, SaveDataTransferCryptoConfiguration.Attributes attribute)
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
        private SaveDataTransferCryptoConfiguration _cryptoConfig;
        private KeySeedPackageV0.Content _keySeedPackage;
        private SaveDataTransferCryptoConfiguration.Attributes _attribute;
        private SharedRef<SaveDataTransferCryptoConfiguration.IEncryptor> _encryptor;

        public Encryptor(SaveDataTransferCryptoConfiguration cryptoConfig, in KeySeedPackageV0.Content keySeedPackage,
            SaveDataTransferCryptoConfiguration.Attributes attribute)
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

file class KeySeedChunkDecryptorFactory : IChunkDecryptorFactory
{
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private KeySeedPackageV0.Content _keySeedPackage;
    private SaveDataTransferCryptoConfiguration.Attributes _attribute;

    public KeySeedChunkDecryptorFactory(SaveDataTransferCryptoConfiguration cryptoConfig,
        in KeySeedPackageV0.Content keySeedPackage, SaveDataTransferCryptoConfiguration.Attributes attribute)
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
        private SaveDataTransferCryptoConfiguration _cryptoConfig;
        private KeySeedPackageV0.Content _keySeedPackage;
        private SharedRef<SaveDataTransferCryptoConfiguration.IDecryptor> _decryptor;
        private SaveDataTransferCryptoConfiguration.Attributes _attribute;
        private AesMac _mac;

        public Decryptor(SaveDataTransferCryptoConfiguration cryptoConfig, in KeySeedPackageV0.Content keySeedPackage,
            SaveDataTransferCryptoConfiguration.Attributes attribute, in AesMac mac)
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

public class SaveDataTransferManagerVersion2 : ISaveDataTransferManagerWithDivision
{
    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private bool _isKeySeedPackageSet;
    private Array16<byte> _challengeData;
    private KeySeedPackageV0.Content _keySeedPackage;
    private SaveDataPorterManager _porterManager;
    private SaveDataTransferCryptoConfiguration.Attributes _attribute;

    public SaveDataTransferManagerVersion2(SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<ISaveDataTransferCoreInterface> transferInterface, SaveDataPorterManager porterManager)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result GetChallenge(OutBuffer challenge)
    {
        throw new NotImplementedException();
    }

    public Result SetKeySeedPackage(InBuffer keySeedPackage)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporter(ref SharedRef<ISaveDataDivisionExporter> outExporter, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterForDiffExport(ref SharedRef<ISaveDataDivisionExporter> outExporter,
        InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterByContext(ref SharedRef<ISaveDataDivisionExporter> outExporter,
        InBuffer exportContext)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterDeprecated(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer initialData, in UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterForDiffImport(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterForDuplicateDiffImport(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer initialData, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer initialData,
        in UserId userId, SaveDataSpaceId spaceId, bool useSwap)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterByContext(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer importContext)
    {
        throw new NotImplementedException();
    }

    public Result CancelSuspendingImport(Ncm.ApplicationId applicationId, in UserId userId)
    {
        throw new NotImplementedException();
    }

    public Result CancelSuspendingImportByAttribute(in SaveDataAttribute attribute)
    {
        throw new NotImplementedException();
    }

    public Result SwapSecondary(in SaveDataAttribute attribute, bool doSwap, long primaryCommitId)
    {
        throw new NotImplementedException();
    }

    private Result DecryptAndVerifyInitialData(out Box<InitialDataVersion2Detail.Content> outInitialDataContent,
        in InitialDataVersion2Detail initialDataEncrypted)
    {
        throw new NotImplementedException();
    }
}