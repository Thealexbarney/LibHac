using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Spl;

namespace LibHac.FsSystem;

/// <summary>
/// Handles reading information from an NCA's header.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class NcaReader17 : IDisposable
{
    private RuntimeNcaHeader _header;
    private SharedRef<IStorage> _bodyStorage;
    private SharedRef<IStorage> _headerStorage;
    private SharedRef<IAesCtrDecryptor> _aesCtrDecryptor;
    private GetDecompressorFunction _getDecompressorFunc;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;

    public NcaReader17(in RuntimeNcaHeader runtimeNcaHeader, ref readonly SharedRef<IStorage> notVerifiedHeaderStorage,
        ref readonly SharedRef<IStorage> bodyStorage, ref readonly SharedRef<IAesCtrDecryptor> aesCtrDecryptor,
        in NcaCompressionConfiguration compressionConfig, IHash256GeneratorFactorySelector hashGeneratorFactorySelector)
    {
        Assert.SdkRequiresNotNull(in notVerifiedHeaderStorage);
        Assert.SdkRequiresNotNull(in bodyStorage);
        Assert.SdkRequiresNotNull(hashGeneratorFactorySelector);

        _header = runtimeNcaHeader;

        _headerStorage = SharedRef<IStorage>.CreateCopy(in notVerifiedHeaderStorage);
        _bodyStorage = SharedRef<IStorage>.CreateCopy(in bodyStorage);
        _aesCtrDecryptor = SharedRef<IAesCtrDecryptor>.CreateCopy(in aesCtrDecryptor);

        _getDecompressorFunc = compressionConfig.GetDecompressorFunc;
        _hashGeneratorFactorySelector = hashGeneratorFactorySelector;
    }

    public void Dispose()
    {
        _bodyStorage.Destroy();
        _headerStorage.Destroy();
        _aesCtrDecryptor.Destroy();
    }

    public Result ReadHeader(out NcaFsHeader outHeader, int index)
    {
        UnsafeHelpers.SkipParamInit(out outHeader);

        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        long offset = _header.FsHeadersOffset + Unsafe.SizeOf<NcaFsHeader>() * (long)index;
        return _headerStorage.Get.Read(offset, SpanHelpers.AsByteSpan(ref outHeader)).Ret();
    }

    public Result GetHeaderSign2(Span<byte> outBuffer)
    {
        Assert.SdkRequiresGreaterEqual((uint)outBuffer.Length, _header.Header2SignInfo.Size);

        return _headerStorage.Get
            .Read(_header.Header2SignInfo.Size, outBuffer.Slice(0, (int)_header.Header2SignInfo.Size)).Ret();
    }

    public void GetHeaderSign2TargetHash(Span<byte> outBuffer)
    {
        Assert.SdkRequiresEqual(outBuffer.Length, Unsafe.SizeOf<Hash>());

        _header.Header2SignInfo.Hash.Value[..].CopyTo(outBuffer);
    }

    public SharedRef<IStorage> GetSharedBodyStorage()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);

        return SharedRef<IStorage>.CreateCopy(in _bodyStorage);
    }

    public NcaHeader.DistributionType GetDistributionType()
    {
        return _header.DistributionType;
    }

    public NcaHeader.ContentType GetContentType()
    {
        return _header.ContentType;
    }

    public byte GetKeyGeneration()
    {
        return _header.KeyGeneration;
    }

    public ulong GetProgramId()
    {
        return _header.ProgramId;
    }

    public void GetRightsId(Span<byte> outBuffer)
    {
        Assert.SdkRequiresGreaterEqual(outBuffer.Length, NcaHeader.RightsIdSize);

        _header.RightsId[..].CopyTo(outBuffer);
    }

    public bool HasFsInfo(int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);
        return _header.FsInfos[index].StartSector != 0 || _header.FsInfos[index].EndSector != 0;
    }

    public void GetFsHeaderHash(out Hash outHash, int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);
        outHash = _header.FsInfos[index].Hash;
    }

    public void GetFsInfo(out NcaHeader.FsInfo outFsInfo, int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        outFsInfo = new NcaHeader.FsInfo
        {
            StartSector = _header.FsInfos[index].StartSector,
            EndSector = _header.FsInfos[index].EndSector,
            HashSectors = _header.FsInfos[index].HashSectors,
            Reserved = 0
        };
    }

    public ulong GetFsOffset(int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].StartSector);
    }

    public ulong GetFsEndOffset(int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].EndSector);
    }

    public ulong GetFsSize(int index)
    {
        Assert.SdkRequiresInRange(index, 0, NcaHeader.FsCountMax);

        return NcaHeader.SectorToByte(_header.FsInfos[index].EndSector - _header.FsInfos[index].StartSector);
    }

    public void PrioritizeSwAes()
    {
        if (_aesCtrDecryptor.HasValue)
        {
            _aesCtrDecryptor.Get.PrioritizeSw();
        }
    }

    public void SetExternalDecryptionKey(in AccessKey keySource)
    {
        if (_aesCtrDecryptor.HasValue)
        {
            _aesCtrDecryptor.Get.SetExternalKeySource(in keySource);
        }
    }

    public RuntimeNcaHeader GetHeader()
    {
        return _header;
    }

    public SharedRef<IAesCtrDecryptor> GetDecryptor()
    {
        return SharedRef<IAesCtrDecryptor>.CreateCopy(in _aesCtrDecryptor);
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

    public Result Verify()
    {
        Assert.SdkRequiresNotNull(_bodyStorage);

        for (int fsIndex = 0; fsIndex < NcaHeader.FsCountMax; fsIndex++)
        {
            var reader = new NcaFsHeaderReader17();
            if (HasFsInfo(fsIndex))
            {
                Result res = reader.Initialize(this, fsIndex);
                if (res.IsFailure()) return res.Miss();

                res = reader.Verify(_header.ContentType);
                if (res.IsFailure()) return res.Miss();
            }
            else
            {
                Result res = ReadHeader(out NcaFsHeader header, fsIndex);
                if (res.IsFailure()) return res.Miss();

                NcaFsHeader zero = default;
                if (!CryptoUtil.IsSameBytes(SpanHelpers.AsReadOnlyByteSpan(in header),
                    SpanHelpers.AsReadOnlyByteSpan(in zero), Unsafe.SizeOf<NcaFsHeader>()))
                {
                    return ResultFs.InvalidNcaFsHeader.Log();
                }
            }
        }

        return Result.Success;
    }
}

