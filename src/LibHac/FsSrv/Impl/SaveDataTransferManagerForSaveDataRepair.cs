// ReSharper disable UnusedMember.Local UnusedType.Local UnusedTypeParameter
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using LibHac.Util;
using AesIv = LibHac.Fs.AesIv;
using AesKey = LibHac.Fs.AesKey;
using ISaveDataDivisionExporter = LibHac.FsSrv.Sf.ISaveDataDivisionExporter;
using ISaveDataDivisionImporter = LibHac.FsSrv.Sf.ISaveDataDivisionImporter;

namespace LibHac.FsSrv.Impl;

file class ExternalKeyChunkEncryptorFactory : IChunkEncryptorFactory
{
    private AesKey _key;

    public ExternalKeyChunkEncryptorFactory(in AesKey key)
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
        private Aes128GcmEncryptor _encryptor;
        private AesKey _key;

        public Encryptor(in AesKey key)
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

file class ExternalKeyChunkDecryptorFactory : IChunkDecryptorFactory
{
    private AesKey _key;

    public ExternalKeyChunkDecryptorFactory(in AesKey key)
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
        private AesKey _key;
        private Aes128GcmDecryptor _decryptor;

        public Decryptor(in AesKey key)
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

file static class Anonymous
{
    public static Result CheckInitialDataConsistency(in InitialDataVersion2Detail.Content initialData1,
        in InitialDataVersion2Detail.Content initialData2)
    {
        throw new NotImplementedException();
    }
}

public interface ISaveDataTransferManagerForSaveDataRepairPolicy
{
    static abstract ulong DecryptAesGcm(Span<byte> destination, Span<byte> outMac, ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, ReadOnlySpan<byte> aad);
}

internal struct SaveDataTransferManagerForSaveDataRepairPolicyV0 : ISaveDataTransferManagerForSaveDataRepairPolicy
{
    public static ulong DecryptAesGcm(Span<byte> destination, Span<byte> outMac, ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source, ReadOnlySpan<byte> aad)
    {
        throw new NotImplementedException();
    }
}

public static class SaveDataTransferManagerForSaveDataRepair
{
    public struct KeyPackageV0
    {
        public uint Version;
        public Array4<byte> Reserved4;
        public byte KeyGeneration;
        public Array7<byte> Reserved9;
        public AesIv Iv;
        public AesMac Mac;
        public Array80<byte> Reserved30;
        public Content PackageContent;
        public Array256<byte> Signature;
        
        public struct Content
        {
            public InitialDataMac InitialDataMacBeforeRepair;
            public byte KeyGenerationBeforeRepair;
            public InitialDataMac InitialDataMacAfterRepair;
            public byte KeyGenerationAfterRepair;
            public Array2<AesKey> Keys;
            public AesIv Iv;
            public AesMac Mac;
            public Array8<byte> Reserved70;
            public Fs.SaveDataTransferManagerVersion2.Challenge ChallengeData;
            public Array120<byte> Reserved88;
        }
    }
}

public class SaveDataTransferManagerForSaveDataRepair<TPolicy> : ISaveDataTransferManagerForSaveDataRepair
    where TPolicy : struct, ISaveDataTransferManagerForSaveDataRepairPolicy
{
    private SharedRef<ISaveDataTransferCoreInterface> _transferInterface;
    private SaveDataTransferCryptoConfiguration _cryptoConfig;
    private bool _isKeyPackageSet;
    private Fs.SaveDataTransferManagerVersion2.Challenge _challengeData;
    private Optional<AesKey> _kek;
    private SaveDataTransferManagerForSaveDataRepair.KeyPackageV0.Content _keyPackage;
    private SaveDataPorterManager _porterManager;
    private bool _canOpenPorterWithKey;

    public SaveDataTransferManagerForSaveDataRepair(SaveDataTransferCryptoConfiguration cryptoConfig,
        ref readonly SharedRef<ISaveDataTransferCoreInterface> coreInterface, SaveDataPorterManager porterManager,
        bool canOpenPorterWithKey)
    {
        _transferInterface = SharedRef<ISaveDataTransferCoreInterface>.CreateCopy(in coreInterface);
        _cryptoConfig = cryptoConfig;
        _isKeyPackageSet = false;
        _kek = new Optional<AesKey>();
        _porterManager = porterManager;
        _canOpenPorterWithKey = canOpenPorterWithKey;
        _cryptoConfig.GenerateRandomData(_challengeData.Value);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result GetChallenge(OutBuffer challenge)
    {
        throw new NotImplementedException();
    }

    public Result SetKeyPackage(InBuffer keyPackage)
    {
        throw new NotImplementedException();
    }

    private Result DecryptAndVerifyInitialDataWithExternalKey(
        ref InitialDataVersion2Detail.Content outInitialDataDecrypted,
        in InitialDataVersion2Detail initialDataEncrypted, SaveDataTransferCryptoConfiguration.KeyIndex keyIndex,
        int keyGeneration, in InitialDataMac expectedMac, in AesKey key)
    {
        throw new NotImplementedException();
    }

    private Result DecryptInitialDataWithExternalKey(ref InitialDataVersion2Detail.Content outInitialDataDecrypted,
        in InitialDataVersion2Detail initialDataEncrypted, in AesKey key)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterAndGetEncryptedKey(ref SharedRef<ISaveDataDivisionExporter> outExporter,
        OutBuffer outEncryptedKey, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result PrepareOpenSaveDataImporter(OutBuffer outEncryptedKey)
    {
        throw new NotImplementedException();
    }

    private Result DecryptAndVerifyKeyPackage(out AesKey outKeyBeforeRepair, out AesKey outKeyAfterRepair)
    {
        throw new NotImplementedException();
    }

    private Result CreateImporter(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2Detail.Content initialDataContent, in InitialDataVersion2Detail initialDataEncrypted,
        in AesKey key, UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterForSaveDataAfterRepair(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer initialDataBeforeRepair, InBuffer initialDataAfterRepair, UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterForSaveDataBeforeRepair(ref SharedRef<ISaveDataDivisionImporter> outImporter,
        InBuffer initialData, UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterWithKey(ref SharedRef<ISaveDataDivisionExporter> outExporter, InBuffer key,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterWithKey(ref SharedRef<ISaveDataDivisionImporter> outImporter, InBuffer key,
        InBuffer initialData, UserId userId, ulong saveDataSpaceId)
    {
        throw new NotImplementedException();
    }
}