using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Gc.Impl;
using LibHac.Gc.Writer;
using static LibHac.Gc.Values;

namespace LibHac.Gc;

public sealed class GameCardEmulated : IGcApi
{
    private static ReadOnlySpan<byte> CardHeaderKey => new byte[]
        { 0x01, 0xC5, 0x8F, 0xE7, 0x00, 0x2D, 0x13, 0x5A, 0xB2, 0x9A, 0x3F, 0x69, 0x33, 0x95, 0x74, 0xB1 };

    private const string LibNotInitializedMessage = "Error: Gc lib is not initialized\n";

    private SharedRef<IStorage> _cardStorage;
    private bool _attached;
    private bool _activated;
    private bool _isSecureMode;
    private bool _initialized;
    private bool _writeMode;
    private bool _hasKeyArea;
    private CardHeader _cardHeader;
    private T1CardCertificate _certificate;
    private Array32<byte> _imageHash;

    public GameCardWriter Writer => new GameCardWriter(this);
    IGcWriterApi IGcApi.Writer => Writer;

    private Result CheckCardReady()
    {
        if (!_attached)
            return ResultFs.GameCardCardNotInserted.Log();

        if (!_activated)
            return ResultFs.GameCardCardNotActivated.Log();

        return Result.Success;
    }

    private void DecryptCardHeader(ref CardHeader header)
    {
        Span<byte> iv = stackalloc byte[GcAesCbcIvLength];
        for (int i = 0; i < GcAesCbcIvLength; i++)
        {
            iv[i] = header.Iv[GcAesCbcIvLength - 1 - i];
        }

        Aes.DecryptCbc128(SpanHelpers.AsReadOnlyByteSpan(in header.EncryptedData),
            SpanHelpers.AsByteSpan(ref header.EncryptedData), CardHeaderKey, iv);
    }

    private long GetCardSize(MemoryCapacity memoryCapacity)
    {
        return memoryCapacity switch
        {
            MemoryCapacity.Capacity1GB => AvailableSizeBase * 1,
            MemoryCapacity.Capacity2GB => AvailableSizeBase * 2,
            MemoryCapacity.Capacity4GB => AvailableSizeBase * 4,
            MemoryCapacity.Capacity8GB => AvailableSizeBase * 8,
            MemoryCapacity.Capacity16GB => AvailableSizeBase * 16,
            MemoryCapacity.Capacity32GB => AvailableSizeBase * 32,
            _ => 0
        };
    }

    public void InsertGameCard(in SharedRef<IStorage> storage)
    {
        _attached = false;
        _activated = false;

        _cardStorage.SetByCopy(in storage);
        _hasKeyArea = HasKeyArea(_cardStorage.Get);

        if (storage.HasValue)
        {
            Abort.DoAbortUnlessSuccess(ReadBaseStorage(0x100, SpanHelpers.AsByteSpan(ref _cardHeader)));
            Abort.DoAbortUnlessSuccess(ReadBaseStorage(GcCertAreaPageAddress * GcPageSize, SpanHelpers.AsByteSpan(ref _certificate)));

            Sha256.GenerateSha256Hash(SpanHelpers.AsReadOnlyByteSpan(in _cardHeader), _imageHash.Items);

            DecryptCardHeader(ref _cardHeader);

            _attached = true;
        }
    }

    public void RemoveGameCard()
    {
        _cardStorage.Destroy();

        _attached = false;
        _activated = false;
    }

    private static bool HasKeyArea(IStorage baseStorage)
    {
        if (baseStorage is null)
            return false;

        Result res = baseStorage.GetSize(out long storageSize);
        if (res.IsFailure()) return false;

        if (storageSize >= 0x1104)
        {
            uint magic = 0;
            res = baseStorage.Read(0x1100, SpanHelpers.AsByteSpan(ref magic));
            if (res.IsFailure()) return false;

            if (magic == CardHeader.HeaderMagic)
            {
                return true;
            }
        }

        return false;
    }

    private Result ReadBaseStorage(long offset, Span<byte> destination)
    {
        long baseStorageOffset = _hasKeyArea ? GcCardKeyAreaSize + offset : offset;

        return _cardStorage.Get.Read(baseStorageOffset, destination).Ret();
    }

    public readonly struct GameCardWriter : IGcWriterApi
    {
        private readonly GameCardEmulated _card;

        public GameCardWriter(GameCardEmulated card)
        {
            _card = card;
        }

        public void ChangeMode(AsicMode mode)
        {
            Abort.DoAbortUnless(_card._initialized, LibNotInitializedMessage);
        }

        public Result ActivateForWriter()
        {
            Abort.DoAbortUnless(_card._initialized, LibNotInitializedMessage);

            _card._writeMode = true;
            _card._activated = true;

            return Result.Success;
        }

        public Result EraseAndWriteParameter(MemorySize size, uint romAreaStartPageIndex)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result Write(ReadOnlySpan<byte> source, uint pageIndex, uint pageCount)
        {
            return ResultFs.NotImplemented.Log();
        }

        public Result GetCardAvailableRawSize(out long outSize)
        {
            outSize = 0;
            return Result.Success;
        }

        public void SetVerifyEnableFlag(bool isEnabled)
        {
            // ...
        }

        public void SetUserAsicFirmwareBuffer(ReadOnlySpan<byte> firmwareBuffer)
        {
            // ...
        }

        public Result GetRmaInformation(out RmaInformation outRmaInformation)
        {
            outRmaInformation = default;
            return Result.Success;
        }

        public Result WriteDevCardParam(in DevCardParameter devCardParam)
        {
            return Result.Success;
        }

        public Result ReadDevCardParam(out DevCardParameter outDevCardParam)
        {
            outDevCardParam = default;
            return Result.Success;
        }

        public Result ForceErase()
        {
            return Result.Success;
        }
    }