/// <summary>
/// Handles reading information from the <see cref="NcaFsHeader"/> of a file system inside an NCA file.
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class NcaFsHeaderReader17
{
    private NcaFsHeader _header;
    private int _fsIndex;

    public NcaFsHeaderReader17()
    {
        _fsIndex = -1;
    }

    public bool IsInitialized()
    {
        return _fsIndex >= 0;
    }

    public Result Initialize(NcaReader17 reader, int index)
    {
        _fsIndex = -1;

        Result res = reader.ReadHeader(out _header, index);
        if (res.IsFailure()) return res.Miss();

        Unsafe.SkipInit(out Hash hash);
        IHash256GeneratorFactory generator = reader.GetHashGeneratorFactorySelector().GetFactory(HashAlgorithmType.Sha2);
        generator.GenerateHash(hash.Value, SpanHelpers.AsReadOnlyByteSpan(in _header));

        reader.GetFsHeaderHash(out Hash fsHeaderHash, index);

        if (!CryptoUtil.IsSameBytes(fsHeaderHash.Value, hash.Value, Unsafe.SizeOf<Hash>()))
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

        Result res = _header.GetHashTargetOffset(out outOffset);
        if (res.IsFailure()) return res.Miss();

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

    public Result Verify(NcaHeader.ContentType contentType)
    {
        Assert.SdkRequires(IsInitialized());
        Assert.SdkRequiresWithinMinMax((int)contentType, (int)NcaHeader.ContentType.Program, (int)NcaHeader.ContentType.PublicData);

        Result res = _header.Verify();
        if (res.IsFailure()) return res.Miss();

        const uint programSecureValue = 1;
        const uint dataSecureValue = 2;
        const uint htmlDocumentSecureValue = 4;
        const uint legalInformationSecureValue = 5;

        // Mask out the program index part of the secure value
        uint secureValue = _header.AesCtrUpperIv.SecureValue & 0xFFFFFF;

        if (GetEncryptionType() == NcaFsHeader.EncryptionType.None)
        {
            if (secureValue != 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            return Result.Success;
        }

        switch (contentType)
        {
            case NcaHeader.ContentType.Program:
                switch (_fsIndex)
                {
                    case 0:
                        if (secureValue != programSecureValue)
                            return ResultFs.InvalidNcaFsHeader.Log();
                        break;
                    case 1:
                        if (secureValue != dataSecureValue)
                            return ResultFs.InvalidNcaFsHeader.Log();
                        break;
                    default:
                        if (secureValue != 0)
                            return ResultFs.InvalidNcaFsHeader.Log();
                        break;
                }

                break;
            case NcaHeader.ContentType.Manual:
                if (secureValue != htmlDocumentSecureValue && secureValue != legalInformationSecureValue)
                    return ResultFs.InvalidNcaFsHeader.Log();
                break;
            default:
                if (secureValue != 0)
                    return ResultFs.InvalidNcaFsHeader.Log();
                break;
        }

        return Result.Success;
    }
}