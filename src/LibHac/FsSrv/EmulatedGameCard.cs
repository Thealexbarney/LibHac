using System;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Tools.Fs;

namespace LibHac.FsSrv;

public class EmulatedGameCard
{
    private IStorage CardImageStorage { get; set; }
    private GameCardHandle Handle { get; set; }
    private XciHeader CardHeader { get; set; }
    private Xci CardImage { get; set; }
    private KeySet KeySet { get; set; }

    public EmulatedGameCard() { }

    public EmulatedGameCard(KeySet keySet)
    {
        KeySet = keySet;
    }

    public GameCardHandle GetGameCardHandle()
    {
        return Handle;
    }

    public bool IsGameCardHandleInvalid(GameCardHandle handle)
    {
        return Handle != handle;
    }

    public bool IsGameCardInserted()
    {
        return CardImageStorage != null;
    }

    public void InsertGameCard(IStorage cardImageStorage)
    {
        RemoveGameCard();

        CardImage = new Xci(KeySet, cardImageStorage);
        CardHeader = CardImage.Header;
        CardImageStorage = CardImage.BaseStorage;
    }

    public void RemoveGameCard()
    {
        if (IsGameCardInserted())
        {
            CardImageStorage = null;
            Handle++;
        }
    }

    internal Result GetXci(out Xci xci, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out xci);

        if (IsGameCardHandleInvalid(handle)) return ResultFs.GameCardFsCheckHandleInReadFailure.Log();
        if (!IsGameCardInserted()) return ResultFs.GameCardCardNotInserted.Log();

        xci = CardImage;
        return Result.Success;
    }

    public Result Read(GameCardHandle handle, long offset, Span<byte> destination)
    {
        if (IsGameCardHandleInvalid(handle)) return ResultFs.GameCardFsCheckHandleInReadFailure.Log();
        if (!IsGameCardInserted()) return ResultFs.GameCardCardNotInserted.Log();

        return CardImageStorage.Read(offset, destination);
    }

    public Result GetGameCardImageHash(Span<byte> outBuffer)
    {
        if (outBuffer.Length < 0x20) return ResultFs.GameCardPreconditionViolation.Log();
        if (!IsGameCardInserted()) return ResultFs.GameCardCardNotInserted.Log();

        CardHeader.ImageHash.CopyTo(outBuffer.Slice(0, 0x20));
        return Result.Success;
    }

    public Result GetGameCardDeviceId(Span<byte> outBuffer)
    {
        if (outBuffer.Length < 0x10) return ResultFs.GameCardPreconditionViolation.Log();
        if (!IsGameCardInserted()) return ResultFs.GameCardCardNotInserted.Log();

        // Skip the security mode check

        // Instead of caching the CardKeyArea data, read the value directly
        return CardImageStorage.Read(0x7110, outBuffer.Slice(0, 0x10));
    }

    internal Result GetCardInfo(out GameCardInfo cardInfo, GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out cardInfo);

        if (IsGameCardHandleInvalid(handle)) return ResultFs.GameCardFsCheckHandleInGetStatusFailure.Log();
        if (!IsGameCardInserted()) return ResultFs.GameCardCardNotInserted.Log();

        cardInfo = GetCardInfoImpl();
        return Result.Success;
    }

    private GameCardInfo GetCardInfoImpl()
    {
        var info = new GameCardInfo();

        CardHeader.RootPartitionHeaderHash.AsSpan().CopyTo(info.RootPartitionHeaderHash);
        info.PackageId = CardHeader.PackageId;
        info.Size = GameCard.GetGameCardSizeBytes(CardHeader.GameCardSize);
        info.RootPartitionOffset = CardHeader.RootPartitionOffset;
        info.RootPartitionHeaderSize = CardHeader.RootPartitionHeaderSize;
        info.SecureAreaOffset = GameCard.CardPageToOffset(CardHeader.LimAreaPage);
        info.SecureAreaSize = info.Size - info.SecureAreaOffset;
        info.UpdateVersion = CardHeader.UppVersion;
        info.UpdateTitleId = CardHeader.UppId;
        info.Attribute = CardHeader.Flags;

        return info;
    }
}