    public void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate)
    {
        // ...
    }

    public void Initialize(Memory<byte> workBuffer, ulong deviceBufferAddress)
    {
        _initialized = true;
    }

    public void FinalizeGc()
    {
        _initialized = false;
    }

    public void PowerOffGameCard()
    {
        // ...
    }

    public void RegisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress)
    {
        // ...
    }

    public void UnregisterDeviceVirtualAddress(Memory<byte> buffer, ulong deviceBufferAddress)
    {
        // ...
    }

    public Result GetInitializationResult()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        return Result.Success;
    }

    public Result Activate()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        _activated = true;
        _writeMode = false;
        return Result.Success;
    }

    public void Deactivate()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        _activated = false;
        _isSecureMode = false;
    }

    public Result SetCardToSecureMode()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        _isSecureMode = true;
        return Result.Success;
    }

    public Result Read(Span<byte> destination, uint pageAddress, uint pageCount)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        if (destination.Length == 0)
            return Result.Success;

        int limArea = (int)_cardHeader.LimAreaPage;
        bool isNormal = pageAddress < limArea;
        bool isSecure = pageAddress + pageCount - 1 >= limArea;

        // Reads cannot span the boundary between the normal area and secure area.
        if (isNormal && isSecure)
            return ResultFs.GameCardInvalidAccessAcrossMode.Log();

        // Reads to the secure area cannot be done in normal mode.
        if (isSecure && !_isSecureMode)
            return ResultFs.GameCardInvalidSecureAccess.Log();

        // Reads to the normal area cannot be done in secure mode.
        if (isNormal && _isSecureMode)
            return ResultFs.GameCardInvalidNormalAccess.Log();

        res = ReadBaseStorage(pageAddress * GcPageSize, destination);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public void PutToSleep()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);
    }

    public void Awaken()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);
    }

    public bool IsCardInserted()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        return _attached;
    }

    public bool IsCardActivationValid()
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        return _activated;
    }

    public Result GetCardStatus(out GameCardStatus outStatus)
    {
        outStatus = default;

        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        long cardSize = GetCardSize((MemoryCapacity)_cardHeader.RomSize);

        GameCardStatus status = default;

        status.CupVersion = _cardHeader.EncryptedData.CupVersion;
        status.PackageId = _cardHeader.PackageId;
        status.CardSize = cardSize;
        status.PartitionFsHeaderHash = _cardHeader.PartitionFsHeaderHash;
        status.CupId = _cardHeader.EncryptedData.CupId;
        status.CompatibilityType = _cardHeader.EncryptedData.CompatibilityType;
        status.PartitionFsHeaderAddress = _cardHeader.PartitionFsHeaderAddress;
        status.PartitionFsHeaderSize = _cardHeader.PartitionFsHeaderSize;
        status.NormalAreaSize = _cardHeader.LimAreaPage * GcPageSize;
        status.SecureAreaSize = cardSize - status.NormalAreaSize;
        status.Flags = _cardHeader.Flags;

        outStatus = status;

        return Result.Success;
    }

    public Result GetCardDeviceId(Span<byte> destBuffer)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        if (!_isSecureMode)
            return ResultFs.GameCardStateCardSecureModeRequired.Log();

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        _certificate.T1CardDeviceId.ItemsRo.CopyTo(destBuffer);
        return Result.Success;
    }

    public Result GetCardDeviceCertificate(Span<byte> destBuffer)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        if (!_isSecureMode)
            return ResultFs.GameCardInvalidGetCardDeviceCertificate.Log();

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        SpanHelpers.AsReadOnlyByteSpan(in _certificate).Slice(0, GcDeviceCertificateSize).CopyTo(destBuffer);
        return Result.Success;
    }

    public Result ChallengeCardExistence(Span<byte> responseBuffer, ReadOnlySpan<byte> challengeSeedBuffer,
        ReadOnlySpan<byte> challengeValueBuffer)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);
        return CheckCardReady().Ret();
    }

    public Result GetCardImageHash(Span<byte> destBuffer)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        Result res = CheckCardReady();
        if (res.IsFailure()) return res.Miss();

        _imageHash.ItemsRo.CopyTo(destBuffer);
        return Result.Success;
    }

    public Result GetGameCardIdSet(out GameCardIdSet outGcIdSet)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        outGcIdSet = default;
        return Result.Success;
    }

    public void RegisterDetectionEventCallback(Action<object> function, object args)
    {
        // ...
    }

    public void UnregisterDetectionEventCallback()
    {
        // ...
    }

    public Result GetCardHeader(Span<byte> destBuffer)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);
        return Result.Success;
    }

    public Result GetErrorInfo(out GameCardErrorReportInfo outErrorReportInfo)
    {
        Abort.DoAbortUnless(_initialized, LibNotInitializedMessage);

        outErrorReportInfo = default;
        return Result.Success;
    }
}