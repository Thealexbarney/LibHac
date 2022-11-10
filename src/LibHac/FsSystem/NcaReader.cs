using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem;

public delegate void GenerateKeyFunction(Span<byte> destKey, ReadOnlySpan<byte> sourceKey, int keyType);
public delegate Result DecryptAesCtrFunction(Span<byte> dest, int keyIndex, int keyGeneration, ReadOnlySpan<byte> encryptedKey, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source);
public delegate Result CryptAesXtsFunction(Span<byte> dest, ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source);
public delegate bool VerifySign1Function(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> data, bool isProd, byte generation);

/// <summary>
/// Handles reading information from an NCA file's header.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class NcaReader : IDisposable
{
    private const uint SdkAddonVersionMin = 0xB0000;

    private NcaHeader _header;
    private Array5<Array16<byte>> _decryptionKeys;
    private SharedRef<IStorage> _bodyStorage;
    private UniqueRef<IStorage> _headerStorage;
    private Array16<byte> _externalDataDecryptionKey;
    private DecryptAesCtrFunction _decryptAesCtr;
    private DecryptAesCtrFunction _decryptAesCtrForExternalKey;
    private bool _isSoftwareAesPrioritized;
    private bool _isAvailableSwKey;
    private NcaHeader.EncryptionType _headerEncryptionType;
    private GetDecompressorFunction _getDecompressorFunc;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;

    public void Dispose()
    {
        _headerStorage.Destroy();
        _bodyStorage.Destroy();
    }

    public Result Initialize(ref SharedRef<IStorage> baseStorage, in NcaCryptoConfiguration cryptoConfig,
        in NcaCompressionConfiguration compressionConfig, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        Assert.SdkRequiresNotNull(in baseStorage);
        Assert.SdkRequiresNotNull(hashGeneratorFactorySelector);
        Assert.SdkRequiresNull(in _bodyStorage);

        if (cryptoConfig.VerifySign1 is null)
            return ResultFs.InvalidArgument.Log();

        using var headerStorage = new UniqueRef<IStorage>();

        if (cryptoConfig.IsAvailableSwKey)
        {
            if (cryptoConfig.GenerateKey is null)
                return ResultFs.InvalidArgument.Log();

            ReadOnlySpan<int> headerKeyTypes = stackalloc int[NcaCryptoConfiguration.HeaderEncryptionKeyCount]
                { (int)KeyType.NcaHeaderKey1, (int)KeyType.NcaHeaderKey2 };

            // Generate the keys for decrypting the NCA header.
            Unsafe.SkipInit(out Array2<Array16<byte>> commonDecryptionKeys);
            for (int i = 0; i < NcaCryptoConfiguration.HeaderEncryptionKeyCount; i++)
            {
                cryptoConfig.GenerateKey(commonDecryptionKeys[i].Items, cryptoConfig.HeaderEncryptedEncryptionKeys[i],
                    headerKeyTypes[i]);
            }

            // Create an XTS storage to read the encrypted header.
            Array16<byte> headerIv = default;
            headerStorage.Reset(new AesXtsStorage(baseStorage.Get, commonDecryptionKeys[0], commonDecryptionKeys[1],
                headerIv, NcaHeader.XtsBlockSize));
        }
        else
        {
            // Software key isn't available, so we need to be able to decrypt externally.
            if (cryptoConfig.DecryptAesXtsForExternalKey is null)
                return ResultFs.InvalidArgument.Log();

            // Create the header storage.
            Array16<byte> headerIv = default;
            headerStorage.Reset(new AesXtsStorageExternal(baseStorage.Get, ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty, headerIv, (uint)NcaHeader.XtsBlockSize, cryptoConfig.EncryptAesXtsForExternalKey,
                cryptoConfig.DecryptAesXtsForExternalKey));
        }

        if (!headerStorage.HasValue)
            return ResultFs.AllocationMemoryFailedInNcaReaderA.Log();

        // Read the decrypted header.
        Result rc = headerStorage.Get.Read(0, SpanHelpers.AsByteSpan(ref _header));
        if (rc.IsFailure()) return rc.Miss();

        // Check if the NCA magic value is correct.
        Result signatureResult = CheckSignature(in _header);
        if (signatureResult.IsFailure())
        {
            // If the magic value is not correct the header might not be encrypted.
            if (cryptoConfig.IsDev)
            {
                // Read the header without decrypting it and check the magic value again.
                rc = baseStorage.Get.Read(0, SpanHelpers.AsByteSpan(ref _header));
                if (rc.IsFailure()) return rc.Miss();

                rc = CheckSignature(in _header);
                if (rc.IsFailure())
                    return signatureResult.Miss();

                // We have a plaintext header. Get an IStorage of just the header.
                rc = baseStorage.Get.GetSize(out long baseStorageSize);
                if (rc.IsFailure()) return rc.Miss();

                headerStorage.Reset(new SubStorage(in baseStorage, 0, baseStorageSize));

                if (!headerStorage.HasValue)
                    return ResultFs.AllocationMemoryFailedInNcaReaderA.Log();

                _headerEncryptionType = NcaHeader.EncryptionType.None;
            }
            else
            {
                return signatureResult.Miss();
            }
        }

        // Validate the fixed key signature.
        if (_header.Header1SignatureKeyGeneration > NcaCryptoConfiguration.Header1SignatureKeyGenerationMax)
            return ResultFs.InvalidNcaHeader1SignatureKeyGeneration.Log();

        int signMessageOffset = NcaHeader.HeaderSignSize * NcaHeader.HeaderSignCount;
        int signMessageSize = NcaHeader.Size - signMessageOffset;
        ReadOnlySpan<byte> signature = _header.Signature1;
        ReadOnlySpan<byte> message = SpanHelpers.AsReadOnlyByteSpan(in _header).Slice(signMessageOffset, signMessageSize);

        if (!cryptoConfig.VerifySign1(signature, message, !cryptoConfig.IsDev, _header.Header1SignatureKeyGeneration))
            return ResultFs.NcaHeaderSignature1VerificationFailed.Log();

        // Validate the sdk version.
        if (_header.SdkAddonVersion < SdkAddonVersionMin)
            return ResultFs.UnsupportedSdkVersion.Log();

        // Validate the key index.
        if (_header.KeyAreaEncryptionKeyIndex >= NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexCount &&
            _header.KeyAreaEncryptionKeyIndex != NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexZeroKey)
        {
            return ResultFs.InvalidNcaKeyIndex.Log();
        }

        _hashGeneratorFactorySelector = hashGeneratorFactorySelector;

        // Get keys from the key area if the NCA doesn't have a rights ID.
        Array16<byte> zeroRightsId = default;
        if (CryptoUtil.IsSameBytes(zeroRightsId, _header.RightsId, NcaHeader.RightsIdSize))
        {
            // If we don't have a rights ID we need to generate decryption keys if software keys are available.
            if (cryptoConfig.IsAvailableSwKey)
            {
                int keyTypeValue = NcaKeyFunctions.GetKeyTypeValue(_header.KeyAreaEncryptionKeyIndex, _header.GetProperKeyGeneration());
                ReadOnlySpan<byte> encryptedKeyCtr = _header.EncryptedKeys.ItemsRo.Slice((int)NcaHeader.DecryptionKey.AesCtr * Aes.KeySize128, Aes.KeySize128);

                cryptoConfig.GenerateKey(_decryptionKeys[(int)NcaHeader.DecryptionKey.AesCtr].Items, encryptedKeyCtr, keyTypeValue);
            }

            // Copy the plaintext hardware key.
            ReadOnlySpan<byte> keyCtrHw = _header.EncryptedKeys.ItemsRo.Slice((int)NcaHeader.DecryptionKey.AesCtrHw * Aes.KeySize128, Aes.KeySize128);
            keyCtrHw.CopyTo(_decryptionKeys[(int)NcaHeader.DecryptionKey.AesCtrHw].Items);
        }

        // Clear the external decryption key.
        _externalDataDecryptionKey.Items.Clear();

        // Copy the configuration to the NcaReader.
        _isAvailableSwKey = cryptoConfig.IsAvailableSwKey;
        _decryptAesCtr = cryptoConfig.DecryptAesCtr;
        _decryptAesCtrForExternalKey = cryptoConfig.DecryptAesCtrForExternalKey;
        _getDecompressorFunc = compressionConfig.GetDecompressorFunc;

        _bodyStorage.SetByMove(ref baseStorage);
        _headerStorage.Set(ref headerStorage.Ref());

        return Result.Success;

        static Result CheckSignature(in NcaHeader header)
        {
            if (header.Magic == NcaHeader.Magic0 ||
                header.Magic == NcaHeader.Magic1 ||
                header.Magic == NcaHeader.Magic2)
            {
                return ResultFs.UnsupportedSdkVersion.Log();
            }

            if (header.Magic != NcaHeader.CurrentMagic)
                return ResultFs.InvalidNcaSignature.Log();

            return Result.Success;
        }
    }

    public Result ReadHeader(out NcaFsHeader outHeader, int index)
    {
        UnsafeHelpers.SkipParamInit(out outHeader);

        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        long offset = Unsafe.SizeOf<NcaHeader>() + Unsafe.SizeOf<NcaFsHeader>() * index;
        return _headerStorage.Get.Read(offset, SpanHelpers.AsByteSpan(ref outHeader));
    }

    public void GetHeaderSign2(Span<byte> outBuffer)
    {
        Assert.SdkRequiresEqual(NcaHeader.HeaderSignSize, outBuffer.Length);

        _header.Signature2.ItemsRo.CopyTo(outBuffer);
    }

    public void GetHeaderSign2TargetHash(Span<byte> outBuffer)
    {
        Assert.SdkRequiresNotNull(_hashGeneratorFactorySelector);
        Assert.SdkRequiresEqual(IHash256Generator.HashSize, outBuffer.Length);

        int signTargetOffset = NcaHeader.HeaderSignSize * NcaHeader.HeaderSignCount;
        int signTargetSize = NcaHeader.Size - signTargetOffset;
        ReadOnlySpan<byte> signTarget =
            SpanHelpers.AsReadOnlyByteSpan(in _header).Slice(signTargetOffset, signTargetSize);

        IHash256GeneratorFactory factory = _hashGeneratorFactorySelector.GetFactory(HashAlgorithmType.Sha2);
        factory.GenerateHash(outBuffer, signTarget);
    }

    public SharedRef<IStorage> GetSharedBodyStorage()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);

        return SharedRef<IStorage>.CreateCopy(in _bodyStorage);
    }

    public uint GetSignature()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.Magic;
    }

    public NcaHeader.DistributionType GetDistributionType()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.DistributionTypeValue;
    }

    public NcaHeader.ContentType GetContentType()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.ContentTypeValue;
    }

    public byte GetKeyGeneration()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.GetProperKeyGeneration();
    }

    public byte GetKeyIndex()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.KeyAreaEncryptionKeyIndex;
    }

    public ulong GetContentSize()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.ContentSize;
    }

    public ulong GetProgramId()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.ProgramId;
    }

    public uint GetContentIndex()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.ContentIndex;
    }

    public uint GetSdkAddonVersion()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        return _header.SdkAddonVersion;
    }

    public void GetRightsId(Span<byte> outBuffer)
    {
        Assert.SdkRequiresGreaterEqual(outBuffer.Length, NcaHeader.RightsIdSize);

        _header.RightsId.ItemsRo.CopyTo(outBuffer);
    }

    public bool HasFsInfo(int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return _header.FsInfos[index].StartSector != 0 || _header.FsInfos[index].EndSector != 0;
    }

    public int GetFsCount()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);

        for (int i = 0; i < NcaHeader.FsCountMax; i++)
        {
            if (!HasFsInfo(i))
            {
                return i;
            }
        }

        return NcaHeader.FsCountMax;
    }

    public NcaHeader.EncryptionType GetEncryptionType()
    {
        return _headerEncryptionType;
    }

    public ref readonly Hash GetFsHeaderHash(int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return ref _header.FsHeaderHashes[index];
    }

    public void GetFsHeaderHash(out Hash outHash, int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        outHash = _header.FsHeaderHashes[index];
    }

    public void GetFsInfo(out NcaHeader.FsInfo outFsInfo, int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        outFsInfo = _header.FsInfos[index];
    }

    public ulong GetFsOffset(int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].StartSector);
    }

    public ulong GetFsEndOffset(int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].EndSector);
    }

    public ulong GetFsSize(int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].EndSector - _header.FsInfos[index].StartSector);
    }

    public void GetEncryptedKey(Span<byte> outBuffer)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresGreaterEqual(outBuffer.Length, NcaHeader.EncryptedKeyAreaSize);

        _header.EncryptedKeys.ItemsRo.CopyTo(outBuffer);
    }

    public ReadOnlySpan<byte> GetDecryptionKey(int index)
    {
        Assert.SdkRequiresNotNull(_bodyStorage);
        Assert.SdkRequiresInRange(index, 0, (int)NcaHeader.DecryptionKey.Count);

        return _decryptionKeys[index];
    }

    public bool HasValidInternalKey()
    {
        Array16<byte> zeroKey = default;

        for (int i = 0; i < (int)NcaHeader.DecryptionKey.Count; i++)
        {
            if (!CryptoUtil.IsSameBytes(zeroKey,
                    _header.EncryptedKeys.ItemsRo.Slice(i * Aes.KeySize128, Aes.KeySize128), Aes.KeySize128))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasInternalDecryptionKeyForAesHw()
    {
        Array16<byte> zeroKey = default;
        return !CryptoUtil.IsSameBytes(zeroKey, GetDecryptionKey((int)NcaHeader.DecryptionKey.AesCtrHw),
            Array16<byte>.Length);
    }

    public bool IsSwAesPrioritized()
    {
        return _isSoftwareAesPrioritized;
    }

    public void PrioritizeSwAes()
    {
        _isSoftwareAesPrioritized = true;
    }

    public bool IsAvailableSwKey()
    {
        return _isAvailableSwKey;
    }

    public void SetExternalDecryptionKey(ReadOnlySpan<byte> key)
    {
        Assert.SdkRequiresEqual(_externalDataDecryptionKey.ItemsRo.Length, key.Length);

        key.CopyTo(_externalDataDecryptionKey.Items);
    }

    public ReadOnlySpan<byte> GetExternalDecryptionKey()
    {
        return _externalDataDecryptionKey.ItemsRo;
    }

    public bool HasExternalDecryptionKey()
    {
        Array16<byte> zeroKey = default;
        return !CryptoUtil.IsSameBytes(zeroKey, GetExternalDecryptionKey(), Array16<byte>.Length);
    }

    public void GetRawData(Span<byte> outBuffer)
    {
        Assert.SdkRequires(_bodyStorage.HasValue);
        Assert.SdkRequiresLessEqual(Unsafe.SizeOf<NcaHeader>(), outBuffer.Length);

        SpanHelpers.AsReadOnlyByteSpan(_header).CopyTo(outBuffer);
    }

    public DecryptAesCtrFunction GetExternalDecryptAesCtrFunction()
    {
        Assert.SdkRequiresNotNull(_decryptAesCtr);
        return _decryptAesCtr;
    }

    public DecryptAesCtrFunction GetExternalDecryptAesCtrFunctionForExternalKey()
    {
        Assert.SdkRequiresNotNull(_decryptAesCtrForExternalKey);
        return _decryptAesCtrForExternalKey;
    }

    public GetDecompressorFunction GetDecompressor()
    {
        Assert.SdkRequiresNotNull(_getDecompressorFunc);
        return _getDecompressorFunc;
    }

    public IHash256GeneratorFactorySelector GetHashGeneratorFactorySelector()
    {
        Assert.SdkRequiresNotNull(_hashGeneratorFactorySelector);
        return _hashGeneratorFactorySelector;
    }
}

/// <summary>
/// Handles reading information from the <see cref="NcaFsHeader"/> of a file system inside an NCA file.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class NcaFsHeaderReader
{
    private NcaFsHeader _header;
    private int _fsIndex;

    public NcaFsHeaderReader()
    {
        _fsIndex = -1;
    }

    public bool IsInitialized()
    {
        return _fsIndex >= 0;
    }

    public Result Initialize(NcaReader reader, int index)
    {
        _fsIndex = -1;

        Result rc = reader.ReadHeader(out _header, index);
        if (rc.IsFailure()) return rc.Miss();

        Unsafe.SkipInit(out Hash hash);
        IHash256GeneratorFactory generator = reader.GetHashGeneratorFactorySelector().GetFactory(HashAlgorithmType.Sha2);
        generator.GenerateHash(hash.Value.Items, SpanHelpers.AsReadOnlyByteSpan(in _header));

        if (!CryptoUtil.IsSameBytes(reader.GetFsHeaderHash(index).Value, hash.Value, Unsafe.SizeOf<Hash>()))
        {
            return ResultFs.NcaFsHeaderHashVerificationFailed.Log();
        }

        _fsIndex = index;
        return Result.Success;
    }

    public ref readonly NcaFsHeader.HashData GetHashData()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.HashDataValue;
    }

    public ushort GetVersion()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.Version;
    }

    public int GetFsIndex()
    {
        Assert.SdkRequires(IsInitialized());
        return _fsIndex;
    }

    public NcaFsHeader.FsType GetFsType()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.FsTypeValue;
    }

    public NcaFsHeader.HashType GetHashType()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.HashTypeValue;
    }

    public NcaFsHeader.EncryptionType GetEncryptionType()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.EncryptionTypeValue;
    }

    public NcaFsHeader.MetaDataHashType GetPatchMetaHashType()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.MetaDataHashTypeValue;
    }

    public NcaFsHeader.MetaDataHashType GetSparseMetaHashType()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.MetaDataHashTypeValue;
    }

    public Result GetHashTargetOffset(out long outOffset)
    {
        Assert.SdkRequires(IsInitialized());

        Result rc = _header.GetHashTargetOffset(out outOffset);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public bool IsSkipLayerHashEncryption()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.IsSkipLayerHashEncryption();
    }

    public ref readonly NcaPatchInfo GetPatchInfo()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.PatchInfo;
    }

    public NcaAesCtrUpperIv GetAesCtrUpperIv()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.AesCtrUpperIv;
    }

    public bool ExistsSparseLayer()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.SparseInfo.Generation != 0;
    }

    public ref readonly NcaSparseInfo GetSparseInfo()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.SparseInfo;
    }

    public bool ExistsCompressionLayer()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.CompressionInfo.TableOffset != 0 && _header.CompressionInfo.TableSize != 0;
    }

    public ref readonly NcaCompressionInfo GetCompressionInfo()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.CompressionInfo;
    }

    public bool ExistsPatchMetaHashLayer()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.MetaDataHashDataInfo.Size != 0 && GetPatchInfo().HasIndirectTable();
    }

    public bool ExistsSparseMetaHashLayer()
    {
        Assert.SdkRequires(IsInitialized());
        return _header.MetaDataHashDataInfo.Size != 0 && ExistsSparseLayer();
    }

    public ref readonly NcaMetaDataHashDataInfo GetPatchMetaDataHashDataInfo()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.MetaDataHashDataInfo;
    }

    public ref readonly NcaMetaDataHashDataInfo GetSparseMetaDataHashDataInfo()
    {
        Assert.SdkRequires(IsInitialized());
        return ref _header.MetaDataHashDataInfo;
    }

    public void GetRawData(Span<byte> outBuffer)
    {
        Assert.SdkRequires(IsInitialized());
        Assert.SdkRequiresLessEqual(Unsafe.SizeOf<NcaFsHeader>(), outBuffer.Length);

        SpanHelpers.AsReadOnlyByteSpan(in _header).CopyTo(outBuffer);
    }
}