using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

public class EmulatedGameCardStorageCreator : IGameCardStorageCreator
{
    private EmulatedGameCard GameCard { get; }

    public EmulatedGameCardStorageCreator(EmulatedGameCard gameCard)
    {
        GameCard = gameCard;
    }

    public Result CreateReadOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        if (GameCard.IsGameCardHandleInvalid(handle))
        {
            return ResultFs.GameCardFsCheckHandleInCreateReadOnlyFailure.Log();
        }

        using var baseStorage = new SharedRef<IStorage>(new ReadOnlyGameCardStorage(GameCard, handle));

        Result res = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
        if (res.IsFailure()) return res.Miss();

        outStorage.Reset(new SubStorage(in baseStorage, 0, cardInfo.SecureAreaOffset));
        return Result.Success;
    }

    public Result CreateSecureReadOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        if (GameCard.IsGameCardHandleInvalid(handle))
        {
            return ResultFs.GameCardFsCheckHandleInCreateSecureReadOnlyFailure.Log();
        }

        Span<byte> deviceId = stackalloc byte[0x10];
        Span<byte> imageHash = stackalloc byte[0x20];

        Result res = GameCard.GetGameCardDeviceId(deviceId);
        if (res.IsFailure()) return res.Miss();

        res = GameCard.GetGameCardImageHash(imageHash);
        if (res.IsFailure()) return res.Miss();

        using var baseStorage =
            new SharedRef<IStorage>(new ReadOnlyGameCardStorage(GameCard, handle, deviceId, imageHash));

        res = GameCard.GetCardInfo(out GameCardInfo cardInfo, handle);
        if (res.IsFailure()) return res.Miss();

        outStorage.Reset(new SubStorage(in baseStorage, cardInfo.SecureAreaOffset, cardInfo.SecureAreaSize));
        return Result.Success;
    }

    public Result CreateWriteOnly(GameCardHandle handle, ref SharedRef<IStorage> outStorage)
    {
        throw new NotImplementedException();
    }

    private class ReadOnlyGameCardStorage : IStorage
    {
        private EmulatedGameCard GameCard { get; }
        private GameCardHandle Handle { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        private bool IsSecureMode { get; }
        private byte[] DeviceId { get; } = new byte[0x10];
        private byte[] ImageHash { get; } = new byte[0x20];

        public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle)
        {
            GameCard = gameCard;
            Handle = handle;
        }

        public ReadOnlyGameCardStorage(EmulatedGameCard gameCard, GameCardHandle handle,
            ReadOnlySpan<byte> deviceId, ReadOnlySpan<byte> imageHash)
        {
            GameCard = gameCard;
            Handle = handle;
            IsSecureMode = true;
            deviceId.CopyTo(DeviceId);
            imageHash.CopyTo(ImageHash);
        }

        public override Result Read(long offset, Span<byte> destination)
        {
            // In secure mode, if Handle is old and the card's device ID and
            // header hash are still the same, Handle is updated to the new handle

            return GameCard.Read(Handle, offset, destination);
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedWriteForReadOnlyGameCardStorage.Log();
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedSetSizeForReadOnlyGameCardStorage.Log();
        }

        public override Result GetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            Result res = GameCard.GetCardInfo(out GameCardInfo info, Handle);
            if (res.IsFailure()) return res.Miss();

            size = info.Size;
            return Result.Success;
        }

        public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            throw new NotImplementedException();
        }
    }
